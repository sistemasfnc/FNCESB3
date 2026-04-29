using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Configuration;
using Google.Apis.Discovery;
using Google.Apis.Services;
using Google.Apis.Calendar.v3;

namespace FNCWSDigiturno
{
    public partial class SendGmail : System.Web.UI.Page
    {
        #region Propiedades privadas, contienen la información de destino del correo

        private string sTarget
        {
            get { return (Request["destino"] != null) ? Request["destino"].ToString() : string.Empty; }
        }

        private string sInitial
        {
            get { return (Request["horainicio"] != null) ? Request["horainicio"].ToString() : string.Empty; }
        }

        private string sFinal
        {
            get { return (Request["horafin"] != null) ? Request["horafin"].ToString() : string.Empty; }
        }

        private string sSubject
        {
            get { return (Request["asunto"] != null) ? Request["asunto"].ToString() : string.Empty; }
        }

        private string sMailTo
        {
            get { return (Request["destinatario"] != null) ? Request["destinatario"].ToString() : string.Empty; }
        }

        #endregion

        /// <summary>
        /// Evento cargar de la página, se encarga de procesar la información recibida por Salesforce y hacer el envío del evento a Google
        /// </summary>
        /// <param name="sender">Objeto página</param>
        /// <param name="e">Argumentos evento</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            this.AccessGoogle();
            /*if (!string.IsNullOrEmpty(this.sTarget))
            {
                //string[] aTarget = this.sTarget.Split('@');
                if (!this.sSubject.ToUpper().Contains("BLOQUEO") && !this.sSubject.ToUpper().Contains("RESERVA") && !this.sSubject.ToUpper().Contains("TRASLADO"))
                {
                    //if (aTarget.Length > 1)
                    {                        
                        string sTarget = this.sTarget;
                        MailMessage m = new MailMessage();
                        m.From = new System.Net.Mail.MailAddress(ConfigurationManager.AppSettings["MailSender"].ToString(), "from");
                        m.To.Add(new System.Net.Mail.MailAddress(sTarget, "to"));
                        m.Subject = "Cita FNC";
                        System.Net.Mime.ContentType typeC = new System.Net.Mime.ContentType("text/calendar");
                        typeC.Parameters.Add("method", "REQUEST");
                        typeC.Parameters.Add("name", "meeting.ics");
                        AlternateView m_calV = AlternateView.CreateAlternateViewFromString(GetText(sTarget), typeC);
                        m.AlternateViews.Add(m_calV);
                        SmtpClient client = new SmtpClient(ConfigurationManager.AppSettings["MailServer"].ToString(), Convert.ToInt32(ConfigurationManager.AppSettings["MailPort"]));
                        client.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["MailSender"].ToString(), ConfigurationManager.AppSettings["MailPassword"].ToString());
                        client.Send(m);
                    }
                    
                }               
            }  */          
        }

        /// <summary>
        /// Método que genera el contenido del evento en Google con la información de Salesforce
        /// </summary>
        /// <returns>Cadena de caracteres que contiene el texto con el formato Google Event para el envío</returns>
        private string GetText(string sTarget)
        {
            StringBuilder str = new StringBuilder();
            str.AppendLine("BEGIN:VCALENDAR");
            str.AppendLine("PRODID:-//GeO");
            str.AppendLine("VERSION:2.0");
            str.AppendLine("METHOD:REQUEST");
            str.AppendLine("BEGIN:VEVENT");            
            str.AppendLine(string.Format("DTSTART:{0}", this.sInitial));
            str.AppendLine(string.Format("DTSTAMP:{0:yyyyMMddTHHmmssZ}", DateTime.UtcNow));            
            str.AppendLine(string.Format("DTEND:{0}", this.sFinal));            
            str.AppendLine(string.Format("UID:{0}", Guid.NewGuid()));
            str.AppendLine(string.Format("DESCRIPTION:{0}", "CITA MEDICA"));
            str.AppendLine(string.Format("DESCRIPTION;ENCODING=QUOTED-PRINTABLE:{0}", "CALENDARIO"));
            str.AppendLine(string.Format("X-ALT-DESC;FMTTYPE=text/html:{0}", this.sSubject));
            str.AppendLine(string.Format("SUMMARY;ENCODING=QUOTED-PRINTABLE:{0}", this.sSubject));
            str.AppendLine(string.Format("ORGANIZER:MAILTO:{0}", "sistemas@neumologica.org"));
            str.AppendLine(string.Format("ATTENDEE;CN=\"{0}\";RSVP=TRUE:mailto:{1}", this.sMailTo, sTarget));
            str.AppendLine("BEGIN:VALARM");
            str.AppendLine("TRIGGER:-PT15M");
            str.AppendLine("ACTION:DISPLAY");
            str.AppendLine("DESCRIPTION;ENCODING=QUOTED-PRINTABLE:Reminder");
            str.AppendLine("END:VALARM");
            str.AppendLine("END:VEVENT");
            str.AppendLine("END:VCALENDAR");
            return str.ToString();
        }

        private void AccessGoogle()
        {
            var Service = new CalendarService(new BaseClientService.Initializer() { ApiKey = "AIzaSyDieYLY17eMQkwp-tupnuGPvaGud2I-fzM", ApplicationName = "GmailInspira" });
           // CalendarListResource calendarListResource = Service.CalendarList()            
        }
    }
}