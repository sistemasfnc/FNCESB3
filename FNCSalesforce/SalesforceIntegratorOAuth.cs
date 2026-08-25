using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using EventLog;
using FNCEntity;

namespace FNCSalesforce
{
    /// <summary>
    /// Fragmento (partial o para pegar dentro de SalesforceIntegrator) con el nuevo
    /// método de Login por OAuth 2.0 Client Credentials Flow.
    ///
    /// REEMPLAZA al método Login() que usaba soapClient.login() (SOAP API login(),
    /// retirado por Salesforce en Summer '27 para versiones 31.0 - 64.0).
    ///
    /// El access token que devuelve OAuth se usa EXACTAMENTE igual que el sessionId
    /// anterior: se asigna a SessionHeader.sessionId. El resto del DLL (query, update,
    /// create) NO requiere cambios.
    /// </summary>
    public partial class SalesforceIntegratorOAuth
    {
        /// <summary>
        /// Versión del API con la que fue generado el proxy SOAP (WSDL Enterprise).
        /// IMPORTANTE: debe coincidir con la versión de la URL que hoy tienes en el
        /// app.config / web.config del endpoint del SoapClient, por ejemplo si el
        /// endpoint actual es https://xxx.salesforce.com/services/Soap/c/52.0
        /// entonces el valor debe ser "52.0".
        /// Debe estar entre 31.0 y 64.0 (65.0+ no soporta el proxy antiguo con login,
        /// pero el resto de operaciones SOAP sí funcionan; mantén la versión de tu WSDL).
        /// </summary>
        private const string SOAP_API_VERSION = "52.0";

        /// <summary>
        /// Objeto para deserializar la respuesta del endpoint /services/oauth2/token
        /// </summary>
        [DataContract]
        internal class OAuthTokenResponse
        {
            [DataMember(Name = "access_token")]
            public string access_token { get; set; }

            [DataMember(Name = "instance_url")]
            public string instance_url { get; set; }

            [DataMember(Name = "token_type")]
            public string token_type { get; set; }

            [DataMember(Name = "error")]
            public string error { get; set; }

            [DataMember(Name = "error_description")]
            public string error_description { get; set; }
        }

        /// <summary>
        /// Método para realizar el login en el API de Salesforce usando OAuth 2.0
        /// Client Credentials Flow (reemplazo del SOAP API login() retirado).
        /// Devuelve el mismo objeto Generic que el método anterior:
        ///   scode = access token (equivalente al session id)
        ///   sname = url del endpoint SOAP (equivalente al serverUrl)
        /// De esta forma los desarrollos que consumen el DLL solo cambian los
        /// parámetros de la llamada, no el manejo del resultado.
        /// </summary>
        /// <param name="sClientId">String Consumer Key del External Client App</param>
        /// <param name="sClientSecret">String Consumer Secret del External Client App</param>
        /// <param name="sLoginUrl">String url de login: https://login.salesforce.com,
        /// https://test.salesforce.com (sandbox) o el My Domain de la org
        /// (https://midominio.my.salesforce.com). Para Client Credentials Salesforce
        /// recomienda usar el My Domain.</param>
        /// <returns>Generic con scode = session id (access token) y sname = url SOAP</returns>
        public Generic Login(string sClientId, string sClientSecret, string sLoginUrl)
        {
            HttpWebRequest oRequest = null;
            try
            {
                // Igual que en el constructor original: forzar TLS 1.2
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                string sBody = "grant_type=client_credentials"
                             + "&client_id=" + Uri.EscapeDataString(sClientId)
                             + "&client_secret=" + Uri.EscapeDataString(sClientSecret);
                byte[] aData = Encoding.UTF8.GetBytes(sBody);

                oRequest = (HttpWebRequest)WebRequest.Create(sLoginUrl.TrimEnd('/') + "/services/oauth2/token");
                oRequest.Method = "POST";
                oRequest.ContentType = "application/x-www-form-urlencoded";
                oRequest.ContentLength = aData.Length;
                using (Stream oStream = oRequest.GetRequestStream())
                {
                    oStream.Write(aData, 0, aData.Length);
                }

                string sJson = string.Empty;
                using (HttpWebResponse oResponse = (HttpWebResponse)oRequest.GetResponse())
                using (StreamReader oReader = new StreamReader(oResponse.GetResponseStream()))
                {
                    sJson = oReader.ReadToEnd();
                }

                OAuthTokenResponse oToken = this.DeserializeToken(sJson);
                if (oToken == null || string.IsNullOrEmpty(oToken.access_token))
                {
                    throw new ApplicationException("OAuth no devolvió access_token. Respuesta: " + sJson);
                }

                // Construir la URL del endpoint SOAP Enterprise (equivalente al
                // serverUrl que antes devolvía loginResult.serverUrl):
                //   {instance_url}/services/Soap/c/{version}
                string sSoapUrl = oToken.instance_url.TrimEnd('/') + "/services/Soap/c/" + SOAP_API_VERSION;

                return new Generic() { scode = oToken.access_token, sname = sSoapUrl };
            }
            catch (WebException wex)
            {
                // Salesforce devuelve el detalle del error OAuth en el cuerpo de la
                // respuesta (400/401), leerlo para que quede en el log
                string sError = wex.Message;
                try
                {
                    if (wex.Response != null)
                    {
                        using (StreamReader oReader = new StreamReader(wex.Response.GetResponseStream()))
                        {
                            string sDetail = oReader.ReadToEnd();
                            OAuthTokenResponse oError = this.DeserializeToken(sDetail);
                            if (oError != null && !string.IsNullOrEmpty(oError.error))
                            {
                                sError = oError.error + ": " + oError.error_description;
                            }
                            else
                            {
                                sError = sDetail;
                            }
                        }
                    }
                }
                catch { /* si no se puede leer el cuerpo, se registra el mensaje original */ }
                LogError.WriteError("Application", "WSInspira", new ApplicationException("Error OAuth login: " + sError, wex));
                return null;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return null;
            }
            finally
            {
                oRequest = null;
            }
        }

        /// <summary>
        /// Sobrecarga de compatibilidad con la firma anterior
        /// Login(sOrganization, sUser, sPassword, sToken) marcada como obsoleta,
        /// para que los desarrollos que aún no migren compilen con advertencia.
        /// </summary>
        [Obsolete("SOAP API login() será retirado por Salesforce (Summer '27). Use Login(sClientId, sClientSecret, sLoginUrl) con OAuth 2.0 Client Credentials.", false)]
        public Generic Login(string sOrganization, string sUser, string sPassword, string sToken)
        {
            throw new NotSupportedException("El login por usuario/contraseña fue reemplazado por OAuth 2.0. Use Login(clientId, clientSecret, loginUrl).");
        }

        /// <summary>
        /// Método para deserializar la respuesta JSON del token sin dependencias
        /// externas (usa System.Runtime.Serialization.Json, incluido en .NET Framework)
        /// </summary>
        private OAuthTokenResponse DeserializeToken(string sJson)
        {
            if (string.IsNullOrEmpty(sJson)) return null;
            using (MemoryStream oStream = new MemoryStream(Encoding.UTF8.GetBytes(sJson)))
            {
                DataContractJsonSerializer oSerializer = new DataContractJsonSerializer(typeof(OAuthTokenResponse));
                return (OAuthTokenResponse)oSerializer.ReadObject(oStream);
            }
        }
    }
}