using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using System.IO;

namespace FNCUtils
{
    public class SendMail
    {
        public string sUser { get; set; }

        public string sPassword { get; set; }

        public string sServer { get; set; }

        public string sMessage { get; set; }

        public int iPort { get; set; }

        public List<string> lRecipient { get; set; }

        public string sFile { get; set; }

        public string sSubject { get; set; }

        public bool bIsHTML { get; set; }

        public string sContentType { get; set; }

        public Attachment oAttachment { get; set; }

        public List<string> attachments { get; set; }

        public bool bisTLS { get; set; }

        public string sFrom { get; set; }

        /// <summary>
        /// Método que realiza el envío del correo electrónico de acuerdo a los parámetros asignados al objeto
        /// </summary>
        /// <param name="isMultiple">Boleano que indica si los archivos a adjuntar en el correo a enviar son muchos o solo uno</param>
        public void Send(bool isMultiple = false)
        {
            string strFrom = (string.IsNullOrEmpty(this.sFrom)) ? this.sUser : this.sFrom;
            SmtpClient oSmtp = new SmtpClient(this.sServer, this.iPort);
            MailMessage oMail = new MailMessage(strFrom, this.lRecipient[0], this.sSubject, this.sMessage) { IsBodyHtml = bIsHTML };
            try
            {
                if (this.lRecipient.Count > 1)
                {
                    for(int i = 1; i < this.lRecipient.Count; i++)
                    {
                        oMail.Bcc.Add(this.lRecipient[i]);
                    }
                }                
                if (!isMultiple)
                {
                    if (!string.IsNullOrEmpty(this.sFile)) oMail.Attachments.Add(GetAttachment());
                    if (this.oAttachment != null) oMail.Attachments.Add(oAttachment);
                }
                else
                {
                    if (attachments != null)
                    {
                        foreach (string item in attachments)
                        {
                            this.sFile = item;
                            oMail.Attachments.Add(GetAttachment());
                        }
                    }                    
                }
                oSmtp.Credentials = new NetworkCredential(this.sUser, this.sPassword);
                if (this.bisTLS) 
                {
                    oSmtp.EnableSsl = true;
                    oSmtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                }                
                oSmtp.Send(oMail);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                oSmtp.Dispose();
                oMail.Dispose();
                oSmtp = null;
                oMail = null;
            }
        }

        private Attachment GetAttachment()
        {
            byte[] contentAsBytes = File.ReadAllBytes(this.sFile);
            MemoryStream memStream = new MemoryStream(contentAsBytes);
            StreamWriter streamWriter = new StreamWriter(memStream);
            streamWriter.Flush();
            memStream.Position = 0;
            return new Attachment(memStream, GetFileName())
            {
                ContentType = new System.Net.Mime.ContentType(this.sContentType),
                //ContentType = new System.Net.Mime.ContentType("application/vnd.ms-excel"),
                Name = GetFileName(),
                NameEncoding = Encoding.UTF8,
            };
        }

        private string GetFileName()
        {
            FileInfo oFile = new FileInfo(this.sFile);
            return oFile.Name;
        }
    }
}
