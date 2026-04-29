using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using SendPatientDocumentation.FNCESB;
using EventLog;
using FNCEntity;
using System.Configuration;
using System.Xml;
using System.Xml.Linq;
using FNCUtils;
using System.Data;
using FNCDAC;

namespace SendPatientDocumentation
{
    class SendPatientDocumentationProgram
    {
        static List<string> lFiles { get; set; }

        static List<string> lValidation { get; set; }

        static List<FNCEntity.Generic> lPatients { get; set; }

        static string[] asDocuments { get; set; }

        static string sXML { get; set; }

        static List<FNCEntity.Generic> lGeneric { get; set; }

        static List<FNCEntity.Generic> lError { get; set; }

        static List<FNCEntity.Generic> lOk { get; set; }

        static void Main(string[] args)
        {
            try
            {
                Initialize();
                GetFiles();
                FilterFiles();
                GetPatients();
                GetXML();
                ProcessXML();
                GetNotFoundPatients();
                SendMails();
                SuccessToDB();
                ErrorToDB();
            }
            catch (Exception ex)
            {
                LogError.WriteError("SendPatient", "Application", ex);
                throw;
            }
            
        }

        static void FilterFiles()
        {
            List<string> inList1ButNotList2 = (from o in lFiles join p in lValidation on o equals p into t
                                               from od in t.DefaultIfEmpty()
                                               where od == null
                                               select o).ToList<string>();
            lFiles = inList1ButNotList2;
        }

        static void Initialize()
        {
            lError = new List<FNCEntity.Generic>();
            lOk = new List<FNCEntity.Generic>();
            using (EnvioHistorias oDAC = new EnvioHistorias())
            {
                oDAC.sConnection = SendPatientDocumentation.Properties.Settings.Default.DBConnection;
                lValidation = oDAC.GetSuccessData();
            }
        }
        static void GetNotFoundPatients()
        {
            foreach (FNCEntity.Generic item in lPatients)
            {
                if (lGeneric.FirstOrDefault(x => x.scode.Trim() == item.scode.Trim()) == null)
                {
                    FNCEntity.Generic generic = new FNCEntity.Generic()
                    {
                        sname = "Documento de archivo de paciente no encontrado en inspira",
                        scode = item.scode,
                        dtDate = DateTime.Now,
                    };
                    lError.Add(generic);
                    generic = null;
                }
            }
        }

        static void GetFiles()
        {
            string sPath = SendPatientDocumentation.Properties.Settings.Default.FilesFolder;
            lFiles = new List<string>();
            if (Directory.GetDirectories(sPath).Count() != 0)
            {
                foreach (string dir in Directory.GetDirectories(sPath))
                {
                    SearchDir("*.pdf", dir);
                }
            }
            else
            {
                SearchDir("*.pdf", sPath);
            }
        }

        static void GetPatients()
        {
            lPatients = new List<FNCEntity.Generic>();
            string sfileName = string.Empty;
            FNCEntity.Generic generic = null;
            string sDocument = string.Empty;
            asDocuments = new string[lFiles.Count];
            int i = 0;
            foreach (string sFile in lFiles)
            {
                sfileName = GetFileName(sFile, 1);
                sDocument = GetPatientDocument(sfileName);
                if (!string.IsNullOrEmpty(sDocument))
                {
                    generic = new FNCEntity.Generic()
                    {
                        scode = sDocument.Trim(),
                        sname = GetFileName(sFile, 2),
                    };
                    lPatients.Add(generic);                  
                }
                asDocuments[i] = "'" + sDocument + "'";
                i++;
            }
        }

        static string GetPatientsList()
        {
            string[] asResult = asDocuments.Distinct().ToArray();
            return string.Join(",", asResult);
        }

        static void GetXML()
        {
            string sDocuments = GetPatientsList();
            WSDigiturno wSDigiturno = new WSDigiturno();
            sXML = wSDigiturno.GetTodayPatients(sDocuments);
        }

        static void ProcessXML()
        {
            byte[] byteArray = Encoding.UTF8.GetBytes(sXML);
            MemoryStream stream = new MemoryStream(byteArray);
            XDocument xDocument = XDocument.Load(stream);
            if (!string.IsNullOrEmpty(xDocument.Root.Element("ERROR").Element("MENSAJE").Value))
            {
                ApplicationException applicationException = new ApplicationException(xDocument.Root.Element("ERROR").Element("MENSAJE").Value);
                throw applicationException;
            }
            else
            {
                lGeneric = new List<FNCEntity.Generic>();
                lGeneric = (from x in xDocument.Descendants("PACIENTE")
                            select new FNCEntity.Generic
                            {
                                scode = x.Element("DOCUMENTO").Value,
                                sname = x.Element("CORREO").Value,
                            }).ToList();
            }
        }
        
        static void InsertSent(string sDocument, List<string> lattachments)
        {
            foreach (string item in lattachments)
            {
                FNCEntity.Generic generic = new FNCEntity.Generic()
                {
                    scode = sDocument,
                    sname = item,
                    dtDate = DateTime.Now,
                };
                lOk.Add(generic);
                generic = null;
            }
        }

