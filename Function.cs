using Amazon.Lambda.Core;
using System.Net.Http.Headers;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using System.Text.Json;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text.Json.Nodes;
using Amazon;
using AWSLambdaPOC.Entidades;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambdaPOC;

public class Function
{
    string result = string.Empty;
    string json = string.Empty;
    const string url = "https://graph.facebook.com/v20.0/519842974541275/messages";
    const string token = "EAARKw58BssABO7lShK8dZByUZBAMiZCEHw65KYVMVZCMzfZBmC9XMo0ror4jYeUD5VAFZBmC2lCvftp3oZA98JEYGfciZCe8lAJ1tO1Itg29lYyAoKQtn03T3IUEmgG04ZByNQtNJMcQ2MrZCEGaM3faxUa8ZBtfKLsrajAcl78VTOjEhu08e96rj34oASyl3Yk78TP";

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequestMeta request, ILambdaContext context)
    {
        try
        {
            Console.WriteLine("Webhook: " + JsonSerializer.Serialize(request));

            if (request.httpMethod == "GET")
            {
                return CadastrarWebhookMeta(request);
            }
            else
            {
                var requestBody = JsonSerializer.Deserialize<RootObject>(request.body);

                if (requestBody != null && requestBody.@object != null && requestBody.entry[0].id != null
                    && requestBody.entry[0].changes[0].value.messages[0].id != null)
                {
                    Console.WriteLine("request: " + request.httpMethod + " " + JsonSerializer.Serialize(request));
                    Console.WriteLine("requestBody: " + JsonSerializer.Serialize(requestBody));

                    if (requestBody.entry[0].changes[0].value.messages[0].type == "image")
                    {
                        json = TratarImagem(requestBody);
                    }
                    else if (requestBody.entry[0].changes[0].value.messages[0].type == "audio")
                    {
                        json = TratarAudio(requestBody);
                    }
                    else
                    {
                        await TratarTexto(request, requestBody);
                    }

                    return await CallbackMensagem();
                }
                else
                {
                    Console.WriteLine("NOK3 ");
                    return new APIGatewayProxyResponse
                    {
                        StatusCode = 200,
                        Body = "NOK3"
                    };
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("NOK4 ");
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = "NOK4" + e.Message
            };
        }
    }

    private async Task TratarTexto(APIGatewayProxyRequestMeta request, RootObject? requestBody)
    {
        result = await ChamarBackend(request);
        object messageData = NewCallbackMessage(requestBody);
        json = JsonSerializer.Serialize(messageData);
    }

    private object NewCallbackMessage(RootObject? requestBody)
    {
        return new
        {
            messaging_product = "whatsapp",
            to = requestBody.entry[0].changes[0].value.messages[0].@from.ToString(),
            type = "text",
            text = new
            {
                body = result.Replace("\"", "")
            }
        };
    }

    private async Task<APIGatewayProxyResponse> CallbackMensagem()
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            Console.WriteLine("messageDataText: " + json);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = result,
                Headers = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" }
                }
            };
        }
    }

    private static async Task<string> ChamarBackend(APIGatewayProxyRequestMeta request)
    {
        HttpClient clienthttp = new HttpClient();

        // Dados para a requisição POST
        var postDataToken = new StringContent("{\"grant_type\":\"client_credentials\"}", System.Text.Encoding.UTF8, "application/json");

        clienthttp.DefaultRequestHeaders.Add("Accept", "application/json");
        clienthttp.DefaultRequestHeaders.Add("Authorization", "Basic ZDc3NzM1NWQtMWQ4MC00NzFhLTkyZWEtODExODVlMzgwYjhmOmQ4ZGRmZTU4LTU0MTktNDViZS1hMmY0LWY0YzU0N2E4ZDYxNw==");

        // Requisição GET para obter o token JWT
        HttpResponseMessage TokenResponse = await clienthttp.PostAsync("https://oauth-hml.bancobmg.com.br/oauth/v1/access-token", postDataToken);

        TokenResponse.EnsureSuccessStatusCode();
        string responseBody = await TokenResponse.Content.ReadAsStringAsync();

        BearerToken response_des = JsonSerializer.Deserialize<BearerToken>(responseBody);
        var tokenJWT = response_des.access_token;

        // Adiciona o token JWT no cabeçalho da requisição POST
        clienthttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenJWT);

        // Dados para a requisição POST
        var postData = new StringContent(request.body, Encoding.UTF8, "application/json");

        // Requisição POST usando o token JWT
        HttpResponseMessage postResponse = await clienthttp.PostAsync("https://api-partners-hml.bancobmg.com.br/whatsapp/v1/webhook-whatsapp?hub.challenge=asdf&hub.verify_token=WhatsappAI&hub.mode=subscribe", postData);

        postResponse.EnsureSuccessStatusCode();
        var postResponseBody = await postResponse.Content.ReadAsStringAsync();
        Console.WriteLine("postResponseBody: " + postResponseBody);
        return postResponseBody;
    }

    private static APIGatewayProxyResponse CadastrarWebhookMeta(APIGatewayProxyRequestMeta request)
    {
        Console.WriteLine("queryStringParameters: " + JsonSerializer.Serialize(request.queryStringParameters));

        // Acessando os parâmetros da query string
        var queryStringParameters = request.queryStringParameters;

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
        else
        {
            return new APIGatewayProxyResponse
            {
                StatusCode = 403,
                Body = "NOK no token"
            };
        }
    }

    private static string TratarImagem(RootObject? requestBody)
    {
        string json;
        var id_img = requestBody.entry[0].changes[0].value.messages[0].image.id;
        var messageDataImg = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = requestBody.entry[0].changes[0].value.messages[0].@from.ToString(),
            type = "image",
            image = new
            {
                id = id_img,
                caption = "Estamos desenvolvendo a extração"
            }
        };
        json = JsonSerializer.Serialize(messageDataImg);

        Console.WriteLine("messageDataImg: " + json);
        return json;
    }

    private static string TratarAudio(RootObject? requestBody)
    {
        string json;
        var id_audio = requestBody.entry[0].changes[0].value.messages[0].audio.id;
        var messageDataImg = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = requestBody.entry[0].changes[0].value.messages[0].@from.ToString(),
            type = "audio",
            audio = new
            {
                id = "2162501764264589"//id_audio
            }
        };
        json = JsonSerializer.Serialize(messageDataImg);

        Console.WriteLine("messageDataAudio: " + json);
        return json;
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