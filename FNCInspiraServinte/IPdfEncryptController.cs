using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;

namespace FNCInspiraServinte
{
    // NOTA: puede usar el comando "Rename" del menú "Refactorizar" para cambiar el nombre de interfaz "IPdfEncryptController" en el código y en el archivo de configuración a la vez.
    [ServiceContract]
    public interface IPdfEncryptController
    {
        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Wrapped)]
        string EncryptPdf(string pdfBase64, string password, bool ishistory);

        [OperationContract]
        [WebInvoke(Method = "POST", UriTemplate = "/EncryptPdfBlob", RequestFormat = WebMessageFormat.Json, ResponseFormat = WebMessageFormat.Json)]
        Stream EncryptPdfBlob(Stream pdfStream);
    }
}
