using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCUtils;
using FNCDAC;
using FNCEntity;
using System.Data;

namespace FNCFacade
{
    public class FacadeEspirometria
    {
        public string sfechainicial { get; set; }

        public string sfechafinal { get; set; }

        public string sconnection { get; set; }

        public FacadeEspirometria() 
        { 
        }

        public DataTable GetSanitasPrfp()
        {
            List<SanitasPrfp> sanitasPrfps = null;
            using (Espirometria espirometria = new Espirometria()) 
            {
                espirometria.sconnection = this.sconnection;
                sanitasPrfps = espirometria.GetSanitasEspirometrias(this.sfechainicial, this.sfechafinal);
                return Tools.ToDataTable(sanitasPrfps);
            }
        }

        public List<SanitasPrfp> GetSanitasPrfp(bool bgenericlist)
        {
            List<SanitasPrfp> sanitasPrfps = null;
            using (Espirometria espirometria = new Espirometria())
            {                
                espirometria.sconnection = this.sconnection;
                sanitasPrfps = espirometria.GetSanitasEspirometrias(this.sfechainicial, this.sfechafinal);
                return sanitasPrfps;
            }
        }

        public List<SanitasPrfp> GetSuraPrfp()
        {
            List<SanitasPrfp> sanitasPrfps = null;
            using (Espirometria espirometria = new Espirometria())
            {
                espirometria.sconnection = this.sconnection;
                sanitasPrfps = espirometria.GetSuraEspirometrias(this.sfechainicial, this.sfechafinal);
                return sanitasPrfps;
            }
        }

        public List<SanitasPrfp> GetEcopetrolPrfp()
        {
            List<SanitasPrfp> sanitasPrfps = null;
            using (Espirometria espirometria = new Espirometria())
            {
                espirometria.sconnection = this.sconnection;
                sanitasPrfps = espirometria.GetEcopetrolEspirometrias(this.sfechainicial, this.sfechafinal);
                return sanitasPrfps;
            }
        }
    }
}
