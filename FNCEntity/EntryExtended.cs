using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class EntryExtended
    {
        public string idPaciente { get; set; }

        public string idCargo { get; set; }

        public int ientry { get; set; }

        public int icharge { get; set; }

        public int iid { get; set; }

        public int ipatient { get; set; }

        public int ientrysource { get; set; }

        public string sdocument { get; set; }

        public string sdocumenttype { get; set; }

        public string sagreement { get; set; }

        public string splan { get; set; }

        public string srate { get; set; }

        public DateTime ddate { get; set; }

        public string scostcenter { get; set; }

        public int iqty { get; set; }

        public string sid { get; set; }

        public string sservice { get; set; }

        public string sauthorization { get; set; }

        public string sappointment { get; set; }

        public string sevent { get; set; }

        public string sconcept { get; set; }

        public string stype { get; set; }

        public string sservicename { get; set; }

        public decimal dvalue { get; set; }

        public int iline { get; set; }

        public string sunit { get; set; }

        public string stemplate { get; set; }
        public string spatient { get; set; }

        public int iinvoice { get; set; }

        public DateTime? dinitial { get; set; }

        public DateTime? dfinal { get; set; }

        public string sconceptname { get; set; }
    }
}
