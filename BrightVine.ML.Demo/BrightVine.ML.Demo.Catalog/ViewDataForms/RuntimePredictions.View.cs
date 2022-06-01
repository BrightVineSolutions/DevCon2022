using Blackbaud.AppFx.Server;
using Blackbaud.AppFx.Server.AppCatalog;
using Microsoft.ML;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using static BrightVine.ML.Demo.Catalog.RecordOperations.TrainBVDLRuntimePredictorRecordOperation;

namespace BrightVine.ML.Demo.Catalog
{
    public sealed class RuntimePredictionsViewDataForm : AppViewDataForm
    {
        public string VALIDATIONRUNTIME;
        private MLContext MLCntx;

        public override AppViewDataFormLoadResult Load()
        {
            MLCntx = new MLContext();
            var result = new AppViewDataFormLoadResult();
            result.DataLoaded = true;

            ITransformer model = LoadModel();
            var importId = ProcessContext.RecordID;

            //ImportRun sampleImportRun = new ImportRun
            //{
            //    AddressCount = 700,
            //    AlternateIdCount = 0,
            //    ConstituencyCount = 0,
            //    ConstituentCount = 700,
            //    EducationCount = 0,
            //    EmailCount = 700,
            //    EventCount = 0,
            //    InteractionCount = 0,
            //    PhoneCount = 0,
            //    RegistrationCount = 0,
            //    RelationshipCount = 0,
            //    RevenueCount = 0
            //};

            ImportRun sampleImportRun = GetImportRun(importId);


            var predictedValidationTime = PredictValidationTime(model, sampleImportRun);

            VALIDATIONRUNTIME = (int) predictedValidationTime.RunTime + " seconds";

            return result;

        }

        private ImportRun GetImportRun(string importId)
        {
            ImportRun importRun = null;
            using (SqlConnection con = new SqlConnection(this.RequestContext.AppDBConnectionString()))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_BVDL_GETIMPORTSUMMARYFORPREDICTION", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@IMPORTID", SqlDbType.UniqueIdentifier).Value = new Guid(importId);
                    con.Open();

                    using (SqlDataReader rdr = cmd.ExecuteReader())
                    {
                        if (rdr.Read())
                        {
                            importRun = new ImportRun
                            {
                                ConstituentCount = rdr.GetFloat(0),
                                AddressCount = rdr.GetFloat(1),
                                EmailCount = rdr.GetFloat(2),
                                PhoneCount = rdr.GetFloat(3),
                                RevenueCount = rdr.GetFloat(4),
                                InteractionCount = rdr.GetFloat(5),
                                RelationshipCount = rdr.GetFloat(6),
                                ConstituencyCount = rdr.GetFloat(7),
                                RegistrationCount = rdr.GetFloat(8),
                                AlternateIdCount = rdr.GetFloat(9),
                                EducationCount = rdr.GetFloat(10),
                                EventCount = rdr.GetFloat(11)
                            };

                        }
                    }
                }
            }

            return importRun;
        }

        private ImportRunPrediction PredictValidationTime(ITransformer model, ImportRun importRun)
        {
            

            PredictionEngine<ImportRun, ImportRunPrediction> predictionFunction = MLCntx.Model.CreatePredictionEngine<ImportRun, ImportRunPrediction>(model);
            return predictionFunction.Predict(importRun);
        }

        private ITransformer LoadModel()
        {
            ITransformer model = null;


            byte[] modelObject;

            using (SqlConnection con = new SqlConnection(this.RequestContext.AppDBConnectionString()))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_ML_MODEL_LOAD", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.Add("@PROCESSID", SqlDbType.UniqueIdentifier).Value = new Guid("4fc4564a-5c8c-4d9a-af31-4e8462a4a31b");
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

    }
}