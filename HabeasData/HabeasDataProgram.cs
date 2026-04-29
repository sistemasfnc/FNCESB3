using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCDAC;
using FNCEntity;
using EventLog;
using FNCFacade;

namespace HabeasData
{
    class HabeasDataProgram
    {
		static List<Patient> patients { get; set; }
        static void Main(string[] args)
        {
			GetPatients();
			UpdatePatients();
        }

		static void UpdatePatients()
		{
			FacadeInspiraServinte facadeInspiraServinte = null;
			try
			{
				facadeInspiraServinte = new FacadeInspiraServinte();
				facadeInspiraServinte.ActualizaCuentaInspra(patients);
			}
			catch (Exception ex)
			{
				LogError.WriteError("Habeas Data", "Application", ex);				
			}
			finally
			{
				facadeInspiraServinte.Dispose();
				facadeInspiraServinte = null;
			}
		}

        static void GetPatients()
        {
			ConsInformado consInformado = new ConsInformado();
			try
			{
				consInformado.sConnection = HabeasData.Properties.Settings.Default.ConsInformado;
				patients = consInformado.GetResearchData();
				patients = patients.Distinct().ToList();
			}
			catch (Exception ex)
			{
				LogError.WriteError("Habeas Data", "Application", ex);
			}
			finally
			{
				consInformado.Dispose();
				consInformado = null;
			}
		}
    }
}
