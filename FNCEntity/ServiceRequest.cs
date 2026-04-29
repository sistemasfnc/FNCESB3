using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class ServiceRequest
    {
        public string idCargo { get; set; }        

        public string srate { get; set; }

        public string sconcept { get; set; }

        public string scostcenter { get; set; }

        public string sauthorization { get; set; }

        public string sservice { get; set; }

        public int iqty { get; set; }

        public decimal ivalue { get; set; }

        public decimal idiscount { get; set; }

        public bool bbilleable { get; set; }
        
        public bool bisprocedure { get; set; }

        public string sservicename { get; set; }

    }
}
