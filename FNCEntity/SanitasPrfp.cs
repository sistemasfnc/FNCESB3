using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    public class SanitasPrfp
    {
        public DateTime dtFecha { get; set; }

        public string sCodIPS { get; set; }

        public string sNomIPS { get; set; }

        public string sTipoDocumento { get; set; }

        public string sDocumento { get; set; }

        public string sNombres { get; set; }

        public string sApellidos { get; set; }

        public int iEdad { get; set; }

        public string sGenero { get; set; }

        public decimal dPeso { get; set; }

        public decimal dTalla { get; set; }

        public decimal dIMC { get; set; }

        public string sDiagnostico { get; set; }

        public string sCIE10 { get; set; }

        public string sFactorRiesgo { get; set; }
        
        public string sTabaquismo { get; set; }

        public string sProcedimiento { get; set; }

        public decimal dCVFPre { get; set; }

        public decimal dVEF1Pre { get; set; }

        public decimal dTasaPre { get; set; }

        public decimal dCVFPos { get; set; }

        public decimal dVEF1Post { get; set; }

        public decimal dTasaPos { get; set; }

        public decimal dCambio { get; set; }

        public string sResultado { get; set; }

        public string sObservaciones { get; set; }

        public string sProviene { get; set; }
    }
}
