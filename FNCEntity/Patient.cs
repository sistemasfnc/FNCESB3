using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class Patient
    {
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

        public string screatedby { get; set; }

        public DateTime dcreateddate { get; set; }

        public DateTime dcreatedtime { get; set; }

        public string surbanzone { get; set; }

        public string sjob { get; set; }

        public string sbranch { get; set; }

        public string saddress { get; set; }

        public Charge oCharge { get; set; }

        public bool bhaserror { get; set; }

        public string serror { get; set; }

        public int iage { get; set; }

        public int istatus { get; set; }

        public string sname
        {
            get { return sfirstname + " " + ssecondname; }
        }

        public string smail { get; set; }     

    }
}
