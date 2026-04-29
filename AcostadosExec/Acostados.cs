using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCDAC;

namespace AcostadosExec
{
    class Acostados
    {
        static void Main(string[] args)
        {
            using (ServinteOracle servinte = new ServinteOracle())
            {
                servinte.sconnection = AcostadosExec.Properties.Settings.Default.FNEUMB;
                servinte.CreatePendingChargesReport();
                Console.WriteLine("Procedimiento ejecutado correctamente");
            }
        }
    }
}
