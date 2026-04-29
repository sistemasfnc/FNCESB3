using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;

namespace FNCEntity
{    
    public class InspiraRequest
    {
        public string sid { get; set; }

        public string stype { get; set; }

        public bool bentryassociate { get; set; }

        public List<ServintePatient> lpatients { get; set; }        

    }
}
