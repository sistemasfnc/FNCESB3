using EventLog;
using FNCEntity;
using FNCFacade;
using FNCFacade.FNCESB;
using FNCUtils;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Authentication;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.ServiceModel.Web;
using System.Text;
using System.Web.Script.Serialization;

namespace FNCInspiraServinte
{
    // NOTA: puede usar el comando "Rename" del menú "Refactorizar" para cambiar el nombre de clase "Service1" en el código, en svc y en el archivo de configuración.
    // NOTE: para iniciar el Cliente de prueba WCF para probar este servicio, seleccione Service1.svc o Service1.svc.cs en el Explorador de soluciones e inicie la depuración.
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class InspiraServinte : IInspiraServinte    
    {
        public InspiraServinteResponse GenerateEntries(string sRequest, string sToken)
        {
            return this.GenerateEntry(sRequest, sToken);
        }
        
        /// <summary>
        /// Método para obtener los tipos de usuario válidos para una empresa y un plan
        /// </summary>
        /// <returns>Lista genérica con los usaurios por empresa y plan</returns>
        private List<FNCEntity.InspiraTemporal> GetUserTypes()
        {
            using (FacadeInspiraServinte facade = new FacadeInspiraServinte())
            {
                facade.sConnection = ConfigurationManager.ConnectionStrings["IntegraBus"].ConnectionString;
                return facade.GetUserTypes();
            }
        }

        private List<FNCEntity.InspiraCita> GetTodayCharges()
        {
            using (FacadeInspiraServinte facade = new FacadeInspiraServinte())
            {
                facade.sConnection = ConfigurationManager.ConnectionStrings["IntegraBus"].ConnectionString;
                return facade.GetTodayCharges();
            }
        }

        /// <summary>
        /// Método para obtener las autorizaciones de los pacientes
        /// </summary>
        /// <returns>Lista genérica con todas las autorizaciones utilizadas</returns>
        private List<FNCEntity.InspiraTemporal> GetAuthorizations()
        {
            using (FacadeInspiraServinte facade = new FacadeInspiraServinte())
            {
                facade.sConnection = ConfigurationManager.ConnectionStrings["IntegraBus"].ConnectionString;
                return facade.GetAuthorizations();
            }
        }

        
        /// <summary>
        /// Método para validar la información recibida en el JSON
        /// </summary>
        /// <param name="inspiraRequest">Objeto integración</param>
        /// <returns>Objeto de respuesta de error en caso de validación no exitosa</returns>
        private ErrorResponse ValidateJSON(InspiraRequest inspiraRequest)
        {
            //List<InspiraCita> linspiraCitas = this.GetTodayCharges();
            List<FNCEntity.InspiraTemporal> inspiraTemporals = this.GetUserTypes();
            //List<InspiraTemporal> inspiraAuth = this.GetAuthorizations();
            using (FacadeInspiraServinte facade = new FacadeInspiraServinte(ConfigurationManager.ConnectionStrings["ServinteFNC"].ConnectionString))
            {
                foreach (FNCEntity.ServintePatient servintePatient in inspiraRequest.lpatients)
                {
                    if (string.IsNullOrEmpty(servintePatient.sfirstname))
                    {
                        return new ErrorResponse()
                        {
                            icode = 10,
                            smessage = "El primer nombre no puede ser vacío",
                        };
                    }
                    else if (string.IsNullOrEmpty(servintePatient.ssurname))
                    {
                        return new ErrorResponse()
                        {
                            icode = 11,
                            smessage = "El primer apellido no puede ser vacío",
                        };
                    }
                    else if (!servintePatient.dbirthdate.HasValue)
                    {
                        return new ErrorResponse()
                        {
                            icode = 12,
                            smessage = "La fecha de nacimiento puede ser vacía",
                        };
                    }
                    else if (string.IsNullOrEmpty(servintePatient.scity))
                    {
                        return new ErrorResponse()
                        {
                            icode = 13,
                            smessage = "La ciudad no puede vacía",
                        };
                    }
                    else if (string.IsNullOrEmpty(servintePatient.sbornplace))
                    {
                        return new ErrorResponse()
                        {
                            icode = 14,
                            smessage = "El lugar de nacimiento no puede ser vacío",
                        };
                    }
                    else if (string.IsNullOrEmpty(servintePatient.safiliation))
                    {
                        return new ErrorResponse()
                        {
                            icode = 20,
                            smessage = "El tipo de afiliación no puede ser vacío",
                        };
                    }
                    else if (!servintePatient.surbanzone.EqualsAnyOf("U", "R"))
                    {
                        return new ErrorResponse()
                        {
                            icode = 27,
                            smessage = "La zona urbana es incorrecta",
                        };
                    }
                    else
                    {
                        foreach (FNCEntity.InspiraCita inspiraCita in servintePatient.lappointments)
                        {
                            if (string.IsNullOrEmpty(inspiraCita.sagreement))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 15,
                                    smessage = "El código del convenio no puede ser vacío",
                                };
                            }
                            else if (string.IsNullOrEmpty(inspiraCita.splan))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 16,
                                    smessage = "El plan no puede ser vacío",
                                };
                            }
                            else if (string.IsNullOrEmpty(inspiraCita.srate))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 17,
                                    smessage = "El código de la tarifa no puede ser vacío",
                                };
                            }
                            else if (string.IsNullOrEmpty(inspiraCita.sagreementname))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 18,
                                    smessage = "El nombre del convenio no puede ser vacío",
                                };
                            }
                            else if (string.IsNullOrEmpty(inspiraCita.sservicetype))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 19,
                                    smessage = "El código del tipo de servicio no puede ser vacío",
                                };
                            }
                            else if (servintePatient.safiliation.EqualsAnyOf("P", "7", "9") && !servintePatient.slevel.EqualsAnyOf("7", "8"))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 21,
                                    smessage = "La combinación de tipo de afiliación y nivel del paciente no coinciden para la empresa y plan",
                                };
                            }
                            else if (servintePatient.safiliation.EqualsAnyOf("P", "7", "9") && !inspiraCita.sagreementname.Contains("NEUMOL"))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 29,
                                    smessage = "La afiliación particular solo está habilitada para el convenio Fundación Neumológica",
                                };
                            }
                            else if (!servintePatient.safiliation.EqualsAnyOf("P", "7", "9") && inspiraTemporals.FirstOrDefault(x => x.scod == inspiraCita.sagreement && x.snombre == inspiraCita.splan && x.sparametro1 == servintePatient.safiliation && x.sparametro2 == servintePatient.slevel) == null)
                            {
                                return new ErrorResponse()
                                {
                                    icode = 21,
                                    smessage = "La combinación de tipo de afiliación y nivel del paciente no coinciden para la empresa y plan",
                                };
                            }
                            else if (string.IsNullOrEmpty(inspiraCita.sunit))
                            {
                                return new ErrorResponse()
                                {
                                    icode = 23,
                                    smessage = "El código de la unidad funcional no puede ser vacío",
                                };
                            }
                            else if (inspiraRequest.stype == "Servicio" && Tools.GetAge(servintePatient.dbirthdate.Value) < 18 && inspiraCita.sunit != "1200")
                            {
                                return new ErrorResponse()
                                {
                                    icode = 25,
                                    smessage = "La unidad funcional no corresponde a la edad del paciente",
                                };
                            }
                            else if (inspiraRequest.stype == "Servicio" && Tools.GetAge(servintePatient.dbirthdate.Value) >= 18 && inspiraCita.sunit != "1100")
                            {
                                return new ErrorResponse()
                                {
                                    icode = 25,
                                    smessage = "La unidad funcional no corresponde a la edad del paciente",
                                };
                            }                            
                            else if (inspiraCita.ddate.Month != DateTime.Now.Month)
                            {
                                return new ErrorResponse()
                                {
                                    icode = 30,
                                    smessage = "El mes del cargo no puede ser diferente del mes actual",
                                };
                            }
                            else
                            {
                                int iservices = 0;
                                foreach (FNCEntity.ServiceRequest serviceRequest in inspiraCita.lservices)
                                {
                                    /*if (!inspiraRequest.bentryassociate && facade.SpecificAuthorizationExists(Tools.GetDocumentType(servintePatient.sdocumenttype), servintePatient.sdocument, inspiraCita.splan, inspiraCita.sauthorization, serviceRequest.sservice))
                                    {
                                        return new ErrorResponse()
                                        {
                                            icode = 27,
                                            smessage = "La autorización ya fue usada para el paciente",
                                        };
                                    }*/
                                    if (string.IsNullOrEmpty(serviceRequest.sconcept))
                                    {
                                        return new ErrorResponse()
                                        {
                                            icode = 21,
                                            smessage = "El concepto no puede ser vacío",
                                        };
                                    }
                                    else if (string.IsNullOrEmpty(serviceRequest.scostcenter))
                                    {
                                        return new ErrorResponse()
                                        {
                                            icode = 22,
                                            smessage = "El centro de costos no puede ser vacío",
                                        };
                                    }
                                    else if (string.IsNullOrEmpty(serviceRequest.sservice))
                                    {
                                        return new ErrorResponse()
                                        {
                                            icode = 24,
                                            smessage = "El código del servicio no puede ser vacío",
                                        };
                                    }
                                    else if (string.IsNullOrEmpty(serviceRequest.sservicename))
                                    {
                                        return new ErrorResponse()
                                        {
                                            icode = 26,
                                            smessage = "El nombre del servicio no puede ser vacío",
                                        };
                                    }
                                    else
                                    {
                                        if (facade.SpecificEntryExists(servintePatient, inspiraCita, serviceRequest))
                                        {
                                            iservices++;
                                        }
                                    }
                                }
                                if (iservices == inspiraCita.lservices.Count && iservices > 0)
                                {
                                    return new ErrorResponse()
                                    {
                                        icode = 98,
                                        smessage = "El ingreso ya existe para todos los servicios solicitados.",
                                    };
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }
        
        
        /// <summary>
        /// Método para generar el ingreso en Servinte
        /// </summary>
        /// <param name="sRequest">String Json con el objeto de integración</param>
        /// <param name="sToken">String Token de validación de acceso</param>
        /// <returns></returns>
        public InspiraServinteResponse GenerateEntry(string sRequest, string sToken)
        {
            sRequest = sRequest.Replace("_", " ");
            LogError.WriteMessage("InspiraServinte", "Aplicacion", sRequest);
            ITokenValidator validator = new TokenValidator();
            ErrorResponse errorResponse = null;
            //if (validator.IsValid(sToken))
            InspiraRequest inspiraRequest = null;
            try
            {
                inspiraRequest = this.Json2Object(sRequest, 1) as InspiraRequest;
                errorResponse = this.ValidateJSON(inspiraRequest);
            }
            catch (Exception ex)
            {
                LogError.WriteError("InspiraServinte", "InspiraServinte", ex);
                return new InspiraServinteResponse()
                {
                    error = new ErrorResponse()
                    {
                        icode = 99,
                        smessage = Tools.ReplaceChars(ex.Message),
                    }
                };
            }
            if (errorResponse == null)
            {
                using (FacadeInspiraServinte facadeInspiraServinte = new FacadeInspiraServinte(ConfigurationManager.ConnectionStrings["ServinteFNC"].ConnectionString))
                {
                    facadeInspiraServinte.sConnection2 = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                    //return null;
                    return facadeInspiraServinte.GenerateEntry(inspiraRequest);
                }
            }
            else
            {
                return new InspiraServinteResponse()
                {
                    error = errorResponse,
                };
            }
        }

        /// <summary>
        /// Método para actualizar un ingreso desde Inspira
        /// </summary>
        /// <param name="sRequest">String JSON que contiene el objeto de integración</param>
        /// <param name="sToken">String token de seguridad</param>
        /// <returns>Objeto genérico de respuesta de transacción</returns>
        public InspiraServinteResponse UpdateEntry(string sRequest, string sToken)
        {
            sRequest = sRequest.Replace("_", " ");
            InspiraRequest inspiraRequest = null;
            FacadeInspiraServinte facadeInspiraServinte = new FacadeInspiraServinte(ConfigurationManager.ConnectionStrings["ServinteFNC"].ConnectionString);
            try
            {
                inspiraRequest = this.Json2Object(sRequest, 1) as InspiraRequest;
                facadeInspiraServinte.sConnection2 = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                return facadeInspiraServinte.Update(inspiraRequest, true);
            }
            catch (Exception ex)
            {
                LogError.WriteError("InspiraServinte", "InspiraServinte", ex);
                return new InspiraServinteResponse()
                {
                    error = new ErrorResponse()
                    {
                        icode = 99,
                        smessage = Tools.ReplaceChars(ex.Message),
                    }
                };
            }
            finally
            {
                facadeInspiraServinte.Dispose();
                facadeInspiraServinte = null;
                inspiraRequest = null;
            }
        }

        /// <summary>
        /// Método que convierte una cadena de caracteres JSON a un objeto .Net
        /// </summary>
        /// <param name="sJson"></param>
        /// <param name="icase"></param>
        /// <returns></returns>
        private object Json2Object(string sJson, int icase)
        {
            //var utf8reader = new System.Text.json
            var oSerializer = new JavaScriptSerializer();
            if (icase == 1)
                return oSerializer.Deserialize<InspiraRequest>(sJson);            
            else if (icase == 2)
                return oSerializer.Deserialize<List<InspiraRequest>>(sJson);
            else
                return oSerializer.Deserialize<InspiraRequest>(sJson);
        }
    }
}
