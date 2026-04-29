using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCDAC;
using FNCEntity;
using FNCUtils;
using EventLog;

namespace MigrateCharges
{
    class DoMigration
    {        
        static List<ServintePatient> servintePatients { get; set; }

        static List<ServintePackage> servintePackage { get; set; }

        static void Main(string[] args)
        {
            try
            {
                GetRates();
                GetPatients();
                DoTheMigration();
            }
            catch (Exception ex)
            {
                LogError.WriteError("Migracion", "Aplicacion", ex);
                throw;
            }
        }

        static void GetPatients()
        {
            using (Servinte servinte = new Servinte(MigrateCharges.Properties.Settings.Default.FNCStats, false))
            {
                servintePatients = servinte.GetMigrationCharges(servintePackage);
                /*int i = 0;
                int j = 0;
                foreach (var item in servintePatients)
                {
                    foreach (var item1 in item.lappointments)
                    {
                        j++;
                        foreach (var item2 in item1.lservices)
                        {
                            i++;
                        }
                    }
                }
                int k = i;*/
            }
        }

        static void DoTheMigration()
        {
            using (ServinteOracle servinteOracle = new ServinteOracle(MigrateCharges.Properties.Settings.Default.ServinteOracle))
            {                
                servinteOracle.CreateChargesFromServinte(servintePatients);
            }
        }

        static void GetRates()
        {
            using (Servinte servinte = new Servinte(MigrateCharges.Properties.Settings.Default.IntegraBus, false))
            {
                servintePackage = servinte.GetProductRates();
            }
        }
    }
}
