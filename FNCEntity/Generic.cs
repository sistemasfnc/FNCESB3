using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    /// <summary>
    /// Objeto genérico para almacenar tablas sencillas
    /// </summary>
    [Serializable]
    public class Generic
    {
        /// <summary>
        /// Id de la tabla
        /// </summary>
        public int iid { get; set; }

        /// <summary>
        /// Nombre de la fila
        /// </summary>
        public string sname { get; set; }


        /// <summary>
        /// Código de la fila
        /// </summary>
        public string scode { get; set; }

        /// <summary>
        /// Filtro adicional para la fila
        /// </summary>
        public string sfilter { get; set; }

        /// <summary>
        /// Filtro fecha para la fila
        /// </summary>
        public DateTime dtDate { get; set; }

        /// <summary>
        /// Valor extra 1
        /// </summary>
        public string sextra1 { get; set; }

        /// <summary>
        /// Valor extra 2
        /// </summary>
       public double dextra2 { get; set; }

        /// <summary>
        /// Valor extra 3
        /// </summary>
        public int iextra3 {  get; set; }

        /// <summary>
        /// Valor extra 4
        /// </summary>
        public int iextra4 { get; set; }
    }
}
