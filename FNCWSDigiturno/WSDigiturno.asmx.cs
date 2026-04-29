using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Services;
using FNCEntity;
using System.Configuration;
using FNCUtils;
using System.Xml.Linq;
using System.Xml;
using System.Text;
using FNCDAC;
using EventLog;
using FNCSalesforce;
using System.Web.Script.Serialization;
using FNCSalesforce.Digiturno5WS;
using System.Security.Cryptography;
using FNCWSDigiturno.Digiturno5WS;
using FNCSalesforce.Sfdc;

namespace FNCWSDigiturno
{
    /// <summary>
    /// Descripción breve de WSDigiturno
    /// </summary>
    [WebService(Namespace = "http://190.144.73.250:9090/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    // Para permitir que se llame a este servicio web desde un script, usando ASP.NET AJAX, quite la marca de comentario de la línea siguiente. 
    // [System.Web.Script.Services.ScriptService]
    public class WSDigiturno : System.Web.Services.WebService
    {

        /// <summary>
        /// Almacena la sesión de Salesforce
        /// </summary>
        private Generic oSession
        {
            get
            {
                return (Session["oSession"] == null) ? new Generic() : Session["oSession"] as Generic;
            }
            set
            {

                Session["oSession"] = value;
            }
        }

        #region Web Services
        /// <summary>
        /// Método web para la generación de un turno o ingreso
        /// </summary>
        /// <param name="oRequest">String que contiene un XML con la información de la cita para la generación del turno o del ingreso</param>
        /// <returns></returns>
        [WebMethod(EnableSession = true)]
        public string GenerateTurn(string oRequest)
        {
            LogError.WriteMessage("Application", "WSInspira", "Este es el XML: " + oRequest);
            XElement xElement = this.GetXElement(oRequest);
            TurnResult oResult = new TurnResult() { errorcode = "06", errordescription = "El turno ya se ha generado para esta cita" };
            string User = this.ReadXmlValue(xElement, "USERINFO", "USERNAME");
            string Password = this.ReadXmlValue(xElement, "USERINFO", "TOKEN");
            string sDate = this.ReadXmlValue(xElement, "APPOINTMENT", "DATE", false);
            bool bAttended = Convert.ToBoolean(this.ReadXmlValue(xElement, "APPOINTMENT", "STATUS", false));
            bool bConsent = false;            
            if (this.ReadXmlValue(xElement, "APPOINTMENT", "CONSENT", false) != null)
            {
                bConsent = Convert.ToBoolean(this.ReadXmlValue(xElement, "APPOINTMENT", "CONSENT", false));
                if (bConsent)
                {
                    try
                    {
                        if (this.ReadXmlValue(xElement, "APPOINTMENT", "USERDOCUMENT", false) == "null" || (this.ReadXmlValue(xElement, "APPOINTMENT", "USERDOCUMENT", false) == null))
                        {
                            LogError.WriteMessage("Application", "WSInspira", "Entra al consentimiento: " + xElement.ToString());
                            this.CreateConsent(xElement);
                        }
                        else
                        {
                            string suser = this.ReadXmlValue(xElement, "APPOINTMENT", "USERDOCUMENT", false);
                            string sappointment = this.ReadXmlValue(xElement, "APPOINTMENT", "CODE", false);
                            this.UpdateConsentProfessional(sappointment, suser);
                        }
                        oResult.errorcode = "00";
                        oResult.errordescription = string.Empty;
                    }
                    catch (Exception ex)
                    {
                        oResult.errorcode = "20";
                        oResult.errordescription = ex.Message;
                        LogError.WriteError("Application", "WSInspira", ex);
                    }
                    return this.GetResponse(oResult, false);
                }
            }            
            if (this.ReadXmlValue(xElement, "APPOINTMENT", "SYNAPSE", false) != null)
            {
                SalesforceViaRestApi salesforceViaRestApi = new SalesforceViaRestApi();
                try
                {
                    salesforceViaRestApi.sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString();
                    salesforceViaRestApi.sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString();
                    string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
                    string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
                    string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
                    string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();
                    salesforceViaRestApi.DoLogin(susr, spwd, sclientid, ssectet);
                    this.oSession = salesforceViaRestApi.salesforceSession;
                    if (this.oSession != null) 
                    {
                        Account account = new Account()
                        {
                            DocumentNumber__c = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTNUMBER").ToUpper(),
                            DocumentType__c = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTTYPE"),
                            PersonBirthdate  = Convert.ToDateTime(this.ReadXmlValue(xElement, "PATIENT", "BIRTHDATE")),
                            FirstName_c__pc = this.ReadXmlValue(xElement, "PATIENT", "FIRSTNAME").ToUpper(),
                            SecondName__pc = this.ReadXmlValue(xElement, "PATIENT", "MIDDLENAME"),
                            SecondSurname__pc = this.ReadXmlValue(xElement, "PATIENT", "SECONDSURNAME"),
                            FirstSurname__pc = this.ReadXmlValue(xElement, "PATIENT", "SURNAME"),
                            Gender__pc = this.ReadXmlValue(xElement, "PATIENT", "GENDER"),
                            PersonEmail = this.ReadXmlValue(xElement, "PATIENT", "EMAIL"),
                            Address__c = this.ReadXmlValue(xElement, "PATIENT", "ADDRESS"),
                            Phone = this.ReadXmlValue(xElement, "PATIENT", "PHONE"),
                            City__r = new Location__c()
                            {
                                Code__c = (this.ReadXmlValue(xElement, "PATIENT", "CITY") != null) ? this.ReadXmlValue(xElement, "PATIENT", "CITY") : string.Empty,
                                Name = (this.ReadXmlValue(xElement, "PATIENT", "CITYNAME") != null) ? this.ReadXmlValue(xElement, "PATIENT", "CITYNAME") : string.Empty,
                            },
                            State__r = new Location__c()
                            {
                                Code__c = (this.ReadXmlValue(xElement, "PATIENT", "STATE") != null) ? this.ReadXmlValue(xElement, "PATIENT", "STATE") : string.Empty,
                                Name = (this.ReadXmlValue(xElement, "PATIENT", "STATENAME") != null) ? this.ReadXmlValue(xElement, "PATIENT", "STATENAME") : string.Empty,
                            },
                        };
                        Appointment__c appointment = new Appointment__c()
                        {
                            Name = this.ReadXmlValue(xElement, "APPOINTMENT", "CODE", false),                            
                            WhatId__r = account,
                            AgreementId__r = new Agreement__c() 
                            { 
                                Code__c = this.ReadXmlValue(xElement, "PATIENT", "AGREEMENTCODE"),
                                Name = this.ReadXmlValue(xElement, "PATIENT", "AGREEMENTNAME"),
                            },
                            PlanId__r = new Plan__c() 
                            { 
                                Name = this.ReadXmlValue(xElement, "PATIENT", "PLANNAME"),
                                RateId__c = this.ReadXmlValue(xElement, "PATIENT", "RATEID"),
                            },
                            PlanId__c = this.ReadXmlValue(xElement, "PATIENT", "PLANID"),
                            PlanName__c = this.ReadXmlValue(xElement, "PATIENT", "PLANNAME"),
                            GroupId__c = this.ReadXmlValue(xElement, "PATIENT", "GROUPID"),
                            CostCenterId__r = new CostCenter__c()
                            {
                                Code__c = this.ReadXmlValue(xElement, "APPOINTMENT", "COSTCENTER", false),
                            },                            
                            ins2_categoria__c = this.ReadXmlValue(xElement, "PATIENT", "CATEGORYID"),
                        };                        
                        salesforceViaRestApi.SendAppointmentToSynapse(appointment);
                    }
                    oResult.errorcode = "00";
                    oResult.errordescription = string.Empty;
                }
                catch (Exception ex)
                {
                    oResult.errorcode = "20";
                    oResult.errordescription = ex.Message;
                    LogError.WriteError("Application", "WSInspira", ex);
                }
                return this.GetResponse(oResult, false);
            }    
            if (DateTime.Now.DayOfYear != Convert.ToDateTime(sDate).DayOfYear)
            {
                oResult = new TurnResult() { errorcode = "07", errordescription = "No se pueden asignar turnos en fechas diferentes a la actual" };
                return this.GetResponse(oResult);
            }            
            else if (!this.ValidateUser(User, Password.ToUpper()))
            {
                oResult = new TurnResult() { errorcode = "01", errordescription = "Usuario o contraseña incorrectos" };
                return this.GetResponse(oResult);
            }            
            else
            {
                string sStartHour = this.ReadXmlValue(xElement, "APPOINTMENT", "STARTHOUR", false);
                string sEndHour = this.ReadXmlValue(xElement, "APPOINTMENT", "ENDHOUR", false);
                if (!bAttended)
                {
                    oResult = this.SendTurnData(xElement);
                    this.CreateConsent(xElement);
                    return this.GetResponse(oResult);
                }
                else
                {
                    if (!Tools.IsSpecial(this.ReadXmlValue(xElement, "PATIENT", "PLANNAME"), this.ReadXmlValue(xElement, "PATIENT", "GROUP")))
                    {
                        oResult.errorcode = string.Empty;
                        oResult.errordescription = string.Empty;
                        try
                        {
                            oResult.chargenumber = this.GenerateCharge(xElement);
                        }
                        catch (Exception ex)
                        {
                            oResult.errorcode = "09";
                            oResult.errordescription = ex.Message;
                            LogError.WriteError("Application", "WSInspira", ex);
                        }
                        return this.GetResponse(oResult, false);
                    }
                    else
                    {
                        oResult.errorcode = "07";
                        oResult.errordescription = "La cita corresponde a programa especial por lo tanto no genera ingreso";
                        return this.GetResponse(oResult, false);
                    }
                }
            }
        }

        /// <summary>
        /// Método que valida contra salesforce si un paciente existe y si tiene citas médicas en el día este método es el que utiliza la máquina de turnos
        /// </summary>
        /// <param name="sdocumenttype">String tipo de documento del paciente</param>
        /// <param name="sdocument">String documento del paciente</param>
        /// <param name="suser">String nombre usuario acceso al servicio</param>
        /// <param name="spassword">String contraseña usuario acceso al servicio</param>
        /// <returns>Objeto Digiturno 5</returns>
        [WebMethod(EnableSession = true)]
        public Digiturno5 GetPatient(string sdocumenttype, string sdocument, string suser, string spassword)
        {
            Digiturno5 oDigiturno = new Digiturno5();
            if (!this.ValidateUser(suser, spassword.ToUpper()))
            {
                oDigiturno.oResult = new Result() { iresult = 0, smessage = "Error de inicio de sesión, favor solicita su turno directamente en la caja" };
            }
            else
            {
                sdocumenttype = FNCUtils.Tools.GetDocumentType(sdocumenttype, false);
                SalesforceViaRestApi salesforceViaRestApi = new SalesforceViaRestApi()
                {
                    sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString(),
                    sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString(),
                };
                try
                {
                    if (string.IsNullOrEmpty(this.oSession.scode))
                    {
                        string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
                        string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
                        string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
                        string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();
                        salesforceViaRestApi.DoLogin(susr, spwd, sclientid, ssectet);
                        this.oSession = salesforceViaRestApi.salesforceSession;
                    }
                    if (this.oSession != null)
                    {
                        salesforceViaRestApi.salesforceSession = this.oSession;
                        oDigiturno = salesforceViaRestApi.GetPatientAsync(sdocumenttype, sdocument, ConfigurationManager.ConnectionStrings["ConsentimientoOracle"].ConnectionString);                      
                    }
                }
                catch (Exception ex)
                {
                    LogError.WriteError("Application", "WSInspira", ex);
                    oDigiturno.oResult = new Result() { iresult = 0, smessage = "Error de comunicaciones con el servicio web de integración" };
                }

                /*
                LogError.WriteMessage("Application", "WSInspira", "Datos que llegan: " + sdocument);
                SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();                
                this.DoLogin();
                
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    LogError.WriteMessage("Application", "WSInspira", "Conexión con Salesforce");
                    oDigiturno = salesforceIntegrator.GetPatient(this.oSession.scode, sdocumenttype, sdocument, this.oSession.sname, ConfigurationManager.ConnectionStrings["ConsentimientoInformado"].ConnectionString);                   
                }
                else
                {
                    oDigiturno.oResult = new Result() { iresult = 0, smessage = "Error de comunicaciones, favor solicita su turno directamente en la caja" };
                }
                salesforceIntegrator.Dispose();
                salesforceIntegrator = null;
                */
            }
            return oDigiturno;
        }

        [WebMethod(EnableSession = true)]
        public string GetPatientAppointments(string sdocumenttype, string sdocument, string suser, string spassword)
        {
            StringBuilder sresponse = new StringBuilder("<respuesta><citas>");
            string serror = string.Empty;
            string sappointments = string.Empty;
            if (!this.ValidateUser(suser, spassword.ToUpper()))
            {
                serror = "Nombre de usuario o contraseña incorrectos";
            }
            else
            {
                SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
                this.DoLogin();
                sdocumenttype = FNCUtils.Tools.GetDocumentType(sdocumenttype, false);
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    try
                    {                        
                        sappointments = salesforceIntegrator.GetPatientAppointments(Convert.ToInt32(ConfigurationManager.AppSettings["AppointmentDays"]), sdocument, sdocumenttype);
                        sresponse.Append(sappointments);
                    }
                    catch (Exception ex)
                    {
                        serror = ex.Message;
                    }                    
                }
                else
                {
                    serror = "Error de comunicación con Salesforce, credenciales incorrectas";
                }
            }
            sresponse.Append("</citas>");
            sresponse.Append("<error>");
            sresponse.Append(serror);
            sresponse.Append("</error>");
            sresponse.Append("</respuesta>");
            return sresponse.ToString();
        }

        [WebMethod(EnableSession = true)]
        public string UpdatePaymentAppointment(string scode, string sidappointment, string svalue, string suser, string spassword)
        {
            if (!this.ValidateUser(suser, spassword.ToUpper()))
            {
                return "Nombre de usuario o contraseña incorrectos";
            }
            else
            {
                SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    return salesforceIntegrator.UpdatePaymentAppointment(scode, sidappointment, svalue);
                }
                return string.Empty;
            }            
        }

        [WebMethod(EnableSession = true)]
        public List<ServintePatient> GetAppointmentsForPrograms(int iyear, int imonth)
        {            
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            this.DoLogin();
            if (!string.IsNullOrEmpty(this.oSession.scode))
            {
                salesforceIntegrator.sSession = this.oSession.scode;
                salesforceIntegrator.sUrl = this.oSession.sname;
                return salesforceIntegrator.GetAppointmentsForPrograms(iyear.ToString(), imonth.ToString());
            }
            return new List<ServintePatient>();
        }

        /// <summary>
        /// Método que recibe el resultado de la generación de turno para los pacientes
        /// </summary>
        /// <param name="sTurnResult">String Json que contiene la información del turno generado</param>
        /// <returns>Objeto Result con el resultado de la operación</returns>
        [WebMethod(EnableSession = true)]
        public Result RecieveTurn(string sTurnResult)
        {
            try
            {
                /*List<Turn> lTurns = this.Json2Object(sTurnResult) as List<Turn>;
                if (lTurns.Count > 0)
                {
                    string sDocument = lTurns[3].Value;
                    string sTurn = lTurns[1].Value;
                    string sDocumentType = lTurns[2].Value;
                    string sidTurn = lTurns[0].Value;
                    if (string.IsNullOrEmpty(sDocument) && !string.IsNullOrEmpty(sTurn))
                    {
                        Generic generic = this.GetPatientFromCiel(sidTurn);
                        sDocument = generic.sname;
                        sDocumentType = generic.scode;
                        generic = null;
                    }
                    SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
                    this.DoLogin();
                    Result result = salesforceIntegrator.UpdatePatientTurn(Tools.GetDocumentType(sDocumentType, false), sDocument, sTurn, this.oSession.scode, this.oSession.sname);
                    salesforceIntegrator.Dispose();
                    salesforceIntegrator = null;                    
                    return result;*/
                return new Result() { iresult = 1, smessage = "Actualizacion correcta" };            
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return new Result() { iresult = 0, smessage = "Error al actualizar las citas del paciente" };
            }
        }

        /// <summary>
        /// Método para obtener la distribución de los tiempos (minutos) laborados de los médicos por centro de costo
        /// </summary>
        /// <param name="sCost">Centro de costos</param>
        /// <param name="iMonth">Entero mes</param>
        /// <param name="iYear">Entero año</param>
        /// <returns>Listado de los médicos con sus tiempos correspondientes</returns>
        [WebMethod(EnableSession = true)]
        public List<Generic> GetTimeDistribution(string sCost, int iMonth, int iYear)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();            
            try
            {
                this.DoLogin();
                return salesforceIntegrator.GetTimeDistribution(sCost, iMonth, iYear, this.oSession.scode, this.oSession.sname);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
        }


        /// <summary>
        /// Método que recibe la información del paciente para mostrarlo en la pantalla de llamado
        /// </summary>
        /// <param name="sRoom">String número de consultorio</param>
        /// <param name="sPatient">String nombre del paciente</param>
        /// <param name="sTurn">String número de turno</param>
        /// <param name="sDocumentType">String tipo de documento del paciente</param>
        /// <param name="sDocument">String documento del paciente</param>
        /// <param name="sIsUpdate">String que indica si se debe hacer insert o update</param>
        /// <param name="sFloor">String que indica la sala de espera del paciente</param>
        [WebMethod]
        public void CallPatient(string sRoom, string sPatient, string sTurn, string sDocumentType, string sDocument, string sIsUpdate, string sFloor)
        {
            bool bIsUpdate = Convert.ToBoolean(sIsUpdate);
            string DocumentType = Tools.GetDocumentType(sDocumentType);
            Digiturno oDAC = new Digiturno(ConfigurationManager.ConnectionStrings["Digiturno5"].ConnectionString);
            try
            {
                oDAC.PatientOnCall(sRoom, sPatient, sTurn, DocumentType, sDocument, bIsUpdate, sFloor);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                oDAC.Dispose();
                oDAC = null;
            }
        }
      
        [WebMethod(EnableSession = true)]
        public List<InspiraTemporal> UpdateInspiraObject(List<InspiraTemporal> linspiratemporal, string sobject)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sConnection = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    switch (sobject)
                    {
                        case "Tarifa":
                            return salesforceIntegrator.UpsertRates(linspiratemporal);                            
                        case "Convenio":
                            return salesforceIntegrator.UpsertAgreements(linspiratemporal);                            
                        case "Plan":
                            return salesforceIntegrator.UpsertHealthCarePlan(linspiratemporal);                            
                        case "Costo":
                            return salesforceIntegrator.UpsertCostCenters(linspiratemporal);                            
                        case "Concepto":
                            return salesforceIntegrator.UpsertConcept(linspiratemporal);                            
                        case "Unidad":
                            salesforceIntegrator.UpsertFunctionalUnit(linspiratemporal);
                            break;
                        case "Producto":
                            return salesforceIntegrator.UpsertProducts(linspiratemporal);                            
                        default:
                            break;
                    }
                }
                return null;
                
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return null;
            }            
            finally
            {
                salesforceIntegrator.Dispose();
                salesforceIntegrator = null;
            }
        }

        [WebMethod(EnableSession = true)]
        public void UpdateProductsByRate(List<TarifaProducto> ltarifaProductos)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sConnection = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    salesforceIntegrator.UpsertProductRates(ltarifaProductos);
                }                
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);                
            }
            finally
            {
                salesforceIntegrator.Dispose();
                salesforceIntegrator = null;
            }
        }

        [WebMethod(EnableSession = true)]
        public void UpdateCostcentersByUnit(List<Generic> lgeneric)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sConnection = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    salesforceIntegrator.InsertCostUnit(lgeneric);
                }                
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
            }
            finally
            {
                salesforceIntegrator.Dispose();
                salesforceIntegrator = null;
            }
        }

        [WebMethod(EnableSession = true)]
        public void UpdateRatesByAgreement(List<InspiraTemporal> lTarifaEmpresa)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sConnection = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    salesforceIntegrator.UpsertAgreementRates(lTarifaEmpresa);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
            }
            finally
            {
                salesforceIntegrator.Dispose();
                salesforceIntegrator = null;
            }
        }

        [WebMethod(EnableSession = true)]
        public void UpdateDiscountsByRate(List<InspiraTemporal> ldescuentoTarifa)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.sConnection = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                    salesforceIntegrator.sSession = this.oSession.scode;
                    salesforceIntegrator.sUrl = this.oSession.sname;
                    salesforceIntegrator.UpsertDiscountRates(ldescuentoTarifa);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
            }
            finally
            {
                salesforceIntegrator.Dispose();
                salesforceIntegrator = null;
            }
        }

        [WebMethod(EnableSession = true)]
        public void UpdateAccount(string sDocument, string sDocumentType, string[] aResponse)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                this.DoLogin();
                salesforceIntegrator.UpdateAccount(this.oSession.scode, this.oSession.sname, sDocument, sDocumentType, aResponse);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
            }
        }

        [WebMethod(EnableSession = true)]
        public string GetTodayPatients(string sPatients)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            List<Patient> lPatients = new List<Patient>();
            StringBuilder sresult = new StringBuilder("<RESULT>");
            try
            {
                this.DoLogin();
                lPatients = salesforceIntegrator.GetTodayPatients(this.oSession.scode, this.oSession.sname, sPatients);
                sresult.Append(this.GetPatientsXML(lPatients));
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                sresult.Append("<ERROR>");
                sresult.Append("<MENSAJE>");
                sresult.Append(ex.Message);
                sresult.Append("</MENSAJE>");
                sresult.Append("</ERROR>");
            }
            sresult.Append("</RESULT>");
            return sresult.ToString();
        }

        [WebMethod(EnableSession = true)]
        public string ConsentConfirmation(string sappointment, string sstatus)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {
                    salesforceIntegrator.MarkConsent(this.oSession.scode, this.oSession.sname, sappointment, sstatus);
                }
                return "Todo bien";
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return ex.Message;
            }
        }

        [WebMethod(EnableSession = true)]
        public string UpdateAppointmentTurn(string sJson)
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            try
            {
                var oSerializer = new JavaScriptSerializer();
                List<Generic> lpatients = oSerializer.Deserialize<List<Generic>>(sJson);
                this.DoLogin();
                if (!string.IsNullOrEmpty(this.oSession.scode))
                {

                    salesforceIntegrator.sConnection = ConfigurationManager.ConnectionStrings["OracleFNC"].ConnectionString;
                    return salesforceIntegrator.UpdateAppointmentTurns(this.oSession.scode, this.oSession.sname, lpatients);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                return "Error al actualizar las citas " + ex.Message;
            }
        }

        #endregion

        #region Métodos de apoyo

        public string CreateConsent(string sidappointment)
        {
            SalesforceViaRestApi salesforceViaRestApi = null;
            string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
            string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
            string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
            string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();                        
            List<Consentimiento> lconsentimientos = null;
            try
            {
                salesforceViaRestApi = new SalesforceViaRestApi()
                {
                    sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString(),
                    sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString(),
                };
                salesforceViaRestApi.DoLogin(susr, spwd, sclientid, ssectet);
                this.oSession = salesforceViaRestApi.salesforceSession;
                if (this.oSession != null)
                {
                    salesforceViaRestApi.salesforceSession = this.oSession;
                    lconsentimientos = salesforceViaRestApi.GetDataForConsent(this.oSession.scode, this.oSession.sname, sidappointment);
                    using (ConsInformado oDAC = new ConsInformado())
                    {
                        oDAC.sConnection = ConfigurationManager.ConnectionStrings["ConsentimientoInformado"].ConnectionString;
                        if (lconsentimientos.Count > 0)
                        {
                            foreach (var item in lconsentimientos)
                            {
                                oDAC.CreateAppointmentRecord(item);
                            }
                            Consentimiento consentimiento = lconsentimientos[0];                           
                            return consentimiento.sappointmemt;
                        }
                    }
                }                
                return string.Empty;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return string.Empty;
            }
        }

        private string GetPatientsXML(List<Patient> lPatients)
        {
            StringBuilder sXML = new StringBuilder("<PACIENTES>");
            foreach (Patient item in lPatients)
            {
                sXML.Append("<PACIENTE>");
                sXML.Append("<DOCUMENTO>");
                sXML.Append(item.sdocument);
                sXML.Append("</DOCUMENTO>");
                sXML.Append("<CORREO>");
                sXML.Append(item.smail);
                sXML.Append("</CORREO>");
                sXML.Append("</PACIENTE>");
            }
            sXML.Append("</PACIENTES>");
            sXML.Append("<ERROR>");
            sXML.Append("<MENSAJE>");
            sXML.Append("</MENSAJE>");
            sXML.Append("</ERROR>");
            return sXML.ToString();
        }

        private void UpdateAppointment(string sappointment, int iresult)
        {
            this.DoLogin();
        }

        private void UpdateEntry(int ientry, int iresult)
        {

        }

        /// <summary>
        /// Método para generar el cargo con la información recibda desde Inspira
        /// </summary>
        /// <param name="xElement"></param>
        private int GenerateCharge(XElement xElement)
        {
            Charge oCharge = null;
            Patient oPatient = null;
            Servinte oServinte = null;
            try
            {
                oPatient = this.GetPatientEntity(xElement);
                oCharge = this.GetChargeEntity(xElement);
                oCharge.sdocumenttype = oPatient.sdocumenttype;
                oServinte = new Servinte(ConfigurationManager.ConnectionStrings["Servinte"].ConnectionString);
                oCharge.ldetail = this.GetChargeDetail(xElement, oServinte, oCharge);
                oPatient.oCharge = oCharge;
                return oServinte.ChargeTransaction(oPatient, oCharge);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xElement"></param>
        /// <returns></returns>
        private Patient GetPatientEntity(XElement xElement)
        {
            string sDocument = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTNUMBER");
            string sDocumentType = this.GetDocumentType(sDocument);
            return new Patient()
            {
                screatedby = "ADMI",
                sdocument = sDocument,
                sdocumenttype = (!string.IsNullOrEmpty(sDocumentType)) ? sDocumentType : Tools.GetDocumentType(this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTTYPE")),
                sfirstname = this.ReadXmlValue(xElement, "PATIENT", "FIRSTNAME"),
                ssecondname = this.ReadXmlValue(xElement, "PATIENT", "MIDDLENAME"),
                ssurname = this.ReadXmlValue(xElement, "PATIENT", "SURNAME"),
                ssecondsurname = this.ReadXmlValue(xElement, "PATIENT", "SECONDSURNAME"),
                dbirthdate = Convert.ToDateTime(this.ReadXmlValue(xElement, "PATIENT", "BIRTHDATE")),
                dcreateddate = DateTime.Now,
                dcreatedtime = DateTime.Now,
                saddress = this.ReadXmlValue(xElement, "PATIENT", "ADDRESS"),
                sbranch = this.ReadXmlValue(xElement, "APPOINTMENT", "ADMISSION", false),
                sgender = this.ReadXmlValue(xElement, "PATIENT", "GENDER"),
                smaritalstatus = this.ReadXmlValue(xElement, "PATIENT", "MARITALSTATUS"),
                sphone = this.ReadXmlValue(xElement, "PATIENT", "PHONE"),
                sbornplace = ConfigurationManager.AppSettings["CodigoCiudad"],
                sneighborhood = ConfigurationManager.AppSettings["CodigoBarrio"],
                surbanzone = "U",
                sjob = "999",
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sDocument"></param>
        /// <returns></returns>
        private string GetDocumentType(string sDocument)
        {
            string sType = string.Empty;
            Servinte oServinte = new Servinte(ConfigurationManager.ConnectionStrings["Servinte"].ConnectionString, false);
            List<Patient> lPatient = null;
            try
            {
                lPatient = oServinte.GetPatients(sDocument);
                if (lPatient.Count > 0) sType = lPatient[0].sdocumenttype;
                return sType;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return sType;
            }
        }

        /// <summary>
        /// Método que obtiene el objeto Cargo (entidad) para crear el cargo
        /// </summary>
        /// <param name="xElement"></param>
        /// <returns></returns>
        private Charge GetChargeEntity(XElement xElement)
        {
            Servinte oServinte = new Servinte(ConfigurationManager.ConnectionStrings["Servinte"].ConnectionString, false);
            Generic oGeneric = oServinte.GetPaymentType(this.ReadXmlValue(xElement, "PATIENT", "PLAN"), this.ReadXmlValue(xElement, "PATIENT", "AGREEMENTCODE"));
            Charge oCharge = new Charge()
            {
                iyear = DateTime.Now.Year,
                smonth = DateTime.Now.ToString("MM"),
                sdocument = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTNUMBER"),
                sdocumenttype = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTTYPE"),
                sagreementcode = this.ReadXmlValue(xElement, "PATIENT", "AGREEMENTCODE"),
                sagreementname = this.ReadXmlValue(xElement, "PATIENT", "AGREEMENTNAME"),
                splan = this.ReadXmlValue(xElement, "PATIENT", "PLAN"),
                dcreateddate = DateTime.Now,
                dassignedtime = DateTime.Now,
                dassigneddate = DateTime.Now,
                screatedby = "ADMI",
                sadmissiontype = Tools.GetSerCod(this.ReadXmlValue(xElement, "APPOINTMENT", "ADMISSION", false), this.ReadXmlValue(xElement, "PATIENT", "PLANNAME")),
                sagreementtype = Tools.GetAgreementType(this.ReadXmlValue(xElement, "PATIENT", "PLANNAME")),
                ssource = this.ReadXmlValue(xElement, "APPOINTMENT", "ADMISSION", false),
                srate = xElement.Element("SERVICES").Elements("SERVICE").ElementAt(0).Element("RATECODE").Value,
                sbranch = Tools.GetBranch(this.ReadXmlValue(xElement, "APPOINTMENT", "ADMISSION", false)),
                sauthorization = this.ReadXmlValue(xElement, "APPOINTMENT", "AUTHORIZATION", false),
                //inumber = oServinte.GetSecuenceNumber("IN", oServinte.GetBranchName(this.ReadXmlValue(xElement, "PATIENT", "ADMISSION", false))),
                //ldetail = this.GetChargeDetail(xElement, oServinte),
                iusertype = Convert.ToInt32(oGeneric.scode),
                ilevel = Convert.ToInt32(oGeneric.sname),
                ldetail = new List<ChargeDetail>(),
                spatientname = this.ReadXmlValue(xElement, "PATIENT", "FIRSTNAME") + " " + this.ReadXmlValue(xElement, "PATIENT", "MIDDLENAME") + " " + this.ReadXmlValue(xElement, "PATIENT", "SURNAME") + " " + this.ReadXmlValue(xElement, "PATIENT", "MIDDLENAME"),
                sassignedto = "ADMI",
                splanname = this.ReadXmlValue(xElement, "PATIENT", "PLANNAME"),
                snit = this.ReadXmlValue(xElement, "APPOINTMENT", "NIT", false),
            };
            oCharge.sprogram = (oCharge.sadmissiontype == "31") ? "R19" : "R18";
            oCharge.sbranchname = oServinte.GetBranchName(oCharge.sbranch);
            oCharge.lssources = oServinte.GetSourcesCodes(oCharge.sbranch);
            oCharge.scode = oCharge.lssources[0];
            //oCharge.inumber = oServinte.GetSecuenceNumber(oCharge.scode, oCharge.sbranchname);
            return oCharge;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="xElement"></param>
        /// <param name="oServinte"></param>
        /// <returns></returns>
        private List<ChargeDetail> GetChargeDetail(XElement xElement, Servinte oServinte, Charge oCharge)
        {
            List<ChargeDetail> lDetail = new List<ChargeDetail>();
            ChargeDetail oDetail = null;
            var nodes = xElement.Element("SERVICES").Elements("SERVICE");
            foreach (var item in nodes)
            {
                oDetail = new ChargeDetail()
                {
                    sservice = item.Element("CODE").Value,
                    iqty = 1,
                    dtotal = 0,
                    scostcenter = this.ReadXmlValue(xElement, "APPOINTMENT", "COSTCENTER", false),
                    sconcept = this.ReadXmlValue(xElement, "APPOINTMENT", "CONCEPT", false),
                    snit = oCharge.snit,
                    //inumber = oServinte.GetSecuenceNumber(oCharge.lssources[1], oCharge.sbranchname),                    
                };
                lDetail.Add(oDetail);
            }
            return lDetail;
        }

        /// <summary>
        /// Método para enviar la información recibida de Inspira y así poder generar el turno
        /// </summary>
        /// <param name="xElement">Objeto XML enviado desde Inspira</param>
        /// <returns></returns>
        private TurnResult SendTurnData(XElement xElement)
        {
            string PriorityCode = this.ReadXmlValue(xElement, "APPOINTMENT", "PRIORITY", false);
            bool bprint = !(PriorityCode.Contains("RHB") || PriorityCode.Contains("Prog") || PriorityCode.Contains("Ped"));
            int iQue = this.GetQueueID(PriorityCode);
            TurnResult turnResult = new TurnResult();
            
            return turnResult;
        }

        private void UpdateConsentProfessional(string sappointment, string sdocument)
        {
            using (ConsInformado oDAC = new ConsInformado())
            {
                oDAC.sConnection = ConfigurationManager.ConnectionStrings["ConsentimientoInformado"].ConnectionString;
                oDAC.UpdateConsentUser(sappointment, sdocument);
            }
        }
        
        private void CreateConsent(XElement xElement)
        {
            Consentimiento consentimiento = null;
            using (ConsInformado oDAC = new ConsInformado())
            {
                oDAC.sConnection = ConfigurationManager.ConnectionStrings["ConsentimientoInformado"].ConnectionString;
                oDAC.sOracleConnection = ConfigurationManager.ConnectionStrings["ConsentimientoOracle"].ConnectionString;
                var nodes = xElement.Element("SERVICES").Elements("SERVICE");
                string sService = string.Empty;
                int iAge = Convert.ToInt32(this.ReadXmlValue(xElement, "PATIENT", "AGE"));
                List<string> lcodes = new List<string>();
                int i = 0;
                foreach (var item in nodes)
                {
                    //sService = (iAge < 18) ? item.Element("CODE").Value + "001" : item.Element("CODE").Value;
                    sService = item.Element("CODE").Value;
                    consentimiento = new Consentimiento()
                    {
                        sappointmemt = this.ReadXmlValue(xElement, "APPOINTMENT", "CODE", false),
                        sfirstname = this.ReadXmlValue(xElement, "PATIENT", "FIRSTNAME").ToUpper(),
                        ssecondname = this.ReadXmlValue(xElement, "PATIENT", "MIDDLENAME").ToUpper(),
                        ssurname = this.ReadXmlValue(xElement, "PATIENT", "SURNAME").ToUpper(),
                        ssecondsurname = this.ReadXmlValue(xElement, "PATIENT", "SECONDSURNAME").ToUpper(),
                        sdocumenttype = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTTYPE").ToUpper(),
                        sdocument = this.ReadXmlValue(xElement, "PATIENT", "DOCUMENTNUMBER").ToUpper(),
                        dappointmentdate = DateTime.Now,
                        scups = sService,
                        sservicename = item.Element("DESCRIPTION").Value,
                        iage = iAge,
                        sid = this.ReadXmlValue(xElement, "PATIENT", "ID").ToUpper(),
                        shabeasdata = this.ReadXmlValue(xElement, "PATIENT", "HABEAS").ToUpper(),
                    };                    
                    if (i == 0)
                    {
                        oDAC.CreateAppointmentRecord(consentimiento);
                    }
                    else
                    {
                        if (lcodes.FirstOrDefault(x => x == sService) == null)
                        {
                            oDAC.CreateAppointmentRecord(consentimiento);
                        }
                    }
                    i++;
                    lcodes.Add(sService);
                }
                /*oDAC.ResearchConsent(consentimiento, false);
                if (iAge >= 6 && iAge <= 13)
                {
                    oDAC.ResearchConsent(consentimiento, true);
                }*/
            }
        }

        /// <summary>
        /// Método para obtener el Id de la cola de un turno por su nombre
        /// </summary>
        /// <param name="strQueueName">String nombre de la cola</param>
        /// <returns>Entero Id de la cola</returns>
        private int GetQueueID(string strQueueName)
        {
            using (Digiturno oDigiturno = new Digiturno(ConfigurationManager.ConnectionStrings["Digiturno5"].ConnectionString))
            {
                return oDigiturno.GetQueueIdByName(strQueueName);
            }
        }

        /// <summary>
        /// Método para obtener el Id de un tipo de documento por su nombre
        /// </summary>
        /// <param name="strDocumentType">String nombre del tipo de documento</param>
        /// <returns>Id del tipo de documento</returns>
        private int GetDocumentTypeId(string strDocumentType)
        {
            using (Digiturno oDigiturno = new Digiturno(ConfigurationManager.ConnectionStrings["Digiturno5"].ConnectionString))
            {
                return oDigiturno.GetDocumentTypeId(strDocumentType);
            }
        }


        /// <summary>
        /// Método que valida la información del usuario que se está registrando en el servicio web
        /// </summary>
        /// <param name="User">String nombre del usuario</param>
        /// <param name="Password">String contraseña del usuario</param>
        /// <returns>Boleano que indica si las credenciales son correctas o no</returns>
        private bool ValidateUser(string User, string Password)
        {
            return (User == ConfigurationManager.AppSettings["ServiceUser"] && Password == Tools.SHA256Crypt(ConfigurationManager.AppSettings["ServicePassword"]));
        }

        /// <summary>
        /// Método para leer el valor de un elemento de un XML
        /// </summary>
        /// <param name="xElement">Objeto XElement (Nodo)</param>
        /// <param name="Parent">String nombre del nodo padre</param>
        /// <param name="Tag">String nombre del nodo</param>
        /// <param name="Node">Boleano que indica si el valor se obtiene del nodo padre o el hijo</param>
        /// <returns></returns>
        private string ReadXmlValue(XElement xElement, string Parent, string Tag, bool Node = true)
        {

            return (Node) ? xElement.Element(Parent).Element(Tag).Value : xElement.Element(Tag).Value;
        }

        /// <summary>
        /// Método que convierte la cadena XML recibida en objeto XML
        /// </summary>
        /// <param name="oRequest"></param>
        /// <returns></returns>
        private XElement GetXElement(string oRequest)
        {
            return XElement.Parse(oRequest);
        }

        /// <summary>
        /// Método que genera la respuesta del servicio para generar el turno o el cargo
        /// </summary>
        /// <param name="oResult">Objeto resultado de la generación del turno o del cargo</param>
        /// <param name="bIsTurn">Boleano que indica si la respuesta corresponde a generación de turno o de cargo</param>
        /// <returns>String que contiene un XML con el resultado de la operación de generación del turno</returns>
        private string GetResponse(TurnResult oResult, bool bIsTurn = true)
        {
            string success = string.Empty;
            if (bIsTurn)
            {
                success = (string.IsNullOrEmpty(oResult.turncode)) ? "FALSE" : "TRUE";
            }
            else
            {
                success = (oResult.chargenumber == 0) ? "FALSE" : "TRUE";
            }
            StringBuilder sResponse = new StringBuilder(Tools.GetResponseHeader());
            sResponse.Append("<SUCCESS>");
            sResponse.Append(success);
            sResponse.Append("</SUCCESS>");
            sResponse.Append("<ERP_CODE>");
            sResponse.Append(oResult.chargenumber.ToString());
            sResponse.Append("</ERP_CODE>");
            sResponse.Append("<TURN>");
            sResponse.Append(oResult.turnnumber);
            sResponse.Append("</TURN>");
            StringBuilder sError = new StringBuilder();
            if (!string.IsNullOrEmpty(oResult.errorcode))
            {
                sResponse.Append("<ERRORLIST>");
                sResponse.Append("<ERROR>");
                sResponse.Append("<CODE>");
                sResponse.Append(oResult.errorcode);
                sResponse.Append("</CODE>");
                sResponse.Append("<DESCRIPTION>");
                sResponse.Append(oResult.errordescription);
                sResponse.Append("</DESCRIPTION>");
                sResponse.Append("</ERROR>");
                sResponse.Append("</ERRORLIST>");
            }
            else
            {
                sResponse.Append("<ERRORLIST>");
                sResponse.Append("<ERROR>");
                sResponse.Append("<CODE>");
                sResponse.Append("</CODE>");
                sResponse.Append("<DESCRIPTION>");
                sResponse.Append("</DESCRIPTION>");
                sResponse.Append("</ERROR>");
                sResponse.Append("</ERRORLIST>");
            }
            sResponse.Append(Tools.GetResponseFooter());
            return sResponse.ToString();
        }

        /// <summary>
        /// Método para hacer login en Salesforce y almacenar el session id en variable de sesión (mejora rendimiento)
        /// </summary>
        private void DoLogin()
        {
            SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
            if (string.IsNullOrEmpty(this.oSession.scode))
            {
                this.oSession = salesforceIntegrator.Login(ConfigurationManager.AppSettings["SalesforceCompany"], ConfigurationManager.AppSettings["SalesforceUser"], ConfigurationManager.AppSettings["SalesforcePassword"], ConfigurationManager.AppSettings["SalesforceToken"]);
            }
        }

        /// <summary>
        /// Método para obtener un objeto que viene de un Json
        /// </summary>
        /// <param name="sJson"></param>
        /// <returns></returns>
        private object Json2Object(string sJson)
        {
            var oSerializer = new JavaScriptSerializer();
            return oSerializer.Deserialize<List<Turn>>(sJson);
        }

        /// <summary>
        /// Método que obtiene un paciente de la base de datos del digiturno por el número de turno
        /// </summary>
        /// <param name="sTurn">String número de turno</param>
        /// <returns>Objeto genérico con el documento y el tipo de documento del paciente</returns>
        private Generic GetPatientFromCiel(string sTurn)
        {
            using (Digiturno oDigiturno = new Digiturno(ConfigurationManager.ConnectionStrings["Digiturno5"].ConnectionString))
            {
                return oDigiturno.GetPatientFromTurn(sTurn);
            }
        }

        #endregion

        #region Métodos de integración con Synapse

        [WebMethod]
        public string UpdateAppointment(string sid, string sprofessional, string surlreport, string surlvideo)
        {
            SalesforceViaRestApi salesforceViaRestApi = new SalesforceViaRestApi();
            string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
            string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
            string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
            string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();
            try
            {
                salesforceViaRestApi = new SalesforceViaRestApi()
                {
                    sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString(),
                    sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString(),
                };
                salesforceViaRestApi.DoLogin(susr, spwd, sclientid, ssectet);
                if (salesforceViaRestApi.salesforceSession != null)
                {
                    LogError.WriteMessage("Application", "WSDigiturno", "Información de cita recibida: " + sid);
                    salesforceViaRestApi.UpdateAppointmentUrl(sid, surlreport.Replace("<![CDATA", string.Empty).Replace("]]", string.Empty), surlvideo);
                }
                return "true";
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSDigiturno", ex);
                return "false";
            }
            
        }
        
        #endregion
    }
}

