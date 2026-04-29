using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    /// <summary>
    /// Entidad que almacena los registros exitosos de la integración
    /// </summary>
    public class SincronizaTemporal
    {
        /// <summary>
        /// String tabla sincronizada
        /// </summary>
        public string stable { get; set; }

        /// <summary>
        /// String parámetro auxiliar 1
        /// </summary>
        public string sparameter1 { get; set; }

        /// <summary>
        /// String parámetro auxiliar 2
        /// </summary>
        public string sparameter2 { get; set; }

        /// <summary>
        /// String parámetro auxiliar 3
        /// </summary>
        public string sparameter3 { get; set; }

        /// <summary>
        /// String parámetro auxiliar 4
        /// </summary>
        public string sparameter4 { get; set; }
    }
}
