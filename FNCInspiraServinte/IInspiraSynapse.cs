using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Web.Script.Services;
using FNCEntity;

namespace FNCInspiraServinte
{
    // NOTA: puede usar el comando "Rename" del menú "Refactorizar" para cambiar el nombre de interfaz "IInspiraSynapse" en el código y en el archivo de configuración a la vez.
    [ServiceContract(Namespace = "")]
    public interface IInspiraSynapse
    {
        [OperationContract]
        [WebGet(BodyStyle = WebMessageBodyStyle.Bare,
        RequestFormat = WebMessageFormat.Json,
        ResponseFormat = WebMessageFormat.Json,
        UriTemplate = "/SendAppointment?sRequest={sRequest}")]
        InspiraServinteResponse SendAppointment(string sRequest);

        [OperationContract]
        [WebGet(BodyStyle = WebMessageBodyStyle.Bare,
        RequestFormat = WebMessageFormat.Json,
        ResponseFormat = WebMessageFormat.Json,
        UriTemplate = "/UpdateAppointment?sid={sid}&sprofessional={sprofessional}&surlreport={surlreport}&surlvideo={surlvideo}")]
        string UpdateAppointment(string sid, string sprofessional, string surlreport, string surlvideo);
    }
}