        static DataTable List2DataTable(List<FNCEntity.Generic> lGeneric, int iType)
        {
            DataTable dataTable = new DataTable();
            object[] values = null;
            if (iType == 1)
            {
                values = new object[3] { "documento", "archivo", "fecha" };
            }
            else if (iType == 2)
            {
                values = new object[3] { "documento", "mensaje", "fecha" };
            }
            for (int i = 0; i < values.Length; i++)
            {
                dataTable.Columns.Add(values[i].ToString());
            }
            foreach (FNCEntity.Generic item in lGeneric)
            {
                values = new object[3]
                {
                    item.scode,
                    item.sname, 
                    item.dtDate,
                };
                dataTable.Rows.Add(values);
            }
            return dataTable;
        }

        static void SuccessToDB()
        {
            using (EnvioHistorias ODAC = new EnvioHistorias())
            {
                ODAC.sConnection = SendPatientDocumentation.Properties.Settings.Default.DBConnection;
                ODAC.InsertSuccess(List2DataTable(lOk, 1));
            }
        }

        static void ErrorToDB()
        {
            using (EnvioHistorias ODAC = new EnvioHistorias())
            {
                ODAC.sConnection = SendPatientDocumentation.Properties.Settings.Default.DBConnection;
                ODAC.InsertErrors(List2DataTable(lError, 2));
            }
        }

        static void SendMails()
        {
            SendMail sendMail = new SendMail();
            sendMail.bIsHTML = true;
            sendMail.sMessage = "Env&iacute;o de &oacute;rdenes m&eacute;dicas";
            sendMail.sServer = SendPatientDocumentation.Properties.Settings.Default.MailServer;
            sendMail.iPort = SendPatientDocumentation.Properties.Settings.Default.MailPort;
            sendMail.sUser = SendPatientDocumentation.Properties.Settings.Default.MailUser;
            sendMail.sPassword = SendPatientDocumentation.Properties.Settings.Default.MailPassword;
            List<string> recipients = new List<string>();
            foreach (FNCEntity.Generic item in lGeneric)
            {
                sendMail.attachments = GetPatientFiles(item.scode);
                if (sendMail.attachments.Count > 0)
                {
                    sendMail.sSubject = item.scode;
                    sendMail.sContentType = "application/pdf";
                    recipients.Add(item.sname);
                    sendMail.lRecipient = recipients;
                    try
                    {
                        sendMail.Send(true);
                        InsertSent(item.scode, sendMail.attachments);
                    }
                    catch (Exception ex)
                    {
                        FNCEntity.Generic generic = new FNCEntity.Generic()
                        {
                            sname = "Error de envío de correo para el paciente " + ex.Message,
                            scode = item.scode,
                            dtDate = DateTime.Now,
                        };
                        generic = null;
                        LogError.WriteError("SendPatient", "Application", ex);                        
                    }                    
                    recipients.RemoveAt(0);
                }
                else
                {
                    FNCEntity.Generic generic = new FNCEntity.Generic()
                    {
                        sname = "Archivos no encontrados para el paciente",
                        scode = item.scode,
                        dtDate = DateTime.Now,
                    };
                    lError.Add(generic);
                    generic = null;
                }
            }
        }

        static List<string> GetPatientFiles(string sDocument)
        {
            return (from item in lPatients where item.scode == sDocument select item.sname).ToList();            
        }

        static string GetPatientDocument(string sfileName)
        {
            string[] result = null;
            if (sfileName.Contains("_"))
            {
                result = sfileName.Split('_');
                return result[0];
            }
            else
            {
                FNCEntity.Generic generic = new FNCEntity.Generic()
                {
                    sname = "Nombre de archivo formato incorrecto",
                    scode = sfileName,
                    dtDate = DateTime.Now,
                };
                lError.Add(generic);
                generic = null;
            }
            return string.Empty;
        }

        static string GetFileName(string sFile, int iProperty)
        {
            FileInfo fileInfo = new FileInfo(sFile);
            if (iProperty == 1)
            {
                return fileInfo.Name;
            }
            else if (iProperty == 2)
            {
                return fileInfo.FullName;
            }
            return fileInfo.Name;
        }

       static void SearchDir(string FileType, string sDir)
       {
            /*var todayFiles = Directory.GetFiles(sDir).Where(x => new FileInfo(x).LastWriteTime.Date == DateTime.Today.AddDays(-1));
            lFiles.AddRange(todayFiles);*/
            if (DateTime.Now.Hour == 13)
            {
                DateTime dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 6, 0, 0);
                DateTime dateTime1 = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 12, 59, 59);
                var todayFiles = Directory.GetFiles(sDir).Where(x => new FileInfo(x).CreationTime >= dateTime && new FileInfo(x).CreationTime <= dateTime1);
                lFiles.AddRange(todayFiles);
            }
            else
            {
                DateTime dateTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 13, 0, 0);
                DateTime dateTime1 = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 20, 0, 0);
                var todayFiles = Directory.GetFiles(sDir).Where(x => new FileInfo(x).CreationTime >= dateTime && new FileInfo(x).CreationTime <= dateTime1);
                lFiles.AddRange(todayFiles);
            }
            foreach (string d in Directory.GetDirectories(sDir))
            {
                SearchDir(FileType, d);
            }
        }
    }
}
