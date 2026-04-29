using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class TarifaProducto
    {
        public string sproducto { get; set; }

        public string starifa { get; set; }

        public string sconcepto { get; set; }

        public string scentro { get; set; }

        public DateTime dfecha { get; set; }

        public int ivalor { get; set; }

        public int isincronizado { get; set; }

        public int iedicion { get; set; }

        public int iid { get; set; }
    }
}
