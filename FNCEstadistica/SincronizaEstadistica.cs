using EventLog;
using FNCEntity;
using FNCFacade;
using FNCSalesforce;
using FNCUtils;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using System.Xml;

namespace FNCEstadistica
{
    public class SincronizaEstadistica : IDisposable
    {
        private Generic oSession { get; set; }


        public SincronizaEstadistica(bool needLogin = true)
        {
            if (needLogin)
            {
                this.DoLogin();
            }
        }

        /// <summary>
        /// Método para ingresar masivamente la información en la base de datos
        /// </summary>
        /// <param name="stable">Cadena nombre de la tabla</param>
        public void BulkData(string stable)
        {
            using (FacadeStatistics facadeStatistics = new FacadeStatistics())
            {
                facadeStatistics.sConnection = FNCEstadistica.Properties.Settings.Default.IntegraBus;
                facadeStatistics.CreateRows(stable);
            }
        }

        public void DoLogin()
        {
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                this.oSession = salesforceIntegrator.Login(FNCEstadistica.Properties.Settings.Default.SalesforceCompany, FNCEstadistica.Properties.Settings.Default.SalesforceUser, FNCEstadistica.Properties.Settings.Default.SalesforcePassword, FNCEstadistica.Properties.Settings.Default.SalesforceToken);
            }
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Sesion de Salesforce iniciada correctamente " + this.oSession.scode);
        }

        /// <summary>
        /// Método que invoca el método para purgar la tabla y cargar la nueva información
        /// </summary>
        /// <param name="stable">Cadena nombre de la tabla</param>
        /// <param name="btruncate">Boleano indica si la tabla se trunca o se elimina</param>
        public void PurgeTable(string stable, bool btruncate)
        {
            using (FacadeStatistics facadeStatistics = new FacadeStatistics())
            {
                facadeStatistics.sConnection = FNCEstadistica.Properties.Settings.Default.IntegraBus;
                facadeStatistics.PurgeTable(stable, btruncate);
            }
        }

