using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using FNCDAC;
using FNCEntity;
using FNCUtils;
using EventLog;
using FNCSalesforce;

namespace FNCServicioProgramas
{
    public partial class CargoProgramas : ServiceBase
    {
        private static List<ServintePatient> lPatient { get; set; }

        private static List<Generic> lTemplate { get; set; }

        private static List<Generic> lGeneric { get; set; }

        private static List<EntryResponse> lentries { get; set; }

        public CargoProgramas()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            Timer timer1 = new Timer();
            timer1.Elapsed += new ElapsedEventHandler(this.OnTimer);
            timer1.Interval = 7200000;
            timer1.Start();
        }

        protected void OnTimer(object sender, ElapsedEventArgs args)
        {

        }

        protected override void OnStop()
        {
        }

        static void GetProductsByRate()
        {
            using (ServinteOracle servinteOracle = new ServinteOracle())
            {
                servinteOracle.sconnection = FNCCargoProgramas.Properties.Settings.Default.ServinteBus;
                lGeneric = servinteOracle.GetProductConceptsyRate();
            }
        }

        static Generic GetProductRate(string srate, string scostcenter, string sproduct, string sstartwith)
        {
            return lGeneric.FirstOrDefault(x => x.scode == srate && x.sname == sproduct && x.sfilter == scostcenter && x.sextra1.StartsWith(sstartwith));
        }

        static void GroupPatients()
        {
            var patientsResult = lPatient.GroupBy(r => new { r.sdocument, r.sdocumenttype }).Select(g => g.ToList()).ToList();
            List<ServintePatient> lResult = new List<ServintePatient>();
            ServintePatient servintePatient = null;
            List<InspiraCita> lCitas = null;
            foreach (var item in patientsResult)
            {
                servintePatient = item[0];
                lCitas = new List<InspiraCita>();
                foreach (var item1 in item)
                {
                    lCitas.Add(item1.lappointments[0]);
                }
                servintePatient.lappointments = lCitas;
                lResult.Add(servintePatient);
            }
            lPatient = lResult;
        }

        static void CreateCharges()
        {
            ServinteOracle oServinte = null;
            InspiraRequest inspiraRequest = null;
            try
            {
                inspiraRequest = new InspiraRequest()
                {
                    lpatients = lPatient,
                    stype = "Programas",
                };
                oServinte = new ServinteOracle(FNCCargoProgramas.Properties.Settings.Default.ServinteDB);
                lentries = oServinte.CreateChargesForPrograms(lPatient);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                oServinte.Dispose();
                oServinte = null;
            }

        }

        static void CreateStatistics()
        {
            using (Integrador integrador = new Integrador())
            {
                integrador.sconnection = FNCCargoProgramas.Properties.Settings.Default.Integrador;
                lentries = integrador.InsertRecord(lentries);
            }
        }

        static void GetPatients()
        {
            lPatient = new List<ServintePatient>();
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                var result = salesforceIntegrator.Login(FNCCargoProgramas.Properties.Settings.Default.SalesforceCompany,
                    FNCCargoProgramas.Properties.Settings.Default.SalesforceUser, FNCCargoProgramas.Properties.Settings.Default.SalesforcePassword, FNCCargoProgramas.Properties.Settings.Default.SalesforceToken);
                if (!string.IsNullOrEmpty(result.scode))
                {
                    salesforceIntegrator.sSession = result.scode;
                    salesforceIntegrator.sUrl = result.sname;
                    if (DateTime.Now.DayOfWeek == DayOfWeek.Tuesday || DateTime.Now.DayOfWeek == DayOfWeek.Friday)
                    {
                        lPatient.AddRange(salesforceIntegrator.GetPatientsforPrograms("FAMISANAR"));
                        lPatient.AddRange(salesforceIntegrator.GetPatientsforPrograms("SANITAS"));
                    }
                    lPatient.AddRange(salesforceIntegrator.GetPatientsforPrograms("INVESTIGACION"));
                }
            }
        }
    }
}
