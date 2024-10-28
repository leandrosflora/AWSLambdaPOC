using Amazon.Lambda.Core;
using System.Net.Http.Headers;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;

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
    public APIGatewayProxyResponse FunctionHandler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            Console.WriteLine("request: "+ JsonSerializer.Serialize(request));
            // Lê o corpo da solicitação
            var requestBody = JsonSerializer.Deserialize<RootObject>(request.Body);
            Console.WriteLine("requestBody: " + JsonSerializer.Serialize(requestBody));

            if (requestBody != null && requestBody.@object != null && requestBody.entry[0].id != null)
            {
                var messageId = requestBody.entry[0].id;/* extrair o ID da mensagem do payload */;

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

                var url = "https://graph.facebook.com/v20.0/501924736326787/messages";
                var token = "EAAHfeKlXMv8BO88oPUp8LpC51DV8Q2HPFisEj6AM3ZAVIX8lZB3ZC9H51f81eNZCapFvqtsn0C7KVO12q9kZB2CzsSyTRu8tVdkTLZBMcMbUXC4y7s3lzX7gkCTRLyLCWqIpUdlr3THhHX8GejxetlAylI2vYDuiurfsTl9ke3YUFh7FCmdyYLnenIJfEl9pKeAB7ykryJN5NnH3DVjwP1YJ9ZAkZAYZD";

                var messageData = new
                {
                    messaging_product = "whatsapp",
                    to = "5511942302556",
                    type = "text",
                    text = new
                    {
                        body = "testes corpo msg"
                    }
                };

                var json = JsonSerializer.Serialize(messageData);

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = client.PostAsync(url, content).Result;
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
                Console.WriteLine("NOK2");
                // Resposta padrão para outros casos
                return new APIGatewayProxyResponse
                {
                    StatusCode = 200,
                    Body = "NOK2"
                };
            }
        }
        catch (Exception e)
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = e.Message
            };
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
}

public class Text
{
    public string body { get; set; }
}