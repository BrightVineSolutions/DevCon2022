using Blackbaud.AppFx.Server;
using Blackbaud.AppFx.Server.AppCatalog;
using System.Data;
using System.Data.SqlClient;
using Microsoft.ML;
using Microsoft.ML.Data;
using static Microsoft.ML.DataOperationsCatalog;
using System.IO;
using System;

namespace BrightVine.ML.Demo.Catalog.RecordOperations
{
    public sealed class TrainBVDLRuntimePredictorRecordOperation : AppRecordOperationPerform
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

            string sqlCommand = "exec dbo.USR_USP_BVDL_GETRUNTIMEHISTORY 1";

            DatabaseLoader loader = MLCntx.Data.CreateDatabaseLoader<ImportRun>();
            DatabaseSource dbSource = new DatabaseSource(SqlClientFactory.Instance, RequestContext.AppDBConnectionString(), sqlCommand);
            dataView = loader.Load(dbSource);

            //TrainTestData splitDataView = MLCntx.Data.TrainTestSplit(dataView, testFraction: 0.2);

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

        public class ImportRun
        {
            [LoadColumn(1)]
            public float ConstituentCount { get; set; }
            [LoadColumn(2)]
            public float AddressCount { get; set; }
            [LoadColumn(3)]
            public float EmailCount { get; set; }
            [LoadColumn(4)]
            public float PhoneCount { get; set; }
            [LoadColumn(5)]
            public float RevenueCount { get; set; }
            [LoadColumn(6)]
            public float InteractionCount { get; set; }
            [LoadColumn(7)]
            public float RelationshipCount { get; set; }
            [LoadColumn(8)]
            public float ConstituencyCount { get; set; }
            [LoadColumn(9)]
            public float RegistrationCount { get; set; }
            [LoadColumn(10)]
            public float AlternateIdCount { get; set; }
            [LoadColumn(11)]
            public float EducationCount { get; set; }
            [LoadColumn(12)]
            public float EventCount { get; set; }
            [LoadColumn(0)]
            public float RunTime { get; set; }

           
        }

        public class ImportRunPrediction
        {
            [ColumnName("Score")]
            public float RunTime;
        }
    }
}