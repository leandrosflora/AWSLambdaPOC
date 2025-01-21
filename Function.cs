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
using System.Text.RegularExpressions;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace AWSLambdaPOC;

public class Function
{
    string result = string.Empty;
    string json = string.Empty;
    const string urlMetaFacebookWhatsapp = "https://graph.facebook.com/v20.0/519842974541275/messages";
    const string tokenMetaWhatsapp = "EAARKw58BssABO7lShK8dZByUZBAMiZCEHw65KYVMVZCMzfZBmC9XMo0ror4jYeUD5VAFZBmC2lCvftp3oZA98JEYGfciZCe8lAJ1tO1Itg29lYyAoKQtn03T3IUEmgG04ZByNQtNJMcQ2MrZCEGaM3faxUa8ZBtfKLsrajAcl78VTOjEhu08e96rj34oASyl3Yk78TP";
    string whiteList = "5511942302556, 5511949047360, 5511948671189, 5511949836043, 5511996924700";
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
                        json = JsonSerializer.Serialize(messageNaoAutorizados);
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
            Console.WriteLine("NOK4 ");
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
                                new { type = "text", text = nomeFavorecido},
                                new { type = "text", text = Banco},
                                new { type = "text", text = chavepix}
                            }
                        }
                    }
            }
        };
        json = JsonSerializer.Serialize(messageTemplate);
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
                                new { type = "text", text = nomeFavorecido},
                                new { type = "text", text = chavePix},
                                new { type = "text", text = banco},

                            }
                        }
                    }
            }
        };
        json = JsonSerializer.Serialize(messageTemplate);
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
        json = JsonSerializer.Serialize(messageTemplate);
        await CallbackMensagem();
    }

    private async Task<string> ExtractValueAsync(string input, string fieldName)
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
            result = await ChamarBackend(request, true);
            //result = "Certo! Vamos revisar as informações do PIX:\r\n\r\n**Chave PIX**: opix@bmg.com\r\n**Nome**: José Silva\r\n**Instituição**: Banco BMG\r\n**Valor**: R$ 55,00\r\n\r\nPor favor, confirme se todas as informações estão corretas digitando \"sim\" para prosseguir ou \"não\" para cancelar ou fazer alterações.";

            string chavePix = await ExtractValueAsync(result, "Chave PIX");
            string nomeFavorecido = await ExtractValueAsync(result, "Nome");
            string instituicao = await ExtractValueAsync(result, "Instituição");
            string valorPix = await ExtractValueAsync(result, "Valor");

            if (!string.IsNullOrEmpty(valorPix))
            {
                var templateRevisao = "revisao";
                await EnviarTemplateRevisaoDadosPix(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRevisao, valorPix.TrimEnd(), nomeFavorecido.TrimEnd(), chavePix.TrimEnd(), instituicao.TrimEnd());
            }
            else
            {
                var templateConfiFav = "confirmacao_favorecido";
                await EnviarTemplateConfirmacaoFavorecido(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateConfiFav, chavePix.TrimEnd(), instituicao.TrimEnd(), chavePix.TrimEnd());
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
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateRevisao);
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
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateConfiFav);
        }
        else if (requestBody.entry[0].changes[0].value.messages[0].text.body.Contains(','))
        {
            var templatePerguntaValorPix = "pergunta_valor_pix";
            await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templatePerguntaValorPix);
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
            result = await ChamarBackend(request, false);

            if (result.Contains("bem-vindo"))
            {
                var templateOla = "ola";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateOla);

                var templateNovidade = "novidade";
                await EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateNovidade);

                var templateTransacoes = "opcoes_transacao";
                EnviarTemplate(requestBody.entry[0].changes[0].value.messages[0].@from.ToString(), templateTransacoes);
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

    private static async Task<string> ChamarBackend(APIGatewayProxyRequestMeta request, bool audio)
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

        HttpResponseMessage postResponse;

        if (audio)
        {
            postResponse = await clienthttp.PostAsync("https://api-partners-hml.bancobmg.com.br/whatsapp/v1/audio?hub.challenge=asdf&hub.verify_token=WhatsappAI&hub.mode=subscribe", postData);
        }
        else
        {
            // Requisição POST usando o token JWT
            postResponse = await clienthttp.PostAsync("https://api-partners-hml.bancobmg.com.br/whatsapp/v1/webhook-whatsapp?hub.challenge=asdf&hub.verify_token=WhatsappAI&hub.mode=subscribe", postData);

        }

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