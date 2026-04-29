using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class SynapseEntity
    {
        public string ID_PACIENTE { get; set; }

        public string NOMBRES_PACIENTE { get; set; }

        public string APELLIDOS_PACIENTE { get; set; }

        public string TIPO_DOCUMENTO { get; set; }

        public string NUMERO_DE_ACCESO { get; set; }

        public string FECHA_NACIMIENTO { get; set; }

        public string GENERO { get; set; }

        public string TELEFONO { get; set; }

        public string EMAIL { get; set; }
         
        public string DIRECCION { get; set; }

        public string DEPARTAMENTO { get; set; }

        public string MUNICIPIO { get; set; }

        public string TIPO_PACIENTE { get; set; }

        public string UBICACION_PACIENTE { get; set; }

        public string MODALIDAD { get; set; }

        public string COD_ESTUDIO { get; set; }

        public string DESCRIPCION_ESTUDIO { get; set; }

        public string JUSTIFICACION { get; set; }

        public string PROFESIONAL_ORDENA { get; set; }

        public string ACCION_ORDEN { get; set; }

        public string ID_ASEGURADORA { get; set; }

        public string NOMBRE_ASEGURADORA { get; set; }
    }
}
