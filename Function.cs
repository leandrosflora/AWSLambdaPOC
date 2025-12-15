using Amazon.Lambda.Core;
using System.Net.Http.Headers;
using System.Text;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using System.Text.Json.Nodes;
using Amazon;
using AWSLambdaPOC.Entidades;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract.Model;
using Amazon.Textract;
using Amazon.TranscribeService;
using Amazon.TranscribeService.Model;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Amazon.Runtime;
using Amazon.LexRuntimeV2;
using System.Diagnostics;
using static System.Net.Mime.MediaTypeNames;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambdaPOC;

public class Function
{
    string result = string.Empty;
    string json = string.Empty;
    const string _bucketName = "whatsappai";
    const string urlBuscaMedia = "https://graph.facebook.com/v20.0/";
    const string urlMetaFacebookWhatsapp = "https://graph.facebook.com/v20.0/519842974541275/messages";
    const string tokenMetaWhatsapp = "";
    string whiteList = "";
    AmazonTranscribeServiceClient? _transcribeClient;
    AmazonS3Client? _s3Client;
    AmazonTextractClient? _textractClient;
    AmazonBedrockRuntimeClient? _bedrockClient;
    AmazonLexRuntimeV2Client? lexClient;

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequestMeta request, ILambdaContext context)
    {
        try
        {
            bool local = false;

            if (local)
            {
                // Ambiente de desenvolvimento local
                var regiond = RegionEndpoint.USEast1;
                var iddachave = "";
                var secretAcces = "";
                var tokenacesso = "";
                lexClient = new AmazonLexRuntimeV2Client(awsAccessKeyId: iddachave, awsSecretAccessKey: secretAcces, region: regiond, awsSessionToken: tokenacesso);
                _transcribeClient = new AmazonTranscribeServiceClient(awsAccessKeyId: iddachave, awsSecretAccessKey: secretAcces, region: regiond, awsSessionToken: tokenacesso);
                _s3Client = new AmazonS3Client(awsAccessKeyId: iddachave, awsSecretAccessKey: secretAcces, region: regiond, awsSessionToken: tokenacesso);
                _textractClient = new AmazonTextractClient(awsAccessKeyId: iddachave, awsSecretAccessKey: secretAcces, region: regiond, awsSessionToken: tokenacesso);
                _bedrockClient = new AmazonBedrockRuntimeClient(awsAccessKeyId: iddachave, awsSecretAccessKey: secretAcces, region: regiond, awsSessionToken: tokenacesso);
            }
            else
            {
                // Ambiente de produção na AWS 
                var credentials = new EnvironmentVariablesAWSCredentials();
                var awsCredentials = credentials.GetCredentials();
                lexClient = new AmazonLexRuntimeV2Client(awsAccessKeyId: awsCredentials.AccessKey, awsSecretAccessKey: awsCredentials.SecretKey, region: RegionEndpoint.USEast1, awsSessionToken: awsCredentials.Token);
                _transcribeClient = new AmazonTranscribeServiceClient(awsAccessKeyId: awsCredentials.AccessKey, awsSecretAccessKey: awsCredentials.SecretKey, region: RegionEndpoint.USEast1, awsSessionToken: awsCredentials.Token);
                _s3Client = new AmazonS3Client(awsAccessKeyId: awsCredentials.AccessKey, awsSecretAccessKey: awsCredentials.SecretKey, region: RegionEndpoint.USEast1, awsSessionToken: awsCredentials.Token);
                _textractClient = new AmazonTextractClient(awsAccessKeyId: awsCredentials.AccessKey, awsSecretAccessKey: awsCredentials.SecretKey, region: RegionEndpoint.USEast1, awsSessionToken: awsCredentials.Token);
                _bedrockClient = new AmazonBedrockRuntimeClient(awsAccessKeyId: awsCredentials.AccessKey, awsSecretAccessKey: awsCredentials.SecretKey, region: RegionEndpoint.USEast1, awsSessionToken: awsCredentials.Token);
            }

            Console.WriteLine("Webhook: " + System.Text.Json.JsonSerializer.Serialize(request));

            if (request.httpMethod == "GET")
            {
                return CadastrarWebhookMeta(request);
            }
            else
            {
                var requestBody = System.Text.Json.JsonSerializer.Deserialize<RootObject>(request.body);

                if (requestBody != null && requestBody.@object != null && requestBody.entry[0].id != null
                    && requestBody.entry[0].changes[0].value.messages[0].id != null)
                {
                    Console.WriteLine("request: " + request.httpMethod + " " + System.Text.Json.JsonSerializer.Serialize(request));
                    Console.WriteLine("requestBody: " + System.Text.Json.JsonSerializer.Serialize(requestBody));

                    if (whiteList.Contains(requestBody.entry[0].changes[0].value.messages[0].@from.ToString()))
                    {
                        await RepassarBackEnd(request, requestBody);

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
                    else
                    {
                        var messageNaoAutorizados = new
                        {
                            messaging_product = "whatsapp",
                            to = requestBody.entry[0].changes[0].value.messages[0].@from.ToString(),
                            type = "text",
                            text = new
                            {
                                body = "Cliente não autorizado"
                            }
                        };
                        json = System.Text.Json.JsonSerializer.Serialize(messageNaoAutorizados);
                        return await CallbackMensagem();
                    }
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
            Console.WriteLine("NOK4: " + e.Message);
            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = "NOK4" + e.Message
            };
        }
    }

    private async Task EnviarTemplateConfirmacaoFavorecido(string numeroDestinatario, string template, string nomeFavorecido, string Banco, string chavepix)
    {
        var messageTemplate = new
        {
            messaging_product = "whatsapp",
            to = numeroDestinatario,
            type = "template",
            template = new
            {
                name = template,
                language = new { code = "pt_BR" },
                components = new[]
                {
                        new
                        {
                            type = "body",
                            parameters = new[]
                            {
                                new { type = "text", text = "POC Pix BMG"},
                                new { type = "text", text = "Banco BMG S.A"},
                                new { type = "text", text = chavepix}
                            }
                        }
                    }
            }
        };
        json = System.Text.Json.JsonSerializer.Serialize(messageTemplate);
        await CallbackMensagem();

    }

    private async Task EnviarTemplateRevisaoDadosPix(string numeroDestinatario, string template, string valorPix, string nomeFavorecido, string chavePix, string banco)
    {
        var messageTemplate = new
        {
            messaging_product = "whatsapp",
            to = numeroDestinatario,
            type = "template",
            template = new
            {
                name = template,
                language = new { code = "pt_BR" },
                components = new[]
                {
                        new
                        {
                            type = "body",
                            parameters = new[]
                            {
                                new { type = "text", text = valorPix},
                                new { type = "text", text = "POC Pix BMG"},
                                new { type = "text", text = chavePix},
                                new { type = "text", text = "Banco BMG S.A"},

                            }
                        }
                    }
            }
        };
        json = System.Text.Json.JsonSerializer.Serialize(messageTemplate);
        await CallbackMensagem();

    }

    private async Task EnviarTemplate(string numeroDestinatario, string template)
    {
        var messageTemplate = new
        {
            messaging_product = "whatsapp",
            to = numeroDestinatario,
            type = "template",
            template = new
            {
                name = template,
                language = new { code = "pt_BR" }
            }
        };
        json = System.Text.Json.JsonSerializer.Serialize(messageTemplate);
        await CallbackMensagem();
    }

    private string ExtractValue(string input, string fieldName)
    {
        string pattern = $@"\*\*{fieldName}\*\*: (.+)";
        Match match = Regex.Match(input, pattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private async Task RepassarBackEnd(APIGatewayProxyRequestMeta request, RootObject? requestBody)
    {
        if (requestBody.entry[0].changes[0].value.messages[0].type == "audio"
            || requestBody.entry[0].changes[0].value.messages[0].type == "image")
        {
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), "processando");
            var transcricao = await this.GetMessageContentAsync(requestBody.entry[0].changes[0].value.messages[0]);

            //var transcricao = "gostaria de fazer um pix para a chave o pix arroba b m g ponto com no valor de cinquenta e cinco reais";
            var complemento = "devolver a chave pix na da frase seguinte como um tag **Chave PIX**: ";
            Console.WriteLine("transcrição: " + transcricao); 
            var responseApi = await ChamarBackend(request, true, false, complemento + transcricao);
            Console.WriteLine("responseApi: " + responseApi);
            //var responseApi = "As informações sobre PIX na frase são:\n\n**Chave PIX**: opix@bmg.com\n**Valor**: R$ 55,00 (cinquenta e cinco reais)";             
            //var responseApi = "Certo! Vamos revisar as informações do PIX:\n\n**Chave PIX**: opix@bmg.com\n**Nome**: José Silva\n**Instituição**: Banco BMG\n**Valor**: R$ 55,00\n\nPor favor, confirme se todas as informações estão corretas digitando \"sim\" para prosseguir ou \"não\" para cancelar ou fazer alterações.";
            responseApi = responseApi.Replace("\n\n", " ").Replace("\n", " ").Trim().Replace("As informações sobre PIX na frase são:", " ").Replace("Certo! Vamos revisar as informações do PIX:", " ").Trim();
            

            string chavePix = string.Empty;
            string valorPix = string.Empty;  
            chavePix = await ExtrairValor(responseApi, @"\*\*Chave PIX\*\*: ([^\s]+)");
            chavePix = await this.RemoverCaracteresEspeciaisEspacos(chavePix);
            valorPix = await ExtrairValor(responseApi, @"\*\*Valor\*\*: R\$ ([\d,]+)");
            

            //chavePix = await ExtractValueAsync(responseApi, "Chave PIX");
            //string nomeFavorecido = await ExtractValueAsync(responseApi, "Nome");
            //string instituicao = await ExtractValueAsync(responseApi, "Instituição");
            //valorPix = await ExtractValueAsync(responseApi, "Valor");
            string numberWhats = requestBody.entry[0].changes[0].value.messages[0].@from.ToString();

            if (!string.IsNullOrEmpty(chavePix))
            {
                //TODO: Buscar favorecidos do cliente na api do pix e verificar se a chave mencionada está nesses favorecidos

                if (!string.IsNullOrEmpty(valorPix) && !valorPix.Contains("frase"))
                {
                    var templateRevisao = "revisao";
                    await EnviarTemplateRevisaoDadosPix(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRevisao, valorPix.TrimEnd(), "", chavePix.TrimEnd(), "");
                }
                else
                {
                    var templateConfiFav = "confirmacao_favorecido";
                    await EnviarTemplateConfirmacaoFavorecido(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateConfiFav, chavePix.TrimEnd(), "", chavePix.TrimEnd());
                }
            }
            else
            {
                var templateRepita = "nao_entendi";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRepita);
            }
        }
        else if (requestBody.entry[0].changes[0].value.messages[0].type == "button")
        {
            if (requestBody.entry[0].changes[0].value.messages[0].button.payload.Contains("corretas"))
            {
                var templatePerguntaValorPix = "pergunta_valor_pix";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templatePerguntaValorPix);
            }
            else if (requestBody.entry[0].changes[0].value.messages[0].button.payload.Contains("confirmo"))
            {
                var templatePixEnviado = "pix_enviado_sucesso";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templatePixEnviado);
            }
            else if (requestBody.entry[0].changes[0].value.messages[0].button.payload.Contains("Não"))
            {
                var templateRepita = "nao_entendi";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRepita);
            }
        }
        else if (requestBody.entry[0].changes[0].value.messages[0].text.body.Contains(','))
        {
            var templateRevisao = "revisao";
            await EnviarTemplateRevisaoDadosPix(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRevisao, requestBody.entry[0].changes[0].value.messages[0].text.body, "", "pocpix@bmg.com", "");
        }
        else if (requestBody.entry[0].changes[0].value.messages[0].type == "request_welcome")
        {
            var templateOla = "ola";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateOla);

            var templateNovidade = "novidade";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateNovidade);

            var templateTransacoes = "opcoes_transacao";
            EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateTransacoes);
        }
        else if (requestBody.entry[0].changes[0].value.messages[0].text.body == "3")
        {
            var templatePerguntaQualFavorecido = "pergunta_qual_chave_pix";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templatePerguntaQualFavorecido);
        }
        else if (requestBody.entry[0].changes[0].value.messages[0].text.body == "pocpix@bmg.com")
        {
            var templateConfiFav = "confirmacao_favorecido";
            await EnviarTemplateConfirmacaoFavorecido(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateConfiFav, "", "", "pocpix@bmg.com");
        } 
        else if(requestBody.entry[0].changes[0].value.messages[0].text.body.ToLower() == "oi")
        {
            var templateOla = "ola";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateOla);

            var templateNovidade = "novidade";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateNovidade);

            var templateTransacoes = "opcoes_transacao";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateTransacoes);
        }
        else if (requestBody.entry[0].changes[0].value.messages[0].text.body == "1"
                || requestBody.entry[0].changes[0].value.messages[0].text.body == "2"
                || requestBody.entry[0].changes[0].value.messages[0].text.body == "4"
                || requestBody.entry[0].changes[0].value.messages[0].text.body == "5"
                || requestBody.entry[0].changes[0].value.messages[0].text.body == "6"
                || requestBody.entry[0].changes[0].value.messages[0].text.body == "7"
                || requestBody.entry[0].changes[0].value.messages[0].text.body == "8"
                || requestBody.entry[0].changes[0].value.messages[0].text.body == "9")
        {
            var templateSomentePix = "somente_pix";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateSomentePix);
        }
        else
        {
            result = await ChamarBackend(request, false, false, "");

            if (result.Contains("bem-vindo"))
            {
                var templateOla = "ola";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateOla);

                var templateNovidade = "novidade";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateNovidade);

                var templateTransacoes = "opcoes_transacao";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateTransacoes);
            }
            else if (result == "\"Invalid Bot Configuration: No usable messages given the current slot, sessionAttribute, and requestAttribute set.\"")
            {
                var templateOla = "ola";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateOla);
            }
            else if (result == "\"Sem resposta do bot\"")
            {
                var templateRepita = "nao_entendi";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRepita);
            }
            else
            {
                var templateRepita = "nao_entendi";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRepita);
            }
        }
    }

    public async Task<string> RemoverCaracteresEspeciaisEspacos(string input)
    {
        // Remove caracteres especiais usando Regex
        string semCaracteresEspeciais = Regex.Replace(input, @"[^a-zA-Z0-9@]", "");

        // Remove espaços em branco
        string semEspacos = semCaracteresEspeciais.Replace(" ", "");

        if (Regex.IsMatch(semEspacos, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b"))
        {
            return semEspacos;
        }
        else
        {
            return Regex.Replace(semEspacos, @"[^\d]", "");
        } 
    }

    public async Task<string>  ExtrairValor(string frase, string padrao)
    {
        Match match = Regex.Match(frase, padrao);
        return match.Success ? match.Groups[1].Value : string.Empty;
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
            try
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenMetaWhatsapp);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                Console.WriteLine("messageDataText: " + json);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(urlMetaFacebookWhatsapp, content);
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
            catch (Exception e)
            {
                throw e;
            }
        }
    }

    private static async Task<string> ChamarBackend(APIGatewayProxyRequestMeta request, bool audio, bool image, string msg)
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

        BearerToken response_des = System.Text.Json.JsonSerializer.Deserialize<BearerToken>(responseBody);
        var tokenJWT = response_des.access_token;

        // Adiciona o token JWT no cabeçalho da requisição POST
        clienthttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenJWT);

        // Dados para a requisição POST
        var postData = new StringContent(request.body, Encoding.UTF8, "application/json");

        HttpResponseMessage postResponse;

        if (audio && !string.IsNullOrEmpty(msg))
        {
            var postbedrock = new StringContent(msg, Encoding.UTF8, "application/json");
            var res = await clienthttp.PostAsync("https://api-partners-hml.bancobmg.com.br/whatsapp/v1/webhook-whatsapp/contexto?message=" + msg, postbedrock);
            res.EnsureSuccessStatusCode();
            var postResponseBody = await res.Content.ReadAsStringAsync();
            return postResponseBody;
        }
        else if (image)
        {
            clienthttp.PostAsync("https://api-partners-hml.bancobmg.com.br/whatsapp/v1/webhook-whatsapp/image", postData);
            return "";
        }
        else
        {
            // Requisição POST usando o token JWT
            postResponse = await clienthttp.PostAsync("https://api-partners-hml.bancobmg.com.br/whatsapp/v1/webhook-whatsapp?hub.challenge=asdf&hub.verify_token=WhatsappAI&hub.mode=subscribe", postData);
            postResponse.EnsureSuccessStatusCode();
            var postResponseBody = await postResponse.Content.ReadAsStringAsync();
            Console.WriteLine("postResponseBody: " + postResponseBody);
            return postResponseBody;
        }
    }

    private static APIGatewayProxyResponse CadastrarWebhookMeta(APIGatewayProxyRequestMeta request)
    {
        Console.WriteLine("queryStringParameters: " + System.Text.Json.JsonSerializer.Serialize(request.queryStringParameters));

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
        json = System.Text.Json.JsonSerializer.Serialize(messageDataImg);

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
        json = System.Text.Json.JsonSerializer.Serialize(messageDataImg);

        Console.WriteLine("messageDataAudio: " + json);
        return json;
    }

    public async Task<string> InvokeModelAsync(string userMessage)
    {
        //var _bedrockClient = new AmazonBedrockRuntimeClient(RegionEndpoint.USEast1);

        // Set the model ID, e.g., Titan Text Premier.
        var modelId = "amazon.titan-text-premier-v1:0";

        // Define the user message.
        //var userMessage = "Describe the purpose of a 'hello world' program in one line.";

        //Format the request payload using the model's native structure.
        var nativeRequest = System.Text.Json.JsonSerializer.Serialize(new
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
            var response = await _bedrockClient.InvokeModelAsync(request);

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

    public async Task DownloadMideaAndUploadIntoS3Async(string fileName, string extension)
    {
        try
        {
            var fileUrl = await GetUrlToDownloadFileAsync(fileName);
            var mediaStream = await DownloadFileStreamAsync(fileUrl);
            if (mediaStream != null)
                await this.UploadFileAsync(mediaStream, $"{fileName}{extension}");
        }
        catch (Exception e)
        {
            System.Console.WriteLine($"Error: {e.Message}");
        }
    }

    public async Task UploadFileAsync(Stream mediaStream, string fileName)
    {
        //var _s3Client = new AmazonS3Client(RegionEndpoint.USEast1);

        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            InputStream = mediaStream
        };

        try
        {
            PutObjectResponse response = await _s3Client.PutObjectAsync(putRequest);
            Console.WriteLine("Upload concluído com sucesso!");
        }
        catch (AmazonS3Exception e)
        {
            System.Console.WriteLine(e.Message);
        }
        catch (Exception e)
        {
            System.Console.WriteLine(e.Message);
        }
    }

    private async Task<Stream?> DownloadFileStreamAsync(string fileUrl)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenMetaWhatsapp);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Add("User-Agent", "watsappai/0.1");
                var result = await client.GetAsync(fileUrl);
                result.EnsureSuccessStatusCode();
                return await result.Content.ReadAsStreamAsync();

            }
        }
        catch (Exception e)
        {
            System.Console.WriteLine($"Error: {e.Message}");
            return null;
        }
    }

    private async Task<string> GetUrlToDownloadFileAsync(string id)
    {
        try
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokenMetaWhatsapp);
                var result = await client.GetAsync(urlBuscaMedia + id);
                result.EnsureSuccessStatusCode();
                string responseBody = await result.Content.ReadAsStringAsync();
                var convertClass = JsonConvert.DeserializeObject<FileWhatsapp>(responseBody);
                return convertClass?.url ?? "";
            }
        }
        catch (Exception e)
        {
            System.Console.WriteLine($"Error: {e.Message}");
            return "";
        }
    }

    private async Task<string> ExtractValueAsync(string input, string fieldName)
    {
        string pattern = $@"\*\*{fieldName}\*\*: (.+)";
        Match match = Regex.Match(input, pattern);
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    public async Task<string> GetMessageContentAsync(Entidades.Message messageObject)
    {
        switch (messageObject?.type)
        {
            case "text":
                return await Task.FromResult(messageObject.text.body);
            case "image":
                string imageExtension = ".jpeg";
                string imageName = $"{messageObject.image.id}{imageExtension}";
                await this.DownloadMideaAndUploadIntoS3Async(messageObject.image.id, imageExtension);
                return await this.ExtractImageAsync(imageName);
            case "audio":
                string audioExtension = ".ogg";
                string audioName = $"{messageObject.audio.id}{audioExtension}";
                await this.DownloadMideaAndUploadIntoS3Async(messageObject.audio.id, audioExtension);
                return await this.TranscribeAudioAsync(audioName);
            default:
                return await Task.FromResult("");
        }
    }

    public async Task<string> ExtractImageAsync(string keyName)
    {
        try
        {
            //var _textractClient = new AmazonTextractClient(RegionEndpoint.USEast1);

            var request = new AnalyzeDocumentRequest
            {
                Document = new Document
                {
                    S3Object = new Amazon.Textract.Model.S3Object
                    {
                        Bucket = _bucketName,
                        Name = keyName
                    }
                },
                FeatureTypes = new List<string> { "LAYOUT" }
            };

            var response = await _textractClient.AnalyzeDocumentAsync(request);

            string result = string.Empty;

            foreach (var block in response.Blocks)
            {
                Console.WriteLine($"Block Type: {block.BlockType}");
                if (block.BlockType == BlockType.LINE)
                {
                    Console.WriteLine($"Key: {block.Text}");
                    result += block.Text + " ";
                }
            }

            System.Console.WriteLine($"=====>>>>> {result}");
            return result;
        }
        catch (Exception e)
        {
            return e.Message;
        }

    }

    public async Task<string> TranscribeAudioAsync(string keyName)
    {

        try
        {
            //var _transcribeClient = new AmazonTranscribeServiceClient(RegionEndpoint.USEast1);

            string transcriptionJobName = $"transcribe-{Guid.NewGuid()}";

            var transcribeRequest = new StartTranscriptionJobRequest
            {
                TranscriptionJobName = transcriptionJobName,
                LanguageCode = "pt-BR",
                MediaFormat = "ogg",
                Media = new Media
                {
                    MediaFileUri = $"https://{_bucketName}.s3.us-east-2.amazonaws.com/{keyName}"
                },
                OutputBucketName = _bucketName
            };
            var transcribeResponse = await _transcribeClient.StartTranscriptionJobAsync(transcribeRequest);
            var transcription = await GetTranscriptionResultAsync(transcriptionJobName);
            return transcription;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(new Exception(ex.ToString()));
            return ex.Message;
        }
    }

    private async Task<GetTranscriptionJobResponse> GetTranscriptionJobStatusAsync(string transcriptionJobName)
    {
        //var _transcribeClient = new AmazonTranscribeServiceClient(awsAccessKeyId: awsCredentials.AccessKey, awsSecretAccessKey: awsCredentials.SecretKey, awsSessionToken: awsCredentials.Token, region: RegionEndpoint.USEast1);

        var describeTranscriptionJobRequest = new GetTranscriptionJobRequest
        {
            TranscriptionJobName = transcriptionJobName
        };
        return await _transcribeClient.GetTranscriptionJobAsync(describeTranscriptionJobRequest);
    }

    // Função para recuperar a transcrição quando o trabalho for concluído
    private async Task<string> GetTranscriptionResultAsync(string transcriptionJobName)
    {
        var status = new GetTranscriptionJobResponse();
        do
        {
            status = await GetTranscriptionJobStatusAsync(transcriptionJobName);
            if (status.TranscriptionJob.TranscriptionJobStatus == "COMPLETED")
            {
                try
                {
                    using (var response = await this.GetFileAsync($"{transcriptionJobName}.json"))
                    using (var inputStream = response.ResponseStream)
                    using (var reader = new StreamReader(inputStream))
                    {
                        string jsonString = await reader.ReadToEndAsync();
                        JObject jsonObject = JObject.Parse(jsonString);
                        string transcript = (string)jsonObject["results"]["transcripts"][0]["transcript"];
                        return transcript;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine(ex.Message);
                }
            }
            else if (status.TranscriptionJob.TranscriptionJobStatus == "FAILED")
            {
                throw new Exception("Transcription job failed.");
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
            }
        }
        while (status.TranscriptionJob.TranscriptionJobStatus == "IN_PROGRESS" || status.TranscriptionJob.TranscriptionJobStatus == "QUEUED");
        return null;
    }

    public async Task<GetObjectResponse> GetFileAsync(string fileName)
    {
        //var _s3Client = new AmazonS3Client(RegionEndpoint.USEast1);

        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName
        };

        return await _s3Client.GetObjectAsync(request);

    }
}
