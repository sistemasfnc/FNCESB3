using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    public class SalesforceResponse<T> where T : class
    {
        public int totalSize { get; set; }
        public bool done { get; set; }
        public List<T> records { get; set; }
    }   
}
