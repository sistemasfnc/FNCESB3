using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class InspiraCita
    {
        public string sappointment { get; set; }
        
        public int ientry { get; set; }

        public string sattentiontype { get; set; }

        public string sservicetype { get; set; }

        public string sagreementtype { get; set; }

        public string scie10 { get; set; }

        public string sservicegroup { get; set; }

        public string sagreement { get; set; }

        public string sagreementname { get; set; }

        public string sratename { get; set; }

        public string sunit { get; set; }

        public string sthird { get; set; }

        public string stemplate { get; set; }

        public string scostcenter { get; set; }

        public string srate { get; set; }

        public DateTime ddate { get; set; }

        public string sauthorization { get; set; }

        public string splan { get; set; }

        public string sattendingtype { get; set; }

        /// <summary>
        /// Lista genérica de servicios
        /// </summary>
        public List<ServiceRequest> lservices { get; set; }

        /// <summary>
        /// Código del cajero
        /// </summary>
        public string suser { get; set; }

        /// <summary>
        /// Nombre de la cita
        /// </summary>
        public string sname { get; set; }

        /// <summary>
        /// Número de contrato para el plan
        /// </summary>
        public string scontract { get; set; }

        /// <summary>
        /// Valor total de la cita
        /// </summary>
        public int itotal { get; set; }

        public int ientrysource { get; set; }
    }
}
