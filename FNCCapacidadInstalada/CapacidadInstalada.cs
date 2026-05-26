using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEstadistica;
using EventLog;

namespace FNCCapacidadInstalada
{
    internal class CapacidadInstalada
    {
        private static SincronizaEstadistica sincronizaEstadistica { get; set; }

        static void Main(string[] args)
        {
            sincronizaEstadistica = new SincronizaEstadistica(false);
            doProcess(args);
        }

        static void doProcess(string[] args)
        {            
            try
            {
                if (args == null || args.Length == 0)
                {
                    DateTime inicial = DateTime.Now.AddDays(-1);
                    //DateTime inicial = new DateTime(2026, 1, 1);
                    //DateTime final = new DateTime(2026, 5, 3);
                    DateTime final = DateTime.Now.AddDays(60);
                    sincronizaEstadistica.GenerateCapacidadAgenda(inicial, final);
                }
                else
                {
                    if (Convert.ToBoolean(args[0]))
                    {

                        sincronizaEstadistica.GenerateAgendaDetalleInicial();
                    }
                    else
                    {
                        sincronizaEstadistica.GenerateAgendaDetalleIncremental();
                    }
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCCapacidadInstalada", "FNCCapacidadInstalada", ex); ;
            }
        }
    }
}
