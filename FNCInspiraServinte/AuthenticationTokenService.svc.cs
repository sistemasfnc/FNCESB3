using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;
using System.Web;
using EventLog;
using FNCEntity;
using Newtonsoft.Json;

namespace FNCInspiraServinte
{
    [ServiceContract(Namespace = "")]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class AuthenticationTokenService
    {
        // Para usar HTTP GET, agregue el atributo [WebGet]. (El valor predeterminado de ResponseFormat es WebMessageFormat.Json)
        // Para crear una operación que devuelva XML,
        //     agregue [WebGet(ResponseFormat=WebMessageFormat.Xml)]
        //     e incluya la siguiente línea en el cuerpo de la operación:
        //         WebOperationContext.Current.OutgoingResponse.ContentType = "text/xml";
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        [OperationContract]
        public string Authenticate(string suser, string spassword)
        {
            InspiraServinte inspiraServinte = new InspiraServinte();
            try
            {
                string sRequest = HttpUtility.UrlDecode(suser);
                InspiraServinteResponse response = inspiraServinte.GenerateEntry(sRequest, spassword);
                string jsonResponse = JsonConvert.SerializeObject(response);
                return jsonResponse;
            }
            catch (Exception ex)
            {
                LogError.WriteError("InspiraServinte", "InspiraServinte", ex);
                throw;
            }
            //return "oAAA0";
            /*Credentials creds = new Credentials()
            {
                User = suser,
                Password = spassword,
            };
            ICredentials validator = new CredentialsValidator();
            if (validator.IsValid(creds))
                return new TokenBuilder().Build(creds);
            throw new InvalidCredentialException("Credenciales Incorrectas");*/
        }       
    }
}
