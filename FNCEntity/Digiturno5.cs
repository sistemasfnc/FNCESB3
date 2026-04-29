using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FNCEntity
{
    [Serializable]
    public class Digiturno5
    {
        public PatientCiel oPatient { get; set; }

        public Result oResult { get; set; }

        public string sappointments { get; set; }
    }

    [Serializable]
    public class PatientCiel
    {        

        public int iplan { get; set; }

        public int iunit { get; set; }

        public int iattendance { get; set; }

        public string sfirstname { get; set; }

        public string ssecondname { get; set; }

        public string sfirstsurname { get; set; }

        public string ssecondsurname { get; set; }
    }

    [Serializable]
    public class Result
    {
        public int iresult { get; set; }

        public string smessage { get; set; }

        public int iroom { get; set; }
    }
    
    public class Turn
    {
        public string Key { get; set; }

        public string Value { get; set; }
    }
}
