using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.IO;
using OAuth2;
using Newtonsoft.Json;

namespace ComputecSMS
{
    class SendSMS
    {
        static void Main(string[] args)
        {
            StringBuilder sParameters = new StringBuilder("Usuario=");
            sParameters.Append(ComputecSMS.Properties.Settings.Default.sUser);
            sParameters.Append("&Contraseña=");
            sParameters.Append(ComputecSMS.Properties.Settings.Default.sPassword);
            //string sResponse = SetHttpPostVar(ComputecSMS.Properties.Settings.Default.sUrl, sParameters.ToString());
            string sResponse = Login();
            Console.Write(sResponse);
        }    
        
        static string Login()
        {

            RestClient restClient = new RestClient();
            restClient.endPoint = ComputecSMS.Properties.Settings.Default.sUrl;
            restClient.authType = authenticationType.Basic;
            restClient.userName = ComputecSMS.Properties.Settings.Default.sUser;
            restClient.userPassword = ComputecSMS.Properties.Settings.Default.sPassword;
            return restClient.makeRequest();
        }

        static string SetHttpPostVar(string sUrl, string sParameters)
        {
            WebRequest oRequest = WebRequest.Create(sUrl);
            //oRequest.Credentials = new System.Net.NetworkCredential() { UserName = ComputecSMS.Properties.Settings.Default.sUser, Password = ComputecSMS.Properties.Settings.Default.sPassword };            
            oRequest.ContentType = "application/x-www-form-urlencoded";
            oRequest.Method = "POST";
            byte[] bytes = Encoding.ASCII.GetBytes(sParameters);
            oRequest.ContentLength = bytes.Length;
            try
            {
                Stream os = oRequest.GetRequestStream();
                os.Write(bytes, 0, bytes.Length);
                os.Close();
                WebResponse resp = oRequest.GetResponse();
                if (resp == null) return string.Empty;
                StreamReader sr = new StreamReader(resp.GetResponseStream());
                return sr.ReadToEnd().Trim();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            
        }
    }
}
