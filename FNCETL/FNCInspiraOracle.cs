using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using FNCSalesforce;
using FNCEntity;
using System.IO;
using EventLog;
using Renci.SshNet.Sftp;
using Renci.SshNet;
using System.Timers;
using FNCFacade;
using FNCEstadistica;

namespace FNCETL
{
    public partial class FNCInspiraOracle : ServiceBase
    {
        /// <summary>
        /// Objeto que almacena la sesión de Salesforce
        /// </summary>
        private Generic oSession { get; set; }

        private SincronizaEstadistica sincronizaEstadistica { get; set; }

        public FNCInspiraOracle()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Método que se ejecuta cuando el servicio de windows se inicia
        /// </summary>
        /// <param name="args">Arreglo de string con los parámetros del servicio</param>
        protected override void OnStart(string[] args)
        {
            this.sincronizaEstadistica = new SincronizaEstadistica();
            try
            {
                /*using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
                {
                    this.oSession = salesforceIntegrator.Login(FNCETL.Properties.Settings.Default.SalesforceCompany, FNCETL.Properties.Settings.Default.SalesforceUser, FNCETL.Properties.Settings.Default.SalesforcePassword, FNCETL.Properties.Settings.Default.SalesforceToken);
                }
                */
                this.sincronizaEstadistica.DoLogin();
                Timer timer1 = new Timer();
                timer1.Elapsed += new ElapsedEventHandler(this.OnTimer);
                //timer1.Interval = 7200000;
                timer1.Interval = FNCETL.Properties.Settings.Default.TimeInterval;
                timer1.Start();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
            }
        }

        protected override void OnStop()
        {
        }

        /// <summary>
        /// Evento timer que se dispara cada n segundos
        /// </summary>
        /// <param name="sender">Objeto q dispara el timer</param>
        /// <param name="args">Argumentos del evento</param>
        public void OnTimer(object sender, ElapsedEventArgs args)
        {
            try
            {
                this.sincronizaEstadistica.GenerateProductByGroup();
                //this.GenerateProductByGroup();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los productos por grupo"));
            }
            try
            {
                this.sincronizaEstadistica.GenerateAccounts();
                //this.GenerateAccounts();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los pacientes"));
            }
            try
            {
                this.sincronizaEstadistica.GenerateAppointments();
                //this.GenerateAppointments();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar las citas"));
            }
            try
            {
                this.sincronizaEstadistica.GenerateUsage();
                //this.GenerateUsage();
            }
            catch (Exception)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", new ApplicationException("Error al descargar los usos de autorización"));
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
                facadeStatistics.sConnection = FNCETL.Properties.Settings.Default.IntegraBus;
                facadeStatistics.CreateRows(stable);
            }
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
                facadeStatistics.sConnection = FNCETL.Properties.Settings.Default.IntegraBus;
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
                if (this.WriteFile(FNCETL.Properties.Settings.Default.AccountFile, saccounts))
                {
                    if (this.UploadFile(FNCETL.Properties.Settings.Default.AccountFile))
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
                if (this.WriteFile(FNCETL.Properties.Settings.Default.AppointmentFile, sappointments))
                {
                    if (this.UploadFile(FNCETL.Properties.Settings.Default.AppointmentFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCETL.Properties.Settings.Default.AppointmentFile);
                        StringBuilder remoteFile = new StringBuilder(FNCETL.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCETL.Properties.Settings.Default.AppointmentFile).Name);
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
                if (this.WriteFile(FNCETL.Properties.Settings.Default.UsageFile, susage))
                {
                    if (this.UploadFile(FNCETL.Properties.Settings.Default.UsageFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCETL.Properties.Settings.Default.UsageFile);
                        StringBuilder remoteFile = new StringBuilder(FNCETL.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCETL.Properties.Settings.Default.UsageFile).Name);
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
                if (this.WriteFile(FNCETL.Properties.Settings.Default.ProductFile, sproductsbygroup))
                {
                    if (this.UploadFile(FNCETL.Properties.Settings.Default.ProductFile))
                    {
                        LogError.WriteMessage("ServicioDescarga", "ServicioDescarga", "Archivo cargado correctamente " + FNCETL.Properties.Settings.Default.ProductFile);
                        StringBuilder remoteFile = new StringBuilder(FNCETL.Properties.Settings.Default.RemoteDir);
                        remoteFile.Append(new FileInfo(FNCETL.Properties.Settings.Default.ProductFile).Name);
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

        /// <summary>
        /// Método para escribir el archivo con la información descargada
        /// </summary>
        /// <param name="sfile">Cadena nombre del archivo</param>
        /// <param name="scontent">Cadena contenido del archivo</param>
        /// <returns>Boleano verdadero si pudo crear el archivo o falso si no</returns>
        private bool WriteFile(string sfile, string scontent)
        {
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
            var connectionInfo = new ConnectionInfo(FNCETL.Properties.Settings.Default.RemoteAddress, FNCETL.Properties.Settings.Default.RemoteUser, new PasswordAuthenticationMethod(FNCETL.Properties.Settings.Default.RemoteUser, FNCETL.Properties.Settings.Default.RemotePassword));
            StringBuilder remoteFile = new StringBuilder(FNCETL.Properties.Settings.Default.RemoteDir);
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
    }
}
