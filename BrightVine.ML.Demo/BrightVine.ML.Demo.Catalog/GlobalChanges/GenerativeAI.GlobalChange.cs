using Blackbaud.AppFx.Server;
using Blackbaud.AppFx.Server.AppCatalog;
using Microsoft.ML;
using System;
using System.Data.SqlClient;
using System.Data;
using static BrightVine.ML.Demo.InteractionSentimentAnalysisGlobalChange;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Text;
using System.Threading.Tasks;

using System.Net.Http;
using System.Net.Http.Headers;

namespace BrightVine.ML.Demo
{
    public sealed class GenerativeAIGlobalChange : AppGlobalChangeProcess
    {
        public Guid ConstituentQuery = Guid.NewGuid();
        private Guid PROCESSID = new Guid("fba76d01-0a8b-4975-9c41-ce585eb66730"); //Global Change Catalog ID
        private string ConnectionString;
        public string APIKey;

        public override AppGlobalChangeResult ProcessGlobalChange()
        {
            AppGlobalChangeResult result = new AppGlobalChangeResult();
            ConnectionString = RequestContext.AppDBConnectionString();
            UpsertInteractionAIAttributeValues();
            return result;
        }

        private int UpsertInteractionAIAttributeValues()
        {
            int recordCount = 0;

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_LOADCONTACTREPORTS", con))
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

                            ChatGptResponse aiResponse = CallChatGPT(APIKey, interaction);

                            SaveAIResponse(InterationId, aiResponse);
                        }
                    }
                }
            }
            return recordCount;
        }

        private ChatGptResponse CallChatGPT(string apikey, string contactreporttext)
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apikey);
                // Prepare the request payload
                ChatGptRequest request = new ChatGptRequest();

                request.Temperature = 0;
                request.Model = "gpt-3.5-turbo";
                Message m = new Message();
                m.Role = "user";
                m.Content = contactreporttext;
                request.Messages.Insert(0, m);

                // Send the API request
                var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                var response = client.PostAsync("https://api.openai.com/v1/chat/completions", content).Result;

                // Handle the API response
                if (response.IsSuccessStatusCode)
                {                   
                    var jsonResponse = response.Content.ReadAsStringAsync().Result;
                    var chatGptResponse = JsonConvert.DeserializeObject<ChatGptResponse>(jsonResponse);
                    return chatGptResponse;
                }
                else
                {
                    // Handle the API error response
                    var errorResponse = response.Content.ReadAsStringAsync().Result;
                    throw new Exception($"API request failed: {response.StatusCode}\n{errorResponse}");
                }
            }
        }


        private void SaveAIResponse(Guid interactionId, ChatGptResponse response)
        {
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand("dbo.USR_USP_INTERACTIONAI_SAVE", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    cmd.Parameters.Add("@ID", SqlDbType.UniqueIdentifier).Value = interactionId;
                    cmd.Parameters.Add("@AITEXT", SqlDbType.NVarChar).Value = response.Choices[0].Message.Content.ToString();

                    con.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public partial class ChatGptResponse
        {
            [JsonProperty("id")]
            public string Id { get; set; }

            [JsonProperty("object")]
            public string Object { get; set; }

            [JsonProperty("created")]
            public long Created { get; set; }

            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("usage")]
            public Usage Usage { get; set; }

            [JsonProperty("choices")]
            public List<Choice> Choices { get; set; }
        }

        public partial class Choice
        {
            [JsonProperty("message")]
            public Message Message { get; set; }

            [JsonProperty("finish_reason")]
            public string FinishReason { get; set; }

            [JsonProperty("index")]
            public long Index { get; set; }
        }

        public partial class Message
        {
            [JsonProperty("role")]
            public string Role { get; set; }

            [JsonProperty("content")]
            public string Content { get; set; }
        }

        public partial class Usage
        {
            [JsonProperty("prompt_tokens")]
            public long PromptTokens { get; set; }

            [JsonProperty("completion_tokens")]
            public long CompletionTokens { get; set; }

            [JsonProperty("total_tokens")]
            public long TotalTokens { get; set; }
        }

        public partial class ChatGptResponse
        {
            public static ChatGptResponse FromJson(string json) => JsonConvert.DeserializeObject<ChatGptResponse>(json,Converter.Settings);
        }

        internal static class Converter
        {
            public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None,
                Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
            };
        }

        public partial class ChatGptRequest
        {
            [JsonProperty("model")]
            public string Model { get; set; }

            [JsonProperty("temperature")]
            public long Temperature { get; set; }

            [JsonProperty("messages")]
            public List<Message> Messages { get; set; }

            public ChatGptRequest()
            {
                Messages = new List<Message>();
            }
        }

        public partial class ChatGptRequest
        {
            public static ChatGptRequest FromJson(string json) => JsonConvert.DeserializeObject<ChatGptRequest>(json, Converter.Settings);
        }

    }
}