using Amazon.Lambda.Core;
using System.Net.Http.Headers;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Amazon.Util;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json.Nodes;
using Amazon;
using System.Net.Http;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambdaPOC;

public class Function
{
    HashSet<string> processedMessageIds = new HashSet<string>();

    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary> 
    /// <param name="context">The ILambdaContext that provides methods for logging and describing the Lambda environment.</param>
    /// <returns></returns>
    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        var token = "EAAHfeKlXMv8BO75jFTgm9pfDGb7q1EzRvqQseTikOtTzmZAJo21cVMxqHtFmFHsawXTlckXZBD4NEdvxp4Svumxevc9DjxxF4YqyRlsEyKCLOhIowdSJrtp5Wy9cQJPAOKRSqtgzbOUbrz9fHB1g19ZAZAp7ZBXhHGy8WEm2hsymZBMsZBTKvYBRNHJgsJrfMixeCNhJJIZBwV1MfUjRao2Nxw4jhwIZD";

        try
        { 
            var queryStringParameters = request.QueryStringParameters;

            // Inicializando variáveis
            string mode = queryStringParameters != null && queryStringParameters.ContainsKey("hub.mode")
                ? queryStringParameters["hub.mode"]
                : null;

            string challenge = queryStringParameters != null && queryStringParameters.ContainsKey("hub.challenge")
                ? queryStringParameters["hub.challenge"]
                : null;

            string verifyToken = queryStringParameters != null && queryStringParameters.ContainsKey("hub.verify_token")
                ? queryStringParameters["hub.verify_token"]
                : null;

            // Verificando se o verify token é válido
            if (verifyToken == "leandro")
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = challenge,
                    Headers = new Dictionary<string, string>
                    {
                        { "Content-Type", "text/plain" }
                    }
                }; 
            }

            Console.WriteLine("request: " + JsonSerializer.Serialize(request));
            var requestBody = JsonSerializer.Deserialize<RootObject>(request.Body);
            Console.WriteLine("requestBody: " + JsonSerializer.Serialize(requestBody));

            if (requestBody != null && requestBody.@object != null && requestBody.entry[0].id != null)
            {
                if (requestBody.entry[0].changes[0].value.messages[0].id != null)
                {
                    var messageId = requestBody.entry[0].changes[0].value.messages[0].id;

                    if (processedMessageIds.Contains(messageId))
                    {
                        return new APIGatewayProxyResponse
                        {
                            StatusCode = 200,
                            Body = "Duplicate message received.",
                            Headers = new Dictionary<string, string>
                            {
                                { "Content-Type", "application/json" }
                            }
                        };
                    }

                    // Adicionar o ID ao conjunto de processados
                    processedMessageIds.Add(messageId);
                    var result = string.Empty;
                    var json = string.Empty;
                    var url = "https://graph.facebook.com/v20.0/501924736326787/messages";

                    if (requestBody.entry[0].changes[0].value.messages[0].type == "image")
                    { 
                        var id_img = requestBody.entry[0].changes[0].value.messages[0].image.id;
                        var messageDataImg = new
                        {
                            messaging_product = "whatsapp",
                            recipient_type = "individual",
                            to = "+5511942302556",
                            type = "image",
                            image = new
                            {
                                id = requestBody.entry[0].changes[0].value.messages[0].image.id,
                                caption = "caption"
                            }
                        };
                        json = JsonSerializer.Serialize(messageDataImg);
                    }
                    else
                    {
                        string inputText = requestBody.entry[0].changes[0].value.messages[0].text.body;
                        result = await this.InvokeModelAsync(inputText);

                        Console.WriteLine("Resultado do modelo:");
                        Console.WriteLine(result);

                        var messageData = new
                        {
                            messaging_product = "whatsapp",
                            to = "5511942302556",
                            type = "text",
                            text = new
                            {
                                body = result
                            }
                        };
                        json = JsonSerializer.Serialize(messageData);
                    }

                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync(url, content);
                        response.EnsureSuccessStatusCode();

                        // Criando uma resposta
                        return new APIGatewayProxyResponse
                        {
                            StatusCode = 200,
                            Body = "Message processed successfully.",
                            Headers = new Dictionary<string, string>
                        {
                            { "Content-Type", "application/json" }
                        }
                        };
                    }
                }
                else
                {
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 403,
                        Body = "NOK2"
                    };
                }
            }
            else
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 403,
                    Body = "NOK3"
                };
            }
        }
        catch (Exception e)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = "NOK4"
            };
        }
    }

    public async Task<string> InvokeModelAsync(string userMessage)
    {
        // Create a Bedrock Runtime client in the AWS Region you want to use.
        var client = new AmazonBedrockRuntimeClient(RegionEndpoint.USEast1);

        // Set the model ID, e.g., Titan Text Premier.
        var modelId = "amazon.titan-text-premier-v1:0";

        // Define the user message.
        //var userMessage = "Describe the purpose of a 'hello world' program in one line.";

        //Format the request payload using the model's native structure.
        var nativeRequest = JsonSerializer.Serialize(new
        {
            inputText = userMessage + " e me responda sempre em portugues",
            textGenerationConfig = new
            {
                maxTokenCount = 512,
                temperature = 0.5
            }
        });

        // Create a request with the model ID and the model's native request payload.
        var request = new InvokeModelRequest()
        {
            ModelId = modelId,
            Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(nativeRequest)),
            ContentType = "application/json"
        };

        try
        {
            // Send the request to the Bedrock Runtime and wait for the response.
            var response = await client.InvokeModelAsync(request);

            // Decode the response body.
            var modelResponse = await JsonNode.ParseAsync(response.Body);

            // Extract and print the response text.
            var responseText = modelResponse["results"]?[0]?["outputText"] ?? "";
            Console.WriteLine(responseText);
            return responseText.ToString();
        }
        catch (AmazonBedrockRuntimeException e)
        {
            Console.WriteLine($"ERROR: Can't invoke '{modelId}'. Reason: {e.Message}");
            throw;
        }
    }
}



