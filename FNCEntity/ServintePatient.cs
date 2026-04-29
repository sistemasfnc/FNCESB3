using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class ServintePatient
    {
        public string idPaciente { get; set; }

        public string sdocumenttype { get; set; }
        
        public string sdocument { get; set; }

        public string sfirstname { get; set; }

        public string ssecondname { get; set; }

        public string ssurname { get; set; }

        public string ssecondsurname { get; set; }

        public string sgender { get; set; }

        public DateTime? dbirthdate { get; set; }

        public string sbornplace { get; set; }

        public string smaritalstatus { get; set; }

        public string sphone { get; set; }

        public string sneighborhood { get; set; }

        public string surbanzone { get; set; }

        public string sjob { get; set; }

        public string saddress { get; set; }

        public string smail { get; set; }

        public string safiliation { get; set; }

        public string slevel { get; set; }

        public string scellphone { get; set; }

        public string snation { get; set; }

        public string scity { get; set; }

        public int iid { get; set; }

        public string scovid1 { get; set; }

        public string scovid2 { get; set; }

        public string scityname { get; set; }

        public string sagreementcode { get; set; }

        public string spolicy { get; set; }

        public string sissuingentity { get; set; }

        public string ssourcecountry { get; set; }

        public string ssecondaryemail { get; set; }

        public bool bisparent { get; set; }

        public List<InspiraCita> lappointments { get; set; }        

    }
}
