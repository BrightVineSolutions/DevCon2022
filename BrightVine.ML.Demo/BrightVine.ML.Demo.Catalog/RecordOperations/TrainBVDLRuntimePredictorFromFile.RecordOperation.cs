using Blackbaud.AppFx.Server;
using Blackbaud.AppFx.Server.AppCatalog;
using Microsoft.ML;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using static BrightVine.ML.Demo.Catalog.RecordOperations.TrainBVDLRuntimePredictorRecordOperation;
using static Microsoft.ML.DataOperationsCatalog;

namespace BrightVine.ML.Demo.Catalog.RecordOperations
{
    public sealed class TrainBVDLRuntimePredictorFromFileRecordOperation : AppRecordOperationPerform
    {
        private MLContext MLCntx;
        public override AppRecordOperationPerformResult Perform()
        {
            MLCntx = new MLContext();
            BuildAndTrainModel();
            var result = new AppRecordOperationPerformResult();
            return result;
        }

        private void SaveModel(ITransformer model, IDataView dataView)
        {
            using (var stream = new MemoryStream())
            {
                MLCntx.Model.Save(model, dataView.Schema, stream);
                byte[] modelObject = stream.ToArray();

                using (SqlConnection con = new SqlConnection(this.RequestContext.AppDBConnectionString()))
                {
                    using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_ML_MODEL_SAVE", con))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.Add("@PROCESSID", SqlDbType.UniqueIdentifier).Value = new Guid("4fc4564a-5c8c-4d9a-af31-4e8462a4a31b");
                        cmd.Parameters.Add("@NAME", SqlDbType.NVarChar).Value = "BVDL Runtime Predictor";
                        cmd.Parameters.Add("@DESCRIPTION", SqlDbType.NVarChar).Value = "BVDL Runtime Predictor";
                        cmd.Parameters.Add("@MODEL", SqlDbType.VarBinary).Value = modelObject;

                        con.Open();
                        cmd.ExecuteNonQuery();
                    }
                }
            }
        }


        private ITransformer BuildAndTrainModel()
        {
            IDataView dataView = null;

            //reading training data from file
            UserImpersonationScope impersonationScope = default;
            Blackbaud.AppFx.SpWrap.USP_IMPORTPROCESSOPTIONS_GET.ResultRow importOptions = ImpersonationHelper.GetImportImpersonationOptions(this.RequestContext.AppDBConnectionString());
            if (importOptions.IMPERSONATE)
            {
                impersonationScope = ImpersonationHelper.GetImpersonationScope(importOptions.IMPERSONATEUSERNAME, importOptions.IMPERSONATEPASSWORD);
            }

            try
            {
                string importFolder = importOptions.IMPORTFILEPATH;
                string dataFile = Path.Combine(importFolder, "RunTimeTrainingSet_Validation.csv");
                dataView = MLCntx.Data.LoadFromTextFile<ImportRun>(dataFile, hasHeader: false, separatorChar: ',');
                //TrainTestData splitDataView = MLCntx.Data.TrainTestSplit(dataView, testFraction: 0.2);
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

            var dataProcessPipeline = MLCntx.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: nameof(ImportRun.RunTime))
                            .Append(MLCntx.Transforms.Concatenate("Features"
                            , nameof(ImportRun.ConstituentCount)
                            , nameof(ImportRun.AddressCount)
                            , nameof(ImportRun.EmailCount)
                            , nameof(ImportRun.PhoneCount)
                            , nameof(ImportRun.RevenueCount)
                            , nameof(ImportRun.InteractionCount)
                            , nameof(ImportRun.RelationshipCount)
                            , nameof(ImportRun.ConstituencyCount)
                            , nameof(ImportRun.RegistrationCount)
                            , nameof(ImportRun.AlternateIdCount)
                            , nameof(ImportRun.EducationCount)
                            , nameof(ImportRun.EventCount)))
                             .Append(MLCntx.Transforms.NormalizeMinMax("Features"));


            var trainer = MLCntx.Regression.Trainers.Sdca(labelColumnName: "RunTime", featureColumnName: "Features");
            var trainingPipeline = dataProcessPipeline.Append(trainer);
            var trainedModel = trainingPipeline.Fit(dataView);

            //var trainedModel = trainingPipeline.Fit(splitDataView.TrainSet);
            //IDataView predictions = trainedModel.Transform(splitDataView.TestSet);
            //var metrics = MLCntx.Regression.Evaluate(predictions, labelColumnName: "Label", scoreColumnName: "Score");

            SaveModel(trainedModel, dataView);

            return trainedModel;
        }
    }
}