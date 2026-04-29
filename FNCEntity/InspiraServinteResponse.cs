
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    public class InspiraServinteResponse
    {
        public List<EntryResponse> lentry { get; set; }

        public ErrorResponse error { get; set; }
    }
}
