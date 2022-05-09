using Blackbaud.AppFx.Server;
using Blackbaud.AppFx.Server.AppCatalog;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using static Microsoft.ML.DataOperationsCatalog;

namespace BrightVine.ML.Demo
{
    public sealed class InteractionSentimentAnalysisGlobalChange : AppGlobalChangeProcess
    {
        public int Action = 0;
        private Guid PROCESSID = new Guid("3172DCA9-2E85-41C7-80EC-28F8A07DE54D"); //Global Change Catalog ID
        private const string MODELNAME = "Interaction Sentiment Analysis Model";
        private const string MODELDESCRIPTION = "Machine learning for interaction sentiment analysis.";
        private MLContext MLCntx;
        private string ConnectionString;

        public override AppGlobalChangeResult ProcessGlobalChange()
        {
            AppGlobalChangeResult result = new AppGlobalChangeResult();
            MLCntx = new MLContext();
            ConnectionString = RequestContext.AppDBConnectionString();

            switch (Action)
            {
                case 0:
                    result.NumberAdded = CreateAndSaveModel();
                    break;
                case 1:
                    result.NumberAdded = UpsertInteractionSentimentAttributeValues();
                    break;
                case 2:
                    result.NumberDeleted = ClearInteractionSentimentAttributeValues();
                    break;
                default:
                    break;
            }

            return result;
        }

        private int ClearInteractionSentimentAttributeValues()
        {
            int recordCount = 0;
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_CLEARINTERACTIONSENTIMENTS", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    con.Open();
                    recordCount = cmd.ExecuteNonQuery();
                    
                }
            }
            return recordCount;
        }

        private int UpsertInteractionSentimentAttributeValues()
        {
            int recordCount = 0;
            ITransformer model = LoadModel();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_LOADINTERACTIONS", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    con.Open();

                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            recordCount++;
                            Guid InterationId = rdr.GetGuid(0);
                            string interaction = rdr.GetString(1);

                            InteractionSentiment interactionSentimentObject = GetInteractionSentiment(model, interaction);
                            string interactionSentiment;
                            interactionSentiment = interactionSentimentObject.Prediction ? "Positive" : "Negative";
                            interactionSentiment = interactionSentiment + " interaction with a probability of: " + interactionSentimentObject.Probability;

                            SaveInteractionSentiment(InterationId, interactionSentiment);
                        }
                    }
                }
            }
            return recordCount;
        }

        private void SaveInteractionSentiment(Guid interactionId, string interactionSentiment)
        {
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_INTERACTIONSENTIMENT_SAVE", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = interactionId;
                    cmd.Parameters.Add("@SENTIMENTTEXT", SqlDbType.NVarChar).Value = interactionSentiment;

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        private TrainTestData LoadData()
        {
            IDataView dataView = null;
            UserImpersonationScope impersonationScope = default;
            Blackbaud.AppFx.SpWrap.USP_IMPORTPROCESSOPTIONS_GET.ResultRow importOptions = ImpersonationHelper.GetImportImpersonationOptions(ConnectionString);
            if (importOptions.IMPERSONATE)
            {
                impersonationScope = ImpersonationHelper.GetImpersonationScope(importOptions.IMPERSONATEUSERNAME, importOptions.IMPERSONATEPASSWORD);
            }

            try
            {
                string importFolder = importOptions.IMPORTFILEPATH;
                string dataFile = Path.Combine(importFolder, "SentimentTestData.txt");
                dataView = MLCntx.Data.LoadFromTextFile<Interaction>(dataFile, hasHeader: false);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Exception ocurred when reading the training dataset. (" + ex.Message + ")");
            }
            finally
            {
                if (impersonationScope != null)
                    impersonationScope.Dispose();
            }

            TrainTestData splitDataView = MLCntx.Data.TrainTestSplit(dataView, testFraction: 0.2);
            return splitDataView;
        }

        private ITransformer BuildAndTrainModel(IDataView splitTrainSet)
        {
            EstimatorChain<BinaryPredictionTransformer<Microsoft.ML.Calibrators.CalibratedModelParametersBase<Microsoft.ML.Trainers.LinearBinaryModelParameters, Microsoft.ML.Calibrators.PlattCalibrator>>> estimator = MLCntx.Transforms.Text.FeaturizeText(outputColumnName: "Features", inputColumnName: nameof(Interaction.SentimentText))
                .Append(MLCntx.BinaryClassification.Trainers.SdcaLogisticRegression(labelColumnName: "Label", featureColumnName: "Features"));

            TransformerChain<BinaryPredictionTransformer<Microsoft.ML.Calibrators.CalibratedModelParametersBase<Microsoft.ML.Trainers.LinearBinaryModelParameters, Microsoft.ML.Calibrators.PlattCalibrator>>> model = estimator.Fit(splitTrainSet);

            return model;
        }

        /* Used for testing
        private void Evaluate(ITransformer model, IDataView splitTestSet)
        {
            IDataView predictions = model.Transform(splitTestSet);
            CalibratedBinaryClassificationMetrics metrics = MLCntx.BinaryClassification.Evaluate(predictions, "Label");
        }
        */

        private InteractionSentiment GetInteractionSentiment(ITransformer model, string sentimentText)
        {
            Interaction sampleStatement = new Interaction
            {
                SentimentText = sentimentText
            };

            PredictionEngine<Interaction, InteractionSentiment> predictionFunction = MLCntx.Model.CreatePredictionEngine<Interaction, InteractionSentiment>(model);
            return  predictionFunction.Predict(sampleStatement);
        }

        private int CreateAndSaveModel()
        {
            int result = 0;
            TrainTestData splitDataView = LoadData();

            ITransformer model = BuildAndTrainModel(splitDataView.TrainSet);

            using (var stream = new MemoryStream())
            {
                MLCntx.Model.Save(model, splitDataView.TrainSet.Schema, stream);
                byte[] modelObject = stream.ToArray();

                using (SqlConnection con = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_ML_MODEL_SAVE", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add("@PROCESSID", SqlDbType.UniqueIdentifier).Value = PROCESSID;
                        cmd.Parameters.Add("@NAME", SqlDbType.NVarChar).Value = MODELNAME;
                        cmd.Parameters.Add("@DESCRIPTION", SqlDbType.NVarChar).Value = MODELDESCRIPTION;
                        cmd.Parameters.Add("@MODEL", SqlDbType.VarBinary).Value = modelObject;

                        con.Open();
                        cmd.ExecuteNonQuery();

                        result = 1;
                    }
                }
            }
            return result;
        }

        private ITransformer LoadModel()
        {
            ITransformer model = null;


            byte[] modelObject;

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_ML_MODEL_LOAD", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@PROCESSID", SqlDbType.UniqueIdentifier).Value = PROCESSID;
                    con.Open();

                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            modelObject = (byte[])rdr["MODEL"];

                            using (var stream = new MemoryStream(modelObject))
                            {
                                model = MLCntx.Model.Load(stream, out DataViewSchema modelSchema);
                            }

                        }
                    }
                }
            }

            return model;
        }

        public class Interaction
        {
            [LoadColumn(0)]
            public string SentimentText { get; set; }
            [LoadColumn(1), ColumnName("Label")]
            public bool Sentiment { get; set; }
        }

        public class InteractionSentiment: Interaction
        {
            [ColumnName("PredictedLabel")]
            public bool Prediction { get; set; }
            public float Probability { get; set; }
            public float Score { get; set; }
        }
    }
}