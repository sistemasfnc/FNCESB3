using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEstadistica;
using EventLog;

namespace FNCSincronizaEstadistica
{
    internal class EstadisticaCMD
    {
        private static SincronizaEstadistica sincronizaEstadistica { get; set; }
        static void Main(string[] args)
        {
            sincronizaEstadistica = new SincronizaEstadistica();
            try
            {
                //sincronizaEstadistica.DoLogin();
                doProcess(args);
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex); ;
            }
            
        }

        static void doProcess(string[] args)
        {   
            /*
            try
            {
                sincronizaEstadistica.GenerateProductByGroup();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los productos por grupo"));
            }
            
            try
            {
                sincronizaEstadistica.GeneratePlans(); 
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los planes"));
            }
            
          
            try
            {
                sincronizaEstadistica.GenerateAccounts();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los pacientes"));
            }
            */
            
            
            /*
            try
            {
                sincronizaEstadistica.GenerateAppointments();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las citas"));
            }
            */

            
            try
            {
                sincronizaEstadistica.GenerateUsage();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los usos de autorización"));
            }                        
            

            /*
            try
            {
                sincronizaEstadistica.GetAssesments();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las consultas"));
            }

            try
            {
                sincronizaEstadistica.GetPrescriptions();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las prescripciones"));
            }

            try
            {
                sincronizaEstadistica.GetDiagnosis();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los diagnosticos"));
            }
            

            try
            {
                sincronizaEstadistica.GetOrders();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las ordenes"));
            }

            */
            /*try
            {
                sincronizaEstadistica.GetPFP();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las pruebas de función pulmonar"));
            }*/
            /*try
            {
                sincronizaEstadistica.GetRHB();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las pruebas de rehabilitación"));
            }*/

            /*
            try
            {
                sincronizaEstadistica.GetSpeelTest();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las pruebas de rehabilitación"));
            }
            */
            /*
            try
            {
                if (args != null && args.Length > 0)
                {
                    if (args[0].Equals("true"))
                    {
                        sincronizaEstadistica.GenerateEspaciosVaciosAgendaInicial();
                    }
                    else
                    {
                        sincronizaEstadistica.GenerateEspaciosVaciosAgendaIncremental();
                    }
                }
                //sincronizaEstadistica.GenerateEspaciosVaciosAgendaIncremental();
            }
            catch (Exception)
            {

                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los espacios vacios de las citas"));
            }
            */
        }

    }
}
