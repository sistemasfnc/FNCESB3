using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class Charge
    {
        public string ssource { get; set; }

        public int inumber { get; set; }

        public int iyear { get; set; }

        public string smonth { get; set; }

        public string screatedby { get; set; }

        public DateTime dcreateddate { get; set; }

        public string sdocument { get; set; }

        public string sdocumenttype { get; set; }

        public string sagreementcode { get; set; }

        public string sagreementname { get; set; }

        public string splan { get; set; }

        public string srate { get; set; }

        public string sadmissiontype { get; set; }

        public string sassignedto { get; set; }

        public DateTime dassigneddate { get; set; }

        public DateTime dassignedtime { get; set; }

        public string sauthorization { get; set; }

        public string sbranch { get; set; }

        public string sagreementtype { get; set; } //Tipo de persona (E para empresa P para Particular)

        public List<ChargeDetail> ldetail { get; set; }

        public string stemplate { get; set; }

        public string snit { get; set; }

        public string scostcenter { get; set; }

        public string splanname { get; set; }

        public int iusertype { get; set; }

        public int ilevel { get; set; }

        public string spatientname { get; set; }

        public string sprogram { get; set; }

        public string scode { get; set; }

        public string sbranchname { get; set; }

        public List<string> lssources { get; set; }

        public string sattentiontype { get; set; }

        public string sconcept { get; set; }

        public string sappointment { get; set; }
    }

    [Serializable]
    public class ChargeDetail
    {
        public int inumber { get; set; }

        public string sservice { get; set; }

        public string sconcept { get; set; }

        public string scostcenter { get; set; }

        public decimal dtotal { get; set; }

        public string snit { get; set; }

        public int iqty { get; set; }

        public string stype { get; set; }

        public int itype { get; set; }

        public string scode { get; set; }

        public List<Generic> lsources { get; set; }

        public string sgroupname { get; set; }
    }
}