public class APIGatewayProxyRequest
{
    public string HttpMethod { get; set; }                          // Método HTTP da solicitação (GET, POST, etc.)
    public string Path { get; set; }                                 // Caminho do endpoint
    public string Body { get; set; }                                 // Corpo da solicitação
    public bool IsBase64Encoded { get; set; }                        // Indica se o corpo está codificado em Base64
    public Dictionary<string, string> QueryStringParameters { get; set; } // Parâmetros de consulta
    public Dictionary<string, string> Headers { get; set; }          // Cabeçalhos da solicitação
    //public RequestContext RequestContext { get; set; }               // Informações contextuais da solicitação
    public string Resource { get; set; }                              // O recurso associado ao endpoint
}

// Classe para desserialização do JSON
public class RootObject
{
    public string @object { get; set; }
    public List<Entry> entry { get; set; }
}

public class Entry
{
    public string id { get; set; }
    public List<Change> changes { get; set; }
}

public class Change
{
    public Value value { get; set; }
    public string field { get; set; }
}

public class Value
{
    public string messaging_product { get; set; }
    public Metadata metadata { get; set; }
    public List<Message> messages { get; set; }
}

public class Metadata
{
    public string display_phone_number { get; set; }
    public string phone_number_id { get; set; }
}

public class Message
{
    public string id { get; set; }
    public string from { get; set; }
    public string to { get; set; }
    public string timestamp { get; set; }
    public Text text { get; set; }
    public string type { get; set; }
    public Image image { get; set; }

    public Audio audio { get; set; }
}

public class Audio
{
    public string id { get; set; } 

}

public class Image
{
    public string id { get; set; }
    public string caption { get; set; }
    public string mime_type { get; set; }
    public string sha256 { get; set; }

}

public class Text
{
    public string body { get; set; }
}


public class ResponseImg
{
    public string url { get; set; }

    public string mime_type { get; set; }

    public string messaging_product { get; set; }

    public int file_size { get; set; }

    public string sha256 { get; set; }
}