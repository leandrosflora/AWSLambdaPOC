# AWSLambdaPOC — Webhook WhatsApp para Pix via AWS Lambda

Este projeto é uma prova de conceito em .NET 8 para uma função AWS Lambda exposta via API Gateway. A Lambda recebe eventos de webhook do WhatsApp Cloud API/Meta, valida o webhook, interpreta mensagens do usuário e conduz um fluxo conversacional de Pix usando serviços AWS e um backend externo.

## O que a Lambda faz

A função principal (`AWSLambdaPOC::AWSLambdaPOC.Function::FunctionHandler`) trata dois tipos de chamada:

1. **Validação do webhook da Meta via GET**
   - Lê os parâmetros `hub.mode`, `hub.challenge` e `hub.verify_token`.
   - Retorna o `hub.challenge` com HTTP 200 quando o token de verificação é válido.
   - Retorna HTTP 403 quando o token não confere.

2. **Processamento de mensagens do WhatsApp via POST**
   - Desserializa o payload do WhatsApp recebido pelo API Gateway.
   - Verifica se o telefone de origem está na whitelist configurada no código.
   - Para usuários não autorizados, envia a mensagem `Cliente não autorizado` pelo WhatsApp.
   - Para usuários autorizados, encaminha o evento para o fluxo de backend e responde com templates ou mensagens do WhatsApp.

## Fluxos implementados

### Saudação e menu inicial

Quando o usuário envia `oi` ou uma mensagem de boas-vindas, a Lambda envia templates do WhatsApp para:

- cumprimento inicial (`ola`);
- apresentação de novidades (`novidade`);
- opções de transação (`opcoes_transacao`).

### Fluxo Pix

A POC é focada em Pix. Entre os comportamentos implementados estão:

- quando o usuário seleciona a opção `3`, a Lambda pergunta qual é a chave Pix;
- quando a chave `pocpix@teste.com` é enviada, a Lambda dispara template de confirmação de favorecido;
- quando o usuário informa um valor contendo vírgula, a Lambda envia um template de revisão com valor e chave Pix;
- respostas de botões como `corretas`, `confirmo` e `Não` disparam os próximos templates do fluxo:
  - pergunta de valor Pix;
  - Pix enviado com sucesso;
  - mensagem de não entendimento.

Opções diferentes das previstas para Pix retornam o template `somente_pix`.

### Áudio e imagem

A Lambda também aceita mensagens do tipo `audio` e `image`:

- envia um template de processamento (`processando`);
- baixa a mídia usando a Graph API da Meta;
- salva o arquivo no bucket S3 configurado (`whatsappai`);
- para áudio, usa Amazon Transcribe em português do Brasil (`pt-BR`) para obter a transcrição;
- para imagem, usa Amazon Textract para extrair texto do documento/imagem;
- envia o texto extraído para o backend/IA com uma instrução para localizar chave Pix e valor;
- extrai `**Chave PIX**` e `**Valor**` da resposta retornada;
- quando encontra chave e valor, envia template de revisão;
- quando encontra apenas a chave, envia template de confirmação de favorecido;
- quando não entende a mensagem, envia template `nao_entendi`.

### Integração com backend externo

Para algumas mensagens, a Lambda:

1. obtém um token JWT em um endpoint OAuth;
2. encaminha o payload do WhatsApp ou o conteúdo extraído de áudio/imagem para endpoints de backend;
3. usa a resposta para decidir qual template enviar ao usuário.

Os endpoints e credenciais de exemplo no código estão mascarados ou vazios e devem ser configurados antes do uso em um ambiente real.

## Serviços e APIs utilizados

- **AWS Lambda** para executar a função serverless.
- **Amazon API Gateway** como entrada HTTP do webhook.
- **WhatsApp Cloud API / Meta Graph API** para receber eventos, consultar mídia e enviar mensagens/templates.
- **Amazon S3** para armazenar mídias recebidas e resultados de transcrição.
- **Amazon Transcribe** para transcrever áudios `.ogg` em `pt-BR`.
- **Amazon Textract** para extrair texto de imagens/documentos.
- **Amazon Bedrock Runtime** está referenciado no projeto e possui método de invocação para o modelo `amazon.titan-text-premier-v1:0`.
- **Amazon Lex Runtime V2** está inicializado para integração conversacional.

## Estrutura principal

```text
.
├── Function.cs                         # Handler da Lambda e regras do fluxo WhatsApp/Pix
├── Entidades/
│   ├── Meta.cs                         # Classes do payload da Meta/WhatsApp
│   └── FileWhatsapp.cs                 # Modelo de resposta de mídia da Graph API
├── AWSLambdaPOC.csproj                 # Projeto .NET 8 e dependências AWS
├── aws-lambda-tools-defaults.json      # Configurações padrão de deploy da Lambda
└── Properties/launchSettings.json      # Configuração do Mock Lambda Test Tool
```

## Configurações importantes

Antes de executar ou publicar a Lambda, revise os seguintes pontos no código e na configuração de deploy:

- `tokenMetaWhatsapp`: token Bearer da WhatsApp Cloud API.
- `urlMetaFacebookWhatsapp`: endpoint de envio de mensagens da Meta.
- `urlBuscaMedia`: endpoint base da Graph API para busca de mídia.
- `_bucketName`: bucket S3 usado para arquivos de mídia e transcrições.
- `whiteList`: lista de números autorizados a usar o fluxo.
- Endpoints OAuth/backend em `ChamarBackend`.
- Perfil, região, nome da função, role e timeout em `aws-lambda-tools-defaults.json`.

> Observação: não é recomendado manter tokens, chaves ou segredos hardcoded no código. Para produção, use variáveis de ambiente, AWS Secrets Manager ou outro mecanismo seguro.

## Executar localmente

Restaure e compile o projeto:

```bash
dotnet restore
dotnet build
```

Também é possível usar o AWS .NET Lambda Mock Test Tool configurado em `Properties/launchSettings.json`.

## Deploy

Instale ou atualize a ferramenta de Lambda para .NET:

```bash
dotnet tool install -g Amazon.Lambda.Tools
# ou
dotnet tool update -g Amazon.Lambda.Tools
```

Publique a função usando as configurações de `aws-lambda-tools-defaults.json`:

```bash
dotnet lambda deploy-function
```

## Payload esperado

A Lambda espera receber um objeto compatível com API Gateway contendo, no mínimo:

- `httpMethod`: método HTTP (`GET` para validação ou `POST` para eventos);
- `queryStringParameters`: parâmetros de validação da Meta no GET;
- `body`: JSON original do webhook da Meta/WhatsApp no POST.

As classes de suporte em `Entidades/Meta.cs` representam o payload da Meta com `entry`, `changes`, `messages`, `text`, `button`, `image` e `audio`.

## Limitações da POC

- Tokens, whitelist e alguns endpoints estão vazios, fixos ou mascarados.
- O fluxo está fortemente acoplado a templates específicos do WhatsApp.
- Alguns dados de Pix são simulados para demonstração.
- O tratamento de erros retorna HTTP 200 em algumas falhas para evitar reprocessamentos do webhook, mas isso deve ser revisado conforme a estratégia operacional.
