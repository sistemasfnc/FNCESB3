using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class Consentimiento
    {
        public string sfirstname { get; set; }

        public string ssecondname { get; set; }

        public string ssurname { get; set; }

        public string ssecondsurname { get; set; }

        public string scups { get; set; }

        public string sdocument { get; set; }

        public string sdocumenttype { get; set; }

        public DateTime dappointmentdate { get; set; }

        public string sappointmemt { get; set; }

        public string sservicename { get; set; }

        public string sphone { get; set; }

        public int iage { get; set; }

        public string sid { get; set; }

        public string shabeasdata { get; set; }
    }
}
