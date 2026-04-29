using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCDAC;
using FNCEntity;
using FNCUtils;
using EventLog;
using System.Configuration;
using System.Data;
using OfficeOpenXml;
using System.IO;
using System.Net.Mail;
using System.Net;
using System.Net.Mime;
using FNCSalesforce;
using static System.Net.WebRequestMethods;

namespace FNCCargoProgramas
{
    class CargoProgramas
    {
        private static List<ServintePatient> lPatient { get; set; }

        private static List<Generic> lTemplate { get; set; }

        private static List<Generic> lGeneric { get; set; }

        private static List<EntryResponse> lentries { get; set; }

        /// <summary>
        /// Punto de entrada del programa.
        /// </summary>
        /// <param name="args">Argumentos de línea de comandos.</param>
        static void Main(string[] args)
        {
            lPatient = new List<ServintePatient>();
            if (args.Length != 0)
            {
                try
                {
                    bool bIsFamisanar = Convert.ToBoolean(args[0]);
                    GetPatients(bIsFamisanar);
                    if (lPatient.Count > 0)
                    {
                        GetProductsByRate();
                        GetGroups();
                        lPatient = FilterPatients();
                        GroupPatients();
                        CreateCharges();
                        CreateStatistics();
                        // SendMail();
                    }
                }
                catch (Exception ex)
                {
                    LogError.WriteError("FNCProgramas", "FNCProgramas", ex);
                    return;
                }
            }
        }

