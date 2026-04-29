using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEntity;
using FNCSalesforce;
using FNCFacade;
using FNCUtils;
using iTextSharp;
using Org.BouncyCastle.Tls;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using PdfiumViewer;
using SkiaSharp;
using FNCDAC;
using System.Runtime.ExceptionServices;
using EventLog;
using Newtonsoft.Json;
using FNCSalesforce.Sfdc;
using System.Runtime.InteropServices;
using System.Net.NetworkInformation;

namespace FNCCreaSporteCargo
{
    internal class CreaSoporteCargo
    {
        static List<Charge> lcharges = new List<Charge>();
        static Generic salesforcesession { get; set; }

        static SalesforceViaRestApi salesforceViaAPI {  get; set; }

        static AWSConnector aWSConnector {  get; set; }

        static StringBuilder sresult { get; set; }

        static void Main(string[] args)
        {
            string scompany = string.Empty, sappointments = string.Empty;
            salesforceViaAPI = new SalesforceViaRestApi();
            aWSConnector = new AWSConnector(FNCCreaSporteCargo.Properties.Settings.Default.AWSKey, FNCCreaSporteCargo.Properties.Settings.Default.AWSSecret);
            sresult = new StringBuilder();
            try
            {
                scompany = GetCompanyData();
                Console.WriteLine("Obteniendo cargos sin soportes");
                GetCharges(scompany);
                sappointments = GetAppointmentsData();
                Console.WriteLine("Obteniendo información de soportes desde Inspira");
                ProcessAppointments(sappointments);
                Console.WriteLine("Buscando soportes digitales en carpetas");
                Proccess();
                Console.WriteLine("Creando reporte de soportes creados");
                CreateResultReport();
                /*SearchFiles();
                Console.WriteLine("Creando la información de los soportes encontrados en Servinte");
                CreateChargeInformation();
                Console.WriteLine("Creando archivos digitales (imagenes) de los soportes");
                CreateSupportFiles();
                Console.WriteLine("Generando archivo de reporte con el resultado de la operación");
                CreateResultReport();*/
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                Console.WriteLine("Error ocurrido en la generación " + ex.ToString() + " revise el log de eventos de windows para más información");
            }
        }

        static string GetCompanyData()
        {
            using (FacadeIntegraBus facade = new FacadeIntegraBus())
            {
                facade.sconnection = FNCCreaSporteCargo.Properties.Settings.Default.IntegraBus;
                List<string> list = facade.GetCompanies();
                return string.Join("','", list);
            }
        }

        static void GetCharges(string scompany)
        {
            using (FacadeInspiraServinte facadeInspiraServinte = new FacadeInspiraServinte())
            {
                facadeInspiraServinte.sConnection = FNCCreaSporteCargo.Properties.Settings.Default.ServinteConnection;
                lcharges = facadeInspiraServinte.GetChargesWithoutSupport(scompany);    
            }
        }

        static string GetAppointmentsData()
        {
            List<string> lappointments = new List<string>();
            StringBuilder sb = new StringBuilder();
            foreach (var item in lcharges)
            {
                if (!string.IsNullOrEmpty(item.sappointment))
                {
                    sb.Append("'");
                    sb.Append(item.sappointment);
                    sb.Append("'");
                    lappointments.Add(sb.ToString());
                    sb.Clear();
                }                
            }
            sb = null;
            return string.Join(",", lappointments);            
        }

        static void ProcessAppointments(string sappointments)
        {
            using (SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator())
            {
                salesforcesession = salesforceIntegrator.Login(FNCCreaSporteCargo.Properties.Settings.Default.SalesforceCompany, FNCCreaSporteCargo.Properties.Settings.Default.SalesforceUser, FNCCreaSporteCargo.Properties.Settings.Default.SalesforcePassword, FNCCreaSporteCargo.Properties.Settings.Default.SalesforceToken);
                salesforceIntegrator.sSession = salesforcesession.scode;
                salesforceIntegrator.sUrl = salesforcesession.sname;
                lcharges = salesforceIntegrator.GetAppointmentSupports(lcharges, sappointments);
            }
        }       

        static void Proccess()
        {
            string[] headers = { "Ingreso", "Fecha Ingreso", "Servicio", "Grupo", "Cita", "Estado", "Archivo", "Fecha Carga", "Plan", "Centro de costos", "Cajero", "Fecha Proceso" };
            sresult.AppendLine(string.Join(",", headers));
            aWSConnector.Connect();
            for (int i = 0; i < lcharges.Count; i++)
            {
                if (lcharges[i].ilevel == 0)
                {
                    AppendRow(sresult, lcharges[i].inumber, lcharges[i].dcreateddate, "El ingreso no cuenta con episodio", lcharges[i].sappointment, lcharges[i].sprogram, string.Empty, string.Empty, lcharges[i].stemplate, lcharges[i].splanname, lcharges[i].scostcenter, lcharges[i].scode);
                }
                else if (!Convert.ToBoolean(lcharges[i].sattentiontype))
                {
                    AppendRow(sresult, lcharges[i].inumber, lcharges[i].dcreateddate, lcharges[i].sagreementcode, lcharges[i].sappointment, "Soprotes cargados previamente", string.Empty, string.Empty, lcharges[i].sagreementname, lcharges[i].splanname, lcharges[i].scostcenter, lcharges[i].scode);
                }
                else
                {
                    if (lcharges[i].ldetail.Count > 0)
                    {
                        for (int j = 0; j < lcharges[i].ldetail.Count; j++)
                        {
                            if (lcharges[i].ldetail[j].stype != "Assesment")
                            {
                                if (lcharges[i].ldetail[j].snit != "Pendiente carga")
                                {
                                    lcharges[i].ldetail[j].lsources = GetDocumentPages(lcharges[i].ldetail[j].sservice, lcharges[i].ldetail[j].stype);
                                }
                            }
                            else
                            {
                                lcharges[i].ldetail[j].lsources = GetDocumentPages(lcharges[i].ldetail[j].sservice);
                            }
                            if (lcharges[i].ldetail[j].lsources != null)
                            {
                                lcharges[i].ldetail[j].lsources = CreateChargeInformation(lcharges[i], lcharges[i].ldetail[j].lsources);
                                CreateSupportFile(lcharges[i], lcharges[i].ldetail[j].lsources);
                                AppendRow(sresult, lcharges[i].inumber, lcharges[i].dcreateddate, lcharges[i].sagreementcode, lcharges[i].sappointment, lcharges[i].ldetail[j].sconcept, lcharges[i].ldetail[j].sservice, lcharges[i].ldetail[j].scostcenter, lcharges[i].ldetail[j].sgroupname, lcharges[i].splanname, lcharges[i].ldetail[j].snit, "admon");
                                lcharges[i].ldetail[j].lsources.Clear();
                            }
                            else
                            {
                                AppendRow(sresult, lcharges[i].inumber, lcharges[i].dcreateddate, lcharges[i].sagreementcode, lcharges[i].sappointment, "Archivo de soporte no encontrado localmente", string.Empty, string.Empty, lcharges[i].ldetail[j].sgroupname, lcharges[i].splanname, lcharges[i].ldetail[j].snit, string.Empty);
                            }
                        }
                    }
                    else
                    {
                        AppendRow(sresult, lcharges[i].inumber, lcharges[i].dcreateddate, lcharges[i].sagreementcode, lcharges[i].sappointment, "Cita no cuenta con soportes", string.Empty, string.Empty, lcharges[i].sagreementname, lcharges[i].splanname, lcharges[i].scostcenter, lcharges[i].scode);
                    }
                }
                
            }
        }

        static List<Generic> GetDocumentPages(string sassesmentid)
        {
            string jsonresponse = string.Empty;
            if (salesforceViaAPI.salesforceSession == null)
            {
                salesforceViaAPI = new SalesforceViaRestApi();
                salesforceViaAPI.sLogingEndPoint = FNCCreaSporteCargo.Properties.Settings.Default.SalesforceURL;
                salesforceViaAPI.sApiEndpoint = FNCCreaSporteCargo.Properties.Settings.Default.SalesforceEndPoint;
                salesforceViaAPI.DoLogin(FNCCreaSporteCargo.Properties.Settings.Default.SalesforceUser, FNCCreaSporteCargo.Properties.Settings.Default.SalesforcePassword, FNCCreaSporteCargo.Properties.Settings.Default.SalesforceClient, FNCCreaSporteCargo.Properties.Settings.Default.SalesforceSecret);
            }
            try
            {
                jsonresponse = salesforceViaAPI.GetAssessmentFormat(sassesmentid);
                jsonresponse = JsonConvert.DeserializeObject<string>(jsonresponse);
                SalesforceServiceResponse result = JsonConvert.DeserializeObject<SalesforceServiceResponse>(jsonresponse);
                if (result.success)
                {
                    return ConvertPdfBase64ToImages(result.base64);
                }
                else
                {
                    return new List<Generic>();
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                return new List<Generic>();
            }            
        }

        static List<Generic> GetDocumentPages(string sfilename, string stype)
        {
            var typePathMap = new Dictionary<string, string>
            {
                { "Prfp", FNCCreaSporteCargo.Properties.Settings.Default.PfpBucket },
                { "Rehab", FNCCreaSporteCargo.Properties.Settings.Default.RhbBucket },
                { "Sleep", FNCCreaSporteCargo.Properties.Settings.Default.SleepBucket },
                { "Aler", FNCCreaSporteCargo.Properties.Settings.Default.AlerBucket }
            };
            if (typePathMap.TryGetValue(stype, out string basePath))
            {
                byte[] pdfBytes = aWSConnector.DownloadFileAsync(sfilename, basePath);
                if (pdfBytes != null)
                {
                    return ConvertPdfToBase64(pdfBytes);
                }
            }
            return new List<Generic>();
        }

        static List<Generic> ConvertPdfBase64ToImages(string base64Pdf)
        {
            List<Generic> lgeneric = new List<Generic>();
            try
            {
                // Convertir la cadena Base64 en bytes
                byte[] pdfBytes = Convert.FromBase64String(base64Pdf);

                using (MemoryStream pdfStream = new MemoryStream(pdfBytes))
                using (var pdfDocument = PdfDocument.Load(pdfStream)) // Cargar el PDF desde el MemoryStream
                {
                    for (int i = 0; i < pdfDocument.PageCount; i++)
                    {
                        using (System.Drawing.Image image = pdfDocument.Render(i, 300, 300, PdfRenderFlags.CorrectFromDpi))
                        using (MemoryStream ms = new MemoryStream())
                        {
                            image.Save(ms, ImageFormat.Jpeg);
                            string base64Image = Convert.ToBase64String(ms.ToArray());
                            lgeneric.Add(new Generic() { sname = base64Image });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
            }
            return lgeneric;
        }

        static List<Generic> ConvertPdfToBase64(byte[] pdfBytes)
        {
            List<Generic> lgeneric = new List<Generic>();
            if (pdfBytes.Length > 0)
            {
                try
                {
                    using (MemoryStream pdfStream = new MemoryStream(pdfBytes))
                    using (var pdfDocument = PdfDocument.Load(pdfStream)) // Cargar el PDF desde el MemoryStream
                    {
                        for (int i = 0; i < pdfDocument.PageCount; i++)
                        {
                            using (System.Drawing.Image image = pdfDocument.Render(i, 300, 300, PdfRenderFlags.CorrectFromDpi))
                            using (MemoryStream ms = new MemoryStream())
                            {
                                image.Save(ms, ImageFormat.Jpeg);
                                string base64Image = Convert.ToBase64String(ms.ToArray());
                                lgeneric.Add(new Generic() { sname = base64Image });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                }
            }
            return lgeneric;
        }

        static List<Generic> CreateChargeInformation(Charge charge, List<Generic> lsources)
        {
            using (FacadeInspiraServinte servinteOracle = new FacadeInspiraServinte(FNCCreaSporteCargo.Properties.Settings.Default.ServinteConnection))
            {
                return servinteOracle.CreateChargeSupports(charge, lsources);
            }                         
        }

        static void CreateSupportFile(Charge charge, List<Generic> lsources)
        {
            int page = 1;
            foreach (Generic generic in lsources)
            {
                CreateFile(DateTime.Now, generic, page);
                page++;
            }
        }                

        static void CreateFile(DateTime dateTime, Generic file, int page)
        {
            string[] paths = new string[] { FNCCreaSporteCargo.Properties.Settings.Default.SupportPath, dateTime.Year.ToString(), dateTime.Month.ToString(), dateTime.Day.ToString() };
            string directory = Path.Combine(paths);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            paths = new string[] { FNCCreaSporteCargo.Properties.Settings.Default.SupportPath, dateTime.Year.ToString(), dateTime.Month.ToString(), dateTime.Day.ToString(), $"{file.scode}-{page.ToString()}.jpg" };
            string sfinalfile = Path.Combine(paths);
            SaveBase64AsImage(file.sname, sfinalfile);
        }

        static void SaveBase64AsImage(string base64String, string outputPath)
        {
            try
            {
                byte[] imageBytes = Convert.FromBase64String(base64String);
                using (var ms = new MemoryStream(imageBytes))
                {
                    using (System.Drawing.Image image = System.Drawing.Image.FromStream(ms))
                    {
                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                        }
                        image.Save(outputPath, ImageFormat.Jpeg);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                //throw;
            }
        }

        static void CreateResultReport()
        {                        
            // Escribir el archivo con el contenido generado
            WriteFile(sresult.ToString());
        }

        // Método auxiliar para agregar una fila al reporte
        static void AppendRow(StringBuilder sb, int ingreso, DateTime fechaIngreso, string servicio, string cita, string estado, string archivo, string fechaCarga, string grupo, string plan, string centrocosto, string cajero)
        {
            string sgrupo = (!string.IsNullOrEmpty(grupo)) ? grupo.Replace(",", " ").Replace(";", " ") : string.Empty;
            string fechaproceso = DateTime.Now.ToShortDateString();
            string row = $"{ingreso}, {fechaIngreso:yyyy-MM-dd}, {servicio}, {sgrupo}, {cita}, {estado}, {archivo}, {fechaCarga}, {plan}, {centrocosto}, {cajero}, {fechaproceso}";
            sb.AppendLine(row);
        }

        /// <summary>
        /// Método para escribir el archivo con el resultado de la operación
        /// </summary>
        /// <param name="scontent"></param>
        static void WriteFile(string scontent)
        {
            if (string.IsNullOrEmpty(scontent))
            {
                // No hay contenido para escribir
                return;
            }
            string sdate = DateTime.Now.ToString("yyyyMMdd");
            string filePath = FNCCreaSporteCargo.Properties.Settings.Default.FileResultPath + "_" + sdate + ".csv";
            try
            {
                // Verificar si la ruta del archivo es válida
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new ArgumentException("La ruta del archivo no está configurada.");
                }
                // Crear el directorio si no existe
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                // Escribir el contenido en el archivo
                File.AppendAllText(filePath, scontent, Encoding.UTF8);
                //File.WriteAllText(filePath, scontent);
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex); // Asegúrate de que LogError esté implementado
                throw; // Relanzar la excepción si es necesario
            }
        }
    }
}
