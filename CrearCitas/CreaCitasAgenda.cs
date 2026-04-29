using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using System.Configuration;
using System.IO;
using FNCDAC;
using System.Data;

namespace CrearCitas
{
    class CreaCitasAgenda
    {
        static string sConnection = "Data Source=HEIMDALL;Initial Catalog=FNCStats;User Id=sa;Password=FNCN3um0!!+;";
        static string sUrl = "http://localhost:9090/SendGmail.aspx";

        static void Main(string[] args)
        {
            using (Inspira oInspira = new Inspira())
            {
                oInspira.sConnection = CreaCitasAgenda.sConnection;
                DataTable dt = new DataTable();
                DateTime dtFinal = new DateTime();
                StringBuilder sQuery = new StringBuilder();
                try
                {                    
                    dt = oInspira.GetAppointmentData();
                    foreach (DataRow dr in dt.Rows)
                    {
                        sQuery.Append("asunto=Nueva Cita Agenda, Paciente ");
                        sQuery.Append(dr["NOMBRES"].ToString());
                        sQuery.Append("&horainicio=");
                        dtFinal = Convert.ToDateTime(Convert.ToDateTime(dr["FECHA_CITA"]).ToShortDateString() + " " + dr["HORA_INICIO"]);
                        /*dtTime = Convert.ToDateTime(dr["HORA_INICIO"]);
                        dtResult = new DateTime(dtFinal.Year, dtFinal.Month, dtFinal.Day, dtTime.Hour, dtTime.Minute, dtTime.Second);*/
                        sQuery.Append(dtFinal.ToString("yyyyMMddTHHmmss"));
                        sQuery.Append("&horafin=");
                        /*dtTime = Convert.ToDateTime(dr["HORA_FIN"]);
                        dtResult = new DateTime(dtFinal.Year, dtFinal.Month, dtFinal.Day, dtTime.Hour, dtTime.Minute, dtTime.Second);*/
                        dtFinal = Convert.ToDateTime(Convert.ToDateTime(dr["FECHA_CITA"]).ToShortDateString() + " " + dr["HORA_FIN"]);
                        sQuery.Append(dtFinal.ToString("yyyyMMddTHHmmss"));
                        sQuery.Append("&destino=");
                        sQuery.Append(dr["correo"].ToString());
                        sQuery.Append("&destinatario=");
                        sQuery.Append(dr["profesional"].ToString());
                        SetHttpPostVar(CreaCitasAgenda.sUrl, sQuery.ToString());
                        sQuery.Remove(0, sQuery.Length);
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }
                finally
                {
                    dt.Dispose();
                    dt = null;
                }
            }
        }

        static public string SetHttpPostVar(string Url, string Parameters)
        {
            WebRequest ObjRequest = WebRequest.Create(Url);
            ObjRequest.ContentType = "application/x-www-form-urlencoded";
            ObjRequest.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(Parameters);
            ObjRequest.ContentLength = bytes.Length;
            Stream os = ObjRequest.GetRequestStream();
            os.Write(bytes, 0, bytes.Length);
            os.Close();
            WebResponse resp = ObjRequest.GetResponse();
            if (resp == null)
                return null;
            StreamReader sr = new StreamReader(resp.GetResponseStream());
            return sr.ReadToEnd().Trim();
        }
    }
}