        /// <summary>
        /// Método para generar los pacientes
        /// </summary>
        public void GenerateAccounts()
        {
            string saccounts = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    saccounts = salesforceIntegrator.GetAccounts();
                }
            }
            if (!string.IsNullOrEmpty(saccounts))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.AccountFile, saccounts))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.AccountFile))
                    {
                        this.BulkData("SALESFORCEACCOUNT");
                    }
                }
            }
        }

        /// <summary>
        /// Método que obtiene las citas desde Salesforce
        /// </summary>
        public void GenerateAppointments()
        {
            string sappointments = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    sappointments = salesforceIntegrator.GetAppointments();
                }
            }
            if (!string.IsNullOrEmpty(sappointments))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.AppointmentFile, sappointments))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.AppointmentFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.AppointmentFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.AppointmentFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEAPPOINTMENT");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Método que genera los registros de usos de autorización
        /// </summary>
        public void GenerateUsage()
        {
            string susage = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    susage = salesforceIntegrator.GetAuthUsage();
                }
            }
            if (!string.IsNullOrEmpty(susage))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.UsageFile, susage))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.UsageFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.UsageFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.UsageFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEUSE_AUTORIZATION");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Método para generar los productos por grupo
        /// </summary>
        public void GenerateProductByGroup()
        {
            string sproductsbygroup = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    sproductsbygroup = salesforceIntegrator.GetProductsByGroup();
                }
            }
            if (!string.IsNullOrEmpty(sproductsbygroup))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.ProductFile, sproductsbygroup))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.ProductFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.ProductFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.ProductFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEPRODUCTSBYGROUP");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GetAssesments()
        {
            string sassesment = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    sassesment = salesforceIntegrator.GetAssesments();
                }
            }
            if (!string.IsNullOrEmpty(sassesment))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.AssesmentFile, sassesment))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.AssesmentFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.AssesmentFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.AssesmentFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEASSESMENT");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GetPrescriptions()
        {
            string sprescriptions = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    sprescriptions = salesforceIntegrator.GetPrescriptions();
                }
            }
            if (!string.IsNullOrEmpty(sprescriptions))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.PrescriptionFile, sprescriptions))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.PrescriptionFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.PrescriptionFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.PrescriptionFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEPRESCRIPTION");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GetOrders()
        {
            string sorders = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    sorders = salesforceIntegrator.GetOrderTest();
                }
            }
            if (!string.IsNullOrEmpty(sorders))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.OrderFile, sorders))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.OrderFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.OrderFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.OrderFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEORDEREDTEST");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GetDiagnosis()
        {
            string sdiagnosis = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    sdiagnosis = salesforceIntegrator.GetDianosis();
                }
            }
            if (!string.IsNullOrEmpty(sdiagnosis))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.DiagnosisFile, sdiagnosis))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.DiagnosisFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.DiagnosisFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.DiagnosisFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEDIAGNOSISBYMEDRECORD");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GetPFP()
        {
            string spfp = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    spfp = salesforceIntegrator.GetPRFP();
                }
            }
            if (!string.IsNullOrEmpty(spfp))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.PrfpFile, spfp))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.PrfpFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.PrfpFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.PrfpFile).Name);
                        try
                        {
                            //this.BulkData("SALESFORCEINFORMEPFP");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GetRHB()
        {
            string srhb = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    srhb = salesforceIntegrator.GetRHB();
                }
            }
            if (!string.IsNullOrEmpty(srhb))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.RhbFile, srhb))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.RhbFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.RhbFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.RhbFile).Name);
                        try
                        {
                            //this.BulkData("SALESFORCEINFORMERHB");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GetSpeelTest()
        {
            string sleep = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    sleep = salesforceIntegrator.GetSleepTest();
                }
            }
            if (!string.IsNullOrEmpty(sleep))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.SleepFile, sleep))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.SleepFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.SleepFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.SleepFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEINFORMESUENO");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }

        public void GeneratePlans()
        {
            string splan = string.Empty;
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    splan = salesforceIntegrator.GetPlan();
                }
            }
            if (!string.IsNullOrEmpty(splan))
            {
                if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.PlanFile, splan))
                {
                    if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.PlanFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.PlanFile);
                        StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCEstadistica.Properties.Settings.Default.PlanFile).Name);
                        try
                        {
                            this.BulkData("SALESFORCEPLAN");
                        }
                        catch (Exception ex)
                        {
                            LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                        }
                    }
                }
            }
        }


        /// <summary>
        /// Método para escribir el archivo con la información descargada
        /// </summary>
        /// <param name="sfile">Cadena nombre del archivo</param>
        /// <param name="scontent">Cadena contenido del archivo</param>
        /// <returns>Boleano verdadero si pudo crear el archivo o falso si no</returns>
        private bool WriteFile(string sfile, string scontent)
        {
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Este es el archivo: " + sfile);
            try
            {
                if (File.Exists(sfile))
                {
                    File.Delete(sfile);
                }
                File.WriteAllText(sfile, scontent, Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                return false;
            }
        }

        /// <summary>
        /// Método para cargar el archivo de registros descargados por SFTP al servidor de Oracle
        /// </summary>
        /// <param name="sfile">Cadena archivo a cargar</param>
        /// <returns>Boleano verdadero si el archivo se pudo cargar, falso en caso contrario</returns>
        private bool UploadFile(string sfile)
        {
            var connectionInfo = new ConnectionInfo(FNCEstadistica.Properties.Settings.Default.RemoteAddress, FNCEstadistica.Properties.Settings.Default.RemoteUser, new PasswordAuthenticationMethod(FNCEstadistica.Properties.Settings.Default.RemoteUser, FNCEstadistica.Properties.Settings.Default.RemotePassword));
            StringBuilder remoteFile = new StringBuilder(FNCEstadistica.Properties.Settings.Default.RemoteDir);
            remoteFile.Append(new FileInfo(sfile).Name);
            try
            {
                var client = new SftpClient(connectionInfo);
                client.Connect();
                using (var file = File.OpenRead(sfile))
                {
                    client.UploadFile(file, remoteFile.ToString());
                }
                client.Disconnect();
                return true;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                return false;
            }
        }

        /// <summary>
        /// Descarga los espacios vacíos de todas las agendas activas para el rango
        /// de fechas indicado e inserta la información en Oracle.
        ///
        /// Flujo explícito en dos pasos (mismo patrón que GenerateAppointments, GenerateAccounts, etc.):
        ///
        ///   PASO 1 — Una sola instancia de SalesforceViaRestApi llama GetCatalogoAgendas()
        ///            para obtener la lista de IDs de agendas activas.
        ///
        ///   PASO 2 — Por cada agenda del catálogo, una nueva instancia llama
        ///            GetEspaciosVaciosPorAgenda() en ventanas de 7 días para respetar
        ///            el timeout de 120 seg de Salesforce. Si una agenda o ventana
        ///            falla se loguea y se continúa con la siguiente.
        ///
        ///   Al finalizar: WriteFile → UploadFile → BulkData (igual que todos los Generate*).
        /// </summary>
        /// <param name="desde">Fecha inicio del rango (debe ser anterior a hoy)</param>
        /// <param name="hasta">Fecha fin del rango (debe ser anterior a hoy)</param>
        public void GenerateEspaciosVaciosAgenda(DateTime desde, DateTime hasta)
        {

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"Iniciando GenerateEspaciosVaciosAgenda: {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");


            // El token SOAP de oSession no es válido para endpoints Apex REST custom.
            // Se hace un login OAuth una sola vez aquí y se reutiliza en todo el método.
            Generic oRestSession = null;
            using (SalesforceViaRestApi restApiLogin = new SalesforceViaRestApi())
            {
                restApiLogin.sLogingEndPoint = FNCEstadistica.Properties.Settings.Default.SalesforceURL;
                restApiLogin.sApiEndpoint = FNCEstadistica.Properties.Settings.Default.SalesforceEndPoint;
                restApiLogin.DoLogin(
                    FNCEstadistica.Properties.Settings.Default.SalesforceUser,
                    FNCEstadistica.Properties.Settings.Default.SalesforcePassword + FNCEstadistica.Properties.Settings.Default.SalesforceToken,
                    FNCEstadistica.Properties.Settings.Default.SalesforceClient,
                    FNCEstadistica.Properties.Settings.Default.SalesforceSecret);
                oRestSession = restApiLogin.salesforceSession;
            }

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"Login OAuth REST exitoso. Instance URL: {oRestSession.sname}");

            // ── PASO 1: Obtener el catálogo de agendas activas ────────────────────────
            List<SalesforceViaRestApi.AgendaInfo> agendas = new List<SalesforceViaRestApi.AgendaInfo>();
            using (SalesforceViaRestApi restApi = new SalesforceViaRestApi())
            {
                restApi.salesforceSession = oRestSession;
                agendas = restApi.GetCatalogoAgendas();
            }

            if (agendas == null || agendas.Count == 0)
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    "GenerateEspaciosVaciosAgenda: no se encontraron agendas activas.");
                return;
            }

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"Catálogo obtenido: {agendas.Count} agendas. Iniciando descarga de espacios vacíos.");

            // ── PASO 2: Por cada agenda, descargar sus espacios vacíos por ventanas de 7 días ──
            List<SalesforceViaRestApi.EspacioVacio> todosLosEspacios =
                new List<SalesforceViaRestApi.EspacioVacio>();

            int totalAgendas = agendas.Count;
            int agendaNum = 0;

            foreach (SalesforceViaRestApi.AgendaInfo agenda in agendas)
            {
                agendaNum++;
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    $"[{agendaNum}/{totalAgendas}] Agenda: {agenda.NombreAgenda} ({agenda.IdAgenda})");

                DateTime inicioVentana = desde;
                while (inicioVentana <= hasta)
                {
                    DateTime finVentana = inicioVentana.AddDays(6);
                    if (finVentana > hasta) finVentana = hasta;

                    try
                    {
                        List<SalesforceViaRestApi.EspacioVacio> espaciosVentana =
                            new List<SalesforceViaRestApi.EspacioVacio>();

                        using (SalesforceViaRestApi restApi = new SalesforceViaRestApi())
                        {
                            restApi.salesforceSession = oRestSession;
                            espaciosVentana = restApi.GetEspaciosVaciosPorAgenda(
                                agenda.IdAgenda, inicioVentana, finVentana);
                        }

                        if (espaciosVentana.Count > 0)
                        {
                            todosLosEspacios.AddRange(espaciosVentana);
                            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                                $"  {inicioVentana:yyyy-MM-dd}→{finVentana:yyyy-MM-dd}: " +
                                $"{espaciosVentana.Count} espacios (total acumulado: {todosLosEspacios.Count})");
                        }
                    }
                    catch (Exception exVentana)
                    {
                        LogError.WriteError("ServicioDescarga", "ServicioDescarga", exVentana);
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                            $"  Error en ventana {inicioVentana:yyyy-MM-dd}→{finVentana:yyyy-MM-dd} " +
                            $"para {agenda.NombreAgenda}. Se continúa con la siguiente.");
                    }

                    inicioVentana = finVentana.AddDays(1);
                }
            }

            if (todosLosEspacios.Count == 0)
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    "GenerateEspaciosVaciosAgenda: ningún espacio vacío encontrado en el rango.");
                return;
            }

            // ── Convertir a CSV (separado por ; igual que todos los demás archivos) ──
            string csvFinal = this.EspaciosVaciosToCSV(todosLosEspacios);

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"GenerateEspaciosVaciosAgenda: {todosLosEspacios.Count} espacios en total. Escribiendo archivo.");

            if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.EspaciosVaciosFile, csvFinal))
            {
                if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.EspaciosVaciosFile))
                {
                    LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                        "Archivo cargado correctamente " +
                        FNCEstadistica.Properties.Settings.Default.EspaciosVaciosFile);
                    try
                    {
                        this.BulkData("SALESFORCEESPACIOSVACIOS");
                    }
                    catch (Exception ex)
                    {
                        LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                    }
                }
            }
        }

        /// <summary>
        /// Convierte la lista de espacios vacíos al formato CSV separado por punto y coma (;)
        /// que usa el proceso de carga masiva hacia Oracle, igual que ProcessAppointments
        /// y los demás métodos de descarga del proyecto.
        ///
        /// Orden de columnas (debe coincidir con la tabla Oracle destino):
        ///   ID_AGENDA ; NOMBRE_AGENDA ; ESPECIALIDAD_AGENDA ;
        ///   ID_CENTRO_COSTO ; NOMBRE_CENTRO_COSTO ;
        ///   ID_CONFIG_HORARIO ; NOMBRE_CONFIGURACION ;
        ///   ID_CATEGORIA ; NOMBRE_CATEGORIA ; TIPO_CATEGORIA ;
        ///   FECHA ; DIA ; FRANJA_HORARIA ;
        ///   HORA_INICIO_STR ; HORA_FIN_STR ;
        ///   HORA_INICIO ; HORA_FIN ;
        ///   DURACION_MINUTOS ; ES_HORARIO_PRINCIPAL
        /// </summary>
        private string EspaciosVaciosToCSV(List<SalesforceViaRestApi.EspacioVacio> espacios)
        {
            StringBuilder csv = new StringBuilder();
            foreach (SalesforceViaRestApi.EspacioVacio item in espacios)
            {
                string[] columnas = new string[]
                {
                    item.IdAgenda ?? string.Empty,
                    Tools.ReplaceChars(item.NombreAgenda ?? string.Empty),
                    Tools.ReplaceChars(item.EspecialidadAgenda ?? string.Empty),
                    item.IdCentroCosto ?? string.Empty,
                    Tools.ReplaceChars(item.NombreCentroCosto ?? string.Empty),
                    item.IdConfiguracionHorario ?? string.Empty,
                    Tools.ReplaceChars(item.NombreConfiguracion ?? string.Empty),
                    item.IdCategoria ?? string.Empty,
                    Tools.ReplaceChars(item.NombreCategoria ?? string.Empty),
                    Tools.ReplaceChars(item.TipoCategoria ?? string.Empty),
                    item.Fecha ?? string.Empty,
                    Tools.ReplaceChars(item.Dia ?? string.Empty),
                    Tools.ReplaceChars(item.FranjaHoraria ?? string.Empty),
                    item.HoraInicioStr ?? string.Empty,
                    item.HoraFinStr ?? string.Empty,
                    item.HoraInicio ?? string.Empty,
                    item.HoraFin ?? string.Empty,
                    item.DuracionMinutos.ToString(),
                    item.EsHorarioPrincipal ? "1" : "0"
                };
                csv.AppendLine(string.Join(";", columnas));
            }
            return csv.ToString();
        }

        /// <summary>
        /// MODO INCREMENTAL — Ejecución normal del programador de tareas (2 veces al día).
        ///
        /// Descarga el rango móvil: (hoy - 2 días) → (ayer + 6 meses).
        /// Esta ventana contiene datos que sí cambian: citas futuras que se crean,
        /// cancelan o modifican entre ejecuciones.
        ///
        /// No hace PurgeTable porque BulkData ya ejecuta internamente un DELETE
        /// por rango de fechas antes de insertar los nuevos registros.
        /// </summary>
        public void GenerateEspaciosVaciosAgendaIncremental()
        {
            DateTime ayer = DateTime.Today.AddDays(-1);
            DateTime desde = DateTime.Today.AddDays(-1);
            //DateTime hasta = ayer.AddMonths(6);
            DateTime hasta = ayer;
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"[INCREMENTAL] Rango: {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");
            this.GenerateEspaciosVaciosAgenda(desde, hasta);
        }

        /// <summary>
        /// MODO INICIAL — Solo la primera vez que se pone en marcha el proceso.
        ///
        /// Ejecuta dos descargas en secuencia:
        ///
        ///   1. HISTÓRICO: 01-ene del año en curso → (hoy - 2 días).
        ///      Datos del pasado que nunca cambian. La tabla está vacía,
        ///      no hay que purgar nada.
        ///
        ///   2. RANGO MÓVIL: (hoy - 2 días) → (ayer + 6 meses).
        ///      Citas pasadas recientes + futuras. BulkData hace internamente
        ///      el DELETE por rango de fechas antes de insertar, así que los
        ///      2 días de solapamiento con el histórico quedan correctamente
        ///      reemplazados sin necesidad de llamar PurgeTable.
        /// </summary>
        public void GenerateEspaciosVaciosAgendaInicial()
        {
            DateTime ayer = DateTime.Today.AddDays(-1);
            DateTime inicioAnio = new DateTime(DateTime.Today.Year, 1, 1);
            DateTime cortePasado = DateTime.Today.AddDays(-1);
            DateTime hastaFuturo = ayer.AddMonths(6);

            // ── Parte 1: Histórico ────────────────────────────────────────────
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"[INICIAL - Histórico] Rango: {inicioAnio:yyyy-MM-dd} → {cortePasado:yyyy-MM-dd}");
            this.GenerateEspaciosVaciosAgenda(inicioAnio, cortePasado);
            // ── Parte 2: Rango móvil ──────────────────────────────────────────
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"[INICIAL - Rango móvil] Rango: {cortePasado:yyyy-MM-dd} → {hastaFuturo:yyyy-MM-dd}");
            //this.GenerateEspaciosVaciosAgenda(cortePasado, hastaFuturo);
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "[INICIAL] Carga inicial completada.");
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // NUEVOS MÉTODOS — agregar dentro de la clase SincronizaEstadistica
        // junto a los métodos GenerateEspaciosVaciosAgenda* existentes.
        //
        // CAMBIO RESPECTO A LA VERSIÓN ANTERIOR:
        //   El endpoint ahora procesa UNA agenda por llamada (igual que espacios vacíos).
        //   SincronizaEstadistica itera el catálogo y llama GetCapacidadPorAgenda()
        //   por cada agenda + ventana de 7 días, exactamente igual que
        //   GenerateEspaciosVaciosAgenda usa GetEspaciosVaciosPorAgenda.
        //
        // Requiere en Properties.Settings.Default:
        //   CapacidadAgendaFile → ruta local del CSV  (ej: C:\integracion\capacidadagenda.csv)
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Descarga la capacidad de todas las agendas activas para el rango de fechas
        /// indicado e inserta la información en Oracle.
        ///
        /// Flujo (mismo patrón que GenerateEspaciosVaciosAgenda):
        ///
        ///   PASO 1 — Login OAuth una sola vez.
        ///   PASO 2 — Obtener catálogo de agendas con GetCatalogoAgendas().
        ///   PASO 3 — Por cada agenda, llamar GetCapacidadPorAgenda() en ventanas
        ///            de 7 días para respetar el timeout de 120 seg de Salesforce.
        ///            Si una agenda o ventana falla, se loguea y se continúa.
        ///   PASO 4 — Consolidar filas duplicadas en el límite entre ventanas.
        ///   PASO 5 — WriteFile → UploadFile → BulkData.
        /// </summary>
        public void GenerateCapacidadAgenda(DateTime desde, DateTime hasta)
        {            
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"Iniciando GenerateCapacidadAgenda: {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");

            // ── PASO 1: Login OAuth una sola vez ──────────────────────────────────
            Generic oRestSession = null;
            using (SalesforceViaRestApi restApiLogin = new SalesforceViaRestApi())
            {
                restApiLogin.sLogingEndPoint = FNCEstadistica.Properties.Settings.Default.SalesforceURL;
                restApiLogin.DoLogin(FNCEstadistica.Properties.Settings.Default.SalesforceUser, FNCEstadistica.Properties.Settings.Default.SalesforcePassword + FNCEstadistica.Properties.Settings.Default.SalesforceToken,
                    FNCEstadistica.Properties.Settings.Default.SalesforceClient, FNCEstadistica.Properties.Settings.Default.SalesforceSecret);
                oRestSession = restApiLogin.salesforceSession;
            }

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"Login OAuth REST exitoso. Instance URL: {oRestSession.sname}");

            // ── PASO 2: Catálogo de agendas ───────────────────────────────────────
            List<SalesforceViaRestApi.AgendaInfo> agendas = new List<SalesforceViaRestApi.AgendaInfo>();

            using (SalesforceViaRestApi restApi = new SalesforceViaRestApi())
            {
                restApi.salesforceSession = oRestSession;
                agendas = restApi.GetCatalogoAgendas();
            }

            if (agendas == null || agendas.Count == 0)
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "GenerateCapacidadAgenda: no se encontraron agendas activas.");
                return;
            }

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"Catálogo obtenido: {agendas.Count} agendas. Iniciando descarga de capacidad.");

            // ── PASO 3: Por cada agenda, descargar en ventanas de 7 días ──────────
            var acumulado = new List<SalesforceViaRestApi.FilaCapacidad>();
            int totalAgendas = agendas.Count;
            int agendaNum = 0;
            foreach (SalesforceViaRestApi.AgendaInfo agenda in agendas)
            {
                agendaNum++;
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"[{agendaNum}/{totalAgendas}] Agenda: {agenda.NombreAgenda} ({agenda.IdAgenda})");
                DateTime inicioVentana = desde;
                while (inicioVentana <= hasta)
                {
                    DateTime finVentana = inicioVentana.AddDays(6);
                    if (finVentana > hasta) finVentana = hasta;

                    try
                    {
                        List<SalesforceViaRestApi.FilaCapacidad> filasVentana =
                            new List<SalesforceViaRestApi.FilaCapacidad>();

                        using (SalesforceViaRestApi restApi = new SalesforceViaRestApi())
                        {
                            restApi.salesforceSession = oRestSession;
                            filasVentana = restApi.GetCapacidadPorAgenda(
                                agenda.IdAgenda, inicioVentana, finVentana);
                        }

                        if (filasVentana.Count > 0)
                        {
                            acumulado.AddRange(filasVentana);
                            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                                $"  {inicioVentana:yyyy-MM-dd}→{finVentana:yyyy-MM-dd}: " +
                                $"{filasVentana.Count} filas (total: {acumulado.Count})");
                        }
                    }
                    catch (Exception exVentana)
                    {
                        LogError.WriteError("ServicioDescarga", "ServicioDescarga", exVentana);
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                            $"  Error en ventana {inicioVentana:yyyy-MM-dd}→{finVentana:yyyy-MM-dd} " +
                            $"para {agenda.NombreAgenda}. Se continúa.");
                    }

                    inicioVentana = finVentana.AddDays(1);
                }
            }

            if (acumulado.Count == 0)
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    "GenerateCapacidadAgenda: ninguna fila encontrada en el rango.");
                return;
            }

            // ── PASO 4: Consolidar filas duplicadas en el límite entre ventanas ───
            List<SalesforceViaRestApi.FilaCapacidad> consolidado =
                ConsolidarFilasCapacidad(acumulado);

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"GenerateCapacidadAgenda: {consolidado.Count} filas consolidadas. Escribiendo CSV.");

            // ── PASO 5: WriteFile → UploadFile → BulkData ─────────────────────────
            string csvFinal = CapacidadAgendaToCSV(consolidado);

            if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.CapacidadAgendaFile, csvFinal))
            {
                if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.CapacidadAgendaFile))
                {
                    LogError.WriteMessage("ServicioDescarga", "ServicioDescarga","Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.CapacidadAgendaFile);
                    try
                    {
                        this.BulkData("SALESFORCE_CAPACIDAD_AGENDA");
                    }
                    catch (Exception ex)
                    {
                        LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                    }
                }
            }
        }

        /// <summary>
        /// MODO INCREMENTAL — ejecución del programador de tareas.
        /// Descarga ayer → ayer + 60 días.
        /// BulkData hace DELETE por rango de fechas antes de insertar.
        /// </summary>
        public void GenerateCapacidadAgendaIncremental()
        {
            DateTime ayer = DateTime.Today.AddDays(-1);
            DateTime hasta = ayer.AddDays(60);
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"[INCREMENTAL] Rango: {ayer:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");
            this.GenerateCapacidadAgenda(ayer, hasta);
        }

        /// <summary>
        /// MODO INICIAL — solo la primera vez.
        /// Descarga desde el 1 de enero de 2026 hasta ayer.
        /// </summary>
        public void GenerateCapacidadAgendaInicial()
        {
            DateTime desde = new DateTime(2026, 1, 1);
            DateTime hasta = DateTime.Today.AddDays(-1);
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"[INICIAL] Rango: {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");
            this.GenerateCapacidadAgenda(desde, hasta);
        }

        /// <summary>
        /// Consolida filas con la misma clave (idAgenda + fecha + categoria) que pueden
        /// aparecer duplicadas en el límite entre ventanas de 7 días.
        /// </summary>
        private List<SalesforceViaRestApi.FilaCapacidad> ConsolidarFilasCapacidad(List<SalesforceViaRestApi.FilaCapacidad> filas)
        {
            var mapa = new Dictionary<string, SalesforceViaRestApi.FilaCapacidad>();
            foreach (var f in filas)
            {
                string clave = $"{f.IdAgenda}|{f.Fecha}|{f.Categoria}";
                if (mapa.ContainsKey(clave))
                {
                    var ex = mapa[clave];
                    ex.CapacidadInstalada += f.CapacidadInstalada;
                    ex.Programados += f.Programados;
                    ex.Atendidos += f.Atendidos;
                    ex.Bloqueos += f.Bloqueos;
                    ex.Inasistencia = Math.Max(0, ex.Programados - ex.Atendidos - ex.Bloqueos);
                }
                else
                {
                    mapa[clave] = new SalesforceViaRestApi.FilaCapacidad
                    {
                        IdAgenda = f.IdAgenda,
                        NombreAgenda = f.NombreAgenda,
                        Especialidad = f.Especialidad,
                        NombreCentroCosto = f.NombreCentroCosto,
                        Fecha = f.Fecha,
                        Dia = f.Dia,
                        Categoria = f.Categoria,
                        CapacidadInstalada = f.CapacidadInstalada,
                        Programados = f.Programados,
                        Atendidos = f.Atendidos,
                        Bloqueos = f.Bloqueos,
                        Inasistencia = f.Inasistencia
                    };
                }
            }
            return new List<SalesforceViaRestApi.FilaCapacidad>(mapa.Values);
        }

        /// <summary>
        /// Convierte la lista de filas al CSV separado por ; para la carga en Oracle.
        /// Columnas (coherentes con el LWC de Capacidad de Agenda):
        ///   ID_AGENDA ; NOMBRE_AGENDA ; FECHA ; DIA ; CATEGORIA ;
        ///   CAPACIDAD_INSTALADA ; PROGRAMADOS ; ATENDIDOS ; BLOQUEOS ; INASISTENCIA
        /// </summary>
        private string CapacidadAgendaToCSV(List<SalesforceViaRestApi.FilaCapacidad> filas)
        {
            var csv = new System.Text.StringBuilder();
            foreach (var f in filas)
            {
                string[] columnas = new string[]
                {
                    f.IdAgenda ?? string.Empty,
                    Tools.ReplaceChars(f.NombreAgenda ?? string.Empty),
                    Tools.ReplaceChars(f.Especialidad      ?? string.Empty),
                    Tools.ReplaceChars(f.NombreCentroCosto ?? string.Empty),
                    f.Fecha ?? string.Empty,
                    Tools.ReplaceChars(f.Dia ?? string.Empty),
                    Tools.ReplaceChars(f.Categoria ?? string.Empty),
                    Tools.ReplaceChars(f.CapacidadInstalada.ToString()),
                    Tools.ReplaceChars(f.Programados.ToString()),
                    Tools.ReplaceChars(f.Atendidos.ToString()),
                    Tools.ReplaceChars(f.Bloqueos.ToString()),
                    Tools.ReplaceChars(f.Inasistencia.ToString())
                };
                csv.AppendLine(string.Join(";", columnas));
            }
            return csv.ToString();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // NUEVOS MÉTODOS — agregar dentro de la clase SincronizaEstadistica
        // junto a los métodos GenerateCapacidadAgenda* y GenerateEspaciosVaciosAgenda*
        //
        // Requiere en Properties.Settings.Default:
        //   AgendaDetalleFile → ruta local del CSV (ej: C:\integracion\agendadetalle.csv)
        //
        // Solo se descargan las agendas configuradas en el Custom Label
        // FNC_AgendaDetalleIds de Salesforce (AM, PM, RHB Ped, etc.)
        // Para agregar/quitar agendas solo editar el label, sin tocar este código.
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Descarga el detalle de slots de las agendas configuradas en el Custom Label
        /// FNC_AgendaDetalleIds para el rango de fechas indicado e inserta en Oracle.
        ///
        /// Flujo (mismo patrón que GenerateCapacidadAgenda):
        ///   PASO 1 — Login OAuth una sola vez.
        ///   PASO 2 — Catálogo de agendas del Custom Label vía GetCatalogoDetalleAgendas().
        ///   PASO 3 — Por cada agenda, GetDetalleAgenda() en ventanas de 7 días.
        ///   PASO 4 — WriteFile → UploadFile → BulkData.
        ///
        /// Se descargan TODOS los slots (con y sin cita).
        /// Los slots vacíos quedan con los campos de paciente en blanco.
        /// </summary>
        public void GenerateAgendaDetalle(DateTime desde, DateTime hasta)
        {
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"Iniciando GenerateAgendaDetalle: {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");
           
            // ── PASO 1: Login OAuth una sola vez ──────────────────────────────────
            Generic oRestSession = null;
            using (SalesforceViaRestApi restApiLogin = new SalesforceViaRestApi())
            {
                restApiLogin.sLogingEndPoint =
                    FNCEstadistica.Properties.Settings.Default.SalesforceURL;
                restApiLogin.DoLogin(FNCEstadistica.Properties.Settings.Default.SalesforceUser, FNCEstadistica.Properties.Settings.Default.SalesforcePassword + FNCEstadistica.Properties.Settings.Default.SalesforceToken, FNCEstadistica.Properties.Settings.Default.SalesforceClient, FNCEstadistica.Properties.Settings.Default.SalesforceSecret);
                oRestSession = restApiLogin.salesforceSession;
            }

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"Login OAuth REST exitoso. Instance URL: {oRestSession.sname}");

            // ── PASO 2: Catálogo de agendas del Custom Label ──────────────────────
            List<SalesforceViaRestApi.AgendaInfo> agendas =
                new List<SalesforceViaRestApi.AgendaInfo>();

            using (SalesforceViaRestApi restApi = new SalesforceViaRestApi())
            {
                restApi.salesforceSession = oRestSession;
                agendas = restApi.GetCatalogoDetalleAgendas();
            }

            if (agendas == null || agendas.Count == 0)
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    "GenerateAgendaDetalle: no se encontraron agendas en FNC_AgendaDetalleIds.");
                return;
            }

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"Catálogo obtenido: {agendas.Count} agendas. Iniciando descarga de detalle.");

            // ── PASO 3: Por cada agenda, descargar en ventanas de 7 días ──────────
            var acumulado = new List<SalesforceViaRestApi.SlotDetalle>();
            int totalAgendas = agendas.Count;
            int agendaNum = 0;

            foreach (SalesforceViaRestApi.AgendaInfo agenda in agendas)
            {
                agendaNum++;
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    $"[{agendaNum}/{totalAgendas}] Agenda: {agenda.NombreAgenda} ({agenda.IdAgenda})");

                DateTime inicioVentana = desde;
                while (inicioVentana <= hasta)
                {
                    DateTime finVentana = inicioVentana.AddDays(6);
                    if (finVentana > hasta) finVentana = hasta;

                    try
                    {
                        List<SalesforceViaRestApi.SlotDetalle> slotsVentana =
                            new List<SalesforceViaRestApi.SlotDetalle>();

                        using (SalesforceViaRestApi restApi = new SalesforceViaRestApi())
                        {
                            restApi.salesforceSession = oRestSession;
                            slotsVentana = restApi.GetDetalleAgenda(
                                agenda.IdAgenda, inicioVentana, finVentana);
                        }

                        // Acumular TODOS los slots (con y sin cita)
                        if (slotsVentana.Count > 0)
                        {
                            acumulado.AddRange(slotsVentana);
                            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                                $"  {inicioVentana:yyyy-MM-dd}→{finVentana:yyyy-MM-dd}: " +
                                $"{slotsVentana.Count} slots (total: {acumulado.Count})");
                        }
                    }
                    catch (Exception exVentana)
                    {
                        LogError.WriteError("ServicioDescarga", "ServicioDescarga", exVentana);
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                            $"  Error en ventana {inicioVentana:yyyy-MM-dd}→{finVentana:yyyy-MM-dd} " +
                            $"para {agenda.NombreAgenda}. Se continúa.");
                    }

                    inicioVentana = finVentana.AddDays(1);
                }
            }

            if (acumulado.Count == 0)
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    "GenerateAgendaDetalle: ningún slot encontrado en el rango.");
                return;
            }

            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                $"GenerateAgendaDetalle: {acumulado.Count} slots. Escribiendo CSV.");

            // ── PASO 4: WriteFile → UploadFile → BulkData ─────────────────────────
            string csvFinal = AgendaDetalleToCSV(acumulado);

            if (this.WriteFile(
                FNCEstadistica.Properties.Settings.Default.AgendaDetalleFile, csvFinal))
            {
                if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.AgendaDetalleFile))
                {
                    LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                        "Archivo cargado correctamente " +
                        FNCEstadistica.Properties.Settings.Default.AgendaDetalleFile);
                    try
                    {
                        this.BulkData("SALESFORCE_AGENDA_DETALLE");
                    }
                    catch (Exception ex)
                    {
                        LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                    }
                }
            }
        }

        /// <summary>
        /// MODO INCREMENTAL — ejecución del programador de tareas.
        /// Descarga hoy → hoy + 15 días.
        /// BulkData hace DELETE por rango de fechas antes de insertar.
        /// </summary>
        public void GenerateAgendaDetalleIncremental()
        {
            DateTime desde = DateTime.Today;
            DateTime hasta = DateTime.Today.AddDays(15);
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"[INCREMENTAL] Rango: {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");
            this.GenerateAgendaDetalle(desde, hasta);
        }

        /// <summary>
        /// MODO INICIAL — solo la primera vez.
        /// Descarga desde el 1 de enero de 2026 hasta hoy + 15 días.
        /// </summary>
        public void GenerateAgendaDetalleInicial()
        {
            DateTime desde = new DateTime(2026, 1, 1);
            DateTime hasta = DateTime.Today.AddDays(15);
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", $"[INICIAL] Rango: {desde:yyyy-MM-dd} → {hasta:yyyy-MM-dd}");
            this.GenerateAgendaDetalle(desde, hasta);
        }

        /// <summary>
        /// Convierte la lista de slots al CSV separado por ; para la carga en Oracle.
        /// Columnas:
        ///   ID_AGENDA ; NOMBRE_AGENDA ; ESPECIALIDAD ; NOMBRE_CENTRO_COSTO ;
        ///   FECHA ; HORA_INICIO ; HORA_FIN ; CATEGORIA ;
        ///   NAME_CITA ; ID_CITA ; NOMBRE_PACIENTE ; DOCUMENTO_PACIENTE ;
        ///   ESTADO ; PACIENTE_ASISTIO ; GRUPO ; PLAN
        /// </summary>
        private string AgendaDetalleToCSV(List<SalesforceViaRestApi.SlotDetalle> slots)
        {
            var csv = new System.Text.StringBuilder();
            foreach (var s in slots)
            {
                string[] columnas = new string[]
                {
                    s.IdAgenda ?? string.Empty,
                    Tools.ReplaceChars(s.NombreAgenda ?? string.Empty),
                    Tools.ReplaceChars(s.Especialidad ?? string.Empty),
                    Tools.ReplaceChars(s.NombreCentroCosto ?? string.Empty),
                    s.Fecha ?? string.Empty,
                    s.HoraInicioStr ?? string.Empty,
                    s.HoraFinStr ?? string.Empty,
                    Tools.ReplaceChars(s.Categoria ?? string.Empty),
                    s.NameCita ?? string.Empty,
                    s.IdCita ?? string.Empty,
                    Tools.ReplaceChars(s.NombrePaciente ?? string.Empty),
                    s.DocumentoPaciente ?? string.Empty,
                    Tools.ReplaceChars(s.Estado ?? string.Empty),
                    s.PacienteAsistio ?? string.Empty,
                    Tools.ReplaceChars(s.Grupo ?? string.Empty),
                    Tools.ReplaceChars(s.Plan ?? string.Empty)
                };
                csv.AppendLine(string.Join(";", columnas));
            }
            return csv.ToString();
        }

        // ═══════════════════════════════════════════════════════════════════════════════
        // NUEVOS MÉTODOS — agregar dentro de la clase SincronizaEstadistica
        // junto a los demás métodos Generate* del proyecto.
        //
        // Requiere en Properties.Settings.Default:
        //   AppointmentHistoryFile → ruta local del CSV (ej: C:\integracion\appointmenthistory.csv)
        //
        // Usa SalesforceViaRestApi (OAuth REST) — no SalesforceIntegrator (SOAP).
        // Oracle hace TRUNCATE completo antes de insertar (THIS_YEAR recarga todo el año).
        // ═══════════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Descarga el historial de cambios del campo ServiceBilled__c de las citas
        /// para el año en curso (THIS_YEAR) e inserta en Oracle con TRUNCATE previo.
        ///
        /// Flujo:
        ///   PASO 1 — Login OAuth una sola vez.
        ///   PASO 2 — GetAppointmentHistory() → CSV del año completo.
        ///   PASO 3 — WriteFile → UploadFile → BulkData (TRUNCATE + INSERT).
        ///
        /// Ejecutar diariamente — descarga el año completo y reemplaza todo.
        /// </summary>
        public void GenerateAppointmentHistory()
        {
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                "Iniciando GenerateAppointmentHistory (THIS_YEAR)");

            if (string.IsNullOrEmpty(this.oSession?.scode))
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga",
                    "GenerateAppointmentHistory: sesión no iniciada. Se cancela.");
                return;
            }

            // ── PASO 1: Login OAuth ───────────────────────────────────────────────
            Generic oRestSession = null;
            using (SalesforceViaRestApi restApiLogin = new SalesforceViaRestApi())
            {
                restApiLogin.sLogingEndPoint = FNCEstadistica.Properties.Settings.Default.SalesforceURL;
                restApiLogin.DoLogin(FNCEstadistica.Properties.Settings.Default.SalesforceUser, FNCEstadistica.Properties.Settings.Default.SalesforcePassword + FNCEstadistica.Properties.Settings.Default.SalesforceToken,
                    FNCEstadistica.Properties.Settings.Default.SalesforceClient,
                    FNCEstadistica.Properties.Settings.Default.SalesforceSecret);
                oRestSession = restApiLogin.salesforceSession;
            }
            // ── PASO 2: Descargar historial del año en curso ───────────────────────
            string sResult = string.Empty;
            try
            {
                using (SalesforceViaRestApi restApi = new SalesforceViaRestApi())
                {
                    restApi.salesforceSession = oRestSession;
                    restApi.sApiEndpoint = FNCEstadistica.Properties.Settings.Default.SalesforceEndPoint;
                    sResult = restApi.GetAppointmentHistory();
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                return;
            }

            if (string.IsNullOrEmpty(sResult))
            {
                LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "GenerateAppointmentHistory: no se encontraron registros.");
                return;
            }
            LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "GenerateAppointmentHistory: datos obtenidos. Escribiendo CSV.");
            // ── PASO 3: WriteFile → UploadFile → BulkData ─────────────────────────
            if (this.WriteFile(FNCEstadistica.Properties.Settings.Default.AppointmentHistoryFile, sResult))
            {
                if (this.UploadFile(FNCEstadistica.Properties.Settings.Default.AppointmentHistoryFile))
                {
                    LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCEstadistica.Properties.Settings.Default.AppointmentHistoryFile);
                    try
                    {
                        this.BulkData("SALESFORCE_APPOINTMENT_HISTORY");
                    }
                    catch (Exception ex)
                    {
                        LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                    }
                }
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}