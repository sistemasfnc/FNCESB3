using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class TurnoEntity
    {
        public bool counting { get; set; }

        public bool notprint { get; set; }

        public int requestedhour { get; set; }

        public string queuecode { get; set; }

        public string prioritycode { get; set; }

        public string roomcode { get; set; }

        public string patientcode { get; set; }

        public string patientid { get; set; }

        public string patientname { get; set; }

        public string observations { get; set; }
    }

    [Serializable]
    public class TurnResult
    {
        public string turnnumber { get; set; }

        public string turncode { get; set; }

        public string errorcode { get; set; }

        public string errordescription { get; set; }

        public int chargenumber { get; set; }

        public string turnid { get;set; }

        public List<string> appointments { get; set; }
    }
}
