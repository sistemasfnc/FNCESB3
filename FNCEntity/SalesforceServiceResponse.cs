using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    public class SalesforceServiceResponse
    {
        public bool success { get; set; }
        public string base64 { get; set; }
        public string message { get; set; }
    }
}
