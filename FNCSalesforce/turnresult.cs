using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCSalesforce
{
    public class turnresult
    {
        public int idturn { get; set; }

        public string status { get; set; }

        public string message { get; set; }
    }

    public class turn
    {
        public int idturn { get; set; }
        public List<Appointment> appointments { get; set; }
        public int distance { get; set; }
    }

    public class Appointment
    {
        public string id { get; set; }
    }

}