        /// <summary>
        /// Obtiene los tipos de documentos de los pacientes de Servinte y los asigna a la lista de pacientes.
        /// </summary>
        static void GetDocumentTypes()
        {
            string[] aPatients = GetDocuments();
            List<ServintePatient> lTmpPatient = new List<ServintePatient>();
            using (ServinteOracle oServinte = new ServinteOracle())
            {
                oServinte.sconnection = FNCCargoProgramas.Properties.Settings.Default.ServinteBus;
                lTmpPatient = oServinte.GetPatients(aPatients);
                if (lTmpPatient.Count > 0)
                {
                    foreach (ServintePatient oPatient in lPatient)
                    {
                        ServintePatient oTmpPatient = lTmpPatient.FirstOrDefault(x => x.sdocument == oPatient.sdocument);
                        if (oTmpPatient != null)
                        {
                            oPatient.sdocumenttype = oTmpPatient.sdocumenttype;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Obtiene los documentos de los pacientes.
        /// </summary>
        /// <returns>Arreglo de documentos de pacientes.</returns>
        static string[] GetDocuments()
        {
            string[] aPatients = new string[lPatient.Count];
            int i = 0;
            foreach (ServintePatient oPatient in lPatient)
            {
                aPatients[i] = oPatient.sdocument;
                i++;
            }
            return aPatients;
        }

        /// <summary>
        /// Filtra los pacientes y procesa sus citas y servicios.
        /// </summary>
        /// <returns>Lista de pacientes filtrados.</returns>
        static List<ServintePatient> FilterPatients()
        {
            Generic generic = null;
            string sunit = string.Empty;
            for (int i = 0; i < lPatient.Count; i++)
            {
                for (int j = 0; j < lPatient[i].lappointments.Count; j++)
                {
                    lPatient[i].ssecondname = (!string.IsNullOrEmpty(lPatient[i].ssecondname)) ? lPatient[i].ssecondname : string.Empty;
                    lPatient[i].ssecondsurname = (!string.IsNullOrEmpty(lPatient[i].ssecondsurname)) ? lPatient[i].ssecondsurname : string.Empty;
                }
            }
            return lPatient;
        }

        /// <summary>
        /// Obtiene los pacientes para los programas, dependiendo de si es Famisanar.
        /// </summary>
        /// <param name="bIsFamisanar">Indica si es Famisanar.</param>
        static void GetPatients(bool bIsFamisanar)
        {
            string sInitialDate = (!string.IsNullOrEmpty(FNCCargoProgramas.Properties.Settings.Default.InitialDate)) ? FNCCargoProgramas.Properties.Settings.Default.InitialDate : DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            string sFinalDate = (!string.IsNullOrEmpty(FNCCargoProgramas.Properties.Settings.Default.FinalDate)) ? FNCCargoProgramas.Properties.Settings.Default.FinalDate : DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");
            using (SalesforceViaRestApi salesforceViaRestApi = new SalesforceViaRestApi())
            {
                salesforceViaRestApi.sLogingEndPoint = FNCCargoProgramas.Properties.Settings.Default.SalesforceURL;
                salesforceViaRestApi.sApiEndpoint = FNCCargoProgramas.Properties.Settings.Default.SalesforceEndPoint;
                string susr = FNCCargoProgramas.Properties.Settings.Default.SalesforceUser;
                string spwd = FNCCargoProgramas.Properties.Settings.Default.SalesforcePassword + FNCCargoProgramas.Properties.Settings.Default.SalesforceToken;
                string ssectet = FNCCargoProgramas.Properties.Settings.Default.SalesforceSecret;
                string sclientid = FNCCargoProgramas.Properties.Settings.Default.SalesforceClient;
                salesforceViaRestApi.DoLogin(susr, spwd, sclientid, ssectet);
                lPatient = salesforceViaRestApi.GetPatientsforPrograms(sInitialDate, sFinalDate, FNCCargoProgramas.Properties.Settings.Default.ErrorFile, bIsFamisanar, FNCCargoProgramas.Properties.Settings.Default.CitasId);
            }
        }

        /// <summary>
        /// Obtiene los grupos desde Servinte.
        /// </summary>
        static void GetGroups()
        {
            lTemplate = new List<Generic>();
            using (Servinte servinte = new Servinte(FNCCargoProgramas.Properties.Settings.Default.InspiraConnection, false))
            {
                lTemplate = servinte.GetGroupsData();
            }
        }

        /// <summary>
        /// Obtiene los productos por tarifa desde Servinte Oracle.
        /// </summary>
        static void GetProductsByRate()
        {
            using (ServinteOracle servinteOracle = new ServinteOracle())
            {
                servinteOracle.sconnection = FNCCargoProgramas.Properties.Settings.Default.ServinteBus;
                lGeneric = servinteOracle.GetProductConceptsyRate();
            }
        }

        /// <summary>
        /// Obtiene la tarifa de un producto específico.
        /// </summary>
        /// <param name="srate">Tarifa.</param>
        /// <param name="scostcenter">Centro de costos.</param>
        /// <param name="sproduct">Producto.</param>
        /// <param name="sstartwith">Inicio del filtro.</param>
        /// <returns>Objeto <see cref="Generic"/> con la información del producto.</returns>
        static Generic GetProductRate(string srate, string scostcenter, string sproduct, string sstartwith)
        {
            return lGeneric.FirstOrDefault(x => x.scode == srate && x.sname == sproduct && x.sfilter == scostcenter && x.sextra1.StartsWith(sstartwith));
        }

        /// <summary>
        /// Obtiene la información del grupo.
        /// </summary>
        /// <param name="sgroup">Código del grupo.</param>
        /// <returns>Información del grupo en formato de cadena.</returns>
        static string GetGroupInfo(string sgroup)
        {
            Generic generic = lTemplate.FirstOrDefault(x => x.scode == sgroup);
            return (generic != null) ? generic.iid.ToString() : "0";
        }

        /// <summary>
        /// Agrupa los pacientes por documento y tipo de documento.
        /// </summary>
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

        /// <summary>
        /// Crea los cargos para los programas.
        /// </summary>
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

        /// <summary>
        /// Crea estadísticas del proceso de carga.
        /// </summary>
        private static void CreateStatistics()
        {
            using (Integrador integrador = new Integrador())
            {
                integrador.sconnection = FNCCargoProgramas.Properties.Settings.Default.Integrador;
                lentries = integrador.InsertRecord(lentries);
            }
        }

        /// <summary>
        /// Envía un correo electrónico con el resultado de la creación de cargos.
        /// </summary>
        static void SendMail()
        {
            SendMail oSendMail = new FNCUtils.SendMail()
            {
                bIsHTML = true,
                sPassword = FNCCargoProgramas.Properties.Settings.Default.MailPassword,
                sUser = FNCCargoProgramas.Properties.Settings.Default.MailUser,
                iPort = FNCCargoProgramas.Properties.Settings.Default.MailPort,
                sServer = FNCCargoProgramas.Properties.Settings.Default.MailServer,
                lRecipient = GetRecipients(),
                sMessage = GetMailMessage(),
                sSubject = FNCCargoProgramas.Properties.Settings.Default.MailSubject,
            };
            oSendMail.Send();
        }

        /// <summary>
        /// Obtiene los destinatarios del correo electrónico.
        /// </summary>
        /// <returns>Lista de destinatarios.</returns>
        static List<string> GetRecipients()
        {
            string[] sRecipients = FNCCargoProgramas.Properties.Settings.Default.MailRecipients.Split(',');
            return sRecipients.ToList();
        }

        /// <summary>
        /// Obtiene el mensaje del correo electrónico.
        /// </summary>
        /// <returns>Mensaje del correo en formato HTML.</returns>
        static string GetMailMessage()
        {
            StringBuilder sHTML = new StringBuilder("<h1>Resultado de la creaci&oacute;n de cargos</h1>");
            sHTML.Append("<p>En el archivo adjunto a este correo se env&iacute;a el listado de pacientes creados para los cargos correspondientes a programas especiales del " + DateTime.Now.AddDays(-1).ToShortDateString() + "</p>");
            sHTML.Append("<p>Cordialmente,</p><br />");
            sHTML.Append("<p><strong>Administrador del sistema</strong></p>");
            return sHTML.ToString();
        }

        /// <summary>
        /// Genera un archivo Excel con los resultados de los cargos creados.
        /// </summary>
        /// <param name="lentryResponses">Lista de respuestas de entrada.</param>
        /// <returns>Stream de memoria con el archivo Excel generado.</returns>
        static MemoryStream GenerateExcel(List<EntryResponse> lentryResponses)
        {
            MemoryStream ms = null;
            ExcelPackage oExcel = null;
            DataTable dt = new DataTable();
            try
            {
                dt = CreateDataTable(lentryResponses);
                oExcel = new ExcelPackage();
                ExcelWorksheet ws = oExcel.Workbook.Worksheets.Add("Cargos");
                ws.Cells["A1"].LoadFromDataTable(dt, true);
                ms = new MemoryStream(oExcel.GetAsByteArray());
                return ms;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                oExcel = null;
            }
        }

        /// <summary>
        /// Obtiene el adjunto a enviar en el correo electrónico.
        /// </summary>
        /// <param name="ms">Stream de memoria con el archivo Excel.</param>
        /// <returns>Objeto <see cref="Attachment"/> con el archivo adjunto.</returns>
        static Attachment GetAttachment(MemoryStream ms)
        {
            Attachment attachment = new Attachment(ms, new ContentType(MediaTypeNames.Application.Octet));
            attachment.ContentDisposition.FileName = "cargoscreados.xlsx";
            return attachment;
        }

        /// <summary>
        /// Crea una tabla de datos con los resultados de los cargos creados.
        /// </summary>
        /// <param name="lCorrectos">Lista de respuestas de entrada.</param>
        /// <returns>Tabla de datos con los resultados.</returns>
        static DataTable CreateDataTable(List<EntryResponse> lCorrectos)
        {
            DataTable dt = new DataTable();
            object[] values = new object[7] { "Tipo Documento", "Documento", "Convenio", "Tarifa", "Servicio", "Ingreso", "Cita" };
            for (int i = 0; i < values.Length; i++)
            {
                dt.Columns.Add(values[i].ToString());
            }
            for (int i = 0; i < lCorrectos.Count; i++)
            {
                values = new object[7]
                {
                    lCorrectos[i].sdocumenttype,
                    lCorrectos[i].sdocument,
                    lCorrectos[i].splan,
                    lCorrectos[i].srate,
                    lCorrectos[i].sservice,
                    lCorrectos[i].ientry,
                    lCorrectos[i].sappointment,
                };
                dt.Rows.Add(values);
            }
            return dt;
        }
    }
}
