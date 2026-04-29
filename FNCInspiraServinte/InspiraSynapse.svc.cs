using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Web.Script.Serialization;
using FNCEntity;
using FNCFacade;
using System.Security.Authentication;
using System.ServiceModel.Activation;
using EventLog;
using FNCUtils;
using FNCSalesforce.Sfdc;
using FNCSalesforce;

namespace FNCInspiraServinte
{
    // NOTA: puede usar el comando "Rename" del menú "Refactorizar" para cambiar el nombre de clase "InspiraSynapse" en el código, en svc y en el archivo de configuración a la vez.
    // NOTA: para iniciar el Cliente de prueba WCF para probar este servicio, seleccione InspiraSynapse.svc o InspiraSynapse.svc.cs en el Explorador de soluciones e inicie la depuración.
    [ServiceContract(Namespace = "")]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class InspiraSynapse
    {
        private Generic oSession { get; set; }        

        [OperationContract]
        [WebInvoke(Method = "POST", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.WrappedRequest)]
        public string SendAppointment(string sRequest)
        {
            SynapseEntity synapseEntity = null;
            bool task = false;
            try
            {
                var oSerializer = new JavaScriptSerializer();
                synapseEntity = oSerializer.Deserialize<SynapseEntity>(sRequest);
                task = Tools.PostJson(ConfigurationManager.AppSettings["SynapseWSURL"].ToString(), sRequest);
                if (task)
                {
                    return string.Empty;
                    //return null;
                }
                else 
                {
                    return "No se ha podido consumir el servicio";
                    /*return new InspiraServinteResponse()
                    {
                        error = new ErrorResponse()
                        {
                            icode = 0,
                            smessage = "No se ha podido consumit el servicio",
                        }
                    };*/
                }
            }
            catch (Exception ex)
            {
                return "Error de servicio " + ex.Message;
                /*return new InspiraServinteResponse()
                {
                    error = new ErrorResponse()
                    {
                        icode = 0,
                        smessage = ex.Message,
                    }
                };*/
            }
            

        }
        [OperationContract]
        [WebInvoke(Method = "POST", 
            RequestFormat = WebMessageFormat.Json, 
            ResponseFormat = WebMessageFormat.Json, 
            BodyStyle = WebMessageBodyStyle.WrappedRequest           
        )]
        public string UpdateAppointment(string sid, string sprofessional, string surlreport, string surlvideo)
        {
            SalesforceViaRestApi salesforceViaRestApi = new SalesforceViaRestApi();
            string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
            string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
            string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
            string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();
            salesforceViaRestApi.DoLogin(susr, spwd, sclientid, ssectet);
            this.oSession = salesforceViaRestApi.salesforceSession;
            if (this.oSession != null) 
            {
                salesforceViaRestApi.salesforceSession = this.oSession;
                salesforceViaRestApi.UpdateAppointmentUrl(sid, surlreport, surlvideo);
            }
            return string.Empty;
        }
    }
}
