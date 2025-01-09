namespace AWSLambdaPOC.Entidades
{

    public class APIGatewayProxyRequestMeta
    {
        public string httpMethod { get; set; }                          // Método HTTP da solicitação (GET, POST, etc.)
        public string path { get; set; }                                 // Caminho do endpoint
        public string body { get; set; }                                 // Corpo da solicitação
        public bool isBase64Encoded { get; set; }                        // Indica se o corpo está codificado em Base64
        public Dictionary<string, string> queryStringParameters { get; set; } // Parâmetros de consulta
        public Dictionary<string, string> headers { get; set; }          // Cabeçalhos da solicitação
                                                                         //public RequestContext RequestContext { get; set; }               // Informações contextuais da solicitação
        public string resource { get; set; }                              // O recurso associado ao endpoint
    }

    public class BearerToken
    {
        public string access_token { get; set; }
        public string token_type { get; set; }
    }

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
}
