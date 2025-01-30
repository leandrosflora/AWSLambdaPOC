using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AWSLambdaPOC.Entidades
{
    public class FileWhatsapp
    {
        public string url { get; set; }
        public string mime_type { get; set; }
        public string sha256 { get; set; }
        public string file_size { get; set; }
        public string id { get; set; }
    }
}
