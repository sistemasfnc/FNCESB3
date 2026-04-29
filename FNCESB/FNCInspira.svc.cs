using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Xml;
using FNCEntity;
using FNCDigiturno;
using System.Configuration;
using FNCUtils;
using System.Xml.Linq;
using FNCESB;

namespace FNCESB
{
    // NOTA: puede usar el comando "Rename" del menú "Refactorizar" para cambiar el nombre de clase "Service1" en el código, en svc y en el archivo de configuración.
    // NOTE: para iniciar el Cliente de prueba WCF para probar este servicio, seleccione Service1.svc o Service1.svc.cs en el Explorador de soluciones e inicie la depuración.
    public class FNCInspira : IFNCInspira
    {
        public XmlElement GenerateTurn(XmlElement oRequest)
        {            
            XElement xElement = this.GetXElement(oRequest);
            TurnResult oResult = null;
            string User = this.ReadXmlValue(xElement, "USERINFO", "USERNAME");
            string Password = this.ReadXmlValue(xElement, "USERINFO", "TOKEN");
            if (this.ValidateUser(User, Password))
            {
                oResult = this.SendTurnData(xElement);                  
            }
            else
            {

            }
            return null;
        }

        private TurnResult SendTurnData(XElement xElement)
        {            
            ESBDigiturno oDigiturno = new ESBDigiturno();
            string PatientDocument, PatientID;
            StringBuilder PatientName = new StringBuilder();
            try
            {
                PatientName.Append(this.ReadXmlValue(xElement, "PATIENT", "FIRSTNAME"));
                PatientName.Append(" ");
                PatientName.Append(this.ReadXmlValue(xElement, "PATIENT", "SURNAME"));
                PatientDocument = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTNUMBER");
                PatientID = this.ReadXmlValue(xElement, "PATIENT", "UNIQUEID");
                return oDigiturno.GenerateTurn(this.GetConnectionEntity(), this.GetTurnEntity(PatientDocument, PatientName.ToString(), PatientID));                
            }
            catch (Exception)
            {
                
                throw;
            }

        }

        private bool ValidateUser(string User, string Password)
        {
            return (User == ConfigurationManager.AppSettings["ServiceUser"] && Password == Tools.SHA256Crypt(ConfigurationManager.AppSettings["ServicePassword"]));
        }

        private string ReadXmlValue(XElement xElement, string Parent, string Tag)
        {
            return xElement.Element(Parent).Element(Tag).Value;            
        }

        private XElement GetXElement(XmlElement oRequest)
        {
            return XElement.Parse(oRequest.InnerText);
        }

        private string GetResponse(TurnResult oResult)
        {
            StringBuilder sResponse = new StringBuilder(Tools.GetResponseHeader());
            sResponse.Append(Tools.GetResponseFooter());
            return sResponse.ToString();
        }

        private DigiturnoConnection GetConnectionEntity()
        {
            return new DigiturnoConnection()
            {
                HostServidor = ConfigurationManager.AppSettings["HostServidor"],
                Puerto = ConfigurationManager.AppSettings["Puerto"],
                ClaveUsuario = ConfigurationManager.AppSettings["ClaveUsuario"],
                CodigoSelector = ConfigurationManager.AppSettings["CodigoSelector"],
                CodigoUsuario = ConfigurationManager.AppSettings["CodigoUsuario"],
            };
        }

        private TurnoEntity GetTurnEntity(string PatientDocument, string PatientName, string PatiendID)
        {
            return new TurnoEntity()
            {
                patientcode = PatientDocument,
                patientname = PatientName,
                patientid = PatiendID,
            };
        }
    }
}
