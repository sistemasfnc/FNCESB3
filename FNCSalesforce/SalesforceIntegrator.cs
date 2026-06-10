using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FNCSalesforce.Sfdc;
using EventLog;
using FNCEntity;
using System.Net;
using FNCDAC;
using FNCSalesforce.Digiturno5WS;
using FNCUtils;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Data.OleDb;
using System.Security.Principal;
using System.Security.Policy;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace FNCSalesforce
{
    /// <summary>
    /// Objeto que permite integrar Salesforce con el ESB de la Neumológica
    /// </summary>
    public class SalesforceIntegrator : IDisposable
    {

        /// <summary>
        /// Constructor del objeto, se asigna cifrado tls2 para certificado de seguridad de Salesforce
        /// </summary>
        public SalesforceIntegrator()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            System.Net.ServicePointManager.ServerCertificateValidationCallback +=
            (se, cert, chain, sslerror) =>
            {
                return true;
            };
        }

        /// <summary>
        /// String cadena de conexión a base de datos
        /// </summary>
        public string sConnection { get; set; }

        public string sSession { get; set; }

        public string sUrl { get; set; }

        private List<sObject> lListInsert { get; set; }

        private List<sObject> lListUpdate { get; set; }

        /// <summary>
        /// Método para realizar el login en el API de Salesforce
        /// </summary>
        /// <param name="sOrganization">String id de la organización en Salesforce</param>
        /// <param name="sUser">String nombre del usuario</param>
        /// <param name="sPassword">String contraseña del usuario</param>
        /// <param name="sToken">String token de conexión de aplicación Salesforce para el usuario</param>
        /// <returns>String con el session id</returns>
        public Generic Login(string sOrganization, string sUser, string sPassword, string sToken)
        {
            SoapClient soapClient = new SoapClient();
            try
            {
                //Aún no sabemos para qué funciona enviar la organización (primer parámetro método login) ya que genera excepción si se envía, igual se deja el código comentado por si se usa después
                //LoginScopeHeader scopeHeader = new LoginScopeHeader() { organizationId = sOrganization };                
                LoginResult loginResult = soapClient.login(null, sUser, sPassword + sToken);
                return new Generic() { scode = loginResult.sessionId, sname = loginResult.serverUrl };
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return null;
            }
            finally
            {
                soapClient = null;
            }
        }

        #region Integracion con Digiturno 5
        /// <summary>
        /// Método para obtener el paciente y si tiene citas disponibles
        /// </summary>
        /// <param name="sSession">String session id del login</param>
        /// <param name="sDocumentType">String tipo de documento del paciente</param>
        /// <param name="sDocument">String documento del paciente</param>
        /// <param name="sUrl">String url de salesforce</param>
        /// <param name="sUrl">String cadena de conexión base de datos consentimiento informado</param>
        /// <returns>Digiturno5 objeto definido para la integración</returns>
        public Digiturno5 GetPatient(string sSession, string sDocumentType, string sDocument, string sUrl, string sConnection)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            Appointment__c appointment = null;
            Digiturno5 digiturno5 = new Digiturno5();
            int iQue = 0;
            bool bNeedsInvoice = false;
            bool bIsVip = false;
            bool bResponse = false;
            SaveResult[] saveResults = null;
            try
            {
                sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.DocumentNumber__c");
                sQuery.Append(",  WhatId__r.DocumentType__c, PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
                sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c, FNC_prefacturado__c FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                //sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                sQuery.Append(sDocument);
                sQuery.Append("' AND WhatId__r.DocumentType__c = '");
                sQuery.Append(sDocumentType);
                sQuery.Append("' AND ActivityDate__c = ");
                sQuery.Append(DateTime.Now.ToString("yyyy-MM-dd"));
                //sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado', 'Facturada') ORDER BY StartDatetime__c");
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size != 0)
                {
                    sObject[] records = queryResult.records;
                    for (int i = 0; i < records.Length; i++)
                    {
                        if (records[i] != null)
                        {
                            appointment = records[i] as Appointment__c;
                            iQue = this.ValidateAppointment(appointment);
                            if (iQue != 0)
                            {
                                bResponse = true;
                                saveResults = this.UpdateAppointment(appointment, soapClient, sessionHeader, records);
                                if (saveResults != null)
                                {
                                    if (saveResults[0].errors != null)
                                    {
                                        ApplicationException applicationException = new ApplicationException(saveResults[0].errors[0].message);
                                        LogError.WriteError("Application", "WSInspira", applicationException);
                                        if (!string.IsNullOrEmpty(appointment.Name))
                                        {
                                            applicationException = new ApplicationException(appointment.Name);
                                            LogError.WriteError("Application", "WSInspira", applicationException);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (bResponse)
                    {
                        bool bCopayment = appointment.IsCoPayment__c.HasValue ? appointment.IsCoPayment__c.Value : false;
                        bNeedsInvoice = this.NeedsInvoice(appointment.PlanName__c, appointment.GroupId__r.Name, appointment.AuthorizationCode__c, bCopayment, appointment.WhatId__r.Age2__pc.Value);
                        if (bNeedsInvoice || appointment.FNC_prefacturado__c.Value)
                        {
                            digiturno5.oResult = new Result() { iresult = 0, smessage = "Su cita no requiere facturación, diríjase directamente a su lugar de atención" };
                            return digiturno5;
                        }
                        PatientCiel oPatient = new PatientCiel()
                        {
                            sfirstname = appointment.WhatId__r.FirstName_c__pc,
                            ssecondname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondName__pc)) ? string.Empty : appointment.WhatId__r.SecondName__pc,
                            sfirstsurname = appointment.WhatId__r.FirstSurname__pc,
                            ssecondsurname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondSurname__pc)) ? string.Empty : appointment.WhatId__r.SecondSurname__pc,
                            iplan = this.GetPlan(appointment.PlanId__r.HealthCarePlanId__c, soapClient, sessionHeader, queryOptions, mruHeader),
                            iunit = this.GetUnit(appointment.PlanName__c, appointment.GroupId__r.Name, bNeedsInvoice, appointment.AgendaId__r.Name.ToUpper(), appointment.WhatId__r.Age2__pc.Value),
                            iattendance = iQue,
                        };
                        digiturno5.oPatient = oPatient;
                        digiturno5.oResult = new Result()
                        {
                            iresult = 1,
                            smessage = this.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name)
                        };
                    }
                    else
                    {
                        digiturno5.oResult = new Result() { iresult = 0, smessage = FNCSalesforce.Properties.Settings.Default.MessageTooLate };
                    }
                }
                else
                {
                    digiturno5.oResult = new Result() { iresult = 0, smessage = FNCSalesforce.Properties.Settings.Default.MessageNoAppointments };
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                digiturno5.oResult = new Result() { iresult = 0, smessage = FNCSalesforce.Properties.Settings.Default.MessageNoAppointments };
            }
            return digiturno5;
        }

        /// <summary>
        /// Método para asignar el número de turno y el estado de las citas del paciente
        /// </summary>
        /// <param name="sDocumentType">String Tipo de documento del paciente</param>
        /// <param name="sDocument">String Número de documento del paciente</param>
        /// <param name="sTurn">String </param>
        /// <param name="sSession"></param>
        /// <param name="sUrl"></param>
        /// <returns></returns>
        public Result UpdatePatientTurn(string sDocumentType, string sDocument, string sTurn, string sSession, string sUrl)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            Appointment__c appointment = null;
            SaveResult[] results = null;
            LimitInfo[] limitInfos = null;
            bool bInvoiced = false;
            sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, ");
            sQuery.Append(" PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
            sQuery.Append(", ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, WhatId__r.DocumentNumber__c, WhatId__r.DocumentType__c, ActivityDate__c FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
            sQuery.Append(sDocument);
            sQuery.Append("' AND WhatId__r.DocumentType__c = '");
            sQuery.Append(sDocumentType);
            sQuery.Append("' AND ActivityDate__c = ");
            sQuery.Append(DateTime.Now.ToString("yyyy-MM-dd"));
            sQuery.Append(" ORDER BY StartDatetime__c");
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
            if (queryResult.size != 0)
            {
                sObject[] records = queryResult.records;
                for (int i = 0; i < records.Length; i++)
                {
                    appointment = records[i] as Appointment__c;
                    bInvoiced = (appointment.ServiceBilled__c.HasValue) ? appointment.ServiceBilled__c.Value : false;
                    if (!string.IsNullOrEmpty(appointment.TurnNumber__c) && this.ValidateAppointment(appointment) != 0)
                    {
                        Appointment__c oAppoinment = new Appointment__c();
                        oAppoinment.TurnNumber__c = sTurn;
                        sObject[] oUpdate = new sObject[] { oAppoinment };
                        soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, null, oUpdate, out limitInfos, out results);
                        if (!results[0].success)
                        {
                            ApplicationException applicationException = new ApplicationException(results[0].errors[0].message);
                            LogError.WriteError("Application", "WSInspira", applicationException);
                        }
                    }
                }
                return new Result() { iresult = 1, smessage = FNCSalesforce.Properties.Settings.Default.MessageTurnOK };
            }
            else
            {
                return new Result() { iresult = 0, smessage = FNCSalesforce.Properties.Settings.Default.MessageNoUpdate };
            }
        }

        private List<Appointment__c> GetAppointmentsForUpdate(string sSession, string sUrl, SoapClient soapClient, List<Generic> lpatients, List<Appointment__c> lstAppointment)
        {
            StringBuilder sQuery = new StringBuilder();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            QueryResult queryResult = new QueryResult();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            PackageVersion[] packageVersions = null;
            string sdocuments = this.GetPatientDocuments(lpatients, false);
            sQuery.Append("SELECT Id, WhatId__r.DocumentNumber__c, WhatId__r.DocumentType__c FROM Appointment__c WHERE WhatId__c <> '' AND WhatId__r.DocumentNumber__c IN ('");
            sQuery.Append(sdocuments);
            if (lstAppointment.Count == 0)
            {
                sQuery.Append("') AND ActivityDate__c = ");
                sQuery.Append(DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"));
                sQuery.Append(" ORDER BY Id LIMIT 2000");
            }
            else
            {
                sQuery.Append("') AND ID > '");
                sQuery.Append(lstAppointment.LastOrDefault().Id);
                sQuery.Append("' AND ActivityDate__c = ");
                sQuery.Append(DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"));
                sQuery.Append(" ORDER BY Id LIMIT 2000");
            }
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lstAppointment.AddRange(records.OfType<Appointment__c>().ToList());
                    return GetAppointmentsForUpdate(sSession, sUrl, soapClient, lpatients, lstAppointment);
                }
                else
                {
                    return lstAppointment;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }


        }

        private string GetPatientDocuments(List<Generic> lpatients, bool bIsDocumentType)
        {
            string[] aresult = new string[lpatients.Count];
            int i = 0;
            foreach (var item in lpatients)
            {
                if (bIsDocumentType)
                {
                    aresult[i] = Tools.GetDocumentType(item.scode, false);
                }
                else
                {
                    aresult[i] = item.sname;
                }
                i++;
            }
            return string.Join("','", aresult);
        }

        public string UpdateAppointmentTurns(string sSession, string sUrl, List<Generic> lpatients)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            List<Appointment__c> lAppointments = this.GetAppointmentsForUpdate(sSession, sUrl, soapClient, lpatients, new List<Appointment__c>());
            this.lListUpdate = new List<sObject>();
            foreach (var item in lpatients)
            {
                foreach (Appointment__c appointment in lAppointments)
                {
                    if (appointment.WhatId__r.DocumentNumber__c == item.sname && appointment.WhatId__r.DocumentType__c == Tools.GetDocumentType(item.scode, false))
                    {
                        Appointment__c tmp = new Appointment__c()
                        {
                            TurnNumber__c = item.sfilter,
                            Id = appointment.Id,
                        };
                        lListUpdate.Add(tmp);
                    }
                }
            }
            try
            {
                this.UpdateValues(lListUpdate, soapClient, sessionHeader, "CITA");
                return string.Empty;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }


        /// <summary>
        /// Método para destruir el objeto
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }

        /// <summary>
        /// Método par actualizar los datos de la cita con la asistencia, el facturado y la presconsulta
        /// </summary>
        /// <param name="appointment">Objeto cita</param>
        /// <param name="soapClient">Objeto conexión a Salesforce</param>
        /// <param name="sessionHeader">Objeto cabecera de sesión de conexión a Salesforce</param>
        /// <param name="records">Objeto registros con las citas encontradas</param>
        /// <returns>Objeto con el resultado de la actualización</returns>
        private SaveResult[] UpdateAppointment(Appointment__c appointment, SoapClient soapClient, SessionHeader sessionHeader, sObject[] records)
        {
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            SaveResult[] results = null;
            LimitInfo[] limitInfos = null;
            bool bInvoiced = false;
            string sStatus = "Asistió";
            bInvoiced = (appointment.ServiceBilled__c.HasValue) ? appointment.ServiceBilled__c.Value : false;
            bool bCopayment = false;
            bool bNeedsPre = (appointment.GroupId__r.Needspreassessment__c.HasValue) ? appointment.GroupId__r.Needspreassessment__c.Value : false;
            if (!bInvoiced)
            {
                Appointment__c oAppoinment = new Appointment__c();
                oAppoinment.Id = appointment.Id;
                oAppoinment.PatientAttended__cSpecified = true;
                oAppoinment.PatientAttended__c = true;
                oAppoinment.PatientWaiting__cSpecified = true;
                oAppoinment.PatientWaiting__c = true;
                oAppoinment.WaitingStartDate__cSpecified = true;
                oAppoinment.WaitingStartDate__c = DateTime.Now;
                oAppoinment.AttendedStartDatetime__cSpecified = true;
                oAppoinment.AttendedStartDatetime__c = DateTime.Now;
                bCopayment = appointment.IsCoPayment__c.HasValue ? appointment.IsCoPayment__c.Value : true;
                bInvoiced = this.NeedsInvoice(appointment.PlanName__c, appointment.GroupId__r.Name, appointment.AuthorizationCode__c, bCopayment, appointment.WhatId__r.Age2__pc.Value);
                if (bInvoiced || appointment.FNC_prefacturado__c.Value)
                {
                    oAppoinment.ServiceBilled__cSpecified = true;
                    oAppoinment.ServiceBilled__c = true;
                    sStatus = "Facturada";
                }
                sObject[] oUpdate = new sObject[] { oAppoinment };
                soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, null, null, null, oUpdate, out limitInfos, out results);
                if (this.NeeedsPreAssessment(appointment.GroupId__r.Name, Convert.ToInt32(appointment.WhatId__r.Age2__pc), appointment.AgendaId__r.Name, records, bNeedsPre) && bInvoiced)
                {
                    oAppoinment.patient_pre__cSpecified = true;
                    oAppoinment.patient_pre__c = true;
                    sStatus = "Pre Consulta";
                    oAppoinment.Status__c = sStatus;
                    oUpdate = new sObject[] { oAppoinment };
                    soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, null, null, null, oUpdate, out limitInfos, out results);
                }
            }
            return results;
        }

        /// <summary>
        /// Método para actualizar el campo de datos de investigación para los pacientes
        /// </summary>
        /// <param name="sSession">String código de la sesión</param>
        /// <param name="sUrl">String url de salesforce</param>
        /// <param name="sDocument">String Documento del Paciente</param>
        /// <param name="sDocumentType">String tipo de documento del paciente</param>
        /// <param name="aResponse">Array string con el valor seleccionado Si o No</param>
        public void UpdateAccount(string sSession, string sUrl, string sDocument, string sDocumentType, string[] aResponse)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            Account account = null;
            SaveResult[] results = null;
            LimitInfo[] limitInfos = null;
            sQuery.Append("SELECT Id FROM Account WHERE DocumentNumber__c = '");
            sQuery.Append(sDocument);
            sQuery.Append("' AND DocumentType__c = '");
            sQuery.Append(sDocumentType);
            sQuery.Append("'");
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
            if (queryResult.size != 0)
            {
                account = queryResult.records[0] as Account;
                if (aResponse.Length == 1)
                {
                    account.DataResearh__c = aResponse[0];
                }
                sObject[] oUpdate = new sObject[] { account };
                soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, null, oUpdate, out limitInfos, out results);
                if (!results[0].success)
                {
                    ApplicationException applicationException = new ApplicationException(results[0].errors[0].message);
                    LogError.WriteError("Application", "WSInspira", applicationException);
                }
            }
        }


        /// <summary>
        /// Método para crear el registro del consentimiento informado, se hizo acá porque tal vez es el medio más rápido para generarlo
        /// </summary>
        /// <param name="appointment">Objeto cita Inspira</param>
        /// <param name="soapClient">Objeto conexión a Salesforce</param>
        /// <param name="sessionHeader">Objeto cabeceras de sesión de Salesforce</param>
        private void CreateConsent(Appointment__c appointment, SoapClient soapClient, SessionHeader sessionHeader, string sConnection)
        {
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            ServiceByGroup__c serviceByGroup = null;
            Consentimiento consentimiento = null;
            ConsInformado oDAC = new ConsInformado();
            oDAC.sConnection = sConnection;
            sQuery.Append("SELECT ServiceId__r.Code__c, ServiceId__r.Name FROM ServiceByGroup__c WHERE GroupId__c = '");
            sQuery.Append(appointment.GroupId__c);
            sQuery.Append("'");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size != 0)
                {
                    sObject[] records = queryResult.records;
                    for (int i = 0; i < records.Length; i++)
                    {
                        serviceByGroup = records[i] as ServiceByGroup__c;
                        consentimiento = new Consentimiento()
                        {
                            dappointmentdate = appointment.ActivityDate__c.Value,
                            sappointmemt = appointment.Name,
                            sfirstname = appointment.WhatId__r.FirstName_c__pc,
                            ssecondname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondName__pc)) ? string.Empty : appointment.WhatId__r.SecondName__pc,
                            ssurname = appointment.WhatId__r.FirstSurname__pc,
                            ssecondsurname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondSurname__pc)) ? string.Empty : appointment.WhatId__r.SecondSurname__pc,
                            sdocument = appointment.WhatId__r.DocumentNumber__c,
                            sdocumenttype = appointment.WhatId__r.DocumentType__c,
                            sservicename = serviceByGroup.ServiceId__r.Name,
                            scups = serviceByGroup.ServiceId__r.Code__c,
                            iage = Convert.ToInt32(appointment.WhatId__r.Age2__pc.Value),
                        };
                        oDAC.CreateAppointmentRecord(consentimiento);
                    }
                }
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
                consentimiento = null;
                serviceByGroup = null;
                queryOptions = null;
                queryResult = null;
            }
        }

        public bool IsRhb(Appointment__c appointment)
        {
            List<string> sgroups = new List<string>
            {
                "CALIDAD DE VIDA",
                "EJERCICIO FUNCIONAL",
                "EVALUACIÓN COMPOSICIÓN CORPORAL",
                "F2 SESION RHB",
                "F1 SESION RHB",
                "MOVIMIENTO CONSCIENTE",
                "MTO-SESION RHB",
                "PACIENTE EXPERTO",
                "PSICOLOGIA EDUCACION GRUPAL",
                "RHB NUTRICION 1VEZ",
                "RHB NUTRICION CONTROL",
                "RHB PSICOLOGIA 1VEZ",
                "RHB PSICOLOGIA CONTROL",
                "SESIÓN 6CM",
                "SESION EDUCACION GRUPAL TALLER",
                "SESION RHB DANZA",
                "SESION RHB EGRESO-SST-CALIDAD",
                "SESION RHB FUERZA MUSCULAR",
                "SESION RHB INGRESO HARBOR-SST-CALIDAD",
                "SESION RHB POSTRASPLANTE",
                "SESION RHB PRETRASPLANTE",
                "SESION-RUMBOTERAPIA",
                "SESIÓN-TAI CHI",
                "SESIÓN YOGA",
            };
            return (sgroups.Any(name => appointment.GroupId__r.Name.Contains(name)) && (DateTime.Now.Hour >= 6 && DateTime.Now.Hour < 17) && ((int)DateTime.Now.DayOfWeek >= 1 && (int)DateTime.Now.DayOfWeek <= 5));
        }

        /// <summary>
        /// Método para validar la hora de llegada del paciente
        /// </summary>
        /// <param name="oAppointment">Objeto cita</param>
        /// <returns>Entero con el tipo de cola para la cita</returns>
        public int ValidateAppointment(Appointment__c oAppointment, bool bisremote = false, int idistance = 0)
        {
            DateTime dateTime = oAppointment.StartDatetime__c.Value;
            DateTime dtNow = DateTime.Now;
            if (!bisremote)
            {
                if (dtNow < dateTime.AddMinutes(-10))
                {
                    return 9;
                }
                else if (dtNow >= dateTime.AddMinutes(-10) && dtNow <= dateTime.AddMinutes(10))
                {
                    return 10;
                }
                else
                {
                    return 0;
                }
            }
            else
            {
                //TODO: Tengo que definir bien cuáles son las colas q van a atender los turnos remotos con el fin de agregar los id en los retornos del método
                if (dtNow < dateTime.AddMinutes(-10))
                {
                    return 23;
                }
                else if (dtNow >= dateTime.AddMinutes(-5) && dtNow <= dateTime.AddMinutes(5) && idistance >= 10)
                {
                    return 23;
                }
                else
                {
                    return 0;
                }
            }
        }

        /// <summary>
        /// Método para obtener el tipo de plan del paciente
        /// </summary>
        /// <param name="sIdPlan">String id del plan</param>
        /// <param name="soapClient">Objeto soap</param>
        /// <param name="sessionHeader">Objeto cabecera de sesión</param>
        /// <param name="queryOptions">Objeto opciones de consulta</param>
        /// <param name="mruHeader">Objeto null</param>
        /// <returns>Entero con el número del plan para el Digiturno</returns>
        private int GetPlan(string sIdPlan, SoapClient soapClient, SessionHeader sessionHeader, QueryOptions queryOptions, MruHeader mruHeader)
        {
            StringBuilder sQuery = new StringBuilder("SELECT PlanType__c, Name FROM HealthCarePlan__c WHERE Id = '");
            sQuery.Append(sIdPlan);
            sQuery.Append("'");
            QueryResult queryResult = new QueryResult();
            soapClient.query(sessionHeader, queryOptions, mruHeader, null, sQuery.ToString(), out queryResult);
            if (queryResult.size != 0)
            {
                HealthCarePlan__c healthCarePlan = queryResult.records[0] as HealthCarePlan__c;
                if (healthCarePlan.Name.Contains("FCI"))
                {
                    if (healthCarePlan.Name.Contains("ECOPETROL"))
                    {
                        return 14;
                    }
                    else
                    {
                        return 11;
                    }
                }
                else if (healthCarePlan.PlanType__c == "POS") return 13;
                else if (healthCarePlan.PlanType__c == "PREPAGADO") return 14;
                else return 12;
            }
            return 13;
        }

        /// <summary>
        /// Método para obtener la unidad del paciente
        /// </summary>
        /// <param name="sPlanName">String nombre del plan</param>
        /// <param name="sGroupName">String nombre de la etiqueta (grupo de servicios)</param>
        /// <param name="bNeedsInvoice">Boolean que indica si el servicio requiere pasar por caja o no</param>
        /// <param name="sSchedule">String nombre de la agenda</param>
        /// <returns>Entero con la unidad del paciente para el Digiturno</returns>
        public int GetUnit(string sPlanName, string sGroupName, bool bNeedsInvoice, string sSchedule, double iAge)
        {
            if (sSchedule.Contains("SALA ANESTESIA GENERAL") || sSchedule.Contains("SALA ANESTESIA LOCAL") || sPlanName.Contains("VASCULAR")
                || sPlanName.Contains("HTP") || sSchedule.Contains("SALA SEDACI") || sPlanName.Contains("FNC CHEQUEO") || sSchedule.Contains("ENTREGA DE MEDICAMENTOS"))
            {
                return 19;
            }
            /*else if (bNeedsInvoice || sGroupName.Contains("PARCHE") || sGroupName.Contains("PPD"))
            {
                return 18;
            }*/
            else if (sPlanName.Contains("ASMAIRE") || sPlanName.Contains("AIREPOC"))
            {
                return 16;
            }
            else if ((sGroupName.Contains("RHB") || sSchedule.ToUpper().Contains("CORPORAL") || sSchedule.ToUpper().Contains("TALLERES")) && !sSchedule.Contains("C6M") && !sGroupName.Contains("ERGO"))
            {
                return 17;
            }
            /*else if (sGroupName.Contains("SUEÑO") || sGroupName.Contains("CPAP") || sGroupName.Contains("ACTIGRAF")
                || sGroupName.Contains("POLIGRA") || sGroupName.Contains("OROFAR") || sGroupName.Contains("BASAL") || sGroupName.Contains("CAPNOGR"))
            {
                return 21;
            }
            */
            else if ((sSchedule.Contains("PFP") || sSchedule.ToUpper().Contains("VMAX")) && iAge >= 18)
            {
                return 22;
            }
            else
            {
                return 15;
            }
        }  

        /// <summary>
        /// Método que valida si la cita con un plan y grupo de servicios requiere q se le asigne el facturado automáticamente
        /// </summary>
        /// <param name="sPlan">String nombre del plan</param>
        /// <param name="sGroup">String nombre del grupo de servicios</param>
        /// <param name="sAuthorization">String número de autorización</param>
        /// <param name="sCoPayment">Boolean requiere copago</param>
        /// <param name="iAge">Entero edad del paciente</param>
        /// <returns>Boleano que indica si la cita requiere o no el facturado</returns>
        public bool NeedsInvoice(string sPlan, string sGroup, string sAuthorization, bool bCoPayment, double iAge)
        {            
            if (sPlan.Contains("PROTOCOLO"))
            {
                return true;
            }
            else if (sPlan.Contains("CORTESIA"))
            {
                return true;
            }
            else if (sPlan.Contains("REPROCESO"))
            {
                return true;
            }
            else if (sPlan.Contains("SOCIAL") && (sGroup.Contains("TUBERCULOSIS CONTROL") || sGroup.Contains("CONS") || sGroup.Contains("TBC")))
            {
                return true;
            }           
            else if (sGroup.Contains("LECTURA PPD") || sGroup.Contains("LECTURA DE PARCHE") || sGroup.Contains("VALORACIÓN FBC") || sGroup.Contains("VALORACIÓN ANESTESIA"))
            {
                return true;
            }
            else if (sGroup.Contains("PRECONSULTA CEASMA") && (sPlan.Contains("AIREPOC") || sPlan.Contains("ASMAIRE")))
            {
                return !sPlan.Contains("FAMISANAR ASMAIRE");
            }
            else if (sGroup.Contains("EDUCACI") && (sPlan.Contains("AIREPOC") || sPlan.Contains("ASMAIRE") || sPlan.Contains("HTP")))
            {
                if (sPlan.Contains("FAMISANAR ASMAIRE"))
                {
                    return false;
                }
                else if (sPlan.Contains("COMPENSAR HTP"))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else if (sPlan.Contains("AIREPOC") && !string.IsNullOrEmpty(sAuthorization))
            {
                return !sPlan.Contains("COLMEDICA");
            }
            else if (sPlan.Contains("ASMAIRE") && !string.IsNullOrEmpty(sAuthorization))
            {
                return !sPlan.Contains("FAMISANAR");
            }
            else if (sPlan.EqualsAnyOf("FNC SANITAS VMI", "FNC SURA EPS VMI", "FNC SURA ARL VMI"))
            {
                return true;
            }
            else
            {
                using (Inspira oDAC = new Inspira())
                {
                    oDAC.sConnection = FNCSalesforce.Properties.Settings.Default.FNCConnection;
                    bool bResult = oDAC.NeedsInvoice(sPlan, sGroup);
                    return (bResult && !string.IsNullOrEmpty(sAuthorization));
                }
            }
        }

        /// <summary>
        /// Metodo que obtiene el método de redireccionamiento a la sala para el Digiturno
        /// </summary>
        /// <param name="iAge">Doble edad del  paciente</param>
        /// <param name="sGroup">String nombre del grupo</param>
        /// <param name="bInvoice">Boleano el servicio no requiere facturación</param>
        /// <param name="bIsVip">Boleano es paciente VIP</param>
        /// <param name="sPlan">String nombre del plan</param>
        /// <param name="bCopayment">Boleano que indica si tiene copago o no</param>
        /// <param name="sSchedule">String nombre de la agenda</param>
        /// <param name="sAgreement">String nombre del convenio</param>
        /// <returns>String lugar de redireccionaiento</returns>
        public string GetDirection(double iAge, string sGroup, bool bInvoice, bool bIsVip, string sPlan, bool bCopayment, string sSchedule, string sAgreement)
        {
            if ((int)DateTime.Now.DayOfWeek == 0 || (int)DateTime.Now.DayOfWeek == 6)
            {
                if (sAgreement.Contains("CARDIO INDANTIL") && (sPlan.Contains("FCI ECOPETROL ASMAIRE") || sPlan.Contains("FCI ECOPETROL AIREPOC")))
                {
                    return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
                }
                else if (sAgreement.Contains("CARDIO INFANTIL"))
                {
                    return FNCSalesforce.Properties.Settings.Default.MessageFCI;
                }
                else
                {
                    return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
                }

            }
            else if ((int)DateTime.Now.DayOfWeek >= 1 && (int)DateTime.Now.DayOfWeek <= 5)
            {
                if (DateTime.Now.Hour >= 17)
                {
                    return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
                }
                else
                {
                    if (sAgreement.Contains("CARDIO INFANTIL") && bCopayment)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
                    }
                    else if (sAgreement.Contains("CARDIO INDANTIL") && (sPlan.Contains("FCI ECOPETROL ASMAIRE") || sPlan.Contains("FCI ECOPETROL AIREPOC")))
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
                    }
                    else if (sAgreement.Contains("CARDIO INFANTIL"))
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFCI;
                    }
                    else if ((sSchedule.Contains("SALA ANESTESIA GENERAL") || sSchedule.Contains("SALA ANESTESIA LOCAL") || sSchedule.Contains("SALA SEDACI")) && !bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageAdmissionsCashDesk;
                    }

                    else if ((sSchedule.Contains("VMAX") || sSchedule.Contains("PFP")) && bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk3;
                        //return FNCSalesforce.Properties.Settings.Default.MessageFloor2;
                    }
                    else if (sSchedule.Contains("SUEÑO") || sGroup.Contains("SUEÑO"))
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageSleepFloorCashDesk;
                    }
                    else if (sSchedule.Contains("PFP") || sSchedule.ToUpper().Contains("VMAX"))
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk3;
                        //return FNCSalesforce.Properties.Settings.Default.MessageFloor2;
                    }
                    else if ((sPlan.Contains("FNC PAF EPS SURA") || sPlan.Contains("FNC PAF EPS-S SURA")) && !bCopayment && iAge < 18)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloor3;
                    }
                    else if ((sPlan.Contains("FNC PAF EPS SURA") || sPlan.Contains("FNC PAF EPS-S SURA")) && !bCopayment && iAge > 18)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloor2;
                    }
                    else if (iAge < 18 && bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloor3;
                    }
                    else if (iAge < 18 && !bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk3;
                    }
                    else if (iAge > 18 && bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloor2;
                    }
                    else if (bIsVip && bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageVIP;
                    }
                    else if ((sGroup.Contains("AIREPOC") || sGroup.Contains("ASMARE") || sGroup.Contains("RHB")) && bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloor4;
                    }
                    else if ((sGroup.Contains("AIREPOC") || sGroup.Contains("ASMARE") || sGroup.Contains("RHB")) && !bInvoice)
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
                    }
                    else if (sGroup.Contains("RHB") || sSchedule.ToUpper().Contains("CORPORAL") || sSchedule.ToUpper().Contains("TALLERES"))
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloor4;
                    }    
                    else
                    {
                        return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
                    }
                }

            }
            return FNCSalesforce.Properties.Settings.Default.MessageFloorCashDesk2;
        }

        public int GetRoom(double iAge, string sGroup, bool bInvoice, bool bIsVip, string sPlan, bool bCopayment, string sSchedule, string sAgreement)
        {
            int iroom = 3;
            if ((int)DateTime.Now.DayOfWeek == 0 || (int)DateTime.Now.DayOfWeek == 6)
            {
                iroom = 3;
            }
            else if ((int)DateTime.Now.DayOfWeek >= 1 && (int)DateTime.Now.DayOfWeek <= 5)
            {
                if (Tools.EsMayorQueHoraEspecificada(17, 30))
                {
                    iroom = 3;
                }
                else if (Tools.EsMayorQueHoraEspecificada(5, 30) && DateTime.Now.Hour < 16)
                {
                    if (iAge < 18)
                    {
                        iroom = 5;
                    }
                    else if (iAge >= 18 && sSchedule.Contains("PFP"))
                    {
                        iroom = 5;
                    }
                }
            }
            return iroom;
        }

        /// <summary>
        /// Método para verificar si una cita requiere el estado de preconsulta
        /// </summary>
        /// <param name="sGroup">String nombre del grupo de servicios</param>
        /// <param name="iAge">Entero edad del paciente</param>
        /// <param name="records">Arreglo de objetos citas para validación</param>
        /// <param name="bNeedsPre">Boleano que indica si el grupo de servicios requiere preconsulta</param>
        /// <returns>Boleano verdadero si la cita requiere preconsulta o falso en caso contrario</returns>
        public bool NeeedsPreAssessment(string sGroup, int iAge, string sSchedule, sObject[] records, bool bNeedsPre)
        {
            bool bFlag = false;
            if (iAge < 18)
            {
                for (int i = 0; i < records.Length && !bFlag; i++)
                {
                    Appointment__c appointment = records[i] as Appointment__c;
                    bFlag = (appointment.AgendaId__r.Name.ToUpper().Contains("PFP") || appointment.GroupId__r.Name.Contains("PSICOLOGIA"));
                }
            }
            if (bFlag)
            {
                return false;
            }
            else if (!bNeedsPre)
            {
                return false;
            }
            else if (iAge < 18 && bNeedsPre)
            {
                return true;
            }
            return false;
            /*else
            {
                if (iAge < 18)
                {
                    bool bFlag = false;
                    for (int i = 0; i < records.Length && !bFlag; i++)
                    {
                        Appointment__c appointment = records[i] as Appointment__c;
                        bFlag = (appointment.AgendaId__r.Name.Contains("Vmax") || appointment.GroupId__r.Name.Contains("PSICOLOGIA"));
                    }
                    return !bFlag;
                }
                return false;
            }*/
        }

        /// <summary>
        /// Método para generar turno de información en caso de que el paciente no tenga citas
        /// </summary>
        /// <param name="sFirstname">String primer nombre del paciente</param>
        /// <param name="sSecondname">String segundo nombre del paciente</param>
        /// <param name="sSurname">String primer apellido del paciente</param>
        /// <param name="sSecondsurname">String segundo apellido del paciente</param>
        private void GenerateInfoTurn(Digiturno5 digiturno5, string sdocument, string sdocumenttype)
        {
            Turno turn = new Turno();
            ServicioSelectorClient servicioSelector = new ServicioSelectorClient();
            Cola[] queue = null;
            UsuarioClienteWS oUser = null;
            Nodo nodo = new Nodo();
            Elemento elemento = new Elemento();
            //elemento.Nivel = new Nivel() { }
            //Cola ola = new Cola() { a }
            try
            {
                queue = new Cola[] { new Cola() { Id = FNCSalesforce.Properties.Settings.Default.InfoQueue } };
                oUser = new UsuarioClienteWS()
                {
                    PrimerNombre = digiturno5.oPatient.sfirstname,
                    SegundoNombre = digiturno5.oPatient.ssecondname,
                    PrimerApellido = digiturno5.oPatient.sfirstsurname,
                    SegundoApellido = digiturno5.oPatient.ssecondsurname,
                    Identificacion = sdocument,
                    TipoIdentificacion = Tools.GetDocumentType(sdocumenttype, true),
                };
                turn = servicioSelector.CrearTurno(1, 8, EntesDelSistemaTipo.Deconocido, 0, queue, false, "Turno de información por error de cita", oUser, "FNC", true);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
            }
            finally
            {
                servicioSelector = null;
                turn = null;
                oUser = null;
                queue = null;
            }
        }

        #endregion

        #region Integracion con sistema de distribución de horarios

        /// <summary>
        /// Método para obtener la suma de tiempos de duración de actividades de un colaborador en Salesforce.
        /// </summary>
        /// <param name="sCost">String centro de costos</param>
        /// <param name="iMonth">Entero mes para la búsqueda</param>
        /// <param name="iYear">Entero año para la búsqueda</param>
        /// <param name="sSession">String session id del login</param>
        /// <param name="sUrl">String url generada del login</param>
        /// <returns>Lista genérica con el resultado de la consulta, contiene el código del centro de costos y la suma de la duración en minutos</returns>
        public List<Generic> GetTimeDistribution(string sCost, int iMonth, int iYear, string sSession, string sUrl)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            AggregateResult appointment = null;
            List<Generic> lGeneric = new List<Generic>();
            Generic generic = null;
            sQuery.Append("SELECT AgendaId__r.ProfessionalId__r.DocumentNumber__c, AgendaId__r.ProfessionalId__r.Name, GroupId__r.CostCenterId__r.Code__c, SUM(DurationMinutes__c) Duracion FROM Appointment__c");
            sQuery.Append(" WHERE CALENDAR_MONTH(ActivityDate__c) = ");
            sQuery.Append(iMonth.ToString());
            sQuery.Append(" AND CALENDAR_YEAR(ActivityDate__c) = ");
            sQuery.Append(iYear.ToString());
            sQuery.Append(" AND AgendaId__r.CostCenterId__r.Code__c = '");
            sQuery.Append(sCost);
            sQuery.Append("' GROUP BY GroupId__r.CostCenterId__r.Code__c, AgendaId__r.ProfessionalId__r.DocumentNumber__c, AgendaId__r.ProfessionalId__r.Name");
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
            if (queryResult.size != 0)
            {
                sObject[] records = queryResult.records;
                for (int i = 0; i < records.Length; i++)
                {
                    appointment = records[i] as AggregateResult;
                    generic = new Generic()
                    {
                        scode = appointment.Any[0].InnerText,
                        sname = appointment.Any[1].InnerText,
                        sfilter = appointment.Any[2].InnerText,
                        dextra2 = Convert.ToDouble(appointment.Any[3].InnerText),
                    };
                    lGeneric.Add(generic);

                }
            }
            return lGeneric;
        }

        #endregion

        #region Integración sistema de envío de historias clínicas teleconsulta

        /// <summary>
        /// Método para obtener el listado de pacientes que se atendieron en el día acorde a sus documentos
        /// </summary>
        /// <param name="sSession">String sesión de Salesforce</param>
        /// <param name="sUrl">String url del endpoint de Salesforce</param>
        /// <param name="asPatients">String lista separada por comas de los pacientes a buscar</param>
        /// <returns>Lista genérica de pacientes con los pacientes con citas cumplidas en el día</returns>
        public List<Patient> GetTodayPatients(string sSession, string sUrl, string asPatients)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Patient> patients = new List<Patient>();
            Patient patient = null;
            Appointment__c appointment = null;
            sQuery.Append("SELECT WhatId__r.PersonEmail, WhatId__r.DocumentNumber__c FROM Appointment__c WHERE ActivityDate__c = ");
            sQuery.Append(DateTime.Now.ToString("yyyy-MM-dd"));
            //sQuery.Append("2021-06-02");
            sQuery.Append(" AND Status__c IN ('Cumplida', 'Facturada') AND WhatId__r.DocumentNumber__c IN (");
            sQuery.Append(asPatients);
            sQuery.Append(")");
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size != 0)
                {
                    sObject[] records = queryResult.records;
                    for (int i = 0; i < records.Length; i++)
                    {
                        if (records[i] != null)
                        {
                            appointment = records[i] as Appointment__c;
                            patient = new Patient()
                            {
                                sdocument = appointment.WhatId__r.DocumentNumber__c,
                                smail = appointment.WhatId__r.PersonEmail,
                            };
                            patients.Add(patient);
                        }
                    }
                }
                return patients;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                appointment = null;
                patient = null;
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                sQuery = null;
                mruHeader = null;
            }
        }

        #endregion

        #region Integración SCSE

        /// <summary>
        /// Método para actualizar las tarifas en Salesforce
        /// </summary>
        /// <param name="lInspiraTemporal">Lista genérica objeto de integración</param>
        public List<InspiraTemporal> UpsertRates(List<InspiraTemporal> lInspiraTemporal)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            LogSincroniza logSincroniza = null;
            Rate__c rate = null;
            List<sObject> lUpdate = new List<sObject>();
            List<sObject> lInsert = new List<sObject>();
            List<InspiraTemporal> lResult = new List<InspiraTemporal>();
            List<Rate__c> lRates = this.GetRates();
            string svalues = string.Join(", ", Tools.SerializeObject(lInspiraTemporal, "code"));
            sQuery.Append("SELECT id, Code__c, Name FROM Rate__c WHERE Code__c IN (");
            sQuery.Append(svalues);
            sQuery.Append(")");
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                foreach (InspiraTemporal item in lInspiraTemporal)
                {
                    item.sid = this.GetRateId(lRates, item.scod);
                    rate = new Rate__c();
                    rate.Name = item.snombre;
                    rate.Code__c = item.scod;
                    rate.IsActive__cSpecified = true;
                    rate.IsActive__c = (item.cactivo == 'S');
                    if (!string.IsNullOrEmpty(item.sid))
                    {
                        rate.Id = item.sid;
                        lUpdate.Add(rate);
                        this.lListUpdate.Add(rate);
                    }
                    else
                    {
                        lInsert.Add(rate);
                        this.lListInsert.Add(rate);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Tarifas");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Tarifas");
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] sObjects = queryResult.records;
                    lResult = this.GetTableResult(sObjects, "TARIFA");
                }
                return lResult;
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Tarifas" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                rate = null;
                lUpdate = lInsert = null;
            }
        }

        /// <summary>
        /// Método para actualizar los planes en Salesforce
        /// </summary>
        /// <param name="lInspiraTemporal">Lista genérica objeto de integración</param>
        public List<InspiraTemporal> UpsertHealthCarePlan(List<InspiraTemporal> lInspiraTemporal)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            LogSincroniza logSincroniza = null;
            HealthCarePlan__c plan = null;
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            List<InspiraTemporal> lResult = new List<InspiraTemporal>();
            List<HealthCarePlan__c> lPlans = new List<HealthCarePlan__c>();
            string svalues = string.Join(", ", Tools.SerializeObject(lInspiraTemporal, "code"));
            sQuery.Append("SELECT id, Code__c, Name FROM HealthCarePlan__c WHERE Code__c IN (");
            sQuery.Append(svalues);
            sQuery.Append(")");
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                lPlans = this.GetPlans();
                foreach (InspiraTemporal item in lInspiraTemporal)
                {
                    plan = new HealthCarePlan__c();
                    item.sid = this.GetPlanId(lPlans, item.scod);
                    plan.Code__c = item.scod;
                    plan.Name = item.snombre;
                    plan.IsActive__cSpecified = true;
                    plan.IsActive__c = (item.cactivo == 'S');
                    plan.PlanType__c = item.sparametro1;
                    plan.StartDate__cSpecified = true;
                    plan.StartDate__c = DateTime.Now;
                    plan.EndDate__cSpecified = true;
                    plan.EndDate__c = new DateTime(DateTime.Now.Year, 12, 31);
                    if (!string.IsNullOrEmpty(item.sid))
                    {
                        plan.Id = item.sid;
                        lUpdate.Add(plan);
                        this.lListUpdate.Add(plan);
                    }
                    else
                    {
                        lInsert.Add(plan);
                        this.lListInsert.Add(plan);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Plan");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Plan");
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] sObjects = queryResult.records;
                    lResult = this.GetTableResult(sObjects, "PLAN");
                }
                return lResult;
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Plan" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                plan = null;
                lUpdate = lInsert = null;
            }
        }

        /// <summary>
        /// Método para obtener el ID de tipo de registro para cuentas
        /// </summary>
        /// <param name="soapClient"></param>
        /// <returns></returns>
        private string GetCompanyRecordType(SoapClient soapClient)
        {
            SessionHeader sessionHeader = new SessionHeader() { sessionId = this.sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder("SELECT Id, Name FROM RecordType WHERE SobjectType = 'Account' AND Name = 'Business'");
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            RecordType recordType = null;
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(this.sUrl);
            soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
            if (queryResult.size > 0)
            {
                sObject[] records = queryResult.records;
                recordType = records[0] as RecordType;
                return recordType.Id;
            }
            return string.Empty;
        }

        private List<Account> GetAccounts(SoapClient soapClient, string svalues, string srecordtype)
        {
            StringBuilder sQuery = new StringBuilder();
            QueryResult queryResult = new QueryResult();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            LogSincroniza logSincroniza = null;
            sQuery.Append("SELECT Id, DocumentNumber__c FROM Account WHERE DocumentNumber__c IN (");
            sQuery.Append(svalues);
            sQuery.Append(") AND RecordTypeId = '");
            sQuery.Append(srecordtype);
            sQuery.Append("'");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    return records.OfType<Account>().ToList();
                }
                return new List<Account>();
            }
            catch (Exception ex)
            {

                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Convenios" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                sQuery = null;
                sessionHeader = null;
                queryOptions = null;
                logSincroniza = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /// <summary>
        /// Método para actualizar los convenios en Salesforce
        /// </summary>
        /// <param name="lInspiraTemporal">Lista genérica objeto de integración</param>
        public List<InspiraTemporal> UpsertAgreements(List<InspiraTemporal> lInspiraTemporal)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            LogSincroniza logSincroniza = null;
            Agreement__c agreement = null;
            List<sObject> lUpdate = new List<sObject>();
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lInsertAccounts = new List<sObject>();
            List<sObject> lUpdateAccounts = new List<sObject>();
            List<InspiraTemporal> lResult = new List<InspiraTemporal>();
            List<Account> accounts = null;
            Account account = null;
            string svalues = string.Join(", ", Tools.SerializeObject(lInspiraTemporal, "code"));
            string svalues1 = string.Join(", ", Tools.SerializeObject(lInspiraTemporal, "nit"));
            sQuery.Append("SELECT id, Code__c, Name FROM Agreement__c WHERE Code__c IN (");
            sQuery.Append(svalues);
            sQuery.Append(")");
            string srecordtype = string.Empty;
            try
            {
                srecordtype = this.GetCompanyRecordType(soapClient);
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                accounts = this.GetAccounts(soapClient, svalues1, srecordtype);
                //lInspiraTemporal = lInspiraTemporal.GroupBy(x => x.scod).Select(y => y.First()).ToList();   
                foreach (InspiraTemporal item in lInspiraTemporal)
                {
                    account = accounts.FirstOrDefault(x => x.DocumentNumber__c == item.iparametro3.ToString());
                    if (account == null)
                    {
                        lInsertAccounts.Clear();
                        account = new Account()
                        {
                            RecordTypeId = srecordtype,
                            DocumentType__c = "NIT",
                            DocumentNumber__c = item.iparametro3.ToString(),
                            Address__c = item.sparametro1,
                            Phone = item.sparametro2,
                            CompanyType__c = "Aseguradora",
                            ERP_Code__c = item.scod,
                            Name = item.snombre,
                            IsActive__cSpecified = true,
                            IsActive__c = true,
                        };
                        lInsertAccounts.Add(account);
                        this.InsertValues(lInsertAccounts, soapClient, sessionHeader, "Cuenta");
                        accounts = this.GetAccounts(soapClient, svalues1, srecordtype);
                        account = accounts.FirstOrDefault(x => x.DocumentNumber__c == item.iparametro3.ToString());
                    }
                    agreement = new Agreement__c();
                    agreement.Code__c = item.scod;
                    agreement.Name = item.snombre;
                    agreement.IsActivo__cSpecified = true;
                    agreement.IsActivo__c = (item.cactivo == 'S');
                    agreement.CompanyId__c = account.Id;
                    if (!string.IsNullOrEmpty(item.sid))
                    {
                        agreement.Id = item.sid;
                        lUpdate.Add(agreement);
                        this.lListUpdate.Add(agreement);
                    }
                    else
                    {
                        lInsert.Add(agreement);
                        this.lListInsert.Add(agreement);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Convenios");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Convenios");
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] sObjects = queryResult.records;
                    lResult = this.GetTableResult(sObjects, "CONVENIO");
                }
                return lResult;
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Convenios" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                agreement = null;
                lUpdate = lInsert = lInsertAccounts = lUpdateAccounts = null;
            }
        }

        private List<InspiraTemporal> GetTableResult(sObject[] sObjects, string stable)
        {
            List<InspiraTemporal> linspiraTemporals = new List<InspiraTemporal>();
            InspiraTemporal inspiraTemporal = null;
            if (stable == "CONVENIO")
            {
                List<Agreement__c> agreements = sObjects.OfType<Agreement__c>().ToList();
                foreach (var item in agreements)
                {
                    inspiraTemporal = new InspiraTemporal()
                    {
                        scod = item.Code__c,
                        sid = item.Id,
                        snombre = item.Name,
                        stabla = stable,
                    };
                    linspiraTemporals.Add(inspiraTemporal);
                }
            }
            else if (stable == "TARIFA")
            {
                List<Rate__c> rates = sObjects.OfType<Rate__c>().ToList();
                foreach (var item in rates)
                {
                    inspiraTemporal = new InspiraTemporal()
                    {
                        scod = item.Code__c,
                        sid = item.Id,
                        snombre = item.Name,
                        stabla = stable,
                    };
                    linspiraTemporals.Add(inspiraTemporal);
                }

            }
            else if (stable == "PLAN")
            {
                List<HealthCarePlan__c> plans = sObjects.OfType<HealthCarePlan__c>().ToList();
                foreach (var item in plans)
                {
                    inspiraTemporal = new InspiraTemporal()
                    {
                        scod = item.Code__c,
                        sid = item.Id,
                        snombre = item.Name,
                        stabla = stable,
                    };
                    linspiraTemporals.Add(inspiraTemporal);
                }
            }
            else if (stable == "CENTRO")
            {
                List<CostCenter__c> costcenters = sObjects.OfType<CostCenter__c>().ToList();
                foreach (var item in costcenters)
                {
                    inspiraTemporal = new InspiraTemporal()
                    {
                        scod = item.Code__c,
                        sid = item.Id,
                        snombre = item.Name,
                        stabla = stable,
                    };
                    linspiraTemporals.Add(inspiraTemporal);
                }
            }
            else if (stable == "CONCEPTO")
            {
                List<Concept__c> concepts = sObjects.OfType<Concept__c>().ToList();
                foreach (var item in concepts)
                {
                    inspiraTemporal = new InspiraTemporal()
                    {
                        scod = item.Code__c,
                        sid = item.Id,
                        snombre = item.Name,
                        stabla = stable,
                    };
                    linspiraTemporals.Add(inspiraTemporal);
                }
            }
            else if (stable == "PRODUCTO")
            {
                List<Product__c> products = sObjects.OfType<Product__c>().ToList();
                foreach (var item in products)
                {
                    inspiraTemporal = new InspiraTemporal()
                    {
                        scod = item.Name,
                        sid = item.Id,
                        snombre = item.Name__c,
                        stabla = stable,
                    };
                    linspiraTemporals.Add(inspiraTemporal);
                }
            }
            return linspiraTemporals;
        }

        /// <summary>
        /// Método para actualizar los centros de costo en Salesforce
        /// </summary>
        /// <param name="lInspiraTemporal">Lista genérica objeto de integración</param>
        public List<InspiraTemporal> UpsertCostCenters(List<InspiraTemporal> lInspiraTemporal)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            LogSincroniza logSincroniza = null;
            CostCenter__c cost = null;
            List<sObject> lUpdate = new List<sObject>();
            List<sObject> lInsert = new List<sObject>();
            List<InspiraTemporal> lResult = new List<InspiraTemporal>();
            string svalues = string.Join(", ", Tools.SerializeObject(lInspiraTemporal, "code"));
            sQuery.Append("SELECT Id, Code__c, Name FROM CostCenter__c WHERE Code__c IN (");
            sQuery.Append(svalues);
            sQuery.Append(")");
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                foreach (InspiraTemporal item in lInspiraTemporal)
                {
                    cost = new CostCenter__c();
                    cost.Code__c = item.scod;
                    cost.Name = item.snombre;
                    cost.IsActive__cSpecified = true;
                    cost.IsActive__c = (item.cactivo == 'S');
                    cost.HealthcareOnly__cSpecified = true;
                    cost.HealthcareOnly__c = (item.sparametro1 == "true");
                    if (!string.IsNullOrEmpty(item.sid))
                    {
                        cost.Id = item.sid;
                        lUpdate.Add(cost);
                        this.lListUpdate.Add(cost);
                    }
                    else
                    {
                        lInsert.Add(cost);
                        this.lListInsert.Add(cost);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Centros de Costo");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Centros de Costo");
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] sObjects = queryResult.records;
                    lResult = this.GetTableResult(sObjects, "CENTRO");
                }
                return lResult;
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Centros de Costo" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                cost = null;
                lUpdate = null;
                lInsert = null;
            }
        }


        /// <summary>
        /// Método para sincronizar los conceptos de facturación
        /// </summary>
        /// <param name="lInspiraTemporal">Lista genérica con los elementos a sincronizar</param>
        /// <returns>Lista genérica con los elementos sincronizados</returns>
        public List<InspiraTemporal> UpsertConcept(List<InspiraTemporal> lInspiraTemporal)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            QueryResult queryResult1 = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            LogSincroniza logSincroniza = null;
            Concept__c concept = null;
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            List<InspiraTemporal> lResult = new List<InspiraTemporal>();
            string svalues = string.Join(", ", Tools.SerializeObject(lInspiraTemporal, "code"));
            sQuery.Append("SELECT id, Code__c, Name FROM Concept__c WHERE Code__c IN (");
            sQuery.Append(svalues);
            sQuery.Append(")");
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, "SELECT Id, Code__c FROM FunctionalUnit__c WHERE Enabled__c = true", out queryResult1);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                foreach (InspiraTemporal item in lInspiraTemporal)
                {
                    concept = new Concept__c()
                    {
                        Code__c = item.scod,
                        Name = item.snombre,
                        HasService__cSpecified = (item.iparametro3 == 1),
                        IsDisccount__cSpecified = false,
                        Type__c = item.sparametro2,
                        FunctionalUnit__c = this.GetId(queryResult1, item.sparametro1, "Unit"),
                        IsActive__cSpecified = true,
                        IsActive__c = (item.cactivo == 'S'),
                    };
                    if (!string.IsNullOrEmpty(item.sid))
                    {
                        concept.Id = item.sid;
                        lUpdate.Add(concept);
                        this.lListUpdate.Add(concept);
                    }
                    else
                    {
                        lInsert.Add(concept);
                        this.lListInsert.Add(concept);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Concepto");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Concepto");
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] sObjects = queryResult.records;
                    lResult = this.GetTableResult(sObjects, "CONCEPTO");
                }
                return lResult;
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Concepto" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                concept = null;
                lUpdate = lInsert = null;
            }
        }

        public void UpsertFunctionalUnit(List<InspiraTemporal> lInspiraTemporal)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            LogSincroniza logSincroniza = null;
            FunctionalUnit__c unit = null;
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            string svalues = string.Join(",", Tools.SerializeObject(lInspiraTemporal, "code"));
            sQuery.Append("SELECT id, Code__c FROM FunctionalUnit__c WHERE Code__c IN (");
            sQuery.Append(svalues);
            sQuery.Append(")");
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                foreach (InspiraTemporal item in lInspiraTemporal)
                {
                    unit = new FunctionalUnit__c()
                    {
                        Code__c = item.scod,
                        Name = item.snombre,
                        Id = this.GetId(queryResult, item.scod, "Unit"),
                        Enabled__cSpecified = true,
                        Enabled__c = true,
                    };
                    if (!string.IsNullOrEmpty(unit.Id))
                    {
                        lUpdate.Add(unit);
                        this.lListUpdate.Add(unit);
                    }
                    else
                    {
                        lInsert.Add(unit);
                        this.lListInsert.Add(unit);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Unidad Funcional");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Unidad Funcional");
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Unidad Funcional" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                unit = null;
                lUpdate = lInsert = null;
            }
        }
        /// <summary>
        /// Método para sincronizar los productos de la Neumológica
        /// </summary>
        /// <param name="lInspiraTemporal">Lista genérica con los productos a sincronizar</param>
        /// <returns>Lista genérica con los productos sincronizados</returns>
        public List<InspiraTemporal> UpsertProducts(List<InspiraTemporal> lInspiraTemporal)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            LogSincroniza logSincroniza = null;
            Product__c product = null;
            PackageVersion[] packageVersions = null;
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            List<InspiraTemporal> lResult = new List<InspiraTemporal>();
            string svalues = string.Join(", ", Tools.SerializeObject(lInspiraTemporal, "code"));
            sQuery.Append("SELECT id, Name, Name__c FROM Product__c WHERE Name IN (");
            sQuery.Append(svalues);
            sQuery.Append(")");
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                //soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                foreach (InspiraTemporal item in lInspiraTemporal)
                {
                    product = new Product__c()
                    {
                        Name = item.scod,
                        Name__c = item.snombre,
                        IsProcedure__cSpecified = true,
                        IsProcedure__c = !item.snombre.ToUpper().Contains("CONSULTA"),
                        IsNoPos__cSpecified = true,
                        IsNoPos__c = (item.sparametro2 != "S"),
                        Billable__cSpecified = true,
                        Billable__c = true,
                        Id = this.GetId(queryResult, item.scod, "Product"),
                        Type__c = item.sparametro1,
                        Enabled__cSpecified = true,
                        Enabled__c = (item.cactivo == 'S'),
                    };
                    if (!string.IsNullOrEmpty(product.Id))
                    {
                        lUpdate.Add(product);
                        this.lListUpdate.Add(product);
                    }
                    else
                    {
                        lInsert.Add(product);
                        this.lListInsert.Add(product);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Producto");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Producto");
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, sQuery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] sObjects = queryResult.records;
                    lResult = this.GetTableResult(sObjects, "PRODUCTO");
                }
                return lResult;
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Producto" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                product = null;
                lUpdate = lInsert = null;
            }
        }

        public void InsertCostUnit(List<Generic> lGeneric)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            LogSincroniza logSincroniza = null;
            List<CostCenter__c> lCost = null;
            List<FunctionalUnit__c> lUnit = null;
            List<sObject> lInsert = new List<sObject>();
            CostCenterByUnit__c costCenterByUnit = null;
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                this.lListInsert = new List<sObject>();
                lUnit = this.GetUnits();
                lCost = this.GetCosts();
                foreach (Generic item in lGeneric)
                {
                    costCenterByUnit = new CostCenterByUnit__c()
                    {
                        CostCenterId__c = this.GetCostId(lCost, item.sname),
                        FunctionalUnitId__c = this.GetUnitId(lUnit, item.scode),
                    };
                    var tmp = new CostCenterByUnit__c();
                    tmp.CostCenterId__r = new CostCenter__c() { Code__c = item.sname };
                    tmp.FunctionalUnitId__r = new FunctionalUnit__c() { Code__c = item.scode };
                    lInsert.Add(costCenterByUnit);
                    this.lListInsert.Add(tmp);
                }
                this.InsertValues(lInsert, soapClient, sessionHeader, "Centro de costo por unidad funcional");
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Centro de costo por unidad funcional" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                lUnit = null;
                lCost = null;
                costCenterByUnit = null;
            }
        }

        public void UpsertAgreementRates(List<InspiraTemporal> ltarifaEmpresas)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            LogSincroniza logSincroniza = null;
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            Plan__c ratebyagreement = null;
            List<Plan__c> lratesbyagreement = null;
            List<Rate__c> lRate = null;
            List<Agreement__c> lAgreement = null;
            List<HealthCarePlan__c> lPlan = null;
            string sId = string.Empty;
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                lRate = this.GetRates();
                lPlan = this.GetPlans();
                lAgreement = this.GetAgreements();
                lratesbyagreement = this.GetRatesByAgreement();
                foreach (InspiraTemporal tarifaEmpresa in ltarifaEmpresas)
                {
                    sId = this.FindAgreementRateInList(tarifaEmpresa, lratesbyagreement);
                    ratebyagreement = new Plan__c();
                    var item = new Plan__c();
                    item.RateId__r = new Rate__c() { Code__c = tarifaEmpresa.sparametro1 };
                    item.AgreementId__r = new Agreement__c() { Code__c = tarifaEmpresa.scod };
                    item.HealthCarePlanId__r = new HealthCarePlan__c() { Code__c = tarifaEmpresa.sparametro2 };
                    if (string.IsNullOrEmpty(sId))
                    {
                        ratebyagreement.IsActivo__cSpecified = true;
                        ratebyagreement.IsActivo__c = (tarifaEmpresa.cactivo == 'S');
                        ratebyagreement.StartDate__cSpecified = true;
                        ratebyagreement.StartDate__c = DateTime.Now;
                        ratebyagreement.EndDate__cSpecified = true;
                        ratebyagreement.EndDate__c = new DateTime(DateTime.Now.Year, 12, 31);
                        ratebyagreement.RateId__c = this.GetRateId(lRate, tarifaEmpresa.snombre);
                        ratebyagreement.AgreementId__c = this.GetAgreementd(lAgreement, tarifaEmpresa.santerior);
                        ratebyagreement.HealthCarePlanId__c = this.GetPlanId(lPlan, tarifaEmpresa.stabla);
                        ratebyagreement.Name = this.GetPlanId(lPlan, tarifaEmpresa.sparametro2, "name");
                        ratebyagreement.Code__c = (tarifaEmpresa.sparametro2 + "-" + tarifaEmpresa.scod + "-" + tarifaEmpresa.sparametro1);
                        if (!string.IsNullOrEmpty(ratebyagreement.RateId__c) && !string.IsNullOrEmpty(ratebyagreement.AgreementId__c) && !string.IsNullOrEmpty(ratebyagreement.HealthCarePlanId__c))
                        {
                            lInsert.Add(ratebyagreement);
                            this.lListInsert.Add(item);
                        }
                    }
                    else
                    {
                        if (tarifaEmpresa.soperacion == "D")
                        {
                            ratebyagreement.IsActivo__cSpecified = true;
                            ratebyagreement.IsActivo__c = false;
                            ratebyagreement.Id = sId;
                            lUpdate.Add(ratebyagreement);
                        }
                        this.lListUpdate.Add(item);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Tarifas por empresa");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Tarifas por empresa");
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Tarifas por empresa" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                lratesbyagreement = null;
                lUpdate = lInsert = null;
                lRate = null;
                lAgreement = null;
                lPlan = null;
                ratebyagreement = null;
            }
        }

        /// <summary>
        /// Método para actualizar las tarifas por productos
        /// </summary>
        /// <param name="ltarifaProductos">Lista genérica de tarifas por producto</param>
        public void UpsertProductRates(List<TarifaProducto> ltarifaProductos)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            LogSincroniza logSincroniza = null;
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            RateByConceptByProduct__c rateByConceptByProduct = null;
            List<RateByConceptByProduct__c> lrateByConceptByProduct = new List<RateByConceptByProduct__c>();
            List<Rate__c> lRate = null;
            List<CostCenter__c> lCost = null;
            List<Concept__c> lConcept = null;
            List<Product__c> lProduct = null;
            string sId = string.Empty;
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                this.lListInsert = new List<sObject>();
                this.lListUpdate = new List<sObject>();
                lrateByConceptByProduct = this.GetProductsByRate(soapClient, sessionHeader, lrateByConceptByProduct);
                lRate = this.GetRates();
                lCost = this.GetCosts();
                lConcept = this.GetConcepts();
                lProduct = this.GetProducts();
                foreach (TarifaProducto item in ltarifaProductos)
                {
                    sId = this.FindRateInList(item, lrateByConceptByProduct);
                    rateByConceptByProduct = new RateByConceptByProduct__c()
                    {
                        Value__cSpecified = true,
                        Value__c = item.ivalor,
                        Date__cSpecified = true,
                        Date__c = item.dfecha,
                    };
                    var tmp = new RateByConceptByProduct__c();
                    tmp.RateId__r = new Rate__c() { Code__c = item.starifa };
                    tmp.ConceptId__r = new Concept__c() { Code__c = item.sconcepto.Trim() };
                    tmp.CostCenterId__r = new CostCenter__c() { Code__c = item.scentro.Trim() };
                    tmp.ProductId__r = new Product__c() { Name = item.sproducto.Trim() };
                    tmp.ProductId__r.Name = item.sproducto.Trim();
                    if (!string.IsNullOrEmpty(sId))
                    {
                        rateByConceptByProduct.Id = sId;
                        lUpdate.Add(rateByConceptByProduct);
                        this.lListUpdate.Add(tmp);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(rateByConceptByProduct.RateId__c) && !string.IsNullOrEmpty(rateByConceptByProduct.ConceptId__c) && !string.IsNullOrEmpty(rateByConceptByProduct.CostCenterId__c) && !string.IsNullOrEmpty(rateByConceptByProduct.ProductId__c))
                        {
                            rateByConceptByProduct.RateId__c = this.GetRateId(lRate, item.starifa);
                            rateByConceptByProduct.ConceptId__c = this.GetConceptId(lConcept, item.sconcepto.Trim());
                            rateByConceptByProduct.CostCenterId__c = this.GetCostId(lCost, item.scentro.Trim());
                            rateByConceptByProduct.ProductId__c = this.GetProductId(lProduct, item.sproducto.Trim());
                            lInsert.Add(rateByConceptByProduct);
                            this.lListInsert.Add(tmp);
                        }
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Productos por tarifa");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Productos por tarifa");
            }
            catch (Exception ex)
            {

                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Productos por tarifa" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                lrateByConceptByProduct = null;
                lUpdate = lInsert = null;
                lRate = null;
                lCost = null;
                lConcept = null;
                lProduct = null;
                rateByConceptByProduct = null;
            }
        }

        /// <summary>
        /// Método para actualizar los descuentos por tarifa y concepto
        /// </summary>
        /// <param name="ldescuentoTarifa">Lista genérica objeto de integración</param>
        public void UpsertDiscountRates(List<InspiraTemporal> ldescuentoTarifa)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            StringBuilder sQuery = new StringBuilder();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            LogSincroniza logSincroniza = null;
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            Descuento__c descuento = null;
            List<Descuento__c> lDescuento = null;
            List<Rate__c> lRate = null;
            List<Concept__c> lConcepto = null;
            List<Agreement__c> lAgreement = null;
            string sId = string.Empty;
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                lRate = this.GetRates();
                lConcepto = this.GetConcepts();
                lAgreement = this.GetAgreements();
                lDescuento = this.GetDiscountsByRate();
                foreach (InspiraTemporal item in ldescuentoTarifa)
                {
                    sId = this.FindDiscountRateInList(item, lDescuento);
                    descuento = new Descuento__c()
                    {
                        Descuento__c1Specified = true,
                        Descuento__c1 = item.iparametro3,
                        Fecha_inicio__cSpecified = true,
                        Fecha_inicio__c = DateTime.Now,
                        Fecha_fin__cSpecified = true,
                        Fecha_fin__c = new DateTime(DateTime.Now.Year, 12, 31),
                    };
                    if (string.IsNullOrEmpty(sId))
                    {
                        descuento.Tarifa__c = this.GetRateId(lRate, item.sparametro1);
                        descuento.Concepto__c = this.GetConceptId(lConcepto, item.sparametro2);
                        descuento.Convenio__c = this.GetAgreementd(lAgreement, item.scod);
                        lInsert.Add(descuento);
                    }
                    else
                    {
                        descuento.Id = sId;
                        lUpdate.Add(descuento);
                    }
                }
                this.UpdateValues(lUpdate, soapClient, sessionHeader, "Descuentos");
                this.InsertValues(lInsert, soapClient, sessionHeader, "Descuentos");
            }
            catch (Exception ex)
            {
                logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = ex.Message, stabla = "Descuentos" };
                LogSincronizacion.CreateLog(logSincroniza, sConnection);
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                queryOptions = null;
                queryResult = null;
                logSincroniza = null;
                sQuery = null;
                mruHeader = null;
                ldescuentoTarifa = null;
                lUpdate = lInsert = null;
                lRate = null;
                lAgreement = null;
                lConcepto = null;
                descuento = null;
            }
        }

        /// <summary>
        /// Método para obtener el id por código de una tarifa del listado de tarifas
        /// </summary>
        /// <param name="lRate">Lista genérica de tarifas</param>
        /// <param name="sCode">String código de la tarifa</param>
        /// <returns>String id de la tarifa encontrada</returns>
        private string GetRateId(List<Rate__c> lRate, string sCode)
        {
            Rate__c rate = lRate.FirstOrDefault(x => x.Code__c == sCode);
            return (rate != null) ? rate.Id : string.Empty;
        }

        /// <summary>
        /// Método para obtener el id por código de un convenio del listado de convenios
        /// </summary>
        /// <param name="lRate">Lista genérica de convenios</param>
        /// <param name="sCode">String código del convenio</param>
        /// <returns>String id del convenio encontrado</returns>
        private string GetCompanyId(List<Account> lAccount, string sCode)
        {
            Account account = lAccount.FirstOrDefault(x => x.DocumentNumber__c == sCode.Trim());
            return (account != null) ? account.Id : string.Empty;
        }

        /// <summary>
        /// Método para obtener el id por código de un centro de costo del listado de centros de costo
        /// </summary>
        /// <param name="lRate">Lista genérica de centros de costo</param>
        /// <param name="sCode">String código del centro de costo</param>
        /// <returns>String id del centro encontrado</returns>
        private string GetCostId(List<CostCenter__c> lCost, string sCode)
        {
            CostCenter__c cost = lCost.FirstOrDefault(x => x.Code__c == sCode);
            return (cost != null) ? cost.Id : string.Empty;
        }

        /// <summary>
        /// Método para obtener el id por código de un concepto del listado de centros de conceptos
        /// </summary>
        /// <param name="lRate">Lista genérica de conceptos</param>
        /// <param name="sCode">String código del concepto</param>
        /// <returns>String id del concepto encontrado</returns>
        private string GetConceptId(List<Concept__c> lConcept, string sCode)
        {
            Concept__c concept = lConcept.FirstOrDefault(x => x.Code__c == sCode);
            return (concept != null) ? concept.Id : string.Empty;
        }

        /// <summary>
        /// Método para obtener el id por código de un producto del listado de productos
        /// </summary>
        /// <param name="lRate">Lista genérica de productos</param>
        /// <param name="sCode">String código del producto</param>
        /// <returns>String id del producto encontrado</returns>
        private string GetProductId(List<Product__c> lProduct, string sCode)
        {
            Product__c product = lProduct.FirstOrDefault(x => x.Name == sCode);
            return (product != null) ? product.Id : string.Empty;
        }

        /// <summary>
        /// Método para obtener el id por código de una unidad funcional del listado de unidades funcionales
        /// </summary>
        /// <param name="lRate">Lista genérica de unidades funcionales</param>
        /// <param name="sCode">String código de la unidad funcional</param>
        /// <returns>String id de la unidad funcional encontrada</returns>
        private string GetUnitId(List<FunctionalUnit__c> lUnit, string sCode)
        {
            FunctionalUnit__c unit = lUnit.FirstOrDefault(x => x.Code__c == sCode);
            return (unit != null) ? unit.Id : string.Empty;
        }

        /// <summary>
        /// Método para obtener el id por código de un producto del listado de planes
        /// </summary>
        /// <param name="lRate">Lista genérica de planes</param>
        /// <param name="sCode">String código del plan</param>
        /// <returns>String id del plan encontrado</returns>
        private string GetPlanId(List<HealthCarePlan__c> lPlan, string scode, string sfield = "")
        {
            HealthCarePlan__c healthCarePlan = lPlan.FirstOrDefault(x => x.Code__c == scode);
            if (healthCarePlan != null)
            {
                return (string.IsNullOrEmpty(sfield)) ? healthCarePlan.Id : healthCarePlan.Name;
            }
            return string.Empty;
        }

        /// <summary>
        /// Método para obtener el id por código de un convenio del listado de convenios
        /// </summary>
        /// <param name="lRate">Lista genérica de convenios</param>
        /// <param name="sCode">String código del convenio</param>
        /// <returns>String id del convenio encontrado</returns>
        private string GetAgreementd(List<Agreement__c> lAgreements, string scode)
        {
            Agreement__c agreement = lAgreements.FirstOrDefault(x => x.Code__c == scode);
            return (agreement != null) ? agreement.Id : string.Empty;
        }

        private string FindRateInList(TarifaProducto tarifaProducto, List<RateByConceptByProduct__c> lrateByConceptByProduct)
        {
            try
            {
                RateByConceptByProduct__c rateByConceptByProduct = lrateByConceptByProduct.FirstOrDefault(x => x.ConceptId__r.Code__c.Trim() == tarifaProducto.sconcepto.Trim() && x.ProductId__r.Name.Trim() == tarifaProducto.sproducto.Trim()
                                                                                                        && x.RateId__r.Code__c.Trim() == tarifaProducto.starifa.Trim() && x.CostCenterId__r.Code__c.Trim() == tarifaProducto.scentro.Trim());
                return (rateByConceptByProduct != null) ? rateByConceptByProduct.Id : string.Empty;
            }
            catch (Exception)
            {

                return string.Empty;
            }
        }

        private string FindAgreementRateInList(InspiraTemporal tarifaConvenio, List<Plan__c> lratebyagreement)
        {
            try
            {
                Plan__c plan = lratebyagreement.FirstOrDefault(x => x.AgreementId__r.Code__c == tarifaConvenio.scod && x.HealthCarePlanId__r.Code__c == tarifaConvenio.sparametro2 && x.RateId__r.Code__c == tarifaConvenio.sparametro1);
                return (plan != null) ? plan.Id : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }

        }

        private string FindDiscountRateInList(InspiraTemporal descuentoTarifa, List<Descuento__c> ldiscount)
        {
            try
            {
                Descuento__c discount = ldiscount.FirstOrDefault(x => x.Tarifa__r.Code__c == descuentoTarifa.sparametro1 && x.Convenio__r.Code__c == descuentoTarifa.scod && descuentoTarifa.sparametro2 == x.Concepto__r.Code__c);
                return (discount != null) ? discount.Id : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }

        }

        private List<FunctionalUnit__c> GetUnits()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 250 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<FunctionalUnit__c> lUnit = new List<FunctionalUnit__c>();
            string squery = "SELECT Id, Code__c FROM FunctionalUnit__c WHERE Enabled__c = true";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lUnit = records.OfType<FunctionalUnit__c>().ToList();
                }
                return lUnit;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /// <summary>
        /// Método para obtener los convenios activos
        /// </summary>
        /// <returns>Lista genérica con los convenios</returns>
        private List<Agreement__c> GetAgreements(bool bactivo = true)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Agreement__c> lAgreements = new List<Agreement__c>();
            string squery = "SELECT Id, Code__c, Name, CompanyId__c FROM Agreement__c";
            if (bactivo) squery += " WHERE IsActivo__c = true";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lAgreements = records.OfType<Agreement__c>().ToList();
                }
                return lAgreements;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }


        /// <summary>
        /// Método para obtener los planes de salud activos
        /// </summary>
        /// <returns>Lista genérica con los planes de salud</returns>
        private List<HealthCarePlan__c> GetPlans()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<HealthCarePlan__c> lPlan = new List<HealthCarePlan__c>();
            string squery = "SELECT Id, Code__c FROM HealthCarePlan__c";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lPlan = records.OfType<HealthCarePlan__c>().ToList();
                }
                return lPlan;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /// <summary>
        /// Método para obtener el listado de tarifas activas
        /// </summary>
        /// <returns>Lista genérica de tarifas</returns>
        private List<Rate__c> GetRates()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Rate__c> lRate = new List<Rate__c>();
            string squery = "SELECT Id, Code__c FROM Rate__c";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lRate = records.OfType<Rate__c>().ToList();
                }
                return lRate;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<CostCenter__c> GetCosts()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<CostCenter__c> lCost = new List<CostCenter__c>();
            string squery = "SELECT Id, Code__c FROM CostCenter__c WHERE IsActive__c = true";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lCost = records.OfType<CostCenter__c>().ToList();
                }
                return lCost;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<Concept__c> GetConcepts()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Concept__c> lConcept = new List<Concept__c>();
            string squery = "SELECT Id, Code__c FROM Concept__c";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lConcept = records.OfType<Concept__c>().ToList();
                }
                return lConcept;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /// <summary>
        /// Método para obtener los productos del objeto Product__c
        /// </summary>
        /// <returns>Lista genérica de productos</returns>
        private List<Product__c> GetProducts()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Product__c> lProduct = new List<Product__c>();
            string squery = "SELECT Id, Name FROM Product__c";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lProduct = records.OfType<Product__c>().ToList();
                }
                return lProduct;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<Plan__c> GetRatesByAgreement()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Plan__c> lratesbyagreement = new List<Plan__c>();
            string squery = "SELECT Id, AgreementId__r.Code__c, HealthCarePlanId__r.Code__c, RateId__r.Code__c FROM Plan__c";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lratesbyagreement = records.OfType<Plan__c>().ToList();
                }
                return lratesbyagreement;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /// <summary>
        /// Método para obtener el listado de productos por tarifa que se encuentran en Salesforce
        /// </summary>
        /// <returns>Lista genérica de Conceptos, Tarifas, Productos</returns>
        private List<RateByConceptByProduct__c> GetProductsByRate(SoapClient soapClient, SessionHeader sessionHeader, List<RateByConceptByProduct__c> lrateByConceptByProduct)
        {
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            StringBuilder squery = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            if (lrateByConceptByProduct.Count == 0)
            {
                squery.Append("SELECT Id, RateId__r.Code__c, ConceptId__r.Code__c, ProductId__r.Name, CostCenterId__r.Code__c, RateId__c, ProductId__c, CostCenterId__c, Value__c FROM RateByConceptByProduct__c ORDER BY Id LIMIT 2000");
            }
            else
            {
                squery.Append("SELECT Id, RateId__r.Code__c, ConceptId__r.Code__c, ProductId__r.Name, CostCenterId__r.Code__c, RateId__c, ProductId__c, CostCenterId__c, Value__c FROM RateByConceptByProduct__c");
                squery.Append(" WHERE Id > '");
                squery.Append(lrateByConceptByProduct.LastOrDefault().Id);
                squery.Append("' ORDER BY Id LIMIT 2000");
            }
            try
            {

                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lrateByConceptByProduct.AddRange(records.OfType<RateByConceptByProduct__c>().ToList());
                    return GetProductsByRate(soapClient, sessionHeader, lrateByConceptByProduct);
                }
                else
                {
                    return lrateByConceptByProduct;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<Descuento__c> GetDiscountsByRate()
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Descuento__c> ldiscounts = new List<Descuento__c>();
            string squery = "SELECT Id, Tarifa__r.Code__c, Concepto__r.Code__c, Convenio__r.Code__c FROM Descuento__c";
            try
            {
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    ldiscounts = records.OfType<Descuento__c>().ToList();
                }
                return ldiscounts;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }


        /// <summary>
        /// Método para obtener el id de un registro a actualizar
        /// </summary>
        /// <param name="queryResult">Array resultado de la consulta</param>
        /// <param name="scode">String código a buscar en el array</param>
        /// <param name="sobject">String indica cuál es el objeto que se va a utilizar</param>
        /// <returns>String con el id encontrado</returns>
        private string GetId(QueryResult queryResult, string scode, string sobject)
        {
            if (queryResult.size > 0)
            {
                sObject[] records = queryResult.records;
                if (sobject == "Rate")
                {
                    List<Rate__c> lrate = records.OfType<Rate__c>().ToList();
                    return this.GetRateId(lrate, scode);
                }
                else if (sobject == "Agreement")
                {
                    List<Agreement__c> lagreement = records.OfType<Agreement__c>().ToList();
                    return this.GetAgreementd(lagreement, scode);
                }
                else if (sobject == "CostCenter")
                {
                    List<CostCenter__c> lcost = records.OfType<CostCenter__c>().ToList();
                    return this.GetCostId(lcost, scode);
                }
                else if (sobject == "Plan")
                {
                    List<HealthCarePlan__c> lplan = records.OfType<HealthCarePlan__c>().ToList();
                    return this.GetPlanId(lplan, scode);
                }
                else if (sobject == "Concept")
                {
                    List<Concept__c> lconcept = records.OfType<Concept__c>().ToList();
                    return this.GetConceptId(lconcept, scode);
                }
                else if (sobject == "Unit")
                {
                    List<FunctionalUnit__c> lunit = records.OfType<FunctionalUnit__c>().ToList();
                    return this.GetUnitId(lunit, scode);
                }
                else if (sobject == "Product")
                {
                    List<Product__c> lproduct = records.OfType<Product__c>().ToList();
                    return this.GetProductId(lproduct, scode);
                }
                else if (sobject == "Company")
                {
                    List<Account> laccount = records.OfType<Account>().ToList();
                    return this.GetCompanyId(laccount, scode);
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// Método para insertar de forma masiva un objeto en Salesforce, tener en cuenta que se pueden solo 100 objetos al tiempo
        /// </summary>
        /// <param name="lInsert">Lista genérica de objetos a insertar</param>
        /// <param name="soapClient">Objeto conexión a Salesforce</param>
        /// <param name="sessionHeader">Objeto cabeceras de conexión a Salesforce</param>
        /// <param name="sTable">String tabla que se está actualizando</param>
        private void InsertValues(List<sObject> lInsert, SoapClient soapClient, SessionHeader sessionHeader, string sTable)
        {
            List<SincronizaTemporal> lsincronizaTemporals = new List<SincronizaTemporal>();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SaveResult[] results = null;
            LimitInfo[] limitInfos = null;
            LogSincroniza logSincroniza = null;
            int i = 0, j = 0;
            List<sObject> lTmp = new List<sObject>();
            List<sObject> lValues = new List<sObject>();
            foreach (sObject item in lInsert)
            {
                lTmp.Add(item);
                lValues.Add(this.lListInsert[j]);
                if (i == 199)
                {
                    soapClient.create(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, lTmp.ToArray(), out limitInfos, out results);
                    if (!results[0].success)
                    {
                        logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = results[0].errors[0].message, stabla = sTable };
                        LogSincronizacion.CreateLog(logSincroniza, sConnection);
                        ApplicationException applicationException = new ApplicationException(results[0].errors[0].message);
                        LogError.WriteError("Application", "WSInspira", applicationException);
                    }
                    else
                    {
                        lsincronizaTemporals.AddRange(this.PutTmpValues(sTable, lValues));
                    }
                    i = 0;
                    lTmp.Clear();
                    lValues.Clear();
                }
                i++;
                j++;
            }
            if (lTmp.Count > 0)
            {
                soapClient.create(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, lTmp.ToArray(), out limitInfos, out results);
                if (!results[0].success)
                {
                    logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = results[0].errors[0].message, stabla = sTable };
                    LogSincronizacion.CreateLog(logSincroniza, sConnection);
                    ApplicationException applicationException = new ApplicationException(results[0].errors[0].message);
                    LogError.WriteError("Application", "WSInspira", applicationException);
                }
                else
                {
                    lsincronizaTemporals.AddRange(this.PutTmpValues(sTable, lValues));
                }
            }
            LogSincronizacion.CreateSyncTmp(lsincronizaTemporals, sConnection);
            mruHeader = null;
            results = null;
            limitInfos = null;
            logSincroniza = null;
            lTmp = null;
        }

        /// <summary>
        /// Método para actualizar de forma masiva un objeto en Salesforce, tener en cuenta que se pueden solo 100 objetos al tiempo
        /// </summary>
        /// <param name="lUpdate">Lista genérica de objetos a actualizar</param>
        /// <param name="soapClient">Objeto conexión a Salesforce</param>
        /// <param name="sessionHeader">Objeto cabeceras de conexión a Salesforce</param>
        /// <param name="sTable">String tabla que se está actualizando</param>
        private void UpdateValues(List<sObject> lUpdate, SoapClient soapClient, SessionHeader sessionHeader, string sTable)
        {
            List<SincronizaTemporal> lsincronizaTemporals = new List<SincronizaTemporal>();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SaveResult[] results = null;
            LimitInfo[] limitInfos = null;
            LogSincroniza logSincroniza = null;
            int i = 0, j = 0;
            List<sObject> lTmp = new List<sObject>();
            List<sObject> lValues = new List<sObject>();
            foreach (sObject item in lUpdate)
            {
                lTmp.Add(item);
                lValues.Add(this.lListUpdate[j]);
                if (i == 199)
                {
                    soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, null, lTmp.ToArray(), out limitInfos, out results);
                    if (!results[0].success)
                    {
                        logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = results[0].errors[0].message, stabla = sTable };
                        LogSincronizacion.CreateLog(logSincroniza, sConnection);
                        ApplicationException applicationException = new ApplicationException(results[0].errors[0].message);
                        LogError.WriteError("Application", "WSInspira", applicationException);
                    }
                    else
                    {
                        lsincronizaTemporals.AddRange(this.PutTmpValues(sTable, lValues));
                    }
                    lTmp.Clear();
                    lValues.Clear();
                    i = 0;
                }
                i++;
                j++;
            }
            if (lTmp.Count > 0)
            {
                soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, null, lTmp.ToArray(), out limitInfos, out results);
                if (!results[0].success)
                {
                    logSincroniza = new LogSincroniza() { scodigo = string.Empty, smensaje = results[0].errors[0].message, stabla = sTable };
                    LogSincronizacion.CreateLog(logSincroniza, sConnection);
                    ApplicationException applicationException = new ApplicationException(results[0].errors[0].message);
                    LogError.WriteError("Application", "WSInspira", applicationException);
                }
                else
                {
                    lsincronizaTemporals.AddRange(this.PutTmpValues(sTable, lValues));
                }
            }
            LogSincronizacion.CreateSyncTmp(lsincronizaTemporals, sConnection);
            mruHeader = null;
            results = null;
            limitInfos = null;
            logSincroniza = null;
            lTmp = null;
        }

        /// <summary>
        /// Método para obtener el listado de objetos de sincronización que fueron exitosos
        /// </summary>
        /// <param name="stable">String nombre de la tabla</param>
        /// <param name="lValues">Lista genérica de objetos de Salesforce</param>
        /// <returns>Lista genérica con el objeto temporal de sincronización</returns>
        private List<SincronizaTemporal> PutTmpValues(string stable, List<sObject> lValues)
        {
            List<SincronizaTemporal> lTmp = new List<SincronizaTemporal>();
            SincronizaTemporal sincronizaTemporal = null;
            foreach (var item in lValues)
            {
                sincronizaTemporal = new SincronizaTemporal() { stable = stable };
                if (stable == "Productos por tarifa")
                {
                    var tmp = item as RateByConceptByProduct__c;
                    sincronizaTemporal.sparameter1 = tmp.RateId__r.Code__c;
                    sincronizaTemporal.sparameter2 = tmp.ConceptId__r.Code__c;
                    sincronizaTemporal.sparameter3 = tmp.CostCenterId__r.Code__c;
                    sincronizaTemporal.sparameter4 = tmp.ProductId__r.Name;
                }
                else if (stable == "Tarifas por empresa")
                {
                    var tmp = item as Plan__c;
                    sincronizaTemporal.sparameter1 = tmp.RateId__r.Code__c;
                    sincronizaTemporal.sparameter2 = tmp.AgreementId__r.Code__c;
                    sincronizaTemporal.sparameter3 = tmp.HealthCarePlanId__r.Code__c;
                }
                else if (stable == "Tarifas")
                {
                    var tmp = item as Rate__c;
                    sincronizaTemporal.sparameter1 = tmp.Code__c;
                }
                else if (stable == "Convenios")
                {
                    var tmp = item as Agreement__c;
                    sincronizaTemporal.sparameter1 = tmp.Code__c;
                }
                else if (stable == "Producto")
                {
                    var tmp = item as Product__c;
                    sincronizaTemporal.sparameter1 = tmp.Name;
                }
                else if (stable == "Unidad Funcional")
                {
                    var tmp = item as FunctionalUnit__c;
                    sincronizaTemporal.sparameter1 = tmp.Code__c;
                }
                else if (stable == "Centro de costo por unidad funcional")
                {
                    var tmp = item as CostCenterByUnit__c;
                    sincronizaTemporal.sparameter1 = tmp.CostCenterId__r.Code__c;
                    sincronizaTemporal.sparameter2 = tmp.FunctionalUnitId__r.Code__c;
                }
                else if (stable == "Centros de Costo")
                {
                    var tmp = item as CostCenter__c;
                    sincronizaTemporal.sparameter1 = tmp.Code__c;
                }
                else if (stable == "Concepto")
                {
                    var tmp = item as Concept__c;
                    sincronizaTemporal.sparameter1 = tmp.Code__c;
                }
                else if (stable == "Plan")
                {
                    var tmp = item as HealthCarePlan__c;
                    sincronizaTemporal.sparameter1 = tmp.Code__c;
                }
                else if (stable == "Cuenta")
                {
                    var tmp = item as Account;
                    sincronizaTemporal.sparameter1 = tmp.DocumentNumber__c;
                }
                lTmp.Add(sincronizaTemporal);
            }
            return lTmp;
        }

        #endregion

        #region Métodos para traer las citas de progrmas

        private List<ProductsByGroup__c> GetProductsInfo(List<ProductsByGroup__c> productsByGroups, SoapClient soapClient, SessionHeader sessionHeader)
        {
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            StringBuilder stringBuilder = new StringBuilder();
            try
            {
                if (productsByGroups.Count == 0)
                {
                    stringBuilder.Append("SELECT Grupo_por_Plan__r.Grupo__c, Grupo_por_Plan__r.Grupo__r.Name, Grupo_por_Plan__r.Plan__c, Tarifa_concepto_producto__r.CostCenterId__c, Tarifa_concepto_producto__r.ConceptId__c" +
                    ", Tarifa_concepto_producto__r.RateId__c, Tarifa_concepto_producto__r.RateId__r.Code__c, Tarifa_concepto_producto__r.ProductId__c, Tarifa_concepto_producto__r.ProductId__r.Name " +
                    ", Tarifa_concepto_producto__r.CostCenterId__r.Code__c, Tarifa_concepto_producto__r.RateId__r.Name, Tarifa_concepto_producto__r.Value__c FROM ProductsByGroup__c");
                    stringBuilder.Append(" WHERE Grupo_por_Plan__c <> '' ORDER BY Id LIMIT 2000");
                }
                else
                {
                    stringBuilder.Append("SELECT Grupo_por_Plan__r.Grupo__c, Grupo_por_Plan__r.Grupo__r.Name, Grupo_por_Plan__r.Plan__c, Tarifa_concepto_producto__r.CostCenterId__c, Tarifa_concepto_producto__r.ConceptId__c" +
                   ", Tarifa_concepto_producto__r.RateId__c, Tarifa_concepto_producto__r.RateId__r.Code__c, Tarifa_concepto_producto__r.ProductId__c, Tarifa_concepto_producto__r.ProductId__r.Name " +
                   ", Tarifa_concepto_producto__r.CostCenterId__r.Code__c, Tarifa_concepto_producto__r.RateId__r.Name, Tarifa_concepto_producto__r.Value__c FROM ProductsByGroup__c");
                    stringBuilder.Append(" WHERE Grupo_por_Plan__c <> '' AND Id > '");
                    stringBuilder.Append(productsByGroups.LastOrDefault().Id);
                    stringBuilder.Append("' ORDER BY Id LIMIT 2000");
                }
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    productsByGroups.AddRange(records.OfType<ProductsByGroup__c>().ToList());
                    return GetProductsInfo(productsByGroups, soapClient, sessionHeader);
                }
                else
                {
                    return productsByGroups;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public List<ServintePatient> GetAppointmentsForPrograms(string syear, string smonth)
        {
            List<Appointment__c> lAppointments = new List<Appointment__c>();
            List<ServintePatient> lservintePatients = new List<ServintePatient>();
            ServintePatient servintePatient = null;
            List<ProductsByGroup__c> productsByGroups = new List<ProductsByGroup__c>();
            List<RateByConceptByProduct__c> rateByConceptByProducts = new List<RateByConceptByProduct__c>();
            List<ProductsByGroup__c> productsTmp = null;
            SoapClient soapClient = new SoapClient();
            string sunit = string.Empty;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            try
            {
                productsByGroups = this.GetProductsInfo(productsByGroups, soapClient, sessionHeader);
                rateByConceptByProducts = this.GetProductsByRate(soapClient, sessionHeader, rateByConceptByProducts);
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                lAppointments = this.GetAppointmentsForPrograms(lAppointments, string.Empty, syear, smonth);
                foreach (var item in lAppointments)
                {
                    productsTmp = productsByGroups.FindAll(x => x.GroupId__c == item.GroupId__c);
                    servintePatient = new ServintePatient()
                    {
                        saddress = item.WhatId__r.Address__c,
                        sdocument = item.WhatId__r.DocumentNumber__c,
                        sdocumenttype = item.WhatId__r.DocumentType__c,
                        sfirstname = item.WhatId__r.FirstName_c__pc,
                        ssecondname = item.WhatId__r.SecondName__pc,
                        ssurname = item.WhatId__r.FirstSurname__pc,
                        ssecondsurname = item.WhatId__r.SecondSurname__pc,
                        dbirthdate = item.WhatId__r.PersonBirthdate.Value,
                        sagreementcode = item.PlanId__r.AgreementId__r.Code__c,
                        scellphone = item.WhatId__r.Phone,
                        smail = item.WhatId__r.PersonEmail,
                        scovid1 = "N",
                        scovid2 = "N",
                        sgender = (!string.IsNullOrEmpty(item.WhatId__r.Gender__pc)) ? item.WhatId__r.Gender__pc.Substring(0, 1) : "M",
                        sjob = (!string.IsNullOrEmpty(item.WhatId__r.Ocupation__pc)) ? item.WhatId__r.Ocupation__pc : "5169",
                        sbornplace = "169",
                        sneighborhood = "43",
                        lappointments = new List<InspiraCita>(),
                        scity = "11001",
                        scityname = "BOGOTA D.C.",
                        snation = "169",
                        surbanzone = "U",
                        smaritalstatus = "S",
                        sphone = item.WhatId__r.Phone,
                        safiliation = "1",
                        slevel = "6",
                        idPaciente = item.WhatId__c,
                    };
                    var appointment = new InspiraCita()
                    {
                        sagreement = item.PlanId__r.AgreementId__r.Code__c,
                        sagreementname = item.PlanId__r.AgreementId__r.Name,
                        sagreementtype = "E",
                        sappointment = item.Id,
                        scie10 = Tools.GetDiagnosis(item.PlanId__r.HealthCarePlanId__r.Name),
                        splan = item.PlanId__r.HealthCarePlanId__r.Code__c,
                        srate = item.PlanId__r.RateId__r.Code__c,
                        sratename = item.PlanId__r.RateId__r.Name,
                        sservicegroup = item.GroupId__r.Name,
                        sunit = (item.WhatId__r.Age2__pc >= 18) ? "1100" : "1200",
                        sattentiontype = "2",
                        sservicetype = "28",
                        ddate = item.ActivityDate__c.Value,
                        lservices = new List<ServiceRequest>(),
                        sthird = item.AgendaId__r.ProfessionalId__r.DocumentNumber__c,
                        scostcenter = item.PlanId__r.HealthCarePlanId__r.Name,
                    };
                    foreach (ProductsByGroup__c product in productsTmp)
                    {
                        var services = new ServiceRequest()
                        {
                            scostcenter = product.Tarifa_concepto_producto__r.CostCenterId__r.Code__c,
                            sservice = product.Tarifa_concepto_producto__r.ProductId__r.Name,
                            iqty = 1,
                            srate = item.PlanId__r.RateId__r.Code__c,
                            sservicename = product.Tarifa_concepto_producto__r.ProductId__r.Name__c,
                            bbilleable = true,
                            bisprocedure = !product.ProductId__r.Name__c.Contains("CONSULTA"),
                            sconcept = product.Tarifa_concepto_producto__r.ConceptId__r.Code__c,
                            idiscount = 0,
                            ivalue = Convert.ToDecimal(product.Tarifa_concepto_producto__r.Value__c)
                        };
                        sunit = (item.WhatId__r.Age2__pc >= 18) ? "1" : "3";
                        appointment.sunit = sunit;
                        appointment.lservices.Add(services);
                    }
                    servintePatient.lappointments.Add(appointment);
                    lservintePatients.Add(servintePatient);
                }
                return lservintePatients;
            }
            catch (Exception ex)
            {
                LogError.WriteError("WSInspira", "Application", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
            }
        }

        private List<Appointment__c> GetAppointmentsForPrograms(List<Appointment__c> lAppoinment, string stype, string syear, string smonth)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            StringBuilder squery = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            if (lAppoinment.Count == 0)
            {
                squery.Append("SELECT Id, WhatId__c, WhatId__r.Address__c, WhatId__r.DocumentNumber__c, WhatId__r.DocumentType__c, WhatId__r.FirstName, WhatId__r.FirstName_c__pc");
                squery.Append(", WhatId__r.FirstSurname__pc, WhatId__r.Gender__pc, WhatId__r.MiddleName, WhatId__r.Ocupation__pc, WhatId__r.PersonBirthdate, WhatId__r.PersonEmail ,WhatId__r.PersonMobilePhone");
                squery.Append(", WhatId__r.SecondName__pc, WhatId__r.SecondSurname__pc, GroupId__c, GroupId__r.Name, PlanId__r.Name, PlanId__r.HealthCarePlanId__r.Code__c");
                squery.Append(", PlanId__r.AgreementId__r.Code__c, PlanId__r.AgreementId__r.Name, PlanId__r.RateId__r.Code__c, WhatId__r.Age2__pc, ActivityDate__c");
                squery.Append(", AgendaId__r.ProfessionalId__r.DocumentNumber__c, PlanId__r.RateId__r.Name, PlanId__r.HealthCarePlanId__r.Name, PlanId__r.RateId__c FROM Appointment__c WHERE PatientAttended__c = true AND FNC_MainAppointment__c = true");
                squery.Append(" AND (PlanId__r.Name LIKE '%FAMISANAR AIREPOC%' OR PlanId__r.Name LIKE '%FAMISANAR ASMAIRE%'");
                squery.Append(" OR PlanId__r.Name LIKE '%SANITAS AIREPOC%' OR PlanId__r.Name LIKE '%VASCU%' OR PlanId__r.Name LIKE '%COOMEVA MP ASMAIRE%'");
                squery.Append(" OR GroupId__r.Name LIKE '%VALORACIÓN ANESTESIA%' OR GroupId__r.Name LIKE '%VALORACIÓN FBC%'");
                squery.Append(" OR PlanId__r.Name LIKE '%SANITAS ASMAIRE%' OR PlanId__r.Name LIKE '%COOMEVA AIREPOC%' OR PlanId__r.Name LIKE '%ALIANSALUD OXIGENAR%'");
                squery.Append(" OR PlanId__r.Name LIKE '%COLMEDICA AIREPOC%' OR PlanId__r.Name LIKE '%FNC INDICE INTEGRACIÓN UCI%' OR PlanId__r.Name LIKE '%FNC VENTIL MECANICA INTERMITEN%'");
                squery.Append(" OR PlanId__r.Name LIKE '%PROTOCOLO%')");
                squery.Append(" AND AgendaId__r.Name <> 'SERVICIOS INVESTIGACIÓN'");
                squery.Append(" AND WhatId__r.DocumentNumber__c <> 'INVEST' AND WhatId__r.DocumentNumber__c <> 'BLOQUEO'");
                squery.Append(" AND CALENDAR_YEAR(ActivityDate__c) = ");
                squery.Append(syear);
                squery.Append(" AND CALENDAR_MONTH(ActivityDate__c) = ");
                squery.Append(smonth);
                squery.Append(" ORDER BY Id LIMIT 2000");
            }
            else
            {
                squery.Append("SELECT Id, WhatId__c, WhatId__r.Address__c, WhatId__r.DocumentNumber__c, WhatId__r.DocumentType__c, WhatId__r.FirstName, WhatId__r.FirstName_c__pc");
                squery.Append(", WhatId__r.FirstSurname__pc, WhatId__r.Gender__pc, WhatId__r.MiddleName, WhatId__r.Ocupation__pc, WhatId__r.PersonBirthdate, WhatId__r.PersonEmail ,WhatId__r.PersonMobilePhone");
                squery.Append(", WhatId__r.SecondName__pc, WhatId__r.SecondSurname__pc, GroupId__c, GroupId__r.Name, PlanId__r.Name, PlanId__r.HealthCarePlanId__r.Code__c");
                squery.Append(", PlanId__r.AgreementId__r.Code__c, PlanId__r.AgreementId__r.Name, PlanId__r.RateId__r.Code__c, WhatId__r.Age2__pc, ActivityDate__c");
                squery.Append(", AgendaId__r.ProfessionalId__r.DocumentNumber__c, PlanId__r.RateId__r.Name, PlanId__r.HealthCarePlanId__r.Name, PlanId__r.RateId__c FROM Appointment__c WHERE PatientAttended__c = true AND FNC_MainAppointment__c = true");
                squery.Append(" AND (PlanId__r.Name LIKE '%FAMISANAR AIREPOC%' OR PlanId__r.Name LIKE '%FAMISANAR ASMAIRE%'");
                squery.Append(" OR PlanId__r.Name LIKE '%SANITAS AIREPOC%' OR PlanId__r.Name LIKE '%VASCU%' OR PlanId__r.Name LIKE '%COOMEVA MP ASMAIRE%'");
                squery.Append(" OR GroupId__r.Name LIKE '%VALORACIÓN ANESTESIA%' OR GroupId__r.Name LIKE '%VALORACIÓN FBC%'");
                squery.Append(" OR PlanId__r.Name LIKE '%SANITAS ASMAIRE%' OR PlanId__r.Name LIKE '%COOMEVA AIREPOC%' OR PlanId__r.Name LIKE '%ALIANSALUD OXIGENAR%'");
                squery.Append(" OR PlanId__r.Name LIKE '%COLMEDICA AIREPOC%' OR PlanId__r.Name LIKE '%FNC INDICE INTEGRACIÓN UCI%' OR PlanId__r.Name LIKE '%FNC VENTIL MECANICA INTERMITEN%'");
                squery.Append(" OR PlanId__r.Name LIKE '%PROTOCOLO%')");
                squery.Append(" AND AgendaId__r.Name <> 'SERVICIOS INVESTIGACIÓN'");
                squery.Append(" AND WhatId__r.DocumentNumber__c <> 'INVEST' AND WhatId__r.DocumentNumber__c <> 'BLOQUEO'");
                squery.Append(" AND CALENDAR_YEAR(ActivityDate__c) = ");
                squery.Append(syear);
                squery.Append(" AND CALENDAR_MONTH(ActivityDate__c) = ");
                squery.Append(smonth);
                squery.Append(" AND Id > '");
                squery.Append(lAppoinment.LastOrDefault().Id);
                squery.Append("' ORDER BY Id LIMIT 2000");
            }
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lAppoinment.AddRange(records.OfType<Appointment__c>().ToList());
                    return GetAppointmentsForPrograms(lAppoinment, stype, syear, smonth);
                }
                else
                {
                    return lAppoinment;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("WSInspira", "Application", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /// <summary>
        /// Método para traer las citas de programas del día
        /// </summary>
        /// <param name="sType">String tipo de envío</param>
        /// <returns>Lista genérica Paciente Servinte</returns>
        public List<ServintePatient> GetPatientsforPrograms(string sType, bool bType)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Appointment__c> lAppointments = new List<Appointment__c>();
            List<ServintePatient> lservintePatients = new List<ServintePatient>();
            ServintePatient servintePatient = null;
            List<ProductsByGroup__c> productsByGroups = new List<ProductsByGroup__c>();
            List<ProductsByGroup__c> productsTmp = null;
            string sunit = string.Empty;
            int idays = 0;
            StringBuilder stringBuilder = new StringBuilder("SELECT Id, WhatId__c, WhatId__r.Address__c, WhatId__r.DocumentNumber__c, WhatId__r.DocumentType__c, WhatId__r.FirstName, WhatId__r.FirstName_c__pc");
            stringBuilder.Append(", WhatId__r.FirstSurname__pc, WhatId__r.Gender__pc, WhatId__r.MiddleName, WhatId__r.Ocupation__pc, WhatId__r.PersonBirthdate, WhatId__r.PersonEmail ,WhatId__r.PersonMobilePhone");
            stringBuilder.Append(", WhatId__r.SecondName__pc, WhatId__r.SecondSurname__pc, GroupId__c, GroupId__r.Name, PlanId__r.Name, PlanId__r.HealthCarePlanId__r.Code__c");
            stringBuilder.Append(", PlanId__r.AgreementId__r.Code__c, PlanId__r.AgreementId__r.Name, PlanId__r.RateId__r.Code__c, WhatId__r.Age2__pc, ActivityDate__c");
            stringBuilder.Append(", AgendaId__r.ProfessionalId__r.DocumentNumber__c, PlanId__r.RateId__r.Name, PlanId__r.HealthCarePlanId__r.Name, PlanId__r.RateId__c FROM Appointment__c WHERE PatientAttended__c = true AND FNC_MainAppointment__c = true");
            if (sType == "FAMISANAR")
            {
                stringBuilder.Append(" AND GroupId__r.Statistics__c = true");
                stringBuilder.Append(" AND (PlanId__r.Name LIKE '%FAMISANAR AIREPOC%' OR PlanId__r.Name LIKE '%FAMISANAR ASMAIRE%')");
                if (DateTime.Now.DayOfWeek == DayOfWeek.Tuesday)
                {
                    idays = -3;
                }
                else if (DateTime.Now.DayOfWeek == DayOfWeek.Friday)
                {
                    idays = -2;
                }
                stringBuilder.Append(" AND ActivityDate__c >= ");
                stringBuilder.Append(DateTime.Now.AddDays(idays).ToString("yyyy-MM-dd"));
                stringBuilder.Append(" AND ActivityDate__c <= ");
                stringBuilder.Append(DateTime.Now.ToString("yyyy-MM-dd"));
            }
            else if (sType == "SANITAS")
            {
                stringBuilder.Append(" AND (PlanId__r.Name LIKE '%SANITAS AIREPOC%' OR PlanId__r.Name LIKE '%VASCU%' OR PlanId__r.Name LIKE '%COOMEVA MP ASMAIRE%'");
                stringBuilder.Append(" OR GroupId__r.Name LIKE '%VALORACIÓN ANESTESIA%' OR GroupId__r.Name LIKE '%VALORACIÓN FBC%'");
                stringBuilder.Append(" OR PlanId__r.Name LIKE '%SANITAS ASMAIRE%' OR PlanId__r.Name LIKE '%COOMEVA AIREPOC%' OR PlanId__r.Name LIKE '%ALIANSALUD OXIGENAR%'");
                stringBuilder.Append(" OR PlanId__r.Name LIKE '%COLMEDICA AIREPOC%' OR PlanId__r.Name LIKE '%FNC INDICE INTEGRACIÓN UCI%' OR PlanId__r.Name LIKE '%FNC VENTIL MECANICA INTERMITEN%')");
                if (DateTime.Now.DayOfWeek == DayOfWeek.Tuesday)
                {
                    idays = -3;
                }
                else if (DateTime.Now.DayOfWeek == DayOfWeek.Friday)
                {
                    idays = -2;
                }
                stringBuilder.Append(" AND ActivityDate__c >= ");
                stringBuilder.Append(DateTime.Now.AddDays(idays).ToString("yyyy-MM-dd"));
                stringBuilder.Append(" AND ActivityDate__c <= ");
                stringBuilder.Append(DateTime.Now.ToString("yyyy-MM-dd"));
            }
            else if (sType == "INVESTIGACION")
            {
                stringBuilder.Append(" AND PlanId__r.Name LIKE '%PROTOCOLO%'");
                stringBuilder.Append(" AND AgendaId__r.Name <> 'SERVICIOS INVESTIGACIÓN'");
                stringBuilder.Append(" AND ActivityDate__c = ");
                stringBuilder.Append(DateTime.Now.ToString("yyyy-MM-dd"));
            }
            stringBuilder.Append(" AND WhatId__r.DocumentNumber__c <> 'INVEST' AND WhatId__r.DocumentNumber__c <> 'BLOQUEO'");
            try
            {
                productsByGroups = this.GetProductsInfo(productsByGroups, soapClient, sessionHeader);
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lAppointments = records.OfType<Appointment__c>().ToList();
                    foreach (var item in lAppointments)
                    {
                        productsTmp = productsByGroups.FindAll(x => x.GroupId__c == item.GroupId__c);
                        servintePatient = new ServintePatient()
                        {
                            saddress = item.WhatId__r.Address__c,
                            sdocument = item.WhatId__r.DocumentNumber__c,
                            sdocumenttype = item.WhatId__r.DocumentType__c,
                            sfirstname = item.WhatId__r.FirstName_c__pc,
                            ssecondname = item.WhatId__r.SecondName__pc,
                            ssurname = item.WhatId__r.FirstSurname__pc,
                            ssecondsurname = item.WhatId__r.SecondSurname__pc,
                            dbirthdate = item.WhatId__r.PersonBirthdate.Value,
                            sagreementcode = item.PlanId__r.AgreementId__r.Code__c,
                            scellphone = item.WhatId__r.Phone,
                            smail = item.WhatId__r.PersonEmail,
                            scovid1 = "N",
                            scovid2 = "N",
                            sgender = (!string.IsNullOrEmpty(item.WhatId__r.Gender__pc)) ? item.WhatId__r.Gender__pc.Substring(0, 1) : "M",
                            sjob = (!string.IsNullOrEmpty(item.WhatId__r.Ocupation__pc)) ? item.WhatId__r.Ocupation__pc : "5169",
                            sbornplace = "169",
                            sneighborhood = "43",
                            lappointments = new List<InspiraCita>(),
                            scity = "11001",
                            scityname = "BOGOTA D.C.",
                            snation = "169",
                            surbanzone = "U",
                            smaritalstatus = "S",
                            sphone = item.WhatId__r.Phone,
                            safiliation = "1",
                            slevel = "6",
                            idPaciente = item.WhatId__c,
                            sissuingentity = "REGISTRADURIA NACIONAL DEL ESTADO CIVIL",
                            ssourcecountry = "169",
                        };
                        var appointment = new InspiraCita()
                        {
                            sagreement = item.PlanId__r.AgreementId__r.Code__c,
                            sagreementname = item.PlanId__r.AgreementId__r.Name,
                            sagreementtype = "E",
                            sappointment = item.Id,
                            scie10 = Tools.GetDiagnosis(item.PlanId__r.HealthCarePlanId__r.Name),
                            splan = item.PlanId__r.HealthCarePlanId__r.Code__c,
                            srate = item.PlanId__r.RateId__r.Code__c,
                            sratename = item.PlanId__r.RateId__r.Name,
                            sservicegroup = item.GroupId__r.Name,
                            sunit = (item.WhatId__r.Age2__pc >= 18) ? "1100" : "1200",
                            sattentiontype = "2",
                            sservicetype = "28",
                            ddate = item.ActivityDate__c.Value,
                            lservices = new List<ServiceRequest>(),
                            sthird = item.AgendaId__r.ProfessionalId__r.DocumentNumber__c,
                            scostcenter = item.PlanId__r.HealthCarePlanId__r.Name,
                        };
                        foreach (ProductsByGroup__c product in productsTmp)
                        {
                            var services = new ServiceRequest()
                            {
                                scostcenter = product.Tarifa_concepto_producto__r.CostCenterId__r.Code__c,
                                sservice = product.Tarifa_concepto_producto__r.ProductId__r.Name,
                                iqty = 1,
                                srate = item.PlanId__r.RateId__r.Code__c,
                                sservicename = product.Tarifa_concepto_producto__r.ProductId__r.Name__c,
                                bbilleable = true,
                                bisprocedure = !product.ProductId__r.Name__c.Contains("CONSULTA"),
                                sconcept = product.Tarifa_concepto_producto__r.ConceptId__r.Code__c,
                                idiscount = 0,
                                ivalue = Convert.ToDecimal(product.Tarifa_concepto_producto__r.Value__c)
                            };
                            sunit = (item.WhatId__r.Age2__pc >= 18) ? "1" : "3";
                            appointment.sunit = sunit;
                            appointment.lservices.Add(services);
                        }
                        servintePatient.lappointments.Add(appointment);
                        lservintePatients.Add(servintePatient);
                    }
                }
                return lservintePatients;
            }
            catch (Exception ex)
            {

                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /// <summary>
        /// Método para traer las citas de programas del día
        /// </summary>
        /// <param name="sType">String tipo de envío</param>
        /// <returns>Lista genérica Paciente Servinte</returns>
        /// <summary>
        /// Método para traer las citas de programas del día
        /// </summary>
        /// <param name="sDate">Fecha del día</param>
        /// <returns>Lista genérica Paciente Servinte</returns>
        public List<ServintePatient> GetPatientsforPrograms(string sInitialDate, string sFinalDate, bool bIsFamisanar = false)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Appointment__c> lAppointments = new List<Appointment__c>();
            List<ServintePatient> lservintePatients = new List<ServintePatient>();
            ServintePatient servintePatient = null;
            List<ProductsByGroup__c> productsByGroups = new List<ProductsByGroup__c>();
            List<ProductsByGroup__c> productsTmp = null;
            string sunit = string.Empty;
            StringBuilder stringBuilder = new StringBuilder("SELECT Id, WhatId__c, WhatId__r.Address__c, WhatId__r.DocumentNumber__c, WhatId__r.DocumentType__c, WhatId__r.FirstName, WhatId__r.FirstName_c__pc");
            stringBuilder.Append(", WhatId__r.FirstSurname__pc, WhatId__r.Gender__pc, WhatId__r.MiddleName, WhatId__r.Ocupation__pc, WhatId__r.PersonBirthdate, WhatId__r.PersonEmail ,WhatId__r.PersonMobilePhone");
            stringBuilder.Append(", WhatId__r.SecondName__pc, WhatId__r.SecondSurname__pc, GroupId__c, GroupId__r.Name, PlanId__r.Name, PlanId__r.HealthCarePlanId__r.Code__c");
            stringBuilder.Append(", PlanId__r.AgreementId__r.Code__c, PlanId__r.AgreementId__r.Name, PlanId__r.RateId__r.Code__c, WhatId__r.Age2__pc, ActivityDate__c");
            stringBuilder.Append(", AgendaId__r.ProfessionalId__r.DocumentNumber__c, PlanId__r.RateId__r.Name, PlanId__r.HealthCarePlanId__r.Name, PlanId__r.RateId__c, ScheduleId__r.FNC_CentroCostos__c FROM Appointment__c WHERE PatientAttended__c = true AND FNC_MainAppointment__c = true");

            //stringBuilder.Append(" AND Name IN ('AP-0043963835', 'AP-0043967669', 'AP-0040790924', 'AP-0040790920', 'AP-0040790912', 'AP-0040863212', 'AP-0043967668')");
            //stringBuilder.Append(" AND (PlanId__r.Name LIKE '%FNC SANITAS VMI%' OR PlanId__r.Name LIKE '%FNC PROTOCOLO PREPOCOL II-EPI%') AND AgendaId__r.Name <> 'SERVICIOS INVESTIGACIÓN' AND WhatId__r.DocumentNumber__c NOT IN ('INVEST', 'BLOQUEO')");
            if (bIsFamisanar)
            {
                stringBuilder.Append(" AND GroupId__r.Statistics__c = true");
                stringBuilder.Append(" AND (PlanId__r.Name LIKE '%FAMISANAR AIREPOC%' OR PlanId__r.Name LIKE '%FAMISANAR ASMAIRE%')");
            }
            else
            {
                stringBuilder.Append(" AND (PlanId__r.Name LIKE '%SANITAS AIREPOC%' OR PlanId__r.Name LIKE '%VASCU%' OR PlanId__r.Name LIKE '%HTP%' OR PlanId__r.Name LIKE '%COOMEVA MP ASMAIRE%'");
                stringBuilder.Append(" OR GroupId__r.Name LIKE '%VALORACIÓN ANESTESIA%' OR GroupId__r.Name LIKE '%VALORACIÓN FBC%' OR PlanId__r.Name LIKE '%PROTOCOLO%'");
                stringBuilder.Append(" OR PlanId__r.Name LIKE '%SANITAS ASMAIRE%' OR PlanId__r.Name LIKE '%COOMEVA AIREPOC%' OR PlanId__r.Name LIKE '%ALIANSALUD OXIGENAR%'");
                stringBuilder.Append(" OR PlanId__r.Name LIKE '%COLMEDICA AIREPOC%' OR PlanId__r.Name LIKE '%FNC INDICE INTEGRACIÓN UCI%' OR PlanId__r.Name LIKE '%FNC SANITAS VMI%'");
                stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC VENTIL MECANICA INTERMITEN%' OR PlanId__r.Name LIKE '%FNC ECOPETROL ASMAIRE%' OR PlanId__r.Name LIKE '%FNC ECOPETROL AIREPOC%'");
                stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC SURA EPS VMI%' OR PlanId__r.Name LIKE '%FNC SURA VMI%' OR PlanId__r.Name LIKE 'FNC ASMAIRE%'");
                stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC COOMEVA MP AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES ASMAIRE%') AND AgendaId__r.Name <> 'SERVICIOS INVESTIGACIÓN'");
                stringBuilder.Append(" AND WhatId__r.DocumentNumber__c NOT IN ('INVEST', 'BLOQUEO')");
                //stringBuilder.Append(" AND Id NOT IN ('a036e00000z6MtgAAE', 'a036e00000rHT8DAAW', 'a036e00000z34MKAAY', 'a036e00000z354lAAA', 'a036e00000rHQFSAA4', 'a036e00000zSbXVAA0', 'a036e00000zUrz1AAC', 'a036e00000z3GgbAAE', 'a036e00000z4ejfAAA', 'a036e00000zVOkTAAW', 'a036e00000z4Mq6AAE', 'a036e00000z4gbkAAA', 'a036e00000zSc5jAAC', 'a036e00000z354zAAA', 'a036e00000z3Gv8AAE', 'a036e00000rI2AaAAK', 'a036e00000rIMNlAAO', 'a036e00000rJ3auAAC', 'a036e00000z4ejkAAA', 'a036e00000z4nCCAAY', 'a036e00000z4nCEAAY', 'a036e00000z5w7DAAQ', 'a036e00000zUuz1AAC', 'a036e00000rJY9MAAW', 'a036e00000zU9e3AAC', 'a036e00000zUsYuAAK', 'a036e00000z3537AAA', 'a036e00000rJY9eAAG', 'a036e00000rJYA5AAO', 'a036e00000rKZNxAAO', 'a036e00000zVcrCAAS', 'a036e00000rJXobAAG', 'a036e00000rHAYeAAO', 'a036e00000zUqTrAAK', 'a036e00000rJ0HbAAK', 'a036e00000rIPExAAO', 'a036e00000rI1tsAAC', 'a036e00000rJYAIAA4', 'a036e00000rJYAmAAO', 'a036e00000rJYB0AAO', 'a036e00000rJYBAAA4', 'a036e00000rIsOoAAK', 'a036e00000rJXoUAAW', 'a036e00000rJ2hUAAS', 'a036e00000rJXsKAAW', 'a036e00000zUNSNAA4', 'a036e00000z605CAAQ', 'a036e00000rJYBIAA4', 'a036e00000rJYBLAA4', 'a036e00000rKLJ4AAO', 'a036e00000z5zlFAAQ', 'a036e00000zUa1tAAC', 'a036e00000rJZrUAAW', 'a036e00000rJmXHAA0', 'a036e00000zUNOiAAO', 'a036e00000zTMsSAAW', 'a036e00000rJY5oAAG', 'a036e00000z35RaAAI', 'a036e00000z35RgAAI', 'a036e00000z63LfAAI', 'a036e00000zUoFqAAK', 'a036e00000rJXsqAAG', 'a036e00000rJY5wAAG', 'a036e00000z5zlLAAQ', 'a036e00000rJXoDAAW', 'a036e00000z5znAAAQ', 'a036e00000rHnYCAA0', 'a036e00000rHnXoAAK', 'a036e00000rHnXqAAK', 'a036e00000z5znMAAQ', 'a036e00000rJXsvAAG', 'a036e00000zU9dmAAC', 'a036e00000zUaaEAAS', 'a036e00000rJY5zAAG', 'a036e00000rKFfYAAW', 'a036e00000rJY6IAAW', 'a036e00000rJY6KAAW', 'a036e00000rJY6MAAW', 'a036e00000rJY6NAAW', 'a036e00000z4fElAAI', 'a036e00000z4fEsAAI', 'a036e00000z4fEvAAI', 'a036e00000z4fF0AAI', 'a036e00000zVvsoAAC', 'a036e00000z4fF4AAI', 'a036e00000rJXopAAG', 'a036e00000z34EIAAY', 'a036e00000rIuXeAAK', 'a036e00000rIxnMAAS', 'a036e00000z4ejeAAA', 'a036e00000rJXq7AAG', 'a036e00000rJXqZAAW', 'a036e00000rJbCeAAK', 'a036e00000z34MJAAY', 'a036e00000rJY72AAG', 'a036e00000rJY73AAG', 'a036e00000rJY79AAG', 'a036e00000z35CkAAI', 'a036e00000rJkVSAA0', 'a036e00000z60CAAAY', 'a036e00000z60CBAAY', 'a036e00000z6N2OAAU', 'a036e00000rJZqbAAG', 'a036e00000zUtL7AAK', 'a036e00000zUuomAAC', 'a036e00000zUtMDAA0', 'a036e00000z4elqAAA', 'a036e00000rJY4bAAG', 'a036e00000rINNNAA4', 'a036e00000rJ0tUAAS', 'a036e00000rKEOlAAO', 'a036e00000zUZs0AAG', 'a036e00000z344uAAA', 'a036e00000z344yAAA', 'a036e00000zUqVzAAK', 'a036e00000zSbUQAA0', 'a036e00000zUNOHAA4', 'a036e00000zUIGtAAO', 'a036e00000zUNOjAAO', 'a036e00000rJY3MAAW', 'a036e00000z3iWeAAI', 'a036e00000rJY3OAAW', 'a036e00000zVuDRAA0', 'a036e00000z43SwAAI', 'a036e00000z4f5GAAQ', 'a036e00000zVvrzAAC', 'a036e00000z4egtAAA', 'a036e00000rJXqmAAG', 'a036e00000zVv2yAAC', 'a036e00000z34T5AAI', 'a036e00000rIUbjAAG', 'a036e00000rJY3QAAW', 'a036e00000rJY3RAAW', 'a036e00000z63UaAAI', 'a036e00000z5w7BAAQ', 'a036e00000z5w98AAA', 'a036e00000z605HAAQ', 'a036e00000z605KAAQ', 'a036e00000z6MzbAAE', 'a036e00000zUr6FAAS', 'a036e00000zVvooAAC', 'a036e00000rJXqwAAG', 'a036e00000zUb5sAAC', 'a036e00000rJXrCAAW', 'a036e00000rJXtSAAW', 'a036e00000rJXrKAAW', 'a036e00000z34TBAAY', 'a036e00000rJW2CAAW', 'a036e00000rJXufAAG', 'a036e00000rK2HEAA0', 'a036e00000rJY41AAG', 'a036e00000rJY48AAG', 'a036e00000rJY49AAG', 'a036e00000rJY4GAAW', 'a036e00000rJY4JAAW', 'a036e00000z6N0BAAU', 'a036e00000zVQqnAAG', 'a036e00000rHQYZAA4', 'a036e00000rJY7uAAG', 'a036e00000rJY7vAAG', 'a036e00000rJXrWAAW', 'a036e00000rJXrpAAG', 'a036e00000rJZr0AAG', 'a036e00000rJW2MAAW', 'a036e00000rJY4WAAW', 'a036e00000rJY4hAAG', 'a036e00000rJjM4AAK', 'a036e00000rJjMCAA0', 'a036e00000z4nVXAAY', 'a036e00000z6N0pAAE', 'a036e00000zUoN4AAK', 'a036e00000z3iZqAAI', 'a036e00000rJeD8AAK', 'a036e00000rJizYAAS', 'a036e00000rJjGTAA0', 'a036e00000rJjKGAA0', 'a036e00000z4nHJAAY', 'a036e00000z5znJAAQ', 'a036e00000z34CcAAI', 'a036e00000zVvrmAAC', 'a036e00000rK1W3AAK', 'a036e00000rK2I2AAK', 'a036e00000rHnc7AAC', 'a036e00000z34b1AAA', 'a036e00000zVBWXAA4', 'a036e00000z34b2AAA', 'a036e00000zVBZkAAO', 'a036e00000zUuuDAAS', 'a036e00000z4ex9AAA', 'a036e00000z35gfAAA', 'a036e00000zViZ6AAK', 'a036e00000z3H92AAE', 'a036e00000z4fJ5AAI', 'a036e00000z4fJEAAY', 'a036e00000zVj8IAAS', 'a036e00000rHnchAAC', 'a036e00000rHphsAAC', 'a036e00000rHzYlAAK', 'a036e00000rJYBhAAO', 'a036e00000zScc3AAC', 'a036e00000zUa2sAAC', 'a036e00000rJYBvAAO', 'a036e00000rJYByAAO', 'a036e00000z3ifvAAA', 'a036e00000z60F0AAI', 'a036e000010bxNEAAY', 'a036e00000rJYCrAAO', 'a036e00000rJYCsAAO', 'a036e00000z35iFAAQ', 'a036e000010bcEXAAY', 'a036e00000zVibwAAC', 'a036e00000zVA84AAG', 'a036e00000rJYDGAA4', 'a036e00000rJYDMAA4', 'a036e00000rJYDUAA4', 'a036e00000rJYDVAA4', 'a036e00000rJYDaAAO', 'a036e00000z4nILAAY', 'a036e00000z4nIOAAY', 'a036e00000z4nITAAY', 'a036e00000z4nIUAAY', 'a036e00000z6MyDAAU', 'a036e00000zVvqIAAS', 'a036e00000z34vSAAQ', 'a036e00000z600SAAQ', 'a036e00000rJYDnAAO', 'a036e00000zVAC5AAO', 'a036e00000rJs4nAAC', 'a036e00000rJz7KAAS', 'a036e00000z35bTAAQ', 'a036e00000z34ajAAA', 'a036e00000z4exHAAQ', 'a036e00000z4exLAAQ', 'a036e00000z4exOAAQ', 'a036e00000rHgG9AAK', 'a036e00000z5w4uAAA', 'a036e00000rJY10AAG', 'a036e00000z4fGUAAY', 'a036e00000rKfj6AAC', 'a036e00000z43exAAA', 'a036e00000z35ZjAAI', 'a036e00000zVgKoAAK', 'a036e00000rJXz2AAG', 'a036e00000rJWdAAAW', 'a036e00000zUoJQAA0', 'a036e00000rJY1DAAW', 'a036e00000rJY1FAAW', 'a036e00000rJY1jAAG', 'a036e00000rJY1mAAG', 'a036e00000zVgLOAA0', 'a036e00000z3H63AAE', 'a036e00000rJgrUAAS', 'a036e00000rHQErAAO', 'a036e00000rJXzdAAG', 'a036e00000rJXyoAAG', 'a036e00000z5zzFAAQ', 'a036e00000rJY25AAG', 'a036e00000rJY29AAG', 'a036e00000rJjQRAA0', 'a036e00000rJjQdAAK', 'a036e00000z4fGBAAY', 'a036e00000zVgvjAAC', 'a036e00000zVgvlAAC', 'a036e00000zVgvuAAC', 'a036e00000z4fGWAAY', 'a036e00000rI4uPAAS', 'a036e00000rJXwJAAW', 'a036e00000rIR1jAAG', 'a036e00000z34bdAAA', 'a036e00000rJ3aPAAS', 'a036e00000z34asAAA', 'a036e00000rJXysAAG', 'a036e00000rJxMYAA0', 'a036e00000z6N3sAAE', 'a036e00000rJYBqAAO', 'a036e00000zViv1AAC', 'a036e00000rJfT5AAK', 'a036e00000zUNUsAAO', 'a036e00000zUNQEAA4', 'a036e00000z5w2UAAQ', 'a036e00000zVNB0AAO', 'a036e00000zVN9yAAG', 'a036e00000rJmfkAAC', 'a036e00000z35ZdAAI', 'a036e00000z35ZhAAI', 'a036e00000rKWztAAG', 'a036e00000rJXwGAAW', 'a036e00000z5zxjAAA', 'a036e00000rJXzYAAW', 'a036e00000z34vPAAQ', 'a036e000010c0YaAAI', 'a036e00000z60HCAAY', 'a036e00000rJYEQAA4', 'a036e00000rJXwzAAG', 'a036e00000rJXx9AAG', 'a036e00000rJXxCAAW', 'a036e00000rJY0FAAW', 'a036e00000z4ezyAAA', 'a036e00000z4ezoAAA', 'a036e00000z4f06AAA', 'a036e00000rJYEVAA4', 'a036e00000z60HHAAY', 'a036e00000rJXxOAAW', 'a036e00000rJXxhAAG', 'a036e00000rJboQAAS', 'a036e00000z34jXAAQ', 'a036e00000zSbu5AAC', 'a036e00000zVO1EAAW', 'a036e00000z5wINAAY', 'a036e00000rJYEeAAO', 'a036e00000rJYEtAAO', 'a036e00000rJc2dAAC', 'a036e00000rJc2sAAC', 'a036e00000z34bfAAA', 'a036e00000z4eu8AAA', 'a036e00000rJroYAAS', 'a036e00000rJs8uAAC', 'a036e00000z4AgbAAE', 'a036e00000z34ZOAAY', 'a036e00000zVFvbAAG', 'a036e00000z34lPAAQ', 'a036e00000zSbvoAAC', 'a036e00000z34lSAAQ', 'a036e00000zVFuvAAG', 'a036e00000rJYFjAAO', 'a036e00000rJjP2AAK', 'a036e00000zVBUoAAO', 'a036e00000zVFwGAAW', 'a036e00000z35gYAAQ', 'a036e00000z36RIAAY', 'a036e000010cGX8AAM', 'a036e00000zUNXWAA4', 'a036e00000z36RfAAI', 'a036e00000zUIiUAAW', 'a036e00000z4feXAAQ', 'a036e00000rH9HjAAK', 'a036e000010cGMEAA2', 'a036e000010cGLzAAM', 'a036e000010cGXGAA2', 'a036e000010cHp3AAE', 'a036e000010cGMdAAM', 'a036e000010cGNQAA2', 'a036e000010cGNSAA2', 'a036e000010cGNbAAM', 'a036e000010cGNhAAM', 'a036e00000z36RHAAY', 'a036e00000z36S4AAI', 'a036e00000z4mnKAAQ', 'a036e00000z4nuoAAA', 'a036e00000z4nuqAAA', 'a036e00000z5wYSAAY', 'a036e00000z60cMAAQ', 'a036e00000z6NBTAA2', 'a036e000010cHq0AAE', 'a036e000010cGYbAAM', 'a036e000010cGYTAA2', 'a036e000010cGYfAAM', 'a036e000010csKFAAY', 'a036e000010czWyAAI', 'a036e000010cGMHAA2', 'a036e00000rH60BAAS', 'a036e00000z36C1AAI', 'a036e00000rH61kAAC', 'a036e00000z36BnAAI', 'a036e00000rHAyoAAG', 'a036e00000rHHcgAAG', 'a036e00000rHHchAAG', 'a036e00000rHgLiAAK', 'a036e00000zUoVlAAK', 'a036e00000rHgLjAAK', 'a036e00000rJjSVAA0', 'a036e00000z36BuAAI', 'a036e00000z36CAAAY', 'a036e00000z36ADAAY', 'a036e000010cHeOAAU', 'a036e00000z36C4AAI', 'a036e00000z4fYoAAI', 'a036e00000z4noaAAA', 'a036e00000z4nobAAA', 'a036e00000zUa3TAAS', 'a036e00000z60VdAAI', 'a036e00000zSciMAAS', 'a036e00000zVkCdAAK', 'a036e00000zVkBJAA0', 'a036e00000zSciNAAS', 'a036e00000z35qsAAA', 'a036e00000yuZqnAAE', 'a036e00000z35xsAAA', 'a036e00000z35xtAAA', 'a036e00000z35xwAAA', 'a036e00000zUvEmAAK', 'a036e00000zVqpsAAC', 'a036e00000z35quAAA', 'a036e00000zVkCxAAK', 'a036e00000z3HBmAAM', 'a036e00000zSjVqAAK', 'a036e00000zVqohAAC', 'a036e00000z35zBAAQ', 'a036e00000z5wMFAAY', 'a036e00000z43mRAAQ', 'a036e00000zUabAAAS', 'a036e00000rK1y1AAC', 'a036e00000z4fMXAAY', 'a036e000010c3VaAAI', 'a036e00000z4fMpAAI', 'a036e00000z4fMkAAI', 'a036e00000z4fMoAAI', 'a036e00000zVkCMAA0', 'a036e00000zVkkRAAS', 'a036e00000z4fPrAAI', 'a036e00000z4niaAAA', 'a036e00000z5wM6AAI', 'a036e00000zUNVFAA4', 'a036e000010cDtpAAE', 'a036e000010cDtiAAE', 'a036e000010cDtnAAE', 'a036e00000zUoSjAAK', 'a036e00000zUvEsAAK', 'a036e00000zUabDAAS', 'a036e00000zVqnkAAC', 'a036e000010bx2vAAA', 'a036e000010cDtrAAE', 'a036e000010cDtyAAE', 'a036e00000z60JGAAY', 'a036e00000z60JJAAY', 'a036e00000rKGO6AAO', 'a036e00000rJYGYAA4', 'a036e00000zVkCcAAK', 'a036e00000rIYyWAAW', 'a036e00000rIsCsAAK', 'a036e00000rJYGIAA4', 'a036e00000zUNVQAA4', 'a036e00000rJYIdAAO', 'a036e00000zSjWUAA0', 'a036e00000rJYHLAA4', 'a036e00000z35z1AAA', 'a036e00000z35z4AAA', 'a036e00000zUNVlAAO', 'a036e00000rJYI4AAO', 'a036e00000rJYIGAA4', 'a036e00000rJYIIAA4', 'a036e000010cDtTAAU', 'a036e00000rHneZAAS', 'a036e000010c4GCAAY', 'a036e00000rJNIaAAO', 'a036e00000rJjPuAAK', 'a036e00000rKFadAAG', 'a036e00000yuZpaAAE', 'a036e00000z35p2AAA', 'a036e00000z35p7AAA', 'a036e00000rJYIeAAO', 'a036e00000rJYIfAAO', 'a036e00000rJurWAAS', 'a036e00000rJzc1AAC', 'a036e00000z35zAAAQ', 'a036e00000rIUdIAAW', 'a036e00000rJVhtAAG', 'a036e00000rJzezAAC', 'a036e00000rJYL7AAO', 'a036e000010cGGYAA2', 'a036e00000rH1viAAC', 'a036e00000z4fVjAAI', 'a036e00000rHUThAAO', 'a036e00000z4fVfAAI', 'a036e00000rIMWIAA4', 'a036e00000z3658AAA', 'a036e00000z4fVXAAY', 'a036e00000z4fVZAAY', 'a036e000010cGGNAA2', 'a036e00000z4nlmAAA', 'a036e000010cGGXAA2', 'a036e00000z60TDAAY', 'a036e000010cGGKAA2', 'a036e000010cGGUAA2', 'a036e000010cGGPAA2', 'a036e000010cGGVAA2', 'a036e000010cGGqAAM', 'a036e000010cGH1AAM', 'a036e000010cGH5AAM', 'a036e000010cGHiAAM', 'a036e000010cGHkAAM', 'a036e000010cGHsAAM', 'a036e000010cGI2AAM', 'a036e00000z36JmAAI', 'a036e00000rH7tNAAS', 'a036e00000rH8B5AAK', 'a036e00000rH8BTAA0', 'a036e000010cGSAAA2', 'a036e00000rJRijAAG', 'a036e00000z36JoAAI', 'a036e00000rJYPfAAO', 'a036e00000rJjSsAAK', 'a036e00000rJjSwAAK', 'a036e00000rJjSyAAK', 'a036e00000rJjT3AAK', 'a036e00000rJkPWAA0', 'a036e00000z36IDAAY', 'a036e00000z36KMAAY', 'a036e000010cHizAAE', 'a036e000010cGRNAA2', 'a036e00000z5wVXAAY', 'a036e00000z5wVhAAI', 'a036e000010cGRJAA2', 'a036e00000rIUcUAAW', 'a036e00000z36WeAAI', 'a036e000010cGS7AAM', 'a036e000010cHjfAAE', 'a036e000010cz3sAAA', 'a036e000010clcFAAQ', 'a036e000010cGRvAAM', 'a036e000010cGRxAAM', 'a036e000010cGS2AAM', 'a036e000010cGSWAA2', 'a036e00000z6MwrAAE', 'a036e00000z6Mw3AAE', 'a036e00000z4fGSAAY', 'a036e00000rHgGOAA0', 'a036e00000z35bZAAQ', 'a036e00000z34E5AAI', 'a036e00000zUrxnAAC', 'a036e00000z34EJAAY', 'a036e00000rJXsTAAW', 'a036e00000z34T1AAI', 'a036e00000z353IAAQ', 'a036e00000z354nAAA', 'a036e00000zVOkEAAW', 'a036e00000z354pAAA', 'a036e00000zSc5YAAS', 'a036e00000zVOmNAAW', 'a036e00000rHQFPAA4', 'a036e00000rJY9fAAG', 'a036e00000z34EMAAY', 'a036e00000z4sRYAAY', 'a036e00000z354vAAA', 'a036e00000z4f2LAAQ', 'a036e00000rIMNnAAO', 'a036e00000rJY9jAAG', 'a036e00000z4ejaAAA', 'a036e00000zVvpEAAS', 'a036e00000zUsYwAAK', 'a036e00000z4f2QAAQ', 'a036e00000z4f2SAAQ', 'a036e00000zVvrfAAC', 'a036e00000z6MyhAAE', 'a036e00000z6MzFAAU', 'a036e00000zTOApAAO', 'a036e00000z6MrwAAE', 'a036e00000rHgVxAAK', 'a036e00000rJXshAAG', 'a036e00000z43RqAAI', 'a036e00000z5w7AAAQ', 'a036e00000rJYABAA4', 'a036e00000rH5ACAA0', 'a036e00000rHgKAAA0', 'a036e00000z344sAAA', 'a036e00000rHnX5AAK', 'a036e00000rINHkAAO', 'a036e00000z34NiAAI', 'a036e00000z35CiAAI', 'a036e00000rI1tyAAC', 'a036e00000rJYADAA4', 'a036e00000rJYAKAA4', 'a036e00000rJYAoAAO', 'a036e00000rJYAwAAO', 'a036e00000rJYB1AAO', 'a036e00000rJYB3AAO', 'a036e00000rJYB7AAO', 'a036e00000rJZb1AAG', 'a036e00000rIMAnAAO', 'a036e00000rJZlHAAW', 'a036e00000rIdgqAAC', 'a036e00000rJZlvAAG', 'a036e00000rJXsRAAW', 'a036e00000rIMKUAA4', 'a036e00000rJY5YAAW', 'a036e00000rJYBDAA4', 'a036e00000rJYBGAA4', 'a036e00000z4fF3AAI', 'a036e00000rJXnkAAG', 'a036e00000rJXsZAAW', 'a036e00000rJY5fAAG', 'a036e00000zVA8vAAG', 'a036e00000zScByAAK', 'a036e00000zUaa3AAC', 'a036e00000z4fExAAI', 'a036e00000zVvsiAAC', 'a036e00000z35RZAAY', 'a036e00000z35RfAAI', 'a036e00000rJXnzAAG', 'a036e00000rJXo1AAG', 'a036e00000rJXqCAAW', 'a036e00000zUNOmAAO', 'a036e00000rJXsnAAG', 'a036e00000z432dAAA', 'a036e00000zSjT5AAK', 'a036e00000zUaa6AAC', 'a036e00000z35SIAAY', 'a036e00000z35SOAAY', 'a036e00000zVcdmAAC', 'a036e00000z4fEqAAI', 'a036e00000rJXoJAAW', 'a036e00000rHnXfAAK', 'a036e00000rHnXnAAK', 'a036e00000rI29DAAS', 'a036e00000z4ejYAAQ', 'a036e00000rJXqOAAW', 'a036e00000rJXt0AAG', 'a036e00000rJXt5AAG', 'a036e00000rJXt9AAG', 'a036e00000rJXtCAAW', 'a036e00000rJY6QAAW', 'a036e00000z4fErAAI', 'a036e00000zVvsqAAC', 'a036e00000rJXoPAAW', 'a036e00000rJXoaAAG', 'a036e00000rJXocAAG', 'a036e00000rJXogAAG', 'a036e00000rJXolAAG', 'a036e00000rIQoYAAW', 'a036e00000rIQoZAAW', 'a036e00000rIyesAAC', 'a036e00000rJ4IvAAK', 'a036e00000rJmRmAAK', 'a036e00000rJmXMAA0', 'a036e00000rJmXWAA0', 'a036e00000rJmpiAAC', 'a036e00000rJY3bAAG', 'a036e00000zVhULAA0', 'a036e00000rJY77AAG', 'a036e00000rJY7CAAW', 'a036e00000rJY7DAAW', 'a036e00000rJY7IAAW', 'a036e00000rJkh9AAC', 'a036e00000z6N2EAAU', 'a036e00000z6N2NAAU', 'a036e00000zU9duAAC', 'a036e00000rJXp0AAG', 'a036e00000rJXp2AAG', 'a036e00000rJXqDAAW', 'a036e00000z49ONAAY', 'a036e00000zVfdVAAS', 'a036e00000rJdu1AAC', 'a036e00000rKEOCAA4', 'a036e00000z35ClAAI', 'a036e00000z344qAAA', 'a036e00000zUqTFAA0', 'a036e00000zUqViAAK', 'a036e00000zUqYkAAK', 'a036e00000zUqVyAAK', 'a036e00000z344zAAA', 'a036e00000rJdjmAAC', 'a036e00000zSjPvAAK', 'a036e00000z63JOAAY', 'a036e00000z4nERAAY', 'a036e00000z63LiAAI', 'a036e00000zVA8dAAG', 'a036e00000z4f57AAA', 'a036e00000z4f5EAAQ', 'a036e00000z4f5FAAQ', 'a036e00000zVvs7AAC', 'a036e00000z345fAAA', 'a036e00000z345hAAA', 'a036e00000z3GeEAAU', 'a036e00000z6Mr0AAE', 'a036e00000rJXqaAAG', 'a036e00000zUsGyAAK', 'a036e00000rJY3TAAW', 'a036e00000z4nU7AAI', 'a036e00000z5w9PAAQ', 'a036e00000z6MrUAAU', 'a036e00000zUNNfAAO', 'a036e00000rJXrMAAW', 'a036e00000rJ0FGAA0', 'a036e00000rJY3dAAG', 'a036e00000rJY42AAG', 'a036e00000rJY44AAG', 'a036e00000rJY4kAAG', 'a036e00000z6N0JAAU', 'a036e00000rJY7tAAG', 'a036e00000rJY81AAG', 'a036e00000rJY85AAG', 'a036e00000rJY88AAG', 'a036e00000rJXrSAAW', 'a036e00000rJXseAAG', 'a036e00000rJkgaAAC', 'a036e00000rJXrjAAG', 'a036e00000rJXrmAAG', 'a036e00000rJW2NAAW', 'a036e00000zSbhEAAS', 'a036e00000rJY4XAAW', 'a036e00000rJY4YAAW', 'a036e00000rJjM5AAK', 'a036e00000z34CZAAY', 'a036e00000z34NkAAI', 'a036e00000z34E4AAI', 'a036e00000z34T9AAI', 'a036e00000z34CYAAY', 'a036e00000z34EPAAY', 'a036e00000rJkVNAA0', 'a036e00000z4f2gAAA', 'a036e00000z4f2fAAA', 'a036e00000rHQFEAA4', 'a036e00000rHQFVAA4', 'a036e00000rJY9ZAAW', 'a036e00000rHQFDAA4', 'a036e00000z34b5AAA', 'a036e00000z4mgIAAQ', 'a036e00000z3GqKAAU', 'a036e00000z4ex5AAA', 'a036e00000z4f05AAA', 'a036e00000z4nKcAAI', 'a036e00000z5w2GAAQ', 'a036e00000z5w2RAAQ', 'a036e00000z35i5AAA', 'a036e00000z3H96AAE', 'a036e00000z4fJ6AAI', 'a036e00000zViv2AAC', 'a036e00000z4fJCAAY', 'a036e00000rHncwAAC', 'a036e00000z4fJPAAY', 'a036e00000z60HFAAY', 'a036e00000zUNUtAAO', 'a036e00000zVjNhAAK', 'a036e00000z60EuAAI', 'a036e00000rJYCbAAO', 'a036e00000rJYCcAAO', 'a036e00000rJYCjAAO', 'a036e00000rJYCtAAO', 'a036e00000rJYCvAAO', 'a036e00000zViZ5AAK', 'a036e00000z35iJAAQ', 'a036e00000z35iKAAQ', 'a036e00000z35iMAAQ', 'a036e00000zVvqFAAS', 'a036e00000zVvqOAAS', 'a036e00000rJYDEAA4', 'a036e00000rJYDXAA4', 'a036e00000rJYDeAAO', 'a036e00000z4nIPAAY', 'a036e00000z6MvsAAE', 'a036e00000rJCo3AAG', 'a036e00000rJY0sAAG', 'a036e00000rJYDiAAO', 'a036e00000z35bUAAQ', 'a036e00000z34aoAAA', 'a036e00000rHUSmAAO', 'a036e00000z4ex1AAA', 'a036e00000z4exPAAQ', 'a036e00000rHnZtAAK', 'a036e00000rJY13AAG', 'a036e00000rJY1CAAW', 'a036e00000z4fGYAAY', 'a036e00000z4fGZAAY', 'a036e00000z35ZlAAI', 'a036e00000z35ZtAAI', 'a036e00000z35ZuAAI', 'a036e00000zVgJtAAK', 'a036e00000z63PpAAI', 'a036e00000rIY4FAAW', 'a036e00000rJ6PgAAK', 'a036e00000rJtrvAAC', 'a036e00000rJWcvAAG', 'a036e00000rJY23AAG', 'a036e00000z35bVAAQ', 'a036e00000zVgOTAA0', 'a036e00000z4As4AAE', 'a036e00000z4fG9AAI', 'a036e00000rHHYJAA4', 'a036e00000rJXySAAW', 'a036e00000rJeVVAA0', 'a036e00000rJXylAAG', 'a036e00000z5w2HAAQ', 'a036e00000rJY1uAAG', 'a036e00000rJY2CAAW', 'a036e00000rJY2FAAW', 'a036e00000rJY2GAAW', 'a036e00000rJdvEAAS', 'a036e00000z4fGPAAY', 'a036e00000z4fGaAAI', 'a036e000010buvFAAQ', 'a036e00000z5wFcAAI', 'a036e00000z60F7AAI', 'a036e00000rHnZIAA0', 'a036e00000zVBWvAAO', 'a036e00000z5zzAAAQ', 'a036e00000rJXyvAAG', 'a036e00000z5w2KAAQ', 'a036e00000rJs49AAC', 'a036e00000rJtqOAAS', 'a036e00000rJzHeAAK', 'a036e00000z34txAAA', 'a036e00000rINSoAAO', 'a036e00000rINSrAAO', 'a036e00000z60HAAAY', 'a036e00000rJXxlAAG', 'a036e00000rJNoUAAW', 'a036e00000z5w2EAAQ', 'a036e00000rJY0rAAG', 'a036e00000zUNRCAA4', 'a036e00000zUZr7AAG', 'a036e00000rJXwBAAW', 'a036e00000z34tzAAA', 'a036e00000z34v9AAA', 'a036e00000z34vCAAQ', 'a036e00000zSc0rAAC', 'a036e00000z34vJAAQ', 'a036e00000rKU8rAAG', 'a036e00000rIycLAAS', 'a036e00000rJYE5AAO', 'a036e00000rJXzOAAW', 'a036e00000rJXzZAAW', 'a036e00000rJXzaAAG', 'a036e00000rJXzeAAG', 'a036e00000rJXzqAAG', 'a036e00000zVNBdAAO', 'a036e00000z34vMAAQ', 'a036e00000zVNC5AAO', 'a036e00000rJYEPAA4', 'a036e00000zTOBgAAO', 'a036e00000rJXwQAAW', 'a036e00000rJXx4AAG', 'a036e00000rJXxAAAW', 'a036e00000rJY0jAAG', 'a036e00000rJkUPAA0', 'a036e00000rJsdnAAC', 'a036e00000z4ezvAAA', 'a036e00000zUNUoAAO', 'a036e00000rJYEUAA4', 'a036e00000z60H7AAI', 'a036e00000rJYEZAA4', 'a036e00000rJXxRAAW', 'a036e00000rJXxjAAG', 'a036e00000rJXxkAAG', 'a036e00000zUZr6AAG', 'a036e00000z34jZAAQ', 'a036e00000rJYEdAAO', 'a036e00000rJYExAAO', 'a036e00000rJYFeAAO', 'a036e00000z4eu7AAA', 'a036e00000rJrqAAAS', 'a036e00000zVBWnAAO', 'a036e00000z34lOAAQ', 'a036e00000zVFysAAG', 'a036e00000zVFvIAAW', 'a036e00000zSbviAAC', 'a036e00000z34lWAAQ', 'a036e00000rJYFlAAO', 'a036e00000rJYFrAAO', 'a036e00000rJjP4AAK', 'a036e00000rJjPDAA0', 'a036e00000rJjPHAA0', 'a036e00000z4sKKAAY', 'a036e00000zSbm5AAC', 'a036e00000z34ldAAA', 'a036e00000zVFvPAAW', 'a036e00000z35gOAAQ', 'a036e00000z36QpAAI', 'a036e00000z60cYAAQ', 'a036e000010cGM5AAM', 'a036e000010cGLpAAM', 'a036e000010cGMMAA2', 'a036e00000rH9elAAC', 'a036e00000rI7aFAAS', 'a036e00000yua4PAAQ', 'a036e000010cGMbAAM', 'a036e000010cGNRAA2', 'a036e000010cGNWAA2', 'a036e000010cGNXAA2', 'a036e000010cGNaAAM', 'a036e000010cNYYAA2', 'a036e000010cPYpAAM', 'a036e000010cUcdAAE', 'a036e00000z36RMAAY', 'a036e00000z3HQ5AAM', 'a036e00000z4femAAA', 'a036e000010cGX7AAM', 'a036e00000z4mnAAAQ', 'a036e000010cGX4AAM', 'a036e00000z6NB9AAM', 'a036e000010biEeAAI', 'a036e000010cGWuAAM', 'a036e000010cGWxAAM', 'a036e000010cGXEAA2', 'a036e000010cGXtAAM', 'a036e000010cGXyAAM', 'a036e000010cGYhAAM', 'a036e000010cGMNAA2', 'a036e00000rH61jAAC', 'a036e00000rH6rAAAS', 'a036e00000rH6rBAAS', 'a036e00000rH6rCAAS', 'a036e00000rHejIAAS', 'a036e00000rJjSLAA0', 'a036e00000rJjSRAA0', 'a036e00000rJohdAAC', 'a036e000010cHfhAAE', 'a036e00000yuZypAAE', 'a036e000010bjn4AAA', 'a036e00000z3HKiAAM', 'a036e00000z3HKxAAM', 'a036e00000z43slAAA', 'a036e00000z4noHAAQ', 'a036e00000z5wStAAI', 'a036e00000z60VXAAY', 'a036e00000z60VfAAI', 'a036e00000z6N8vAAE', 'a036e00000z35qtAAA', 'a036e00000zScm2AAC', 'a036e00000yt0hQAAQ', 'a036e00000z4B1dAAE', 'a036e00000z35xyAAA', 'a036e000010c3owAAA', 'a036e00000zVkAfAAK', 'a036e00000z4fMrAAI', 'a036e00000z35z2AAA', 'a036e00000zVqnhAAC', 'a036e00000z35z7AAA', 'a036e00000z5wMKAAY', 'a036e00000zVkYoAAK', 'a036e00000z4fMgAAI', 'a036e00000z4fMhAAI', 'a036e00000zVkYqAAK', 'a036e00000zVkYsAAK', 'a036e00000zVkkrAAC', 'a036e00000z4fPsAAI', 'a036e00000zVr6YAAS', 'a036e00000z4fQCAAY', 'a036e00000z4niZAAQ', 'a036e00000z60JHAAY', 'a036e00000z5wMAAAY', 'a036e00000z60NOAAY', 'a036e00000z60NSAAY', 'a036e00000z63jcAAA', 'a036e000010cDtXAAU', 'a036e00000zUNVuAAO', 'a036e00000zUab5AAC', 'a036e00000zVrTbAAK', 'a036e000010cDtwAAE', 'a036e000010cDtzAAE', 'a036e000010cDu0AAE', 'a036e000010cDu2AAE', 'a036e000010cDu3AAE', 'a036e00000rHndtAAC', 'a036e00000rJYGWAA4', 'a036e00000zUNVPAA4', 'a036e000010cDu5AAE', 'a036e000010cDuqAAE', 'a036e00000z35qrAAA', 'a036e00000rHndlAAC', 'a036e00000rIYyUAAW', 'a036e00000z60JCAAY', 'a036e00000rJYGKAA4', 'a036e00000rJYGQAA4', 'a036e00000rJYGZAA4', 'a036e00000zUNVLAA4', 'a036e00000z60JEAAY', 'a036e00000rK1tGAAS', 'a036e00000rJYGoAAO', 'a036e00000z5wMJAAY', 'a036e000010cDtVAAU', 'a036e000010cNHaAAM', 'a036e00000rJYGtAAO', 'a036e000010c0jNAAQ', 'a036e00000rJYHbAAO', 'a036e00000rJYHhAAO', 'a036e00000rJYHmAAO', 'a036e00000rJYIDAA4', 'a036e00000rJYIEAA4', 'a036e00000rJYIJAA4', 'a036e00000rJjPgAAK', 'a036e00000rJjPjAAK', 'a036e00000rJjPoAAK', 'a036e00000rJjPsAAK', 'a036e000010cNdFAAU', 'a036e00000rIMSAAA4', 'a036e00000rJW7dAAG', 'a036e00000rJv3SAAS', 'a036e00000rKFbHAAW', 'a036e00000zVkYpAAK', 'a036e00000z4fMtAAI', 'a036e00000zVkkqAAC', 'a036e00000z35p5AAA', 'a036e00000rJjHHAA0', 'a036e000010cGIAAA2', 'a036e000010cGIBAA2', 'a036e000010cPWzAAM', 'a036e000010cPbFAAU', 'a036e000010cijHAAQ', 'a036e00000rJVcsAAG', 'a036e00000rJVL2AAO', 'a036e000010cAPkAAM', 'a036e000010cAPlAAM', 'a036e00000rJb7cAAC', 'a036e00000rJb7dAAC', 'a036e00000z37YbAAI', 'a036e00000z37YcAAI', 'a036e000010cAPhAAM', 'a036e00000z3650AAA', 'a036e000010cGHFAA2', 'a036e00000zVAHRAA4', 'a036e000010cV87AAE', 'a036e00000z3653AAA', 'a036e00000rH2XUAA0', 'a036e00000rHUTgAAO', 'a036e00000rHUTiAAO', 'a036e00000z4fVVAAY', 'a036e00000z4fVUAAY', 'a036e00000rHUTzAAO', 'a036e00000z4fVgAAI', 'a036e00000rJYL6AAO', 'a036e00000rKU1lAAG', 'a036e00000z363QAAQ', 'a036e00000z363VAAQ', 'a036e00000z43qzAAA', 'a036e00000z4B40AAE', 'a036e00000z4nllAAA', 'a036e00000z4nlnAAA', 'a036e00000z5wPaAAI', 'a036e00000z60TFAAY', 'a036e00000z60TIAAY', 'a036e000010cGGSAA2', 'a036e00000z6N83AAE', 'a036e000010c0l9AAA', 'a036e000010cHUzAAM', 'a036e000010cGGLAA2', 'a036e000010cGH8AAM', 'a036e00000rH7cIAAS', 'a036e00000rH7caAAC', 'a036e00000z36JzAAI', 'a036e000010cGRdAAM', 'a036e00000z4fbgAAA', 'a036e00000rH7zmAAC', 'a036e00000rHzbyAAC', 'a036e00000rIMYfAAO', 'a036e000010cGRLAA2', 'a036e00000rItTaAAK', 'a036e00000rJjT0AAK', 'a036e00000z36JvAAI', 'a036e00000rJkbQAAS', 'a036e00000rJohTAAS', 'a036e00000rK3toAAC', 'a036e00000z36IeAAI', 'a036e00000z36IhAAI', 'a036e00000z36KHAAY', 'a036e00000z36KLAAY', 'a036e00000z36KPAAY', 'a036e00000z36KQAAY', 'a036e00000z4nrkAAA', 'a036e000010cGRKAA2', 'a036e00000z60YzAAI', 'a036e00000z60Z0AAI', 'a036e000010cHjlAAE', 'a036e00000rIUdwAAG', 'a036e00000z6NAEAA2', 'a036e00000z6NAVAA2', 'a036e00000z6NAXAA2', 'a036e000010cGRXAA2', 'a036e000010cGS8AAM', 'a036e000010cGcKAAU', 'a036e000010crtWAAQ', 'a036e000010cGSTAA2', 'a036e000010cGScAAM', 'a036e000010coFvAAI', 'a036e00000rHT8CAAW', 'a036e00000rHUTSAA4', 'a036e00000rHgGYAA0', 'a036e00000rHglCAAS', 'a036e00000zSbT1AAK', 'a036e00000zUs0IAAS', 'a036e00000z353FAAQ', 'a036e00000z353GAAQ', 'a036e00000z353aAAA', 'a036e00000z354rAAA', 'a036e00000rHQFGAA4', 'a036e00000rHQFRAA4', 'a036e00000rJY9oAAG', 'a036e00000rKFZuAAO', 'a036e00000zUrxyAAC', 'a036e00000z3GgfAAE', 'a036e00000z354uAAA', 'a036e00000z4f2JAAQ', 'a036e00000zVcrHAAS', 'a036e00000rJOOtAAO', 'a036e00000zVcrEAAS', 'a036e00000z4AUcAAM', 'a036e00000z4nCDAAY', 'a036e00000z5vtiAAA', 'a036e00000z4f2TAAQ', 'a036e00000z4f2VAAQ', 'a036e00000z4f2oAAA', 'a036e00000rJY9RAAW', 'a036e00000rJYBCAA4', 'a036e00000rJY9aAAG', 'a036e00000z6MseAAE', 'a036e00000zVvrbAAC', 'a036e00000rJY4IAAW', 'a036e00000rJY9cAAG', 'a036e00000rJY9iAAG', 'a036e00000rJY9kAAG', 'a036e00000z344oAAA', 'a036e00000z344pAAA', 'a036e00000rJXsaAAG', 'a036e00000rJCqaAAG', 'a036e00000rJYAFAA4', 'a036e00000rJYAlAAO', 'a036e00000rJYAvAAO', 'a036e00000rJYAxAAO', 'a036e00000rJYAyAAO', 'a036e00000rJYB8AAO', 'a036e00000rJXjzAAG', 'a036e00000rJ3fnAAC', 'a036e00000rJc8sAAC', 'a036e00000rJYBEAA4', 'a036e00000rJYBKAA4', 'a036e00000rJYBMAA4', 'a036e00000rK2I7AAK', 'a036e00000z35SPAAY', 'a036e00000zUNNbAAO', 'a036e00000rJXsVAAW', 'a036e00000rJXsbAAG', 'a036e00000z3lyXAAQ', 'a036e00000z63WuAAI', 'a036e00000rJY5nAAG', 'a036e00000yuZhiAAE', 'a036e00000z35QfAAI', 'a036e00000z35QgAAI', 'a036e00000z35RBAAY', 'a036e00000zUv5hAAC', 'a036e00000zVcZhAAK', 'a036e00000zUa23AAC', 'a036e00000rHAaTAAW', 'a036e00000rJXskAAG', 'a036e00000zVECkAAO', 'a036e00000rJXsoAAG', 'a036e00000rJY5uAAG', 'a036e00000zVcaXAAS', 'a036e00000z3H2rAAE', 'a036e00000zSbOJAA0', 'a036e00000rJXo7AAG', 'a036e00000rJXqJAAW', 'a036e00000rIMBzAAO', 'a036e00000rJXtAAAW', 'a036e00000zScFcAAK', 'a036e00000rJY6OAAW', 'a036e00000rJY6PAAW', 'a036e00000z4fEdAAI', 'a036e00000zVdAmAAK', 'a036e00000z4fEkAAI', 'a036e00000z4fF1AAI', 'a036e00000rJXojAAG', 'a036e00000zSbUAAA0', 'a036e00000z4elmAAA', 'a036e00000rJmVUAA0', 'a036e00000rJmZwAAK', 'a036e00000rJY70AAG', 'a036e00000z35CjAAI', 'a036e00000z6N2CAAU', 'a036e00000z6N2PAAU', 'a036e00000zSjUHAA0', 'a036e00000zU9dvAAC', 'a036e00000rJXosAAG', 'a036e00000rJXovAAG', 'a036e00000rJXoxAAG', 'a036e00000z4egrAAA', 'a036e00000rJXqIAAW', 'a036e00000z5vtPAAQ', 'a036e00000rJdk1AAC', 'a036e00000zUtMdAAK', 'a036e00000zUuoyAAC', 'a036e00000rHHZCAA4', 'a036e00000rJxJeAAK', 'a036e00000zVhUMAA0', 'a036e00000rINNLAA4', 'a036e00000z354wAAA', 'a036e00000rJY3eAAG', 'a036e00000rJudDAAS', 'a036e00000rKBKHAA4', 'a036e00000z35CgAAI', 'a036e00000zScCFAA0', 'a036e00000zSbMHAA0', 'a036e00000rJZqvAAG', 'a036e00000z3iIsAAI', 'a036e00000rJXqNAAW', 'a036e00000rJXqPAAW', 'a036e00000rJXqWAAW', 'a036e00000z4elrAAA', 'a036e00000z4eltAAA', 'a036e00000z4nETAAY', 'a036e00000z6MtYAAU', 'a036e00000zVfh0AAC', 'a036e00000rJY3IAAW', 'a036e00000z63UbAAI', 'a036e00000z4sRzAAI', 'a036e00000zVvs0AAC', 'a036e00000z4f59AAA', 'a036e00000zUqVWAA0', 'a036e00000z5vrLAAQ', 'a036e00000z63HUAAY', 'a036e00000z5vteAAA', 'a036e00000rJXqjAAG', 'a036e00000rJXqtAAG', 'a036e00000zVA2vAAG', 'a036e00000zVv2rAAC', 'a036e00000rINIjAAO', 'a036e00000rJY3SAAW', 'a036e00000z4nU8AAI', 'a036e00000z63X2AAI', 'a036e00000z6MzpAAE', 'a036e00000rJXrAAAW', 'a036e00000rJ3g2AAC', 'a036e00000rJW2TAAW', 'a036e00000rJW2IAAW', 'a036e00000rJW2JAAW', 'a036e00000rJyp5AAC', 'a036e00000rJY4UAAW', 'a036e00000rJY4VAAW', 'a036e00000zUv1RAAS', 'a036e00000zVQqoAAG', 'a036e00000rHnbgAAC', 'a036e00000rJY7wAAG', 'a036e00000rJY86AAG', 'a036e00000rJY8AAAW', 'a036e00000rJXrZAAW', 'a036e00000rJXraAAG', 'a036e00000rJkLOAA0', 'a036e00000rJmbxAAC', 'a036e00000rJjM0AAK', 'a036e00000z6N0eAAE', 'a036e00000rJay6AAC', 'a036e00000rJbPgAAK', 'a036e00000z34EFAAY', 'a036e00000z34CbAAI', 'a036e00000zUtM4AAK', 'a036e00000z4nHLAAY', 'a036e00000rJogBAAS', 'a036e00000z354qAAA', 'a036e00000zSc91AAC', 'a036e00000rK1VJAA0', 'a036e00000rK2IgAAK', 'a036e00000rK5HlAAK', 'a036e00000rJY9YAAW', 'a036e00000rHHaIAAW', 'a036e00000z34b4AAA', 'a036e00000z34b8AAA', 'a036e00000zVBUYAA4', 'a036e00000z4exQAAQ', 'a036e00000z35bXAAQ', 'a036e00000rHmQzAAK', 'a036e00000z35ggAAA', 'a036e00000z35giAAA', 'a036e00000z35gnAAA', 'a036e00000z35i7AAA', 'a036e00000zUoQnAAK', 'a036e00000zViXBAA0', 'a036e00000z35iEAAQ', 'a036e00000zViXXAA0', 'a036e00000z35iRAAQ', 'a036e00000z4fIzAAI', 'a036e00000z4fJBAAY', 'a036e00000zViv4AAC', 'a036e000010c0boAAA', 'a036e00000z4fJFAAY', 'a036e00000rJYC7AAO', 'a036e00000z5wFfAAI', 'a036e00000rHnceAAC', 'a036e00000z4fGMAAY', 'a036e00000zVgvkAAC', 'a036e00000rJYBuAAO', 'a036e00000z60FDAAY', 'a036e00000z4fJOAAY', 'a036e00000zVj8JAAS', 'a036e00000zVj8KAAS', 'a036e00000z60F2AAI', 'a036e00000zSjUmAAK', 'a036e00000zVjNiAAK', 'a036e000010bwjVAAQ', 'a036e000010bxTgAAI', 'a036e000010c0WUAAY', 'a036e00000rJYC5AAO', 'a036e00000rJYCYAA4', 'a036e00000rJYCwAAO', 'a036e00000z35iGAAQ', 'a036e00000z4euCAAQ', 'a036e00000z4euMAAQ', 'a036e00000z6MwhAAE', 'a036e00000rJY1GAAW', 'a036e00000zUoKZAA0', 'a036e00000rJYD1AAO', 'a036e00000rJYDIAA4', 'a036e00000rJYDZAA4', 'a036e00000rJYDcAAO', 'a036e00000z4nISAAY', 'a036e00000rJY2LAAW', 'a036e00000z600UAAQ', 'a036e00000rJY0tAAG', 'a036e00000z600TAAQ', 'a036e00000rJs4TAAS', 'a036e00000rJuozAAC', 'a036e00000rJXwKAAW', 'a036e00000z34ZPAAY', 'a036e00000rH71TAAS', 'a036e00000rHUSpAAO', 'a036e00000rHUT3AAO', 'a036e00000rJY14AAG', 'a036e00000z35ZrAAI', 'a036e00000z35bPAAQ', 'a036e00000z35bQAAQ', 'a036e00000rJXygAAG', 'a036e00000rJY01AAG', 'a036e00000z34lZAAQ', 'a036e00000z4erSAAQ', 'a036e00000z34lIAAQ', 'a036e00000rJuhFAAS', 'a036e00000rJY1YAAW', 'a036e00000rJY1aAAG', 'a036e00000rJY1bAAG', 'a036e00000rJY1dAAG', 'a036e00000rHQExAAO', 'a036e00000rHQEzAAO', 'a036e00000rJXyVAAW', 'a036e00000rJXybAAG', 'a036e00000rJXymAAG', 'a036e00000z63PjAAI', 'a036e00000zUwnZAAS', 'a036e00000rJY26AAG', 'a036e00000rJY2BAAW', 'a036e00000rJjQVAA0', 'a036e00000z4fGOAAY', 'a036e00000zVgvsAAC', 'a036e00000z5wFmAAI', 'a036e00000rHHb3AAG', 'a036e00000zUNQLAA4', 'a036e00000rIMFsAAO', 'a036e00000rJ3acAAC', 'a036e00000z5zzEAAQ', 'a036e00000rJmYFAA0', 'a036e00000rJxA8AAK', 'a036e00000zVNAaAAO', 'a036e00000yuZUEAA2', 'a036e00000z34twAAA', 'a036e000010bwjpAAA', 'a036e00000zSjUlAAK', 'a036e00000rJrkMAAS', 'a036e00000rJXz7AAG', 'a036e00000rJXz9AAG', 'a036e00000zVNAVAA4', 'a036e00000z34vEAAQ', 'a036e00000z35iOAAQ', 'a036e00000zUZrDAAW', 'a036e00000zUZrCAAW', 'a036e00000rJY2DAAW', 'a036e00000rJY2KAAW', 'a036e00000z600fAAA', 'a036e00000rJXzfAAG', 'a036e00000rJXzgAAG', 'a036e00000rJXzhAAG', 'a036e00000zSc2RAAS', 'a036e00000z4gZFAAY', 'a036e00000z3GsNAAU', 'a036e00000z60H9AAI', 'a036e00000rJXwoAAG', 'a036e00000rJXx2AAG', 'a036e00000rJXx6AAG', 'a036e00000rJXzuAAG', 'a036e00000rJs9dAAC', 'a036e00000z34lNAAQ', 'a036e00000z4ezuAAA', 'a036e00000z4ezxAAA', 'a036e00000zVvr7AAC', 'a036e00000rJYESAA4', 'a036e00000rJYETAA4', 'a036e00000zUNUqAAO', 'a036e00000rJYEWAA4', 'a036e00000rJYEaAAO', 'a036e00000rJXxPAAW', 'a036e00000rJY1kAAG', 'a036e00000rJs1AAAS', 'a036e00000rJXxbAAG', 'a036e00000zUNQSAA4', 'a036e00000yuZTAAA2', 'a036e00000z34jYAAQ', 'a036e00000z34jeAAA', 'a036e00000zUIreAAG', 'a036e00000z34lGAAQ', 'a036e00000zVFv7AAG', 'a036e00000zVA7bAAG', 'a036e00000z43hkAAA', 'a036e00000rJYEpAAO', 'a036e00000rJYFdAAO', 'a036e00000rJYFgAAO', 'a036e00000rJYFhAAO', 'a036e00000zVBZlAAO', 'a036e00000rJrmXAAS', 'a036e00000zVA7gAAG', 'a036e00000z34lVAAQ', 'a036e00000rJYFoAAO', 'a036e00000rJYFyAAO', 'a036e00000rJYFzAAO', 'a036e00000rJjPAAA0', 'a036e00000rJjPCAA0', 'a036e00000rJjPFAA0', 'a036e00000rJjioAAC', 'a036e00000z34atAAA', 'a036e00000z34awAAA', 'a036e00000z34leAAA', 'a036e00000zVFvQAAW', 'a036e00000z34liAAA', 'a036e00000z35iQAAQ', 'a036e00000rKG5nAAG', 'a036e00000z4fJAAAY', 'a036e00000z35gSAAQ', 'a036e00000z35gXAAQ', 'a036e00000z36RCAAY', 'a036e00000z60cSAAQ', 'a036e00000rH8zLAAS', 'a036e00000rH90nAAC', 'a036e00000z36RJAAY', 'a036e00000z36QlAAI', 'a036e00000rH91HAAS', 'a036e00000z4fejAAA', 'a036e000010cGNcAAM', 'a036e000010cGMBAA2', 'a036e000010cGMTAA2', 'a036e00000rH9ehAAC', 'a036e000010cGNeAAM', 'a036e000010cGNiAAM', 'a036e00000z36QhAAI', 'a036e000010cr8BAAQ', 'a036e00000z36RAAAY', 'a036e00000z4fehAAA', 'a036e000010ckzQAAQ', 'a036e00000z4mnOAAQ', 'a036e00000z4nurAAA', 'a036e00000z6NAtAAM', 'a036e00000z6NBAAA2', 'a036e000010cHp1AAE', 'a036e000010cGX3AAM', 'a036e000010cGXzAAM', 'a036e000010cGYXAA2', 'a036e000010cGYgAAM', 'a036e000010cGYkAAM', 'a036e00000z36C2AAI', 'a036e00000rH65VAAS', 'a036e00000z4fYUAAY', 'a036e00000z4fYVAAY', 'a036e00000rH6OhAAK', 'a036e00000rH6boAAC', 'a036e00000z36C9AAI', 'a036e00000rIPV6AAO', 'a036e00000rJYNMAA4', 'a036e00000rJjSMAA0', 'a036e00000rJjSOAA0', 'a036e00000rJjSPAA0', 'a036e000010cHcqAAE', 'a036e00000rK3tiAAC', 'a036e00000z36AOAAY', 'a036e000010bjn3AAA', 'a036e00000z36BzAAI', 'a036e00000zUoVcAAK', 'a036e00000z5wSqAAI', 'a036e00000z6N99AAE', 'a036e00000z6N9GAAU', 'a036e00000z6N9TAAU', 'a036e000010cGM0AAM', 'a036e00000zVkCGAA0', 'a036e00000z35qpAAA', 'a036e00000z35qqAAA', 'a036e00000zSjWMAA0', 'a036e00000z4fPpAAI', 'a036e00000z4fMbAAI', 'a036e00000z4fMdAAI', 'a036e00000z4fMmAAI', 'a036e00000z4fMnAAI', 'a036e00000zVr6cAAC', 'a036e000010cDtCAAU', 'a036e00000z6N5eAAE', 'a036e000010bvdfAAA', 'a036e000010bveZAAQ', 'a036e000010c3BSAAY', 'a036e000010cDthAAE', 'a036e00000z60NJAAY', 'a036e00000z6N6SAAU', 'a036e00000z6N6UAAU', 'a036e00000z6N6aAAE', 'a036e00000zUNVsAAO', 'a036e000010cDtQAAU', 'a036e000010cDtPAAU', 'a036e00000zUabEAAS', 'a036e000010cDtvAAE', 'a036e000010cDu9AAE', 'a036e000010cDueAAE', 'a036e00000rIYyYAAW', 'a036e00000rHiTvAAK', 'a036e00000rHne2AAC', 'a036e00000rJYGHAA4', 'a036e00000rJYGaAAO', 'a036e00000rJYGbAAO', 'a036e00000rKFTUAA4', 'a036e00000rJYGrAAO', 'a036e000010cDtgAAE', 'a036e00000rJYHMAA4', 'a036e00000rJYHOAA4', 'a036e00000rJYHaAAO', 'a036e00000rJYHdAAO', 'a036e00000rJYHkAAO', 'a036e00000rJYHpAAO', 'a036e00000rHEu2AAG', 'a036e000010cDtdAAE', 'a036e00000rJYI5AAO', 'a036e00000rJYI8AAO', 'a036e00000rJYIAAA4', 'a036e00000rJbVIAA0', 'a036e00000rJjPkAAK', 'a036e00000rJjPqAAK', 'a036e000010cNcqAAE', 'a036e00000zUNVMAA4', 'a036e00000z4fMfAAI', 'a036e00000z35qmAAA', 'a036e00000rJjQvAAK', 'a036e00000zUNVqAAO', 'a036e000010cGI4AAM', 'a036e000010cGIHAA2', 'a036e000010cGIIAA2', 'a036e00000rJVL5AAO', 'a036e00000rHn2dAAC', 'a036e00000rINmEAAW', 'a036e00000z37Y8AAI', 'a036e00000rJVcqAAG', 'a036e00000z37Y7AAI', 'a036e00000rJVL6AAO', 'a036e00000rJb7ZAAS', 'a036e000010cAPnAAM', 'a036e00000rJb7kAAC', 'a036e000010cAPpAAM', 'a036e00000z37YXAAY', 'a036e00000z37YaAAI', 'a036e00000z37YeAAI', 'a036e00000z363NAAQ', 'a036e00000z60TTAAY', 'a036e000010cGGWAA2', 'a036e00000rH1wxAAC', 'a036e00000rH1zuAAC', 'a036e00000z4fVWAAY', 'a036e00000z4fViAAI', 'a036e00000z4fVaAAI', 'a036e00000z4fVbAAI', 'a036e00000z363OAAQ', 'a036e00000z3HHwAAM', 'a036e00000z4meMAAQ', 'a036e00000z4mePAAQ', 'a036e00000z60TBAAY', 'a036e000010cGGIAA2', 'a036e000010cHVYAA2', 'a036e00000z60TMAAY', 'a036e00000z6N7zAAE', 'a036e00000z6N85AAE', 'a036e000010biUMAAY', 'a036e000010c44FAAQ', 'a036e000010cGH3AAM', 'a036e000010cGH7AAM', 'a036e000010cGHKAA2', 'a036e000010cGHhAAM', 'a036e000010cGHtAAM', 'a036e00000z36JtAAI', 'a036e00000rH7cZAAS', 'a036e000010cHhjAAE', 'a036e00000z4fbcAAA', 'a036e00000rH8AyAAK', 'a036e00000rHB0XAAW', 'a036e000010cGS5AAM', 'a036e00000rJjSuAAK', 'a036e00000rJjT4AAK', 'a036e00000rJjT6AAK', 'a036e000010cHitAAE', 'a036e00000z36INAAY', 'a036e00000z36IdAAI', 'a036e00000z36IfAAI', 'a036e000010clc5AAA', 'a036e00000z4nDVAAY', 'a036e000010cGRTAA2', 'a036e000010c0v4AAA', 'a036e00000z4nrlAAA', 'a036e00000z5wVPAAY', 'a036e000010cGROAA2', 'a036e00000zUFu5AAG', 'a036e00000z60Z4AAI', 'a036e00000z36WfAAI', 'a036e000010cHuMAAU', 'a036e00000zUNXqAAO', 'a036e00000zUNXzAAO', 'a036e000010cGRVAA2', 'a036e000010cz2jAAA', 'a036e00000z4nw4AAA', 'a036e000010cGcMAAU', 'a036e000010cGcXAAU', 'a036e000010cGRgAAM', 'a036e000010cGRlAAM', 'a036e000010cGSRAA2', 'a036e000010cGSeAAM', 'a036e000010cGShAAM', 'a036e000010cGSlAAM', 'a036e000010cbCnAAI', 'a036e000010crtgAAA', 'a036e00000rJbn4AAC', 'a036e00000z6MzHAAU', 'a036e00000z4fGdAAI', 'a036e00000zUrzGAAS', 'a036e00000zUs0SAAS', 'a036e00000rJXsmAAG', 'a036e00000rJXsrAAG', 'a036e00000z34NfAAI', 'a036e00000z353CAAQ', 'a036e00000z354oAAA', 'a036e00000zSc5LAAS', 'a036e00000rJY9qAAG', 'a036e00000rHnc8AAC', 'a036e00000rHncDAAS', 'a036e00000z34ELAAY', 'a036e00000z354yAAA', 'a036e00000z4mlXAAQ', 'a036e00000z3GvCAAU', 'a036e00000rHncFAAS', 'a036e00000rJW9FAAW', 'a036e00000rJY9mAAG', 'a036e00000rIxg1AAC', 'a036e00000z4ejdAAA', 'a036e00000z4nCAAAY', 'a036e00000z4nCBAAY', 'a036e00000zVvraAAC', 'a036e00000z4f2RAAQ', 'a036e00000z6039AAA', 'a036e00000z6Mz9AAE', 'a036e00000zVPa3AAG', 'a036e00000zVPa4AAG', 'a036e00000zVPa5AAG', 'a036e00000z6MsDAAU', 'a036e00000zVhQUAA0', 'a036e00000rJY3NAAW', 'a036e00000z354mAAA', 'a036e00000rJY9hAAG', 'a036e00000rKZRjAAO', 'a036e00000rJYA7AAO', 'a036e00000rJYAAAA4', 'a036e00000rJXnlAAG', 'a036e00000rHnXCAA0', 'a036e00000rIMD5AAO', 'a036e00000rJXsMAAW', 'a036e00000rIMDMAA4', 'a036e00000zU9cxAAC', 'a036e00000rJdj8AAC', 'a036e00000rHnbTAAS', 'a036e00000rJY5hAAG', 'a036e00000rJYAnAAO', 'a036e00000rJYAsAAO', 'a036e00000rJYAuAAO', 'a036e00000rJYB6AAO', 'a036e00000zUZryAAG', 'a036e00000rIdaEAAS', 'a036e00000rJXo9AAG', 'a036e00000z4egnAAA', 'a036e00000zVvomAAC', 'a036e00000z34NoAAI', 'a036e00000rJXsLAAW', 'a036e00000rJXsNAAW', 'a036e00000zSbZZAA0', 'a036e00000zUwtSAAS', 'a036e00000rKEM1AAO', 'a036e00000rKFPXAA4', 'a036e00000z605DAAQ', 'a036e00000z4fF2AAI', 'a036e00000z35SKAAY', 'a036e00000rKVbeAAG', 'a036e00000rKZPPAA4', 'a036e00000z5zlBAAQ', 'a036e00000zUNNjAAO', 'a036e00000rJXsUAAW', 'a036e00000rJY5iAAG', 'a036e00000zScTJAA0', 'a036e00000z35RcAAI', 'a036e00000rJXnrAAG', 'a036e00000z5zlKAAQ', 'a036e00000rJXo3AAG', 'a036e00000rHnXdAAK', 'a036e00000z63LgAAI', 'a036e00000z5vw0AAA', 'a036e00000rJXslAAG', 'a036e00000zSjQOAA0', 'a036e00000z605MAAQ', 'a036e00000rJY5tAAG', 'a036e00000z35SHAAY', 'a036e00000z35SLAAY', 'a036e00000z3H2vAAE', 'a036e00000z3H33AAE', 'a036e00000z43YEAAY', 'a036e00000rJds0AAC', 'a036e00000rJXoBAAW', 'a036e00000rJZkeAAG', 'a036e00000rHnXgAAK', 'a036e00000rJXrhAAG', 'a036e00000rHnYhAAK', 'a036e00000rI2F7AAK', 'a036e00000rJXsyAAG', 'a036e00000zUuoxAAC', 'a036e00000z5vwDAAQ', 'a036e00000rJZr5AAG', 'a036e00000rJY6BAAW', 'a036e00000rJY6FAAW', 'a036e00000zVvsdAAC', 'a036e00000z4fEpAAI', 'a036e00000z4fEoAAI', 'a036e00000z4fEyAAI', 'a036e00000zUuncAAC', 'a036e00000rJXoSAAW', 'a036e00000zUFhUAAW', 'a036e00000rJXoVAAW', 'a036e00000rJXohAAG', 'a036e00000rJXokAAG', 'a036e00000z34EEAAY', 'a036e00000rJXq3AAG', 'a036e00000rJj16AAC', 'a036e00000rJkWGAA0', 'a036e00000rJY75AAG', 'a036e00000rJY7EAAW', 'a036e00000z6N2QAAU', 'a036e00000rJXoqAAG', 'a036e00000z3447AAA', 'a036e00000z34NnAAI', 'a036e00000rK8wmAAC', 'a036e00000z35CnAAI', 'a036e00000z35CfAAI', 'a036e00000zVQC3AAO', 'a036e00000z35ChAAI', 'a036e00000zVXr6AAG', 'a036e00000zUv17AAC', 'a036e00000zVQBbAAO', 'a036e00000zVcYZAA0', 'a036e00000zVdAKAA0', 'a036e00000zUqUyAAK', 'a036e00000zSbQ9AAK', 'a036e00000rJXqTAAW', 'a036e00000zUoEzAAK', 'a036e00000z4elvAAA', 'a036e00000z4nESAAY', 'a036e00000z6Mt0AAE', 'a036e00000zU9cgAAC', 'a036e00000z43RzAAI', 'a036e00000rJY3KAAW', 'a036e00000zVfgtAAC', 'a036e00000zVQ9YAAW', 'a036e00000z4f50AAA', 'a036e00000z4f52AAA', 'a036e00000zSbQZAA0', 'a036e00000z5vtgAAA', 'a036e00000rJXrQAAW', 'a036e00000z42yHAAQ', 'a036e00000rJXqoAAG', 'a036e00000rJXqsAAG', 'a036e00000zUb5rAAC', 'a036e00000rJY3VAAW', 'a036e00000rJY3ZAAW', 'a036e00000z6MziAAE', 'a036e00000z6MzvAAE', 'a036e00000zUqTUAA0', 'a036e00000zUr6HAAS', 'a036e00000zVv20AAC', 'a036e00000rJXrBAAW', 'a036e00000rJXrEAAW', 'a036e00000rJXrHAAW', 'a036e00000rJXrPAAW', 'a036e00000rJXrRAAW', 'a036e00000z34T8AAI', 'a036e00000rJY3vAAG', 'a036e00000rJY3wAAG', 'a036e00000rJY47AAG', 'a036e00000rJY4AAAW', 'a036e00000rJY4CAAW', 'a036e00000rJXrTAAW', 'a036e00000rJXrXAAW', 'a036e00000rJXrbAAG', 'a036e00000rJXreAAG', 'a036e00000rJXrgAAG', 'a036e00000rJXsdAAG', 'a036e00000rJW2OAAW', 'a036e00000z34T6AAI', 'a036e00000rJY4cAAG', 'a036e00000zVfdTAAS', 'a036e00000rJY4pAAG', 'a036e00000rJjM1AAK', 'a036e00000rJY8BAAW', 'a036e00000rJyvIAAS', 'a036e00000rIthaAAC', 'a036e00000z43SuAAI', 'a036e00000rJjAKAA0', 'a036e00000rJoejAAC', 'a036e00000rJsFiAAK', 'a036e00000zSc62AAC', 'a036e00000rJynOAAS', 'a036e00000rK2HZAA0', 'a036e00000rK93NAAS', 'a036e00000rJY9TAAW', 'a036e00000z4ewvAAA', 'a036e00000z5w2TAAQ', 'a036e00000z5zz8AAA', 'a036e00000z35gcAAA', 'a036e00000zUvA0AAK', 'a036e00000z4fIyAAI', 'a036e00000z4fJTAAY', 'a036e00000z4fJKAAY', 'a036e00000rJYBkAAO', 'a036e00000rJYBpAAO', 'a036e00000rJYBsAAO', 'a036e00000z4fJLAAY', 'a036e00000rJYBzAAO', 'a036e00000rJYC1AAO', 'a036e00000zUv8vAAC', 'a036e00000rJYCVAA4', 'a036e00000rJYCkAAO', 'a036e00000rJYClAAO', 'a036e00000zViYdAAK', 'a036e00000zVibuAAC', 'a036e00000z35iLAAQ', 'a036e00000z4etzAAA', 'a036e00000z4euGAAQ', 'a036e00000zVvqEAAS', 'a036e00000z4euHAAQ', 'a036e00000z4euLAAQ', 'a036e00000zVvqHAAS', 'a036e00000z6MwZAAU', 'a036e00000zUa2WAAS', 'a036e00000rJYD0AAO', 'a036e00000rJYD8AAO', 'a036e00000rJYDAAA4', 'a036e00000rJYDDAA4', 'a036e00000rJYDJAA4', 'a036e00000rJYDNAA4', 'a036e00000rJYDPAA4', 'a036e00000rJYDTAA4', 'a036e00000rJYDYAA4', 'a036e00000zVC6mAAG', 'a036e00000z4nIMAAY', 'a036e00000z4nINAAY', 'a036e00000z6MviAAE', 'a036e00000zU9dGAAS', 'a036e00000rJY0uAAG', 'a036e00000rJYDhAAO', 'a036e00000zVvqRAAS', 'a036e00000rHUT5AAO', 'a036e00000rHUT6AAO', 'a036e00000z4exNAAQ', 'a036e00000zUa2RAAS', 'a036e00000rJujVAAS', 'a036e00000rJx90AAC', 'a036e00000z34lUAAQ', 'a036e00000rJbxOAAS', 'a036e00000rJVv7AAG', 'a036e00000rJXyOAAW', 'a036e00000rJtuRAAS', 'a036e00000rJzX8AAK', 'a036e00000zUa2XAAS', 'a036e00000rJY1eAAG', 'a036e00000rJY1iAAG', 'a036e00000z4fGcAAI', 'a036e00000rHHYFAA4', 'a036e00000rHnZ8AAK', 'a036e00000rJXyQAAW', 'a036e00000rJXyTAAW', 'a036e00000rJXyUAAW', 'a036e00000rJXydAAG', 'a036e00000rJXyjAAG', 'a036e00000rJXywAAG', 'a036e00000rJY2HAAW', 'a036e00000rJjQQAA0', 'a036e00000rJjQTAA0', 'a036e00000rJjQaAAK', 'a036e00000zVgvrAAC', 'a036e00000z4fGbAAI', 'a036e00000z5wFkAAI', 'a036e00000z35gmAAA', 'a036e00000z35iBAAQ', 'a036e00000rHndFAAS', 'a036e00000rIR1kAAG', 'a036e00000rJXyuAAG', 'a036e00000z5w2IAAQ', 'a036e00000rJXyxAAG', 'a036e00000z34tNAAQ', 'a036e000010bvaLAAQ', 'a036e000010bxI4AAI', 'a036e00000rHzZAAA0', 'a036e00000rI2HmAAK', 'a036e00000rJXw3AAG', 'a036e00000rJXw4AAG', 'a036e00000rJXw7AAG', 'a036e00000rJXz0AAG', 'a036e00000z5zzQAAQ', 'a036e00000z34v7AAA', 'a036e00000z34vFAAQ', 'a036e00000zVNB6AAO', 'a036e00000z35a6AAA', 'a036e00000rIfQlAAK', 'a036e00000rJYEHAA4', 'a036e00000z49o3AAA', 'a036e00000rJYEIAA4', 'a036e00000rJXwHAAW', 'a036e00000rJY20AAG', 'a036e00000zUoKXAA0', 'a036e00000rJXzbAAG', 'a036e00000z4gZEAAY', 'a036e00000z34vKAAQ', 'a036e00000z34vLAAQ', 'a036e00000zVNEnAAO', 'a036e00000zSc2pAAC', 'a036e00000zUa2tAAC', 'a036e00000rJYENAA4', 'a036e00000rJYERAA4', 'a036e00000rJXwqAAG', 'a036e00000rJY16AAG', 'a036e00000rJXx8AAG', 'a036e00000rJXxBAAW', 'a036e00000rJXyqAAG', 'a036e00000rJfIZAA0', 'a036e00000rJXzsAAG', 'a036e00000rJY0DAAW', 'a036e00000rJY0GAAW', 'a036e00000rJY0HAAW', 'a036e00000rJjIyAAK', 'a036e00000z4exIAAQ', 'a036e00000rJsbNAAS', 'a036e00000z34lHAAQ', 'a036e00000rJv26AAC', 'a036e00000z3GsgAAE', 'a036e00000z4ezlAAA', 'a036e00000z4ezzAAA', 'a036e00000z4f0CAAQ', 'a036e00000rJYEXAA4', 'a036e00000rJXxLAAW', 'a036e00000rJXxmAAG', 'a036e00000zUNQTAA4', 'a036e00000z34jhAAA', 'a036e00000z34lLAAQ', 'a036e00000zVFyrAAG', 'a036e00000zUa2cAAC', 'a036e00000z5wIPAAY', 'a036e00000rJYEwAAO', 'a036e00000rJrmcAAC', 'a036e00000z34lQAAQ', 'a036e00000z34lRAAQ', 'a036e00000zVFtlAAG', 'a036e00000rJYFnAAO', 'a036e00000rJYFpAAO', 'a036e00000rJYFtAAO', 'a036e00000rJYG0AAO', 'a036e00000rJjP0AAK', 'a036e00000zVBVlAAO', 'a036e00000z34arAAA', 'a036e00000zVBWqAAO', 'a036e00000z3Gq6AAE', 'a036e00000z3Gq8AAE', 'a036e00000rKFYXAA4', 'a036e00000rKaJ1AAK', 'a036e00000z4fJ9AAI', 'a036e00000z35gMAAQ', 'a036e00000z35gQAAQ', 'a036e00000z35gRAAQ', 'a036e00000z35gWAAQ', 'a036e00000z36R9AAI', 'a036e00000z36RGAAY', 'a036e00000z5wYQAAY', 'a036e000010cGWvAAM', 'a036e00000rH8zGAAS', 'a036e000010cGMCAA2', 'a036e00000zUa3OAAS', 'a036e000010cGM9AAM', 'a036e00000zUa3WAAS', 'a036e00000zUa3bAAC', 'a036e00000zUa3fAAC', 'a036e00000rH90sAAC', 'a036e00000z4feWAAQ', 'a036e00000z4feZAAQ', 'a036e00000rH9HpAAK', 'a036e00000rH9HqAAK', 'a036e000010cGLoAAM', 'a036e000010cGLqAAM', 'a036e000010cGLsAAM', 'a036e000010cGMOAA2', 'a036e000010cGMXAA2', 'a036e000010cGMZAA2', 'a036e00000rH9ejAAC', 'a036e00000rK3tzAAC', 'a036e000010cGNZAA2', 'a036e000010cGNlAAM', 'a036e00000z4feiAAA', 'a036e00000z36REAAY', 'a036e00000z4feOAAQ', 'a036e00000z4febAAA', 'a036e000010clD1AAI', 'a036e00000z4nupAAA', 'a036e00000z5wYWAAY', 'a036e00000z6NAxAAM', 'a036e00000z6NB5AAM', 'a036e000010cGWmAAM', 'a036e000010cGWoAAM', 'a036e000010cGX1AAM', 'a036e000010cHp2AAE', 'a036e000010cGX6AAM', 'a036e000010cGXCAA2', 'a036e000010cGXHAA2', 'a036e000010cGXdAAM', 'a036e000010cGXgAAM', 'a036e000010cGXkAAM', 'a036e000010cGXrAAM', 'a036e000010cGXuAAM', 'a036e000010cGYYAA2', 'a036e000010cGMKAA2', 'a036e00000z36BwAAI', 'a036e00000z4fYTAAY', 'a036e00000rH6OeAAK', 'a036e000010cGM2AAM', 'a036e00000rJjSTAA0', 'a036e00000rJjSWAA0', 'a036e00000z36AJAAY', 'a036e00000z36BiAAI', 'a036e00000z4fYLAAY', 'a036e00000z4noZAAQ', 'a036e00000zVkCBAA0', 'a036e000010c0gOAAQ', 'a036e00000zScjhAAC', 'a036e00000zVqocAAC', 'a036e00000z35qvAAA', 'a036e00000z4fMuAAI', 'a036e00000z4fMvAAI', 'a036e00000z4fPqAAI', 'a036e00000z4fPtAAI', 'a036e00000z4fQ3AAI', 'a036e00000z4fQDAAY', 'a036e00000z4fQFAAY', 'a036e00000zUoSqAAK', 'a036e000010cDtkAAE', 'a036e000010cDtfAAE', 'a036e000010cDtbAAE', 'a036e00000zVrTcAAK', 'a036e00000zVrTdAAK', 'a036e000010c3j6AAA', 'a036e000010cDtYAAU', 'a036e000010cDu1AAE', 'a036e00000z60J7AAI', 'a036e00000rJYGfAAO', 'a036e000010cDu4AAE', 'a036e000010cDu6AAE', 'a036e000010cDurAAE', 'a036e000010cDuuAAE', 'a036e00000rJYGsAAO', 'a036e00000z4fMlAAI', 'a036e00000rIxoeAAC', 'a036e00000rJYGTAA4', 'a036e00000rJYGUAA4', 'a036e00000rJYHVAA4', 'a036e00000z5wKvAAI', 'a036e00000z5wL6AAI', 'a036e00000z35yxAAA', 'a036e000010cDtEAAU', 'a036e00000z5wL7AAI', 'a036e00000rJYGwAAO', 'a036e00000rJYHKAA4', 'a036e00000rJYHQAA4', 'a036e00000rJYHTAA4', 'a036e00000rJYHUAA4', 'a036e00000rJYHXAA4', 'a036e00000rJYHfAAO', 'a036e00000rJYHnAAO', 'a036e00000rJYI3AAO', 'a036e00000rJYIBAA4', 'a036e00000rJjPfAAK', 'a036e00000rJjPmAAK', 'a036e00000rJjPpAAK', 'a036e00000z60NTAAY', 'a036e00000zUabCAAS', 'a036e00000rHnebAAC', 'a036e00000rJjPwAAK', 'a036e00000rKgmkAAC', 'a036e00000z4fMcAAI', 'a036e00000z4fMiAAI', 'a036e00000zVkYxAAK', 'a036e00000rJYIqAAO', 'a036e000010cDtNAAU', 'a036e00000rK1vHAAS', 'a036e00000rK2IHAA0', 'a036e000010cGI7AAM', 'a036e000010cGIDAA2', 'a036e000010cUbQAAU', 'a036e00000rJb7bAAC', 'a036e00000rJb7fAAC', 'a036e000010cAPjAAM', 'a036e00000z37YZAAY', 'a036e00000zSjX6AAK', 'a036e00000z364zAAA', 'a036e00000z4fVQAAY', 'a036e00000rHUTjAAO', 'a036e00000z4fVTAAY', 'a036e000010cHYIAA2', 'a036e00000rINYoAAO', 'a036e000010cGGRAA2', 'a036e00000z363PAAQ', 'a036e00000z364vAAA', 'a036e000010cHVGAA2', 'a036e000010cNHVAA2', 'a036e000010cSfjAAE', 'a036e00000z43r0AAA', 'a036e00000z4fVYAAY', 'a036e00000z4meJAAQ', 'a036e00000z4meNAAQ', 'a036e000010cGGMAA2', 'a036e000010cGGJAA2', 'a036e000010cGGQAA2', 'a036e000010cGHBAA2', 'a036e000010cGHDAA2', 'a036e000010cGHIAA2', 'a036e000010cGHmAAM', 'a036e000010cGHnAAM', 'a036e000010cGHpAAM', 'a036e00000rH7cMAAS', 'a036e00000z36JrAAI', 'a036e000010cGRZAA2', 'a036e000010cGRbAAM', 'a036e00000z4fbdAAA', 'a036e00000rH7zfAAC', 'a036e00000z4fbqAAA', 'a036e00000z4fbsAAA', 'a036e00000rIMYcAAO', 'a036e00000rINbeAAG', 'a036e000010cGSBAA2', 'a036e00000rJjT1AAK', 'a036e00000rJjT2AAK', 'a036e00000rJjT5AAK', 'a036e00000z36IaAAI', 'a036e00000z36JuAAI', 'a036e00000z3HO2AAM', 'a036e00000z3HO6AAM', 'a036e00000z3ixgAAA', 'a036e00000z43xNAAQ', 'a036e00000z4nrRAAQ', 'a036e00000z5wVVAAY', 'a036e00000z5wVkAAI', 'a036e000010cHhoAAE', 'a036e00000zUvNIAA0', 'a036e000010cGRWAA2', 'a036e00000zSjYCAA0', 'a036e000010cGRPAA2', 'a036e00000zUNXyAAO', 'a036e00000z36WkAAI', 'a036e000010d2doAAA', 'a036e000010cGRUAA2', 'a036e000010cpAfAAI', 'a036e000010cGRqAAM', 'a036e000010cGSNAA2', 'a036e000010cGSOAA2', 'a036e000010cGSQAA2', 'a036e000010cGSVAA2', 'a036e000010cGSoAAM', 'a036e000010cGSqAAM', 'a036e000010coMQAAY', 'a036e00000z6Ms2AAE', 'a036e00000z6MscAAE', 'a036e00000z6MxpAAE', 'a036e00000rHT8BAAW', 'a036e00000z4fGFAAY', 'a036e00000zV1WYAA0', 'a036e00000zVOkIAAW', 'a036e00000rJY9pAAG', 'a036e00000zUrxxAAC', 'a036e00000z3GgjAAE', 'a036e00000z5znLAAQ', 'a036e00000zVCleAAG', 'a036e00000z4f2KAAQ', 'a036e00000z35SJAAY', 'a036e00000rJY9gAAG', 'a036e00000rJ3ayAAC', 'a036e00000zVvpCAAS', 'a036e00000z4ejcAAA', 'a036e00000z4ejiAAA', 'a036e00000z4ejjAAA', 'a036e00000z4nC9AAI', 'a036e00000zVvrcAAC', 'a036e00000z6MyrAAE', 'a036e00000z6MytAAE', 'a036e00000rJY9UAAW', 'a036e00000zUZrzAAG', 'a036e00000z6MsFAAU', 'a036e00000rJY4oAAG', 'a036e00000zUZs4AAG', 'a036e00000rJY9lAAG', 'a036e00000rJYA6AAO', 'a036e00000z344vAAA', 'a036e00000rHAYhAAO', 'a036e00000rIyVWAA0', 'a036e00000rJ3ekAAC', 'a036e00000rJYACAA4', 'a036e00000rJYAEAA4', 'a036e00000rJYAMAA4', 'a036e00000rJYB4AAO', 'a036e00000rJYB5AAO', 'a036e00000zTS8sAAG', 'a036e00000rJkmOAAS', 'a036e00000rJZreAAG', 'a036e00000rIybQAAS', 'a036e00000rJYBFAA4', 'a036e00000rJYBHAA4', 'a036e00000rJYBJAA4', 'a036e00000rJZoVAAW', 'a036e00000rK1j1AAC', 'a036e00000zScRqAAK', 'a036e00000zVcZSAA0', 'a036e00000rKcs3AAC', 'a036e00000rJXneAAG', 'a036e00000rJXnmAAG', 'a036e00000rJZl7AAG', 'a036e00000rJXsYAAW', 'a036e00000rJY5eAAG', 'a036e00000rJY5gAAG', 'a036e00000rJY5mAAG', 'a036e00000zUNSAAA4', 'a036e00000z35QeAAI', 'a036e00000z35RCAAY', 'a036e00000z5vrUAAQ', 'a036e00000rJXsjAAG', 'a036e00000zUNOcAAO', 'a036e00000rJY5pAAG', 'a036e00000rJs8LAAS', 'a036e00000rJY5rAAG', 'a036e00000zUaaGAAS', 'a036e00000rJbDcAAK', 'a036e00000z35SMAAY', 'a036e00000rJXo6AAG', 'a036e00000rJXoFAAW', 'a036e00000zUNOIAA4', 'a036e00000zTSBZAA4', 'a036e00000rJXt3AAG', 'a036e00000zUb5qAAC', 'a036e00000rJXtEAAW', 'a036e00000rJY5xAAG', 'a036e00000rJY5yAAG', 'a036e00000rJY6HAAW', 'a036e00000rJY80AAG', 'a036e00000zVdApAAK', 'a036e00000zVdAoAAK', 'a036e00000z4fEtAAI', 'a036e00000zVvseAAC', 'a036e00000zVvsnAAC', 'a036e00000rJXoXAAW', 'a036e00000rJXoeAAG', 'a036e00000zUa28AAC', 'a036e00000rIdphAAC', 'a036e00000z4ejXAAQ', 'a036e00000rIsLpAAK', 'a036e00000z4elnAAA', 'a036e00000z34NtAAI', 'a036e00000rJmVZAA0', 'a036e00000z34NhAAI', 'a036e00000rJY7GAAW', 'a036e00000rJY7JAAW', 'a036e00000z6N2IAAU', 'a036e00000rJXowAAG', 'a036e00000rJXp3AAG', 'a036e00000rJbtRAAS', 'a036e00000rJc3MAAS', 'a036e00000rJdstAAC', 'a036e00000rJXq9AAG', 'a036e00000zUNOAAA4', 'a036e00000rJXqEAAW', 'a036e00000rJXqFAAW', 'a036e00000zSbZxAAK', 'a036e00000z34NrAAI', 'a036e00000z432fAAA', 'a036e00000rHnarAAC', 'a036e00000rJY3HAAW', 'a036e00000rIY4rAAG', 'a036e00000zVOltAAG', 'a036e00000rKBIpAAO', 'a036e00000rKEPPAA4', 'a036e00000zVQAuAAO', 'a036e00000zUunQAAS', 'a036e00000z344rAAA', 'a036e00000z344wAAA', 'a036e00000zUqVVAA0', 'a036e00000z4eldAAA', 'a036e00000z63LdAAI', 'a036e00000zUuyuAAC', 'a036e00000zVfguAAC', 'a036e00000z602zAAA', 'a036e00000z63WzAAI', 'a036e00000z4f51AAA', 'a036e00000z4f53AAA', 'a036e00000z4f5BAAQ', 'a036e00000z4f5CAAQ', 'a036e00000z345jAAA', 'a036e00000z4egsAAA', 'a036e00000z4n9HAAQ', 'a036e00000rJXqYAAW', 'a036e00000z5vtcAAA', 'a036e00000z4nU5AAI', 'a036e00000z4nU6AAI', 'a036e00000z6MzeAAE', 'a036e00000z6MznAAE', 'a036e00000rJXqxAAG', 'a036e00000rJXrDAAW', 'a036e00000rJXrIAAW', 'a036e00000rJXrOAAW', 'a036e00000rIUevAAG', 'a036e00000rJXuiAAG', 'a036e00000rJW2HAAW', 'a036e00000rJkcxAAC', 'a036e00000rJY40AAG', 'a036e00000rJY4BAAW', 'a036e00000z6N0DAAU', 'a036e00000zVQ9XAAW', 'a036e00000zVQqpAAG', 'a036e00000zVQqqAAG', 'a036e00000rJY83AAG', 'a036e00000rJY84AAG', 'a036e00000rJXrYAAW', 'a036e00000rJW2FAAW', 'a036e00000rJXrfAAG', 'a036e00000rJXrkAAG', 'a036e00000rJW2PAAW', 'a036e00000rJW2QAAW', 'a036e00000z5w7VAAQ', 'a036e00000z3H0oAAE', 'a036e00000zUoNDAA0', 'a036e00000z35CmAAI', 'a036e00000zVA2QAAW', 'a036e00000z34ECAAY', 'a036e00000z34CWAAY', 'a036e00000zSbZTAA0', 'a036e00000z34CdAAI', 'a036e00000zVA5MAAW', 'a036e00000rJXqQAAW', 'a036e00000rJXqHAAW', 'a036e00000z354tAAA', 'a036e00000rK1XLAA0', 'a036e00000rINR7AAO', 'a036e00000z35RbAAI', 'a036e00000rHnc6AAC', 'a036e00000zVBUpAAO', 'a036e00000z34b3AAA', 'a036e00000zVBWYAA4', 'a036e00000z34beAAA', 'a036e00000rJzIrAAK', 'a036e00000z35gbAAA', 'a036e00000z3H9IAAU', 'a036e00000zViuzAAC', 'a036e00000z4fGNAAY', 'a036e00000rIZr1AAG', 'a036e00000zUNUuAAO', 'a036e00000zUa35AAC', 'a036e000010bjsIAAQ', 'a036e000010c0V2AAI', 'a036e00000rJYC9AAO', 'a036e00000rJYCiAAO', 'a036e00000rJYCmAAO', 'a036e00000rJYCuAAO', 'a036e00000z3GmzAAE', 'a036e00000z4ex0AAA', 'a036e00000zVC6kAAG', 'a036e00000zVC6lAAG', 'a036e00000z4euFAAQ', 'a036e00000zVsVJAA0', 'a036e00000rHnaKAAS', 'a036e00000rJY18AAG', 'a036e00000rHsLCAA0', 'a036e00000rJY15AAG', 'a036e00000rHzWeAAK', 'a036e00000rJYDCAA4', 'a036e00000rJYDbAAO', 'a036e00000z4euOAAQ', 'a036e00000z4nIJAAY', 'a036e00000z4nIRAAY', 'a036e00000z6MveAAE', 'a036e00000z6Mw9AAE', 'a036e00000rJY0nAAG', 'a036e00000rJY0zAAG', 'a036e00000rJYDjAAO', 'a036e00000rJYDkAAO', 'a036e00000rJYDpAAO', 'a036e00000rJYDqAAO', 'a036e00000rJYDrAAO', 'a036e00000z35a3AAA', 'a036e00000rK1ZDAA0', 'a036e00000rJXwRAAW', 'a036e00000zVBnFAAW', 'a036e00000z34lXAAQ', 'a036e00000rI094AAC', 'a036e00000z600aAAA', 'a036e00000rJY12AAG', 'a036e00000rJY19AAG', 'a036e00000rJY1BAAW', 'a036e00000z4fGTAAY', 'a036e00000z35ZiAAI', 'a036e00000z35bNAAQ', 'a036e00000zVgKpAAK', 'a036e00000zUPQEAA4', 'a036e00000z34jdAAA', 'a036e00000rItdKAAS', 'a036e00000rJXyhAAG', 'a036e00000zUa2dAAC', 'a036e00000rJymBAAS', 'a036e00000zUa2ZAAS', 'a036e00000zVgOSAA0', 'a036e00000zVgvwAAC', 'a036e00000rHHYHAA4', 'a036e00000rHQEpAAO', 'a036e00000rJXwFAAW', 'a036e00000rJXyRAAW', 'a036e00000rJXycAAG', 'a036e00000rJXyiAAG', 'a036e00000z5zzJAAQ', 'a036e00000rJY1vAAG', 'a036e00000rJY2IAAW', 'a036e00000rJY2PAAW', 'a036e00000rJjQNAA0', 'a036e00000z60EwAAI', 'a036e00000z34ayAAA', 'a036e00000rJXypAAG', 'a036e00000z3iSKAAY', 'a036e00000zUv98AAC', 'a036e000010biEsAAI', 'a036e00000rJYBwAAO', 'a036e00000rJYBxAAO', 'a036e00000rJYEbAAO', 'a036e00000z4fJ7AAI', 'a036e00000rINSqAAO', 'a036e00000zUNUnAAO', 'a036e00000rJCNgAAO', 'a036e00000zU9d4AAC', 'a036e00000z5w2VAAQ', 'a036e00000z34v8AAA', 'a036e00000z34vBAAQ', 'a036e00000rJYC6AAO', 'a036e00000rJYE1AAO', 'a036e00000zTaKCAA0', 'a036e00000rJXwNAAW', 'a036e00000rJXwOAAW', 'a036e00000rJXzTAAW', 'a036e00000rJXzcAAG', 'a036e00000rJXziAAG', 'a036e00000rJXzmAAG', 'a036e00000rJXznAAG', 'a036e00000zVN9kAAG', 'a036e00000z3GsMAAU', 'a036e00000z3GsUAAU', 'a036e00000z60HBAAY', 'a036e00000zVA7CAAW', 'a036e00000rJXx1AAG', 'a036e00000rJXx3AAG', 'a036e00000rJXx5AAG', 'a036e00000zVBnHAAW', 'a036e00000rJXztAAG', 'a036e00000rJY0IAAW', 'a036e00000rJY0JAAW', 'a036e00000z4exJAAQ', 'a036e00000rJkUoAAK', 'a036e00000z34jVAAQ', 'a036e00000zVvrBAAS', 'a036e00000z4f00AAA', 'a036e00000zVvrDAAS', 'a036e00000z3EqcAAE', 'a036e00000z34lFAAQ', 'a036e00000zVFyoAAG', 'a036e00000z34lJAAQ', 'a036e00000zVFvcAAG', 'a036e00000zSjS8AAK', 'a036e00000rJrplAAC', 'a036e00000rJrqKAAS', 'a036e00000rJs6UAAS', 'a036e00000rJseCAAS', 'a036e00000z34ZNAAY', 'a036e00000zVFvHAAW', 'a036e00000zVFwAAAW', 'a036e00000rJYFuAAO', 'a036e00000rJjP1AAK', 'a036e00000rJjP8AAK', 'a036e00000zSbnXAAS', 'a036e00000z34laAAA', 'a036e00000z34lgAAA', 'a036e00000z3Gq4AAE', 'a036e00000z35gTAAQ', 'a036e00000z35gVAAQ', 'a036e00000zUa3QAAS', 'a036e00000zVZLRAA4', 'a036e000010bvZmAAI', 'a036e00000z4feYAAQ', 'a036e000010cGMQAA2', 'a036e000010cGMYAA2', 'a036e000010cGMaAAM', 'a036e00000rHzcPAAS', 'a036e00000zUoXIAA0', 'a036e00000rIMZfAAO', 'a036e000010cHopAAE', 'a036e000010cGNTAA2', 'a036e000010cGNUAA2', 'a036e000010cGNgAAM', 'a036e000010cGNkAAM', 'a036e00000z36QZAAY', 'a036e00000z4M59AAE', 'a036e000010bxFZAAY', 'a036e000010cGXAAA2', 'a036e000010cGWtAAM', 'a036e000010cGXDAA2', 'a036e000010cGWnAAM', 'a036e000010cGXsAAM', 'a036e000010cGXvAAM', 'a036e000010cGYZAA2', 'a036e000010cympAAA', 'a036e000010d0OVAAY', 'a036e000010d0ZKAAY', 'a036e00000z36BjAAI', 'a036e00000z36BtAAI', 'a036e00000rH612AAC', 'a036e00000rH6OdAAK', 'a036e00000zUNXPAA4', 'a036e000010cGM4AAM', 'a036e000010cGMDAA2', 'a036e00000rJjSJAA0', 'a036e00000rJmfpAAC', 'a036e00000zUa3hAAC', 'a036e00000z4fYKAAY', 'a036e00000z36AFAAY', 'a036e00000z36AHAAY', 'a036e00000z36AKAAY', 'a036e00000z4fYMAAY', 'a036e00000z4noJAAQ', 'a036e00000z5wSrAAI', 'a036e000010cGLyAAM', 'a036e00000z60ViAAI', 'a036e00000z60VkAAI', 'a036e00000zUNXSAA4', 'a036e00000z35qnAAA', 'a036e00000zUIawAAG', 'a036e00000zVqodAAC', 'a036e00000z3HBaAAM', 'a036e00000z4n2ZAAQ', 'a036e00000z4B0PAAU', 'a036e00000z3ineAAA', 'a036e00000z4fMjAAI', 'a036e00000z4fMqAAI', 'a036e00000z4fMxAAI', 'a036e00000z4fPwAAI', 'a036e00000z4fQ5AAI', 'a036e00000z4fQAAAY', 'a036e00000z4fQEAAY', 'a036e000010cDtoAAE', 'a036e00000z6N5LAAU', 'a036e00000z5wMCAAY', 'a036e00000z60NUAAY', 'a036e000010cDtOAAU', 'a036e00000z6N70AAE', 'a036e00000zUNVrAAO', 'a036e00000zUabFAAS', 'a036e000010cDtRAAU', 'a036e000010cDtZAAU', 'a036e000010cDtxAAE', 'a036e00000rJYGhAAO', 'a036e000010cDu7AAE', 'a036e000010cDuBAAU', 'a036e00000z35qjAAA', 'a036e00000rHTUdAAO', 'a036e00000rIYyXAAW', 'a036e00000rJYGOAA4', 'a036e00000z5wKqAAI', 'a036e00000rJYGeAAO', 'a036e00000rJYGiAAO', 'a036e00000rJYGlAAO', 'a036e00000z5wL2AAI', 'a036e00000rHnePAAS', 'a036e00000rJYHWAA4', 'a036e00000rHO0JAAW', 'a036e00000rJYI2AAO', 'a036e00000rJYI6AAO', 'a036e00000rJYI7AAO', 'a036e00000rJYIFAA4', 'a036e00000rJYIHAA4', 'a036e00000rJjPiAAK', 'a036e00000rJjPrAAK', 'a036e000010cDtmAAE', 'a036e000010cDtKAAU', 'a036e00000rK264AAC', 'a036e00000yuZpbAAE', 'a036e00000z35p4AAA', 'a036e00000z4fQ9AAI', 'a036e000010cGIFAA2', 'a036e000010ciXwAAI', 'a036e00000rJUmwAAG', 'a036e000010cAPmAAM', 'a036e000010cAPoAAM', 'a036e00000z37YWAAY', 'a036e00000z37YYAAY', 'a036e000010cGHMAA2', 'a036e00000rH2XKAA0', 'a036e00000z4fVRAAY', 'a036e00000z4fVeAAI', 'a036e00000z5wPlAAI', 'a036e00000rJYL4AAO', 'a036e00000z363WAAQ', 'a036e00000z3HHjAAM', 'a036e000010cUl8AAE', 'a036e00000z4nlTAAQ', 'a036e00000zUFt7AAG', 'a036e000010cGGOAA2', 'a036e000010cHUQAA2', 'a036e00000z60TJAAY', 'a036e00000z63m4AAA', 'a036e00000zSjX0AAK', 'a036e000010cGGTAA2', 'a036e000010cGH9AAM', 'a036e000010cGHCAA2', 'a036e000010cGHxAAM', 'a036e00000z36KSAAY', 'a036e00000z36JxAAI', 'a036e00000z36K0AAI', 'a036e00000rH7coAAC', 'a036e00000z36JwAAI', 'a036e00000zVALLAA4', 'a036e00000rH7zeAAC', 'a036e00000z4fbeAAA', 'a036e00000rH7zgAAC', 'a036e00000rH8BUAA0', 'a036e00000z4fbtAAA', 'a036e00000z60ZCAAY', 'a036e00000rJdy3AAC', 'a036e00000rJjT9AAK', 'a036e00000z4fboAAA', 'a036e00000z4fbpAAA', 'a036e00000rKEP5AAO', 'a036e00000rKXDqAAO', 'a036e00000z36IJAAY', 'a036e00000z36IiAAI', 'a036e00000z5wVRAAY', 'a036e000010cGS9AAM', 'a036e000010cGRcAAM', 'a036e000010cHigAAE', 'a036e00000z6N9vAAE', 'a036e00000z36WhAAI', 'a036e00000z36WiAAI', 'a036e000010cHxGAAU', 'a036e00000z3HSzAAM', 'a036e00000z4msSAAQ', 'a036e000010cGcNAAU', 'a036e000010cGcOAAU', 'a036e000010cGcTAAU', 'a036e000010cGRQAA2', 'a036e000010cGRoAAM', 'a036e000010cGRrAAM', 'a036e000010cGRtAAM', 'a036e000010cGRuAAM', 'a036e000010cGRwAAM', 'a036e000010cGSPAA2', 'a036e000010cGSSAA2', 'a036e000010cGSYAA2', 'a036e000010cGSbAAM', 'a036e000010cGSjAAM', 'a036e000010cGSmAAM', 'a036e000010cmBwAAI', 'a036e00000z4fGCAAY', 'a036e00000rHUTRAA4', 'a036e00000rHUTVAA4', 'a036e00000zUrxjAAC', 'a036e00000zUs0AAAS', 'a036e00000z34E9AAI', 'a036e00000z34T0AAI', 'a036e00000z353JAAQ', 'a036e00000zVOmRAAW', 'a036e00000zVOmMAAW', 'a036e00000rJY9dAAG', 'a036e00000rHncEAAS', 'a036e00000rJY9QAAW', 'a036e00000z34EOAAY', 'a036e00000z4mVSAAY', 'a036e00000z3GgrAAE', 'a036e00000z3iIvAAI', 'a036e00000z4AUaAAM', 'a036e00000z3Gv0AAE', 'a036e00000z3GvSAAU', 'a036e00000rHzYUAA0', 'a036e00000z35RYAAY', 'a036e00000rJY9IAAW', 'a036e00000z4ejhAAA', 'a036e00000zUsYvAAK', 'a036e00000z5vtfAAA', 'a036e00000z4f2PAAQ', 'a036e00000z6MyfAAE', 'a036e00000zUNTyAAO', 'a036e00000zSjUBAA0', 'a036e00000zUZrxAAG', 'a036e00000rJY9XAAW', 'a036e00000z5znKAAQ', 'a036e00000zVv2eAAC', 'a036e00000z34NgAAI', 'a036e00000zVhUFAA0', 'a036e00000z3iWhAAI', 'a036e00000rJXo2AAG', 'a036e00000zUa26AAC', 'a036e00000rJXooAAG', 'a036e00000zUa1rAAC', 'a036e00000rHnXEAA0', 'a036e00000rHnXFAA0', 'a036e00000z34NlAAI', 'a036e00000rIi6sAAC', 'a036e00000rIwAfAAK', 'a036e00000rJkgpAAC', 'a036e00000rHeHCAA0', 'a036e00000rJYALAA4', 'a036e00000rJYAqAAO', 'a036e00000rJYArAAO', 'a036e00000rJYB2AAO', 'a036e00000rItjxAAC', 'a036e00000rJ0GLAA0', 'a036e00000rJUi5AAG', 'a036e00000rJXsQAAW', 'a036e00000zUNSDAA4', 'a036e00000rJY5jAAG', 'a036e00000rINO4AAO', 'a036e00000rIti0AAC', 'a036e00000rKFPmAAO', 'a036e00000z3GyQAAU', 'a036e00000rJYBNAA4', 'a036e00000rJxIlAAK', 'a036e00000rJXniAAG', 'a036e00000rJXnoAAG', 'a036e00000rJXsSAAW', 'a036e00000z5vvqAAA', 'a036e00000zUb5mAAC', 'a036e00000rJdiyAAC', 'a036e00000rJXsWAAW', 'a036e00000rJXscAAG', 'a036e00000zU9dcAAC', 'a036e00000rJY5lAAG', 'a036e00000zVvspAAC', 'a036e00000z4fEwAAI', 'a036e00000z35ReAAI', 'a036e00000z35RhAAI', 'a036e00000zUv5RAAS', 'a036e00000rJXsgAAG', 'a036e00000rKBJdAAO', 'a036e00000zVcaSAAS', 'a036e00000zVcdhAAC', 'a036e00000z35SQAAY', 'a036e00000zVcYfAAK', 'a036e00000z5zlMAAQ', 'a036e00000zSbO7AAK', 'a036e00000zTMntAAG', 'a036e00000rJXoAAAW', 'a036e00000rJXoCAAW', 'a036e00000zUa1sAAC', 'a036e00000rJXoOAAW', 'a036e00000rINH3AAO', 'a036e00000rJXqRAAW', 'a036e00000rIMC0AAO', 'a036e00000rJXtRAAW', 'a036e00000rK954AAC', 'a036e00000rJY6DAAW', 'a036e00000rJY6JAAW', 'a036e00000rJY6LAAW', 'a036e00000z4fEnAAI', 'a036e00000z4fEmAAI', 'a036e00000zVdAnAAK', 'a036e00000z4fEuAAI', 'a036e00000zVvsfAAC', 'a036e00000z4fEzAAI', 'a036e00000rJXoYAAW', 'a036e00000rJXofAAG', 'a036e00000rJXodAAG', 'a036e00000rJXoiAAG', 'a036e00000rJXomAAG', 'a036e00000z34EHAAY', 'a036e00000rIrqNAAS', 'a036e00000z4eloAAA', 'a036e00000rJmazAAC', 'a036e00000rJmhfAAC', 'a036e00000rJY3YAAW', 'a036e00000rJY74AAG', 'a036e00000rK96HAAS', 'a036e00000rJY7MAAW', 'a036e00000rJfQZAA0', 'a036e00000rJXorAAG', 'a036e00000rJXouAAG', 'a036e00000z4egqAAA', 'a036e00000rJbyHAAS', 'a036e00000yuZD0AAM', 'a036e00000z344AAAQ', 'a036e00000z344mAAA', 'a036e00000zUqUAAA0', 'a036e00000zUunqAAC', 'a036e00000rJXqGAAW', 'a036e00000z34NjAAI', 'a036e00000zV1WyAAK', 'a036e00000z34NsAAI', 'a036e00000z354xAAA', 'a036e00000z344tAAA', 'a036e00000zSbPUAA0', 'a036e00000rJXqMAAW', 'a036e00000z4nEOAAY', 'a036e00000z4nEPAAY', 'a036e00000z6MteAAE', 'a036e00000rJY3DAAW', 'a036e00000rJY3EAAW', 'a036e00000z4f54AAA', 'a036e00000z4f56AAA', 'a036e00000z4nU2AAI', 'a036e00000z3452AAA', 'a036e00000z345eAAA', 'a036e00000z345iAAA', 'a036e00000zUsH4AAK', 'a036e00000zUtkpAAC', 'a036e00000zVv2xAAC', 'a036e00000rIUalAAG', 'a036e00000rJj3QAAS', 'a036e00000rJY3WAAW', 'a036e00000rJY3XAAW', 'a036e00000rJbAVAA0', 'a036e00000z4nU3AAI', 'a036e00000zSjPPAA0', 'a036e00000zVv1yAAC', 'a036e00000rJXrGAAW', 'a036e00000rJW2GAAW', 'a036e00000rJXugAAG', 'a036e00000rJY46AAG', 'a036e00000rJY4DAAW', 'a036e00000rJY4EAAW', 'a036e00000rJY4FAAW', 'a036e00000rHnboAAC', 'a036e00000rJXrdAAG', 'a036e00000rJXriAAG', 'a036e00000rJXrnAAG', 'a036e00000rJXrqAAG', 'a036e00000rJXuoAAG', 'a036e00000rJXupAAG', 'a036e00000rJW2RAAW', 'a036e00000rJXurAAG', 'a036e00000rJY4dAAG', 'a036e00000rJY4gAAG', 'a036e00000rJY4iAAG', 'a036e00000rJY4jAAG', 'a036e00000rJY4nAAG', 'a036e00000rJY4qAAG', 'a036e00000rJjLyAAK', 'a036e00000rJjM3AAK', 'a036e00000rJjM6AAK', 'a036e00000rJjM8AAK', 'a036e00000rJjMAAA0', 'a036e00000rJjMBAA0', 'a036e00000zUoNBAA0', 'a036e00000zVA8wAAG', 'a036e00000z34EAAAY', 'a036e00000z34T7AAI', 'a036e00000zV5yhAAC', 'a036e00000rJ0HNAA0', 'a036e00000zUsGzAAK', 'a036e00000zTa0MAAS', 'a036e00000zVBWSAA4', 'a036e00000zVBVXAA4', 'a036e00000z4Af2AAE', 'a036e00000z4ex4AAA', 'a036e00000z35gZAAQ', 'a036e00000z35gaAAA', 'a036e00000z35gdAAA', 'a036e00000z35gjAAA', 'a036e00000z35i6AAA', 'a036e00000zViXTAA0', 'a036e00000zViWtAAK', 'a036e00000z4fJ8AAI', 'a036e00000z4fJDAAY', 'a036e00000z5wFoAAI', 'a036e00000rJYC0AAO', 'a036e00000rJYC4AAO', 'a036e000010bb9MAAQ', 'a036e000010c0pLAAQ', 'a036e00000z5wFaAAI', 'a036e00000rJYCRAA4', 'a036e00000rJYCgAAO', 'a036e00000rJYCnAAO', 'a036e00000rJYCpAAO', 'a036e00000rJYCqAAO', 'a036e00000rJYCyAAO', 'a036e00000z35iHAAQ', 'a036e00000zVibvAAC', 'a036e00000zVBVaAAO', 'a036e00000z3GmfAAE', 'a036e00000rHUSnAAO', 'a036e00000zVvqDAAS', 'a036e00000z4euEAAQ', 'a036e00000z5zzCAAQ', 'a036e00000zUNRMAA4', 'a036e00000rHzWiAAK', 'a036e00000rJYDHAA4', 'a036e00000rJYDKAA4', 'a036e00000rJYDLAA4', 'a036e00000rJYDOAA4', 'a036e00000rJYDSAA4', 'a036e00000rJYDfAAO', 'a036e00000z4nIKAAY', 'a036e00000z4nIQAAY', 'a036e00000z6MwDAAU', 'a036e00000zSblRAAS', 'a036e00000rJY0vAAG', 'a036e00000zUNRFAA4', 'a036e00000rJYDmAAO', 'a036e00000rJYDoAAO', 'a036e00000rJdfQAAS', 'a036e00000rJoetAAC', 'a036e00000rJXwIAAW', 'a036e00000z34bZAAQ', 'a036e00000z4ex2AAA', 'a036e00000z4exGAAQ', 'a036e00000rJY2MAAW', 'a036e00000rJY21AAG', 'a036e00000rJY1AAAW', 'a036e00000z600bAAA', 'a036e00000z35ZkAAI', 'a036e00000z35ZqAAI', 'a036e00000z35a7AAA', 'a036e00000zUNRNAA4', 'a036e00000z34vAAAQ', 'a036e00000zUa2aAAC', 'a036e00000rJtrlAAC', 'a036e00000rJts5AAC', 'a036e00000rJXykAAG', 'a036e00000zVgK7AAK', 'a036e00000z3H67AAE', 'a036e00000z3ifyAAA', 'a036e00000rHQEoAAO', 'a036e00000rHna9AAC', 'a036e00000rJXynAAG', 'a036e00000rJY2AAAW', 'a036e00000z600eAAA', 'a036e00000rJY2NAAW', 'a036e00000rJY2OAAW', 'a036e00000rJjQMAA0', 'a036e00000rJkkcAAC', 'a036e00000z4fGHAAY', 'a036e00000z4fGXAAY', 'a036e00000z4naeAAA', 'a036e00000z35iNAAQ', 'a036e00000rIMFpAAO', 'a036e00000rINKvAAO', 'a036e00000rJ94LAAS', 'a036e00000z5w2FAAQ', 'a036e00000rJXyzAAG', 'a036e00000rJklBAAS', 'a036e00000rJrtfAAC', 'a036e00000rJskVAAS', 'a036e00000rJuhAAAS', 'a036e00000z5w4qAAA', 'a036e00000rJxKrAAK', 'a036e00000z6N3MAAU', 'a036e00000z6N3aAAE', 'a036e00000z6N3qAAE', 'a036e000010biOCAAY', 'a036e00000rHndHAAS', 'a036e00000rJYELAA4', 'a036e00000zUoIMAA0', 'a036e00000zUFkIAAW', 'a036e00000z5zzHAAQ', 'a036e00000zTO7vAAG', 'a036e00000z34u0AAA', 'a036e00000z60EzAAI', 'a036e00000zUNUvAAO', 'a036e00000zVAE9AAO', 'a036e00000zU9d2AAC', 'a036e00000zSbnvAAC', 'a036e00000rJXzNAAW', 'a036e00000rJY1UAAW', 'a036e00000rJXzUAAW', 'a036e00000rJY22AAG', 'a036e00000rJXzjAAG', 'a036e00000rJXzlAAG', 'a036e00000rJXzoAAG', 'a036e00000rJXzpAAG', 'a036e00000zSc2dAAC', 'a036e00000z34vQAAQ', 'a036e00000zUNUjAAO', 'a036e00000rJXwvAAG', 'a036e00000rJdnWAAS', 'a036e00000rJY0EAAW', 'a036e00000rJbvhAAC', 'a036e00000zVvr8AAC', 'a036e00000z4f04AAA', 'a036e00000z5w4pAAA', 'a036e00000z6MyIAAU', 'a036e00000zUoQuAAK', 'a036e00000z60HJAAY', 'a036e00000rJXxMAAW', 'a036e00000rJXxaAAG', 'a036e00000rJXxiAAG', 'a036e00000zVFvJAAW', 'a036e00000z4ex6AAA', 'a036e00000z34tyAAA', 'a036e00000z34jfAAA', 'a036e00000zUvAJAA0', 'a036e00000rJYEsAAO', 'a036e00000rJYEuAAO', 'a036e00000rJYF1AAO', 'a036e000010c0ZJAAY', 'a036e00000z4exAAAQ', 'a036e00000z34azAAA', 'a036e00000z34ZMAAY', 'a036e00000z34jaAAA', 'a036e00000rJYFiAAO', 'a036e00000rJYFxAAO', 'a036e00000zVBVjAAO', 'a036e00000zW1QwAAK', 'a036e00000z34lhAAA', 'a036e00000rJkjaAAC', 'a036e00000z35gPAAQ', 'a036e00000z35gUAAQ', 'a036e000010cGXIAA2', 'a036e000010cGX2AAM', 'a036e00000zUoVeAAK', 'a036e00000z36R8AAI', 'a036e00000z4fekAAA', 'a036e000010bwixAAA', 'a036e000010cGMIAA2', 'a036e000010cGMLAA2', 'a036e000010cGMRAA2', 'a036e00000rH9eiAAC', 'a036e00000z36R5AAI', 'a036e00000rINbvAAG', 'a036e000010cGWyAAM', 'a036e00000rJezVAAS', 'a036e000010cGNOAA2', 'a036e000010cGNjAAM', 'a036e000010ciHZAAY', 'a036e000010bvaaAAA', 'a036e000010cz1dAAA', 'a036e00000z4mnCAAQ', 'a036e00000z4nujAAA', 'a036e00000z4nukAAA', 'a036e000010cGWzAAM', 'a036e000010cHo2AAE', 'a036e000010cGXFAA2', 'a036e000010cGXcAAM', 'a036e000010cGXeAAM', 'a036e000010cGYWAA2', 'a036e000010cGYcAAM', 'a036e000010cGYdAAM', 'a036e000010cHnoAAE', 'a036e000010cNI5AAM', 'a036e000010cNIYAA2', 'a036e000010cUfIAAU', 'a036e00000rH62TAAS', 'a036e00000rH65UAAS', 'a036e00000z4fYWAAY', 'a036e00000zUoVjAAK', 'a036e00000rIMXJAA4', 'a036e00000rIMXVAA4', 'a036e00000rJjSHAA0', 'a036e00000rJjSNAA0', 'a036e00000rJjSSAA0', 'a036e00000z36AEAAY', 'a036e00000z36AIAAY', 'a036e000010cUdmAAE', 'a036e000010cl0dAAA', 'a036e00000z4mi1AAA', 'a036e00000z4noIAAQ', 'a036e00000z4noKAAQ', 'a036e00000z60VcAAI', 'a036e00000zTaVZAA0', 'a036e000010c3SbAAI', 'a036e00000rKEQSAA4', 'a036e00000z35xvAAA', 'a036e00000z35xxAAA', 'a036e00000z35xzAAA', 'a036e00000z35yzAAA', 'a036e00000z35z0AAA', 'a036e00000zVkCtAAK', 'a036e00000z35qxAAA', 'a036e00000z4B0QAAU', 'a036e00000z4fMsAAI', 'a036e00000z3HEVAA2', 'a036e000010cDtcAAE', 'a036e00000z4fMeAAI', 'a036e00000z4n2bAAA', 'a036e00000zVr6TAAS', 'a036e00000z4fQ0AAI', 'a036e00000z4fQ4AAI', 'a036e00000z6N5cAAE', 'a036e00000zVkSOAA0', 'a036e00000z5wMBAAY', 'a036e00000z5wMLAAY', 'a036e000010cDtLAAU', 'a036e00000zUNVkAAO', 'a036e00000zUNVnAAO', 'a036e000010cDttAAE', 'a036e000010cDuwAAE', 'a036e00000rHTLNAA4', 'a036e00000rIY8OAAW', 'a036e00000z35qlAAA', 'a036e00000zSci3AAC', 'a036e00000rIZMhAAO', 'a036e00000rJYGcAAO', 'a036e00000rJYGkAAO', 'a036e00000rJYGqAAO', 'a036e00000z60NVAAY', 'a036e00000rJYHNAA4', 'a036e00000rJYHRAA4', 'a036e00000rJYHgAAO', 'a036e00000rJYHlAAO', 'a036e00000rJYI1AAO', 'a036e00000z35z3AAA', 'a036e00000rHneJAAS', 'a036e00000rJYICAA4', 'a036e00000rJjPeAAK', 'a036e00000rJjPhAAK', 'a036e00000rHneXAAS', 'a036e000010cNcMAAU', 'a036e00000z5wMIAAY', 'a036e00000zUNVpAAO', 'a036e00000yuZpYAAU', 'a036e00000z35p6AAA', 'a036e00000z5wM1AAI', 'a036e00000zUNViAAO', 'a036e00000rJupiAAC', 'a036e00000rK1iPAAS', 'a036e000010cDtjAAE', 'a036e000010cGI6AAM', 'a036e000010cGI9AAM', 'a036e00000rJb7aAAC', 'a036e00000rJb7lAAC', 'a036e00000z37YdAAI', 'a036e000010cGGHAA2', 'a036e00000z3657AAA', 'a036e00000rH1w7AAC', 'a036e00000rH1wyAAC', 'a036e00000z364xAAA', 'a036e00000z4fVPAAY', 'a036e00000rItgDAAS', 'a036e00000z363UAAQ', 'a036e00000z4fVNAAY', 'a036e000010cGHNAA2', 'a036e00000z60THAAY', 'a036e00000zTODaAAO', 'a036e000010cHYSAA2', 'a036e00000z60TRAAY', 'a036e00000z6N89AAE', 'a036e000010cGGZAA2', 'a036e000010cGH6AAM', 'a036e000010cGHfAAM', 'a036e000010cGHjAAM', 'a036e000010cGHrAAM', 'a036e000010cGHyAAM', 'a036e000010cGI0AAM', 'a036e000010cGI1AAM', 'a036e00000rH7dMAAS', 'a036e00000z36JpAAI', 'a036e00000rH7tMAAS', 'a036e00000rH7tOAAS', 'a036e00000rH7zdAAC', 'a036e00000z4fbaAAA', 'a036e00000z4fbfAAA', 'a036e00000z4fbhAAA', 'a036e00000z4fbkAAA', 'a036e00000z5wVdAAI', 'a036e000010cGRIAA2', 'a036e00000rJjStAAK', 'a036e00000rJohiAAC', 'a036e00000z36IEAAY', 'a036e00000z36IGAAY', 'a036e00000z36IHAAY', 'a036e00000z36IcAAI', 'a036e00000z36IgAAI', 'a036e00000z36KIAAY', 'a036e00000z36KKAAY', 'a036e00000z36KOAAY', 'a036e00000z3HOAAA2', 'a036e00000z5wVTAAY', 'a036e000010cGRSAA2', 'a036e00000z5wVcAAI', 'a036e000010cGS6AAM', 'a036e00000z6MkCAAU', 'a036e00000z6NACAA2', 'a036e00000rIUdpAAG', 'a036e00000zSjYEAA0', 'a036e000010cGSCAA2', 'a036e00000zUNXwAAO', 'a036e000010cHjNAAU', 'a036e00000z6NBzAAM', 'a036e000010cGcPAAU', 'a036e000010cGcRAAU', 'a036e000010cGcSAAU', 'a036e000010bjv0AAA', 'a036e000010cGRnAAM', 'a036e000010cGRhAAM', 'a036e000010cGRmAAM', 'a036e000010cGS1AAM', 'a036e000010cGSUAA2', 'a036e000010cGSXAA2', 'a036e000010cGSaAAM', 'a036e000010cGSdAAM', 'a036e000010cGSpAAM', 'a036e000010coxWAAQ', 'a036e00000z6Mx6AAE', 'a036e00000z6MxzAAE', 'a036e00000rHHabAAG', 'a036e00000z4fGEAAY', 'a036e00000z34E6AAI', 'a036e00000zUrybAAC', 'a036e00000z34EBAAY', 'a036e00000zUs0OAAS', 'a036e00000z34EKAAY', 'a036e00000z34NmAAI', 'a036e00000rJXuhAAG', 'a036e00000rJXujAAG', 'a036e00000rJXukAAG', 'a036e00000z353BAAQ', 'a036e00000z353DAAQ', 'a036e00000z353HAAQ', 'a036e00000zSnDLAA0', 'a036e00000zVOkHAAW', 'a036e00000z354sAAA', 'a036e00000zUuyjAAC', 'a036e00000rHQFNAA4', 'a036e00000rJW90AAG', 'a036e00000rHQFQAA4', 'a036e00000rJY9nAAG', 'a036e00000rHQFTAA4', 'a036e00000rHQFUAA4', 'a036e00000rJY9OAAW', 'a036e00000z43RrAAI', 'a036e00000z4AhuAAE', 'a036e00000rJ3b2AAC', 'a036e00000zVfdaAAC', 'a036e00000zSjUGAA0', 'a036e00000rJY9VAAW', 'a036e00000z63axAAA', 'a036e00000z63JKAAY', 'a036e00000z6Ms6AAE', 'a036e00000z6MtaAAE', 'a036e00000z6MsWAAU', 'a036e00000rHnYEAA0', 'a036e00000rI7wKAAS', 'a036e00000zVPa6AAG', 'a036e00000z63UcAAI', 'a036e00000rJY3UAAW', 'a036e00000z3538AAA', 'a036e00000z3539AAA', 'a036e00000rJY9bAAG', 'a036e00000zU9e5AAC', 'a036e00000rKZNEAA4', 'a036e00000rJYA2AAO', 'a036e00000z344BAAQ', 'a036e00000z344nAAA', 'a036e00000rINHiAAO', 'a036e00000rJXsiAAG', 'a036e00000zTCoBAAW', 'a036e00000rIMKQAA4', 'a036e00000rJYAzAAO', 'a036e00000rHnXOAA0', 'a036e00000rHyq5AAC', 'a036e00000rIrfxAAC', 'a036e00000z3GdqAAE', 'a036e00000rJXnqAAG', 'a036e00000z5vvrAAA', 'a036e00000rJY5aAAG', 'a036e00000zUaa7AAC', 'a036e00000rJCqfAAG', 'a036e00000z4AmuAAE', 'a036e00000rJY5ZAAW', 'a036e00000z35R9AAI', 'a036e00000rJXnpAAG', 'a036e00000rJZrtAAG', 'a036e00000rJXsfAAG', 'a036e00000zVcaZAAS', 'a036e00000z35RdAAI', 'a036e00000z35SDAAY', 'a036e00000zVcZTAA0', 'a036e00000zUa27AAC', 'a036e00000rJXo4AAG', 'a036e00000rJXo5AAG', 'a036e00000z5zlOAAQ', 'a036e00000rJXqKAAW', 'a036e00000zUNOBAA4', 'a036e00000z3iLUAAY', 'a036e00000rJY5qAAG', 'a036e00000zUaaCAAS', 'a036e00000rJY5vAAG', 'a036e00000zUv5dAAC', 'a036e00000zVcYbAAK', 'a036e00000zVcZfAAK', 'a036e00000rJXoMAAW', 'a036e00000rJXoNAAW', 'a036e00000rJXqVAAW', 'a036e00000rJkN5AAK', 'a036e00000rJXqSAAW', 'a036e00000rJdjXAAS', 'a036e00000rJXsuAAG', 'a036e00000rJkfmAAC', 'a036e00000zUup1AAC', 'a036e00000rJY69AAG', 'a036e00000rJY6AAAW', 'a036e00000rJY6EAAW', 'a036e00000rJY6GAAW', 'a036e00000rJY6xAAG', 'a036e00000rJY6zAAG', 'a036e00000z4fEgAAI', 'a036e00000z4fEhAAI', 'a036e00000z4nXQAAY', 'a036e00000z5vrRAAQ', 'a036e00000zVA2XAAW', 'a036e00000rJbNzAAK', 'a036e00000rJdehAAC', 'a036e00000rJdkVAAS', 'a036e00000rJkVcAAK', 'a036e00000rJmU2AAK', 'a036e00000rJme3AAC', 'a036e00000rHAjVAAW', 'a036e00000rJY78AAG', 'a036e00000rJY7AAAW', 'a036e00000rJY7BAAW', 'a036e00000rJY7HAAW', 'a036e00000rJY7LAAW', 'a036e00000z6N2aAAE', 'a036e00000zUNU2AAO', 'a036e00000rJXoyAAG', 'a036e00000rJXozAAG', 'a036e00000rJb31AAC', 'a036e00000rJbpYAAS', 'a036e00000z5vtWAAQ', 'a036e00000rJXqBAAW', 'a036e00000rJXqLAAW', 'a036e00000z63JJAAY', 'a036e00000zUtKHAA0', 'a036e00000z34NqAAI', 'a036e00000zUb5vAAC', 'a036e00000rJY3aAAG', 'a036e00000rIRdMAAW', 'a036e00000zUoM6AAK', 'a036e00000rKFSvAAO', 'a036e00000z35CoAAI', 'a036e00000zUqVoAAK', 'a036e00000z3450AAA', 'a036e00000rJXqUAAW', 'a036e00000zVvpgAAC', 'a036e00000z4eluAAA', 'a036e00000z4nEQAAY', 'a036e00000z5vvtAAA', 'a036e00000rJY3FAAW', 'a036e00000rJY3GAAW', 'a036e00000rJY3JAAW', 'a036e00000rJY3LAAW', 'a036e00000z4f4sAAA', 'a036e00000zVvryAAC', 'a036e00000z4f5AAAQ', 'a036e00000z3451AAA', 'a036e00000z345gAAA', 'a036e00000z3Ge2AAE', 'a036e00000z3Ge6AAE', 'a036e00000z4n9EAAQ', 'a036e00000z4n9GAAQ', 'a036e00000z5vrOAAQ', 'a036e00000z5vrQAAQ', 'a036e00000z6MrKAAU', 'a036e00000rJXqXAAW', 'a036e00000zUsH0AAK', 'a036e00000zUuo1AAC', 'a036e00000rJXqkAAG', 'a036e00000zUb5uAAC', 'a036e00000zVv31AAC', 'a036e00000z34T2AAI', 'a036e00000z34T3AAI', 'a036e00000rIUcwAAG', 'a036e00000z6MzhAAE', 'a036e00000zUr6GAAS', 'a036e00000zVv1zAAC', 'a036e00000rJXtBAAW', 'a036e00000rJXrLAAW', 'a036e00000rJXrNAAW', 'a036e00000rJXuqAAG', 'a036e00000rIUewAAG', 'a036e00000yszvCAAQ', 'a036e00000rJY3uAAG', 'a036e00000rJY3yAAG', 'a036e00000rJY4HAAW', 'a036e00000zSjTEAA0', 'a036e00000rI8YVAA0', 'a036e00000rJY89AAG', 'a036e00000rJXroAAG', 'a036e00000rJXumAAG', 'a036e00000rJXunAAG', 'a036e00000zSbhQAAS', 'a036e00000rJY4lAAG', 'a036e00000rJjLzAAK', 'a036e00000rJzC0AAK', 'a036e00000z3H0rAAE', 'a036e00000rHnbIAAS', 'a036e00000rJY5kAAG', 'a036e00000z43SvAAI', 'a036e00000rJbJTAA0', 'a036e00000z34CeAAI', 'a036e00000z4nHKAAY', 'a036e00000rJXqdAAG', 'a036e00000rJjMEAA0', 'a036e00000rJkV8AAK', 'a036e00000rJY9rAAG', 'a036e00000z35RiAAI', 'a036e00000rHHaLAAW', 'a036e00000zVBZfAAO', 'a036e00000z34bbAAA', 'a036e00000z3GqWAAU', 'a036e00000z35geAAA', 'a036e00000zViWcAAK', 'a036e00000z35iDAAQ', 'a036e00000z3H8qAAE', 'a036e00000z3H9MAAU', 'a036e00000z43hdAAA', 'a036e00000z4fIxAAI', 'a036e00000z60F6AAI', 'a036e00000rJYBiAAO', 'a036e00000rJYBjAAO', 'a036e00000z4fJRAAY', 'a036e00000z4fJSAAY', 'a036e00000zVjNfAAK', 'a036e00000rJYC2AAO', 'a036e00000rJYC3AAO', 'a036e00000z5wFYAAY', 'a036e00000rJYCZAA4', 'a036e00000rJYCfAAO', 'a036e00000rJYChAAO', 'a036e00000rJYCxAAO', 'a036e00000zScdKAAS', 'a036e00000zVuVsAAK', 'a036e000010bb6rAAA', 'a036e00000z4eu2AAA', 'a036e00000z4euDAAQ', 'a036e00000z6MwlAAE', 'a036e00000z6Mx7AAE', 'a036e00000z6MxDAAU', 'a036e00000rJYDWAA4', 'a036e00000rJYDdAAO', 'a036e00000rJYDgAAO', 'a036e00000z4nIHAAY', 'a036e00000z4nIIAAY', 'a036e00000z4nIVAAY', 'a036e00000rIMIEAA4', 'a036e00000rJCoDAAW', 'a036e00000rJumAAAS', 'a036e00000zVvqPAAS', 'a036e00000z4exKAAQ', 'a036e00000z4exMAAQ', 'a036e00000z34lYAAQ', 'a036e00000rJY11AAG', 'a036e00000zUa2YAAS', 'a036e00000z4fGVAAY', 'a036e00000z35ZeAAI', 'a036e00000z35ZfAAI', 'a036e00000z35ZgAAI', 'a036e00000z35ZnAAI', 'a036e00000z35ZpAAI', 'a036e00000z35ZsAAI', 'a036e00000z35a5AAA', 'a036e00000z34jcAAA', 'a036e00000rIXseAAG', 'a036e00000rJts6AAC', 'a036e00000rJXyPAAW', 'a036e00000z5w4rAAA', 'a036e00000z5w4sAAA', 'a036e00000rJY1cAAG', 'a036e00000rJY1pAAG', 'a036e00000rJY1rAAG', 'a036e00000z35bWAAQ', 'a036e00000z4mwkAAA', 'a036e00000z60F3AAI', 'a036e00000z43f1AAA', 'a036e00000rJXwDAAW', 'a036e00000zTMqVAAW', 'a036e00000rHQF0AAO', 'a036e00000rJXyWAAW', 'a036e00000rJXyYAAW', 'a036e00000rJXyZAAW', 'a036e00000rJXzkAAG', 'a036e00000rJb0NAAS', 'a036e00000rJY28AAG', 'a036e00000rJY2JAAW', 'a036e00000rJYEcAAO', 'a036e00000rHnZCAA0', 'a036e00000rJsHUAA0', 'a036e00000z63PgAAI', 'a036e00000rJuIzAAK', 'a036e00000rJyoXAAS', 'a036e00000zVNAZAA4', 'a036e00000z60F9AAI', 'a036e00000z63dOAAQ', 'a036e00000zUNUlAAO', 'a036e00000rJXw6AAG', 'a036e00000rJXw9AAG', 'a036e00000rJXzAAAW', 'a036e00000z4sPJAAY', 'a036e00000zVN9VAAW', 'a036e00000zVNAxAAO', 'a036e00000zVNBwAAO', 'a036e00000z34vIAAQ', 'a036e00000rINSuAAO', 'a036e00000rJ6oRAAS', 'a036e00000rJXwEAAW', 'a036e00000z3EqOAAU', 'a036e00000rJXwLAAW', 'a036e00000rJeA6AAK', 'a036e00000rJXwMAAW', 'a036e00000rJXzQAAW', 'a036e00000zVNEmAAO', 'a036e00000zUNUmAAO', 'a036e00000rJXwxAAG', 'a036e00000rJXx7AAG', 'a036e00000rJXzrAAG', 'a036e00000rJts0AAC', 'a036e00000z3GsZAAU', 'a036e00000z4AgeAAE', 'a036e00000z600WAAQ', 'a036e00000z6MxfAAE', 'a036e00000zUXDzAAO', 'a036e00000zUa2wAAC', 'a036e00000zUoQvAAK', 'a036e00000zUa33AAC', 'a036e00000rJXxNAAW', 'a036e00000rJXxQAAW', 'a036e00000rJv2yAAC', 'a036e00000z34jgAAA', 'a036e00000zUvACAA0', 'a036e00000rJYEqAAO', 'a036e00000rJYEvAAO', 'a036e00000rJYEyAAO', 'a036e00000rJmnFAAS', 'a036e00000zVBUVAA4', 'a036e00000zSbvnAAC', 'a036e00000rJYFmAAO', 'a036e00000rJYFqAAO', 'a036e00000rJYFsAAO', 'a036e00000rJYFwAAO', 'a036e00000rJjP7AAK', 'a036e00000rJjPBAA0', 'a036e00000rJjPEAA0', 'a036e00000z34jbAAA', 'a036e00000z34amAAA', 'a036e00000zVBUnAAO', 'a036e00000z3Gq5AAE', 'a036e00000rJkmyAAC', 'a036e00000rJmf6AAC', 'a036e00000rJomJAAS', 'a036e00000zScbzAAC', 'a036e000010c0cIAAQ', 'a036e000010cGWwAAM', 'a036e000010cGX9AAM', 'a036e000010cGM1AAM', 'a036e00000zUa3UAAS', 'a036e000010cGMAAA2', 'a036e00000rH91BAAS', 'a036e00000z36RKAAY', 'a036e00000rH9HiAAK', 'a036e00000z441FAAQ', 'a036e000010bve4AAA', 'a036e000010cGMPAA2', 'a036e000010cGMVAA2', 'a036e000010cGMWAA2', 'a036e00000rH9ekAAC', 'a036e000010cGX0AAM', 'a036e000010cGMfAAM', 'a036e000010cGMhAAM', 'a036e000010cGNMAA2', 'a036e00000z36QsAAI', 'a036e00000z36RBAAY', 'a036e00000z36RFAAY', 'a036e00000z36RNAAY', 'a036e00000z441IAAQ', 'a036e00000z4feLAAQ', 'a036e00000z4feUAAQ', 'a036e00000z4mnBAAQ', 'a036e000010cm3xAAA', 'a036e00000zUoXGAA0', 'a036e000010cGWpAAM', 'a036e000010cGXBAA2', 'a036e000010cGXiAAM', 'a036e000010cGXlAAM', 'a036e000010cGXmAAM', 'a036e000010cGXxAAM', 'a036e000010cGYUAA2', 'a036e000010cGYVAA2', 'a036e000010cHnSAAU', 'a036e000010cGMGAA2', 'a036e000010cl0nAAA', 'a036e00000rH6OfAAK', 'a036e00000z36BxAAI', 'a036e00000rJj9vAAC', 'a036e00000rJjSKAA0', 'a036e000010c3uOAAQ', 'a036e00000rJjSUAA0', 'a036e00000rJrqPAAS', 'a036e00000z43t0AAA', 'a036e00000z36AGAAY', 'a036e00000z36AMAAY', 'a036e00000z36ANAAY', 'a036e00000z4fYNAAY', 'a036e00000z4fYpAAI', 'a036e00000z6N91AAE', 'a036e00000zUNXMAA4', 'a036e00000z35qoAAA', 'a036e00000zSnDgAAK', 'a036e000010cDtlAAE', 'a036e00000z35xuAAA', 'a036e00000z35y0AAA', 'a036e00000z3HByAAM', 'a036e00000z4B0SAAU', 'a036e00000z3inZAAQ', 'a036e00000z4fMaAAI', 'a036e00000zVkksAAC', 'a036e00000z4fN0AAI', 'a036e00000z4fN1AAI', 'a036e00000zVkYyAAK', 'a036e00000z4fN2AAI', 'a036e000010bveUAAQ', 'a036e00000zVr6UAAS', 'a036e00000z4fQ6AAI', 'a036e00000zVr6ZAAS', 'a036e00000z5wM8AAI', 'a036e00000z4nfUAAQ', 'a036e00000z6N5KAAU', 'a036e000010bwinAAA', 'a036e00000z5wMHAAY', 'a036e000010cDtUAAU', 'a036e000010cDtWAAU', 'a036e00000z63jhAAA', 'a036e00000z6N6qAAE', 'a036e00000zUNVjAAO', 'a036e000010cJJGAA2', 'a036e00000rJYGdAAO', 'a036e00000rJYGnAAO', 'a036e000010cDu8AAE', 'a036e00000rHQ7lAAG', 'a036e00000z4B0RAAU', 'a036e00000rJYGLAA4', 'a036e00000rJYGPAA4', 'a036e00000rJYGSAA4', 'a036e00000rJYGXAA4', 'a036e00000rJYGgAAO', 'a036e00000rJYGjAAO', 'a036e00000zUoRiAAK', 'a036e00000rJYGpAAO', 'a036e00000z5wL5AAI', 'a036e000010cDtFAAU', 'a036e000010cDteAAE', 'a036e00000rJYHYAA4', 'a036e00000rJYHjAAO', 'a036e00000rHNcQAAW', 'a036e00000rHneLAAS', 'a036e00000zUab4AAC', 'a036e00000rJjPdAAK', 'a036e00000rJjPtAAK', 'a036e00000rIMSGAA4', 'a036e00000rINV0AAO', 'a036e00000yuZpcAAE', 'a036e00000z35p1AAA', 'a036e00000z35p3AAA', 'a036e00000rJznIAAS', 'a036e00000rKEQXAA4', 'a036e000010cGI5AAM', 'a036e000010cGI8AAM', 'a036e000010cGICAA2', 'a036e000010cGIEAA2', 'a036e000010cNjrAAE', 'a036e00000z37Y6AAI', 'a036e00000rJb7YAAS', 'a036e00000z37YVAAY', 'a036e00000rJYL5AAO', 'a036e000010cGHvAAM', 'a036e00000z3655AAA', 'a036e00000z3656AAA', 'a036e00000zVAHLAA4', 'a036e00000rH2XRAA0', 'a036e00000z4fVkAAI', 'a036e00000z4fVOAAY', 'a036e00000z4fVcAAI', 'a036e00000z4fVdAAI', 'a036e00000rHUU1AAO', 'a036e00000rJog1AAC', 'a036e000010bjmVAAQ', 'a036e000010bjmTAAQ', 'a036e000010bjmUAAQ', 'a036e00000z4fVlAAI', 'a036e00000z4meRAAQ', 'a036e00000z5wPmAAI', 'a036e00000z60TGAAY', 'a036e000010cHWKAA2', 'a036e000010cHXgAAM', 'a036e000010cGGGAA2', 'a036e000010cGHAAA2', 'a036e000010cGHEAA2', 'a036e000010cGHqAAM', 'a036e000010cGHwAAM', 'a036e000010cGHzAAM', 'a036e000010cGRRAA2', 'a036e000010cGSDAA2', 'a036e00000z36JsAAI', 'a036e00000rH7dwAAC', 'a036e00000zVALGAA4', 'a036e000010cGRYAA2', 'a036e00000rH7zhAAC', 'a036e00000z4fbiAAA', 'a036e00000rH7znAAC', 'a036e00000rH8BSAA0', 'a036e00000rJYPgAAO', 'a036e00000rJjSzAAK', 'a036e00000rJjT8AAK', 'a036e00000rKEPKAA4', 'a036e00000z36IFAAY', 'a036e00000z36IKAAY', 'a036e00000z36IbAAI', 'a036e000010cHiUAAU', 'a036e00000z36KJAAY', 'a036e00000zSjYFAA0', 'a036e00000z4fbVAAQ', 'a036e00000z4fbWAAQ', 'a036e00000z5wVSAAY', 'a036e000010cGRMAA2', 'a036e00000z60ZAAAY', 'a036e00000rIUcTAAW', 'a036e00000z6NARAA2', 'a036e00000z36WgAAI', 'a036e00000zUoWZAA0', 'a036e000010cGRpAAM', 'a036e000010cGRsAAM', 'a036e00000z6MxBAAU', 'a036e00000z4fGDAAY', 'a036e00000z4fGRAAY', 'a036e00000rHgGNAA0', 'a036e00000z34EDAAY', 'a036e00000zSbTsAAK', 'a036e00000z34EQAAY', 'a036e00000rJXspAAG', 'a036e00000rJXtcAAG', 'a036e00000z3530AAA', 'a036e00000z353EAAQ', 'a036e00000rHQFHAA4', 'a036e00000z4mVUAAY', 'a036e00000z4ejgAAA', 'a036e00000zVOkUAAW', 'a036e00000z3Gv1AAE', 'a036e00000z3GvOAAU', 'a036e00000rJY9HAAW', 'a036e00000z4ejbAAA', 'a036e00000zVvpHAAS', 'a036e00000z5vtbAAA', 'a036e00000z5znCAAQ', 'a036e00000z4f2UAAQ', 'a036e00000zTMs1AAG', 'a036e00000zVA8XAAW', 'a036e00000rJY9WAAW', 'a036e00000z6Ms4AAE', 'a036e00000z6Ms8AAE', 'a036e00000z6MtdAAE', 'a036e00000zUNO9AAO', 'a036e00000rI7wPAAS', 'a036e00000z353AAAQ', 'a036e00000rJuh0AAC', 'a036e00000rJXonAAG', 'a036e00000z3449AAA', 'a036e00000rHgKBAA0', 'a036e00000rHnXMAA0', 'a036e00000zU9chAAC', 'a036e00000rJXtDAAW', 'a036e00000rIMKZAA4', 'a036e00000rJYAGAA4', 'a036e00000rJYAHAA4', 'a036e00000rJYB9AAO', 'a036e00000zUa24AAC', 'a036e00000zVA0XAAW', 'a036e00000rIyGtAAK', 'a036e00000rJXsOAAW', 'a036e00000rJXsPAAW', 'a036e00000rJZpeAAG', 'a036e00000rJY5sAAG', 'a036e00000rJYBBAA4', 'a036e00000rJYBOAA4', 'a036e00000zUNNcAAO', 'a036e00000rJXnjAAG', 'a036e00000rJXnnAAG', 'a036e00000rJZrZAAW', 'a036e00000rJY6RAAW', 'a036e00000zUNSJAA4', 'a036e00000z43SoAAI', 'a036e00000z35RAAAY', 'a036e00000zScOmAAK', 'a036e00000zVcalAAC', 'a036e00000z35SGAAY', 'a036e00000rJXnyAAG', 'a036e00000z5vrTAAQ', 'a036e00000z5vrWAAQ', 'a036e00000rJXo0AAG', 'a036e00000z5vrSAAQ', 'a036e00000rJXssAAG', 'a036e00000zSjQNAA0', 'a036e00000rKFUhAAO', 'a036e00000zScRYAA0', 'a036e00000z35SRAAY', 'a036e00000rJXo8AAG', 'a036e00000z4ejZAAQ', 'a036e00000rI45EAAS', 'a036e00000rJXsxAAG', 'a036e00000rJXt8AAG', 'a036e00000rJXtUAAW', 'a036e00000rJY6CAAW', 'a036e00000z4nXPAAY', 'a036e00000z4nXRAAY', 'a036e00000z60C7AAI', 'a036e00000zUa21AAC', 'a036e00000rJXoRAAW', 'a036e00000rJXoZAAW', 'a036e00000zUNNeAAO', 'a036e00000rIQoXAAW', 'a036e00000z34EGAAY', 'a036e00000z4elpAAA', 'a036e00000z34NpAAI', 'a036e00000rJdi5AAC', 'a036e00000z4AWBAA2', 'a036e00000rJmUCAA0', 'a036e00000rJY71AAG', 'a036e00000rJY7FAAW', 'a036e00000rJY7KAAW', 'a036e00000rJj1nAAC', 'a036e00000z6N2AAAU', 'a036e00000zUNTqAAO', 'a036e00000rJXotAAG', 'a036e00000rJXp1AAG', 'a036e00000rJdhCAAS', 'a036e00000rJdjcAAC', 'a036e00000z3448AAA', 'a036e00000z344CAAQ', 'a036e00000z5znDAAQ', 'a036e00000zUtOFAA0', 'a036e00000zUtMkAAK', 'a036e00000rHnawAAC', 'a036e00000rJY4mAAG', 'a036e00000zSc5RAAS', 'a036e00000z35BMAAY', 'a036e00000zScEMAA0', 'a036e00000zVcrIAAS', 'a036e00000zSbURAA0', 'a036e00000z63JNAAY', 'a036e00000z4elbAAA', 'a036e00000z5zqTAAQ', 'a036e00000rK2JtAAK', 'a036e00000z35CpAAI', 'a036e00000z35CtAAI', 'a036e00000zUqTZAA0', 'a036e00000z4AS6AAM', 'a036e00000z4egfAAA', 'a036e00000z4n9FAAQ', 'a036e00000z4n9IAAQ', 'a036e00000zUuo3AAC', 'a036e00000rJXqcAAG', 'a036e00000zUup5AAC', 'a036e00000rIUd4AAG', 'a036e00000rIUbiAAG', 'a036e00000z5w79AAA', 'a036e00000zVhUHAA0', 'a036e00000zSjSfAAK', 'a036e00000z6034AAA', 'a036e00000z4nU4AAI', 'a036e00000z4nUAAAY', 'a036e00000z605LAAQ', 'a036e00000z6MzdAAE', 'a036e00000zUNNlAAO', 'a036e00000zUa22AAC', 'a036e00000rJXqvAAG', 'a036e00000rJXrFAAW', 'a036e00000rJXrJAAW', 'a036e00000z5vtZAAQ', 'a036e00000z34TAAAY', 'a036e00000rJ0J9AAK', 'a036e00000rJXulAAG', 'a036e00000rJY3cAAG', 'a036e00000rJY4eAAG', 'a036e00000zUNSBAA4', 'a036e00000zUXCLAA4', 'a036e00000rJY7xAAG', 'a036e00000rJY7yAAG', 'a036e00000rJY7zAAG', 'a036e00000rJY82AAG', 'a036e00000rJXrcAAG', 'a036e00000rJdjNAAS', 'a036e00000rJY4aAAG', 'a036e00000rJjM2AAK', 'a036e00000rJjM7AAK', 'a036e00000z4nVYAAY', 'a036e00000zUaaBAAS', 'a036e00000z34ENAAY', 'a036e00000rJdmgAAC', 'a036e00000z34CXAAY', 'a036e00000rJXqAAAW', 'a036e00000rJXqbAAG', 'a036e00000zUsH6AAK', 'a036e00000z34CaAAI', 'a036e00000rJjMDAA0', 'a036e00000rJzGlAAK', 'a036e00000rK1aoAAC', 'a036e00000rK1zGAAS', 'a036e00000rK2aYAAS', 'a036e00000rK8yvAAC', 'a036e00000zUNTmAAO', 'a036e00000zVBVYAA4', 'a036e00000z34bcAAA', 'a036e00000z3GqCAAU', 'a036e00000z4mgKAAQ', 'a036e00000z4ex8AAA', 'a036e00000z4exBAAQ', 'a036e00000z4f07AAA', 'a036e00000zVvrEAAS', 'a036e00000z4nKdAAI', 'a036e00000z35ghAAA', 'a036e00000zViXRAA0', 'a036e00000z4fJ0AAI', 'a036e00000rHncdAAC', 'a036e00000rJYBrAAO', 'a036e00000z60HKAAY', 'a036e00000z63gRAAQ', 'a036e00000zVjNgAAK', 'a036e00000rJYBtAAO', 'a036e00000z60EtAAI', 'a036e00000rJYCaAAO', 'a036e00000rJYCeAAO', 'a036e00000rJYCoAAO', 'a036e00000z35iIAAQ', 'a036e00000z3EqNAAU', 'a036e00000z4eu0AAA', 'a036e00000z4euAAAQ', 'a036e00000z4euBAAQ', 'a036e00000z4euKAAQ', 'a036e00000zVvqNAAS', 'a036e00000z4euNAAQ', 'a036e00000z5zzNAAQ', 'a036e00000z6MxXAAU', 'a036e00000z6MwjAAE', 'a036e00000rI87JAAS', 'a036e00000rJYCzAAO', 'a036e00000rJYD7AAO', 'a036e00000rJYD9AAO', 'a036e00000rJYDBAA4', 'a036e00000z5vzTAAQ', 'a036e00000z6MvjAAE', 'a036e00000zUZr3AAG', 'a036e00000z600dAAA', 'a036e00000rIrwzAAC', 'a036e00000rJYDlAAO', 'a036e00000rJog6AAC', 'a036e00000z34lKAAQ', 'a036e00000rHUT7AAO', 'a036e00000z34lbAAA', 'a036e00000rIR2EAAW', 'a036e00000z63S6AAI', 'a036e00000rJmXqAAK', 'a036e00000rJY17AAG', 'a036e00000zUNRLAA4', 'a036e00000z35ZoAAI', 'a036e00000rJXyNAAW', 'a036e00000rJXz8AAG', 'a036e00000z34jWAAQ', 'a036e00000rJXz1AAG', 'a036e00000zVA8BAAW', 'a036e00000rJY1EAAW', 'a036e00000rJY1ZAAW', 'a036e00000rJY1nAAG', 'a036e00000rJY1sAAG', 'a036e00000rJY1tAAG', 'a036e00000z35baAAA', 'a036e00000z35bbAAA', 'a036e00000z3H5vAAE', 'a036e00000z34lfAAA', 'a036e00000rHHYGAA4', 'a036e00000rJedqAAC', 'a036e00000rJj7DAAS', 'a036e00000rJXyeAAG', 'a036e00000rJY2QAAW', 'a036e00000z34vOAAQ', 'a036e00000rJjQSAA0', 'a036e00000z4fGKAAY', 'a036e00000z4fGLAAY', 'a036e00000z4nafAAA', 'a036e00000z5wFbAAI', 'a036e00000rHndLAAS', 'a036e00000rJYEKAA4', 'a036e00000z35gLAAQ', 'a036e00000rJXxgAAG', 'a036e00000zSbpHAAS', 'a036e00000z34auAAA', 'a036e00000z5w2JAAQ', 'a036e00000rJXyrAAG', 'a036e00000rJXyyAAG', 'a036e00000rJkkhAAC', 'a036e00000z34vRAAQ', 'a036e00000rJypuAAC', 'a036e00000rJz88AAC', 'a036e00000z34tMAAQ', 'a036e000010bibgAAA', 'a036e000010bq6pAAA', 'a036e00000rHnclAAC', 'a036e00000rHncoAAC', 'a036e000010bvVAAAY', 'a036e000010c0dzAAA', 'a036e00000rHndKAAS', 'a036e00000rINSsAAO', 'a036e00000rJXytAAG', 'a036e00000rJXwAAAW', 'a036e00000rJXz4AAG', 'a036e00000rJXz5AAG', 'a036e00000z5zzOAAQ', 'a036e00000rJXz6AAG', 'a036e00000z5zzRAAQ', 'a036e00000z34u1AAA', 'a036e00000rJYEYAA4', 'a036e00000rIycKAAS', 'a036e00000z35iPAAQ', 'a036e00000zUa2xAAC', 'a036e00000rJXwCAAW', 'a036e00000zSbnjAAC', 'a036e00000rJXxfAAG', 'a036e00000rJXzPAAW', 'a036e00000rJXzSAAW', 'a036e00000rJXzVAAW', 'a036e00000rJzXcAAK', 'a036e00000rJXzWAAW', 'a036e00000rJXzXAAW', 'a036e00000z34vNAAQ', 'a036e00000rJYEJAA4', 'a036e00000z60H8AAI', 'a036e00000rJYEOAA4', 'a036e00000zTaKBAA0', 'a036e00000zUNQOAA4', 'a036e00000rJXwtAAG', 'a036e00000rJXxDAAW', 'a036e00000rJY27AAG', 'a036e00000rJXzxAAG', 'a036e00000rJytuAAC', 'a036e00000z3GsYAAU', 'a036e00000z4ezwAAA', 'a036e00000zVvr9AAC', 'a036e00000z4f03AAA', 'a036e00000z4f09AAA', 'a036e00000z4f0GAAQ', 'a036e00000z6MxUAAU', 'a036e00000zUNQRAA4', 'a036e00000z34lTAAQ', 'a036e00000z34lMAAQ', 'a036e00000zVA8JAAW', 'a036e00000zVO1FAAW', 'a036e00000zUa36AAC', 'a036e00000rJYF3AAO', 'a036e00000rJil4AAC', 'a036e00000z4eu9AAA', 'a036e00000zVFw9AAG', 'a036e00000rJYFkAAO', 'a036e00000rJYFvAAO', 'a036e00000rJjP3AAK', 'a036e00000rJjP5AAK', 'a036e00000rJjP9AAK', 'a036e00000z34akAAA', 'a036e00000z34alAAA', 'a036e00000zVBVBAA4', 'a036e00000z34avAAA', 'a036e00000z34axAAA', 'a036e00000zVFtoAAG', 'a036e00000zVFz4AAG', 'a036e00000rJzgvAAC', 'a036e00000rK8xCAAS', 'a036e00000z35gNAAQ', 'a036e00000zUa3RAAS', 'a036e000010cGM6AAM', 'a036e000010cGMFAA2', 'a036e00000zUvK3AAK', 'a036e00000z36QmAAI', 'a036e00000rH9HhAAK', 'a036e000010cGM8AAM', 'a036e000010cGMSAA2', 'a036e000010cGMUAA2', 'a036e00000zUNYVAA4', 'a036e00000rINc2AAG', 'a036e00000z36QxAAI', 'a036e00000rKX08AAG', 'a036e000010cGMcAAM', 'a036e000010cGMeAAM', 'a036e000010cGMgAAM', 'a036e000010cGNKAA2', 'a036e000010cGNVAA2', 'a036e000010cGNYAA2', 'a036e000010cGNfAAM', 'a036e000010ckzBAAQ', 'a036e00000z4mn8AAA', 'a036e00000z4mn9AAA', 'a036e00000zUvOJAA0', 'a036e00000z60cWAAQ', 'a036e00000z4nulAAA', 'a036e00000z4nunAAA', 'a036e000010cHooAAE', 'a036e000010cGX5AAM', 'a036e000010cGXpAAM', 'a036e000010cGY0AAM', 'a036e000010cGYaAAM', 'a036e000010cGYeAAM', 'a036e000010cGYiAAM', 'a036e000010cGYjAAM', 'a036e000010cHpKAAU', 'a036e00000z36BmAAI', 'a036e000010cGMJAA2', 'a036e00000z36BoAAI', 'a036e00000z36BkAAI', 'a036e00000z36C3AAI', 'a036e00000rH6bnAAC', 'a036e00000rH6rDAAS', 'a036e00000z36C5AAI', 'a036e00000z36C8AAI', 'a036e00000z60VaAAI', 'a036e00000rIMXQAA4', 'a036e00000z60VlAAI', 'a036e00000rJjSXAA0', 'a036e00000rJohJAAS', 'a036e00000rKFXPAA4', 'a036e00000z36ACAAY', 'a036e00000z36ALAAY', 'a036e00000z36ByAAI', 'a036e00000z3HKhAAM', 'a036e000010c4DwAAI', 'a036e00000z5ZImAAM', 'a036e000010cGM7AAM', 'a036e00000z5wSkAAI', 'a036e00000z5wSnAAI', 'a036e000010cGM3AAM', 'a036e00000rK3seAAC', 'a036e00000z35ywAAA', 'a036e00000zVkkTAAS', 'a036e00000zTaMSAA0', 'a036e000010c4LWAAY', 'a036e00000z4fPoAAI', 'a036e00000z4fMyAAI', 'a036e00000z4fMwAAI', 'a036e00000z4nibAAA', 'a036e00000z6N5QAAU', 'a036e00000zVkkSAAS', 'a036e000010c3ryAAA', 'a036e00000z5wMOAAY', 'a036e00000z60NQAAY', 'a036e00000z6N6sAAE', 'a036e00000z6N6uAAE', 'a036e00000zTOCjAAO', 'a036e00000zUab9AAC', 'a036e00000zVrTeAAK', 'a036e00000zVrTfAAK', 'a036e000010cDtMAAU', 'a036e000010cDtSAAU', 'a036e000010cDtaAAE', 'a036e000010cDtsAAE', 'a036e000010cDtuAAE', 'a036e000010cDusAAE', 'a036e000010cDutAAE', 'a036e00000z35qwAAA', 'a036e00000rJYGuAAO', 'a036e00000rJYGJAA4', 'a036e00000rJYGNAA4', 'a036e00000rJYGRAA4', 'a036e00000z5wKtAAI', 'a036e00000z5wKxAAI', 'a036e00000rJYGmAAO', 'a036e00000z5wKwAAI', 'a036e00000rKX0mAAG', 'a036e00000z60NLAAY', 'a036e000010cDtDAAU', 'a036e00000rJYHZAA4', 'a036e00000rJYHeAAO', 'a036e00000rJYHiAAO', 'a036e00000rJYHoAAO', 'a036e000010cEsOAAU', 'a036e00000rHEu5AAG', 'a036e00000z35z5AAA', 'a036e00000rHneSAAS', 'a036e00000rJYI9AAO', 'a036e00000rJjPvAAK', 'a036e00000rJz2vAAC', 'a036e00000rJz9GAAS', 'a036e00000z4fMzAAI', 'a036e00000rJYIoAAO', 'a036e000010cDukAAE', 'a036e00000z4fQ7AAI', 'a036e00000rJjQuAAK', 'a036e00000rJUlFAAW', 'a036e00000rJb7gAAC', 'a036e00000rH1wwAAC', 'a036e00000rH1zgAAC', 'a036e00000z363XAAQ', 'a036e00000z43r5AAA', 'a036e00000yuZxdAAE', 'a036e00000z4fVSAAY', 'a036e00000rHUTwAAO', 'a036e00000rHUTyAAO', 'a036e00000rHUU0AAO', 'a036e00000z4fVhAAI', 'a036e00000rJCuFAAW', 'a036e00000rJofmAAC', 'a036e00000rK1k9AAC', 'a036e00000rKX69AAG', 'a036e00000yuZxfAAE', 'a036e00000z363MAAQ', 'a036e00000z3HHzAAM', 'a036e00000z4meSAAQ', 'a036e00000z4meQAAQ', 'a036e000010cHYTAA2', 'a036e000010cGH2AAM', 'a036e000010cGH4AAM', 'a036e000010cGHGAA2', 'a036e000010cGHLAA2', 'a036e000010cGHoAAM', 'a036e000010cGHuAAM', 'a036e000010cGI3AAM', 'a036e00000zUvNUAA0', 'a036e00000rH7cbAAC', 'a036e00000rH7dHAAS', 'a036e00000rH7tSAAS', 'a036e00000z4fbbAAA', 'a036e00000z4fblAAA', 'a036e00000z4fbjAAA', 'a036e00000z4fbrAAA', 'a036e00000zSjYDAA0', 'a036e00000z60ZBAAY', 'a036e00000rJYPhAAO', 'a036e00000rJjSvAAK', 'a036e00000rJjSxAAK', 'a036e00000rJjT7AAK', 'a036e00000rJzq8AAC', 'a036e00000z36JqAAI', 'a036e000010cGRaAAM', 'a036e00000z36IIAAY', 'a036e00000z36ILAAY', 'a036e00000z36IMAAY', 'a036e00000z36KNAAY', 'a036e00000z36KRAAY', 'a036e00000z5wVbAAI', 'a036e00000z60Z8AAI', 'a036e00000zUoWUAA0', 'a036e00000z6N9yAAE', 'a036e00000zUNY0AAO', 'a036e00000z36WjAAI', 'a036e00000z4msXAAQ', 'a036e000010cGcWAAU', 'a036e000010cGS4AAM', 'a036e000010cGRiAAM', 'a036e000010cGRkAAM', 'a036e000010cGS0AAM', 'a036e000010cGSfAAM', 'a036e000010cGSgAAM', 'a036e000010cGSkAAM', 'a036e000010cGSnAAM', 'a036e000010cqudAAA')");
            }
            stringBuilder.Append(" AND ActivityDate__c >= " + sInitialDate + " AND ActivityDate__c <= " + sFinalDate);

            /*stringBuilder.Append(" AND (PlanId__r.Name LIKE '%SANITAS AIREPOC%' OR PlanId__r.Name LIKE '%VASCU%' OR PlanId__r.Name LIKE '%COOMEVA MP ASMAIRE%'");
            stringBuilder.Append(" OR GroupId__r.Name LIKE '%VALORACIÓN ANESTESIA%' OR GroupId__r.Name LIKE '%VALORACIÓN FBC%' OR PlanId__r.Name LIKE '%PROTOCOLO%'");
            stringBuilder.Append(" OR PlanId__r.Name LIKE '%SANITAS ASMAIRE%' OR PlanId__r.Name LIKE '%COOMEVA AIREPOC%' OR PlanId__r.Name LIKE '%ALIANSALUD OXIGENAR%'");
            stringBuilder.Append(" OR PlanId__r.Name LIKE '%COLMEDICA AIREPOC%' OR PlanId__r.Name LIKE '%FNC INDICE INTEGRACIÓN UCI%' OR PlanId__r.Name LIKE '%FNC SANITAS VMI%'");
            stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC VENTIL MECANICA INTERMITEN%' OR PlanId__r.Name LIKE '%FNC ECOPETROL ASMAIRE%' OR PlanId__r.Name LIKE '%FNC ECOPETROL AIREPOC%'");
            stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC COOMEVA MP AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES ASMAIRE%') AND AgendaId__r.Name <> 'SERVICIOS INVESTIGACIÓN'");
            stringBuilder.Append(" AND WhatId__r.DocumentNumber__c NOT IN ('INVEST', 'BLOQUEO')");  */
            try
            {
                productsByGroups = this.GetProductsInfo(productsByGroups, soapClient, sessionHeader);
                //rateByConceptByProducts = this.GetProductsByRate(soapClient, sessionHeader, rateByConceptByProducts);
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lAppointments = records.OfType<Appointment__c>().ToList();
                    foreach (var item in lAppointments)
                    {
                        productsTmp = productsByGroups.FindAll(x => x.GroupId__c == item.Grupo_por_Plan__r.Grupo__c && x.Grupo_por_Plan__r.Plan__r.RateId__c == item.PlanId__r.RateId__c && x.Tarifa_concepto_producto__r.CostCenterId__c == item.ScheduleId__r.FNC_CentroCostos__c);
                        servintePatient = new ServintePatient()
                        {
                            saddress = item.WhatId__r.Address__c,
                            sdocument = item.WhatId__r.DocumentNumber__c,
                            sdocumenttype = item.WhatId__r.DocumentType__c,
                            sfirstname = item.WhatId__r.FirstName_c__pc,
                            ssecondname = item.WhatId__r.SecondName__pc,
                            ssurname = item.WhatId__r.FirstSurname__pc,
                            ssecondsurname = item.WhatId__r.SecondSurname__pc,
                            dbirthdate = item.WhatId__r.PersonBirthdate.Value,
                            sagreementcode = item.PlanId__r.AgreementId__r.Code__c,
                            scellphone = item.WhatId__r.Phone,
                            smail = item.WhatId__r.PersonEmail,
                            scovid1 = "N",
                            scovid2 = "N",
                            sgender = (!string.IsNullOrEmpty(item.WhatId__r.Gender__pc)) ? item.WhatId__r.Gender__pc.Substring(0, 1) : "M",
                            sjob = (!string.IsNullOrEmpty(item.WhatId__r.Ocupation__pc)) ? item.WhatId__r.Ocupation__pc : "5169",
                            sbornplace = "169",
                            sneighborhood = "43",
                            lappointments = new List<InspiraCita>(),
                            scity = "11001",
                            scityname = "BOGOTA D.C.",
                            snation = "169",
                            surbanzone = "U",
                            smaritalstatus = "S",
                            sphone = item.WhatId__r.Phone,
                            safiliation = "1",
                            slevel = "6",
                            idPaciente = item.WhatId__c,
                            sissuingentity = "REGISTRADURIA NACIONAL DEL ESTADO CIVIL",
                            ssourcecountry = "169",
                        };
                        var appointment = new InspiraCita()
                        {
                            sagreement = item.PlanId__r.AgreementId__r.Code__c,
                            sagreementname = item.PlanId__r.AgreementId__r.Name,
                            sagreementtype = "E",
                            sappointment = item.Id,
                            scie10 = Tools.GetDiagnosis(item.PlanId__r.HealthCarePlanId__r.Name),
                            splan = item.PlanId__r.HealthCarePlanId__r.Code__c,
                            srate = item.PlanId__r.RateId__r.Code__c,
                            sratename = item.PlanId__r.RateId__r.Name,
                            sservicegroup = item.GroupId__r.Name,
                            sunit = (item.WhatId__r.Age2__pc >= 18) ? "1100" : "1200",
                            sattentiontype = "2",
                            sservicetype = "28",
                            ddate = item.ActivityDate__c.Value,
                            lservices = new List<ServiceRequest>(),
                            sthird = item.AgendaId__r.ProfessionalId__r.DocumentNumber__c,
                            scostcenter = item.PlanId__r.HealthCarePlanId__r.Name,
                        };
                        foreach (ProductsByGroup__c product in productsTmp)
                        {
                            var services = new ServiceRequest()
                            {
                                scostcenter = product.Tarifa_concepto_producto__r.CostCenterId__r.Code__c,
                                sservice = product.Tarifa_concepto_producto__r.ProductId__r.Name,
                                iqty = 1,
                                srate = item.PlanId__r.RateId__r.Code__c,
                                sservicename = product.Tarifa_concepto_producto__r.ProductId__r.Name__c,
                                bbilleable = true,
                                bisprocedure = !product.ProductId__r.Name__c.Contains("CONSULTA"),
                                sconcept = product.Tarifa_concepto_producto__r.ConceptId__r.Code__c,
                                idiscount = 0,
                                ivalue = Convert.ToDecimal(product.Tarifa_concepto_producto__r.Value__c)
                            };
                            sunit = (item.WhatId__r.Age2__pc >= 18) ? "1" : "3";
                            appointment.sunit = sunit;
                            appointment.lservices.Add(services);
                        }
                        servintePatient.lappointments.Add(appointment);
                        lservintePatients.Add(servintePatient);
                    }
                }
                return lservintePatients;
            }
            catch (Exception ex)
            {

                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }

        }

        #endregion

        #region Métodos para conexión con la página de la FNC

        public string GetPatientAppointments(int idays, string sdocument, string sdocumenttype)
        {
            StringBuilder stringBuilder = new StringBuilder();
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            List<Appointment__c> lAppointments = new List<Appointment__c>();
            StringBuilder squery = new StringBuilder("SELECT Id, Name, GroupId__r.Name, ActivityDate__c, AppointmentValue__c, AgendaId__r.ProfessionalId__r.Name FROM Appointment__c WHERE AppointmentValue__c > 0 AND FNC_MainAppointment__c = true AND PaymentCode__c = '' AND ActivityDate__c >= ");
            squery.Append(DateTime.Now.AddDays(idays).ToString("yyyy-MM-dd"));
            squery.Append(" AND ActivityDate__c <=");
            squery.Append(DateTime.Now.AddDays(idays).ToString("yyyy-MM-dd"));
            squery.Append(" AND WhatId__r.DocumentNumber__c = '");
            squery.Append(sdocument);
            squery.Append("' AND WhatId__r.DocumentType__c = '");
            squery.Append(sdocumenttype);
            squery.Append("'");
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, squery.ToString(), out queryResult);
            if (queryResult.size > 0)
            {
                sObject[] records = queryResult.records;
                lAppointments = records.OfType<Appointment__c>().ToList();
                foreach (var item in lAppointments)
                {
                    stringBuilder.Append("<cita>");
                    stringBuilder.Append("<idcita>");
                    stringBuilder.Append(item.Id);
                    stringBuilder.Append("</idcita>");
                    stringBuilder.Append("<nombrecita>");
                    stringBuilder.Append(item.Name);
                    stringBuilder.Append("</nombrecita>");
                    stringBuilder.Append("<tipocita>");
                    stringBuilder.Append(item.GroupId__r.Name);
                    stringBuilder.Append("</tipocita>");
                    stringBuilder.Append("<valorcita>");
                    stringBuilder.Append(item.AppointmentValue__c.ToString());
                    stringBuilder.Append("</valorcita>");
                    stringBuilder.Append("<fechacita>");
                    stringBuilder.Append(item.ActivityDate__c.Value.ToString("dd/MM/yyyy"));
                    stringBuilder.Append("</fechacita>");
                    stringBuilder.Append("<profesional>");
                    stringBuilder.Append(item.AgendaId__r.ProfessionalId__r.Name);
                    stringBuilder.Append("</profesional>");
                    stringBuilder.Append("</cita>");
                }
            }
            else
            {
                throw new ApplicationException("No se han encontrado citas para el paciente");
            }
            return stringBuilder.ToString();
        }

        public string UpdatePaymentAppointment(string scode, string sidappointment, string svalue)
        {
            SoapClient soapClient = new SoapClient();
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SaveResult[] results = null;
            LimitInfo[] limitInfos = null;
            Appointment__c appointment = null;
            sObject[] sObjects = null;
            try
            {
                appointment = new Appointment__c()
                {
                    Id = sidappointment,
                    PaymentCode__c = scode,
                    PatientAttended__cSpecified = true,
                    PatientAttended__c = true,
                    ServiceBilled__cSpecified = true,
                    ServiceBilled__c = true,
                    Status__c = "Facturada",
                };
                soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
                sObjects = new sObject[] { appointment };
                soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, null, sObjects, out limitInfos, out results);
                if (!results[0].success)
                {
                    throw new ApplicationException("Cita: " + sidappointment + " Error: " + results[0].errors[0].message);
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                LogError.WriteError("WordPress", "Aplicacion", ex);
                return "Ha ocurrido un error al actualizar la cita";
            }
        }

        #endregion

        #region Métodos que descargan la información de Inspira para Oracle

        /// <summary>
        /// Método para obtener las citas
        /// </summary>
        /// <returns>String con todas la información de las citas en líneas separadas por ;</returns>
        public string GetAppointments()
        {
            List<Appointment__c> lAppointments = new List<Appointment__c>();
            //string[] scolumns = null;
            string[] scolumns = new string[] { "ID", "NAME", "ACTIVITYDATE__C", "ACTIVITYDATETIME__C", "ENDDATETIME__C", "APPOINTMENTOPPORTUNITY__C",
                                                "APPOINTMENTOPPORTUNITYDESIRED__C", "ASSIGNEDBYU__RNAME", "ASSIGNDATE__C", "STATUS__C", "WHATID__C", "PHONES__C",
                                                "AGENDAID__RNAME", "CATEGORY__C", "GROUPID__C", "AGREEMENTID__RNAME", "AGREEMENTCODE__C", "PLANNAME__C", "PLANID__RCODE__C",
                                                "USERWHOCANCELED__C", "CANCELEDDATE__C", "CANCELLATIONREASON__C", "PATIENTDATE__C", "PATIENTATTENDED__C",
                                                "SERVICEBILLED__C", "BILLINGTIME__C", "SERVICETIME__C", "TOTALATTENTIONTIME__C", "COMPLIANCETIME__C", "ERP_ENTRYCODE__C", "MIPRES__C",
                                                "TIPOAGENDAMIENTO__C", "TURNNUMBER__C", "LASTMODIFIEDDATE", "NUMEROENTREGA__C", "COSTOCENTER__C", "Users_who_billed__c", "GroupId__r.Name", "CASHIER__C"
                                                };
            //sappointments.AppendLine(string.Join(";", scolumns));
            string suser = string.Empty;
            string scostcenter = string.Empty;
            string scashier = string.Empty;
            try
            {
                lAppointments = this.GetAppointmentsData(lAppointments);
                List<Appointment__c> ltmp = new List<Appointment__c> ();
                ltmp = this.GetAppointmentsData(ltmp, true);
                foreach (Appointment__c item in ltmp)
                {
                    if (lAppointments.FirstOrDefault(x => x.Id == item.Id) == null)
                    {
                        lAppointments.Add(item);
                    }
                }
                return this.ProcessAppointments(lAppointments);
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                return string.Empty;
            }
            finally
            {
                scolumns = null;
                lAppointments = null;
            }
        }

        private string ProcessAppointments(List<Appointment__c> lAppointments)
        {
            StringBuilder sappointments = new StringBuilder();
            string suser = string.Empty;
            string scostcenter = string.Empty;
            string scashier = string.Empty;
            string[] scolumns = null;
            foreach (var item in lAppointments)
            {
                suser = (item.AssignedByU__r != null) ? item.AssignedByU__r.Name : string.Empty;
                scashier = (!string.IsNullOrEmpty(item.Cashier__c)) ? item.Cashier__c : string.Empty;
                if (item.PlanId__r != null)
                {
                    if (item.CostCenterId__r != null)
                    {
                        scostcenter = item.CostCenterId__r.Code__c;
                    }
                    else if (item.ScheduleId__r != null)
                    {
                        if (item.ScheduleId__r.FNC_CentroCostos__r != null)
                        {
                            scostcenter = item.ScheduleId__r.FNC_CentroCostos__r.Code__c;
                        }
                    }
                    else if (item.GroupId__r.CostCenterId__r != null)
                    {
                        scostcenter = item.GroupId__r.CostCenterId__r.Code__c;
                    }
                    else if (item.AgendaId__r != null)
                    {
                        scostcenter = item.AgendaId__r.CostCenterId__r.Code__c;
                    }
                    scolumns = new string[]
                    {
                            item.Id,
                            item.Name,
                            item.ActivityDate__c.HasValue ? item.ActivityDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                            item.ActivityDatetime__c.HasValue ? item.ActivityDatetime__c.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                            item.EndDatetime__c.HasValue ? item.EndDatetime__c.Value.ToString("yyyy-MM-dd HH:mm:ss") : string.Empty,
                            item.AppointmentOpportunity__c.ToString(),
                            item.AppointmentOpportunitydesired__c,
                            suser,
                            item.AssignDate__c.HasValue ? item.AssignDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                            item.Status__c,
                            item.WhatId__c,
                            item.Phones__c,
                            item.AgendaId__r.Name,
                            //item.AgendaId__r.ProfessionalId__r.DocumentNumber__c,
                            (!string.IsNullOrEmpty(item.Category__c)) ? item.Category__c.Replace('"', ' ') : string.Empty,
                            item.GroupId__c,
                            item.AgreementId__r.Name,
                            item.AgreementCode__c,
                            item.PlanName__c,
                            item.PlanId__r.HealthCarePlanId__r.Code__c,
                            item.UserWhoCanceled__c,
                            item.CanceledDate__c.HasValue ? item.CanceledDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                            item.CancellationReason__c,
                            item.PatientDate__c.HasValue ? item.PatientDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                            item.PatientAttended__c.ToString(),
                            item.ServiceBilled__c.ToString(),
                            item.BillingTime__c.ToString(),
                            item.ServiceTime__c.ToString(),
                            item.TotalAttentionTime__c.ToString(),
                            item.ComplianceTime__c.ToString(),
                            item.Erp_EntryCode__c,
                            item.Mipres__c,
                            Tools.ReplaceChars(item.TipoAgendamiento__c),
                            Tools.ReplaceChars(item.TurnNumber__c),
                            item.LastModifiedDate.Value.AddHours(-5).ToString("yyyy-MM-dd"),
                            item.NumeroEntrega__c,
                            scostcenter,
                            item.PlanId__c,
                            (item.ins2_categoria__r != null) ? item.ins2_categoria__r.Name : string.Empty,
                            (item.ScheduleId__r != null) ? item.ScheduleId__r.Name : string.Empty,
                            (item.ScheduleId__r != null) ? (item.ScheduleId__r.FNC_CentroCostos__r != null) ? Tools.ReplaceChars(item.ScheduleId__r.FNC_CentroCostos__r.Code__c) : string.Empty : string.Empty,
                            item.Users_who_billed__c,
                            Tools.ReplaceChars(item.GroupId__r.Name),
                            Tools.ReplaceChars(scashier),
                            item.FNC_RequierePreconsulta__c.Value.ToString(),
                            item.RescheduleCheck__c.Value.ToString(),
                            item.AgendaId__r.GrupoEspecialidad__c,
                    };
                    sappointments.AppendLine(string.Join(";", scolumns));
                }               

            }
            return sappointments.ToString();
        }
        
        private List<Appointment__c> GetAppointmentsData(List<Appointment__c> lAppointments, bool bisfuture = false)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            bool bdone = false;
            try
            {
                if (!bisfuture)
                {
                    stringBuilder.Append("SELECT ID, SCHEDULEID__R.NAME,  PLANID__C, GROUPID__R.COSTCENTERID__R.CODE__C, ins2_categoria__r.Name, NAME, AGENDAID__R.COSTCENTERID__R.CODE__C, ACTIVITYDATE__C" +
                    ", ACTIVITYDATETIME__C, ENDDATETIME__C, APPOINTMENTOPPORTUNITY__C, APPOINTMENTOPPORTUNITYDESIRED__C, ASSIGNEDBYU__R.NAME,  ASSIGNDATE__C, STATUS__C, WHATID__C, PHONES__C, AGENDAID__R.NAME" +
                    ", AGENDAID__R.PROFESSIONALID__R.DOCUMENTNUMBER__C, CATEGORY__C, GROUPID__C, AGREEMENTID__R.NAME, AGREEMENTCODE__C, PLANNAME__C, PLANID__R.HEALTHCAREPLANID__R.CODE__C" +
                    ", USERWHOCANCELED__C, CANCELEDDATE__C, CANCELLATIONREASON__C, CANCELLATIONSOURCE__C, PATIENTDATE__C, PATIENTATTENDED__C, SERVICEBILLED__C, BILLINGTIME__C, SERVICETIME__C, TOTALATTENTIONTIME__C" +
                    ", COMPLIANCETIME__C, ERP_ENTRYCODE__C,  MIPRES__C, TIPOAGENDAMIENTO__C, TURNNUMBER__C, LASTMODIFIEDDATE, NUMEROENTREGA__C" +
                    ", ScheduleId__r.FNC_CentroCostos__r.Code__c, SCHEDULEID__C, COSTCENTERID__C, COSTCENTERID__R.CODE__C, USERS_WHO_BILLED__C, GROUPID__R.NAME, CASHIER__C, FNC_RequierePreconsulta__c, RescheduleCheck__c, AgendaId__r.GrupoEspecialidad__c" +
                    //" FROM APPOINTMENT__C WHERE ACTIVITYDATE__C = LAST_N_DAYS:120" +
                    " FROM APPOINTMENT__C WHERE ACTIVITYDATE__C >= 2026-01-01 AND ACTIVITYDATE__C <= 2026-05-18" +
                    //" FROM APPOINTMENT__C WHERE ACTIVITYDATE__C >= 2025-01-01 AND ACTIVITYDATE__C <= 2025-04-30" +
                    //", ScheduleId__r.FNC_CentroCostos__r.Code__c, SCHEDULEID__C, COSTCENTERID__C, COSTCENTERID__R.CODE__C, USERS_WHO_BILLED__C, GROUPID__R.NAME, CASHIER__C, FNC_RequierePreconsulta__c, RescheduleCheck__c FROM APPOINTMENT__C WHERE ACTIVITYDATE__C = LAST_N_MONTHS:2 " +                    
                    //", ScheduleId__r.FNC_CentroCostos__r.Code__c, SCHEDULEID__C, COSTCENTERID__C, COSTCENTERID__R.CODE__C, USERS_WHO_BILLED__C, GROUPID__R.NAME, CASHIER__C, FNC_RequierePreconsulta__c, RescheduleCheck__c FROM APPOINTMENT__C WHERE ACTIVITYDATE__C >= 2024-11-01 AND ACTIVITYDATE__C <= 2025-03-31 " +
                    " AND WHATID__C NOT IN ('0011N00001LO9TWQAA', '0011N00001HKCQTQAC', '001O000000G2MTM', '001O000000FPBKT', '001O000000IMBLYAAG', '003O000000L2TYVAAE', '003O000000L2TYV', '0011N00001HKCQT', '0031N00001HQS9H'" +
                    ", '003O0000010MPV0', '001O000000VRK5C', '0011N00001A16SW', '0013I00000MHOVEAAG', '0012G00001DRK7MQAG', '0013I00000C0GRXAAA', '0013I00000C0J37AAA', '0013I00000FHT32AAE', '0013I00000FHTWNAA2'" +
                    ", '0013I00000FHU0NAAU', '0013I00000FHU8KAAU', '0013I00000FHUVTAA2', '0013I00000FHUATAAU', '0013I00000FHVHBAA2', '0013I00000FHVTPAA2', '0013I00000FHVHWAAU', '0016E00002BGD17AAG', '0016E00002EI2GIAAY'" +
                    ", '0016E00002EIY5QAAE', '0016E00002EIJDEAAI', '0016E00002EJ11EAAQ', '0016E00002BGAF9AAG', '0016E00002EILMTAAA', '0016E00002EIOEKAAQ', '0016E00002EIATGAAQ', '0016E00002EJ6OEAAA', '0016E00002RB5BDAAI'" +
                    ", '0016E00002BGOWOAAW', '0013I00000C0JYYAA2', '0013I00000C0KCPAAU', '0013I00000C0LU0AAM', '0013I00000C0LZCAAU', '0013I00000C0MIKAAM', '0013I00000C0MQLAAE', '0013I00000C0HSZAAQ', '0013I00000C0JT8AAI'" +
                    ", '0013I00002BB91SAAT', '0011N00001HKCQTQAC', '0012G00001DROXLQAG', '001O000000MGYVAAAY', '0011N00001LO9TWQAA', '001o000000fPbKtAAK', '0013i00000c0grxAAA', '0013i00000c0j37AAA', '0016e00002ej11EAAQ'" +
                    ", '0016e00002bGAf9AAG', '0016e00002bGD17AAG', '001o000000iMBlyAAG', '0011N00001hKcqtQAC', '0012G00001dRK7mQAG', '0013i00000mHOveAAG', '0013i00002BB91SAAT') AND ISDELETED = FALSE" +
                    " AND STATUS__C NOT IN ('LIBRE', 'CERRADO', 'RESERVADA', 'POR TRANSFERIR') AND WHATID__C <> '' AND FNC_MainAppointment__c = true ORDER BY ACTIVITYDATE__C DESC");

                }
                else
                {
                    stringBuilder.Append("SELECT ID, SCHEDULEID__R.NAME,  PLANID__C, GROUPID__R.COSTCENTERID__R.CODE__C, ins2_categoria__r.Name, NAME, AGENDAID__R.COSTCENTERID__R.CODE__C, ACTIVITYDATE__C" +
                    ", ACTIVITYDATETIME__C, ENDDATETIME__C, APPOINTMENTOPPORTUNITY__C, APPOINTMENTOPPORTUNITYDESIRED__C, ASSIGNEDBYU__R.NAME,  ASSIGNDATE__C, STATUS__C, WHATID__C, PHONES__C, AGENDAID__R.NAME" +
                    ", AGENDAID__R.PROFESSIONALID__R.DOCUMENTNUMBER__C, CATEGORY__C, GROUPID__C, AGREEMENTID__R.NAME, AGREEMENTCODE__C, PLANNAME__C, PLANID__R.HEALTHCAREPLANID__R.CODE__C" +
                    ", USERWHOCANCELED__C, CANCELEDDATE__C, CANCELLATIONREASON__C, CANCELLATIONSOURCE__C, PATIENTDATE__C, PATIENTATTENDED__C, SERVICEBILLED__C, BILLINGTIME__C, SERVICETIME__C, TOTALATTENTIONTIME__C" +
                    ", COMPLIANCETIME__C, ERP_ENTRYCODE__C,  MIPRES__C, TIPOAGENDAMIENTO__C, TURNNUMBER__C, LASTMODIFIEDDATE, NUMEROENTREGA__C" +
                    ", ScheduleId__r.FNC_CentroCostos__r.Code__c, SCHEDULEID__C, COSTCENTERID__C, COSTCENTERID__R.CODE__C, USERS_WHO_BILLED__C, GROUPID__R.NAME, CASHIER__C, FNC_RequierePreconsulta__c, RescheduleCheck__c, AgendaId__r.GrupoEspecialidad__c" +
                    " FROM APPOINTMENT__C WHERE ActivityDate__c > TODAY AND ActivityDate__c <= NEXT_N_DAYS:90" +
                    //", ScheduleId__r.FNC_CentroCostos__r.Code__c, SCHEDULEID__C, COSTCENTERID__C, COSTCENTERID__R.CODE__C, USERS_WHO_BILLED__C, GROUPID__R.NAME, CASHIER__C, FNC_RequierePreconsulta__c FROM APPOINTMENT__C WHERE ACTIVITYDATE__C = LAST_N_MONTHS:2 " +                    
                    //", ScheduleId__r.FNC_CentroCostos__r.Code__c, SCHEDULEID__C, COSTCENTERID__C, COSTCENTERID__R.CODE__C, USERS_WHO_BILLED__C, GROUPID__R.NAME, CASHIER__C, FNC_RequierePreconsulta__c FROM APPOINTMENT__C WHERE ACTIVITYDATE__C >= 2024-01-01 AND ACTIVITYDATE__C <= 2024-08-31 " +
                    " AND WHATID__C NOT IN ('0011N00001LO9TWQAA', '0011N00001HKCQTQAC', '001O000000G2MTM', '001O000000FPBKT', '001O000000IMBLYAAG', '003O000000L2TYVAAE', '003O000000L2TYV', '0011N00001HKCQT', '0031N00001HQS9H'" +
                    ", '003O0000010MPV0', '001O000000VRK5C', '0011N00001A16SW', '0013I00000MHOVEAAG', '0012G00001DRK7MQAG', '0013I00000C0GRXAAA', '0013I00000C0J37AAA', '0013I00000FHT32AAE', '0013I00000FHTWNAA2'" +
                    ", '0013I00000FHU0NAAU', '0013I00000FHU8KAAU', '0013I00000FHUVTAA2', '0013I00000FHUATAAU', '0013I00000FHVHBAA2', '0013I00000FHVTPAA2', '0013I00000FHVHWAAU', '0016E00002BGD17AAG', '0016E00002EI2GIAAY'" +
                    ", '0016E00002EIY5QAAE', '0016E00002EIJDEAAI', '0016E00002EJ11EAAQ', '0016E00002BGAF9AAG', '0016E00002EILMTAAA', '0016E00002EIOEKAAQ', '0016E00002EIATGAAQ', '0016E00002EJ6OEAAA', '0016E00002RB5BDAAI'" +
                    ", '0016E00002BGOWOAAW', '0013I00000C0JYYAA2', '0013I00000C0KCPAAU', '0013I00000C0LU0AAM', '0013I00000C0LZCAAU', '0013I00000C0MIKAAM', '0013I00000C0MQLAAE', '0013I00000C0HSZAAQ', '0013I00000C0JT8AAI'" +
                    ", '0013I00002BB91SAAT', '0011N00001HKCQTQAC', '0012G00001DROXLQAG', '001O000000MGYVAAAY', '0011N00001LO9TWQAA', '001o000000fPbKtAAK', '0013i00000c0grxAAA', '0013i00000c0j37AAA', '0016e00002ej11EAAQ'" +
                    ", '0016e00002bGAf9AAG', '0016e00002bGD17AAG', '001o000000iMBlyAAG', '0011N00001hKcqtQAC', '0012G00001dRK7mQAG', '0013i00000mHOveAAG', '0013i00002BB91SAAT') AND ISDELETED = FALSE" +
                    " AND STATUS__C NOT IN ('CERRADO', 'RESERVADA', 'POR TRANSFERIR') AND WHATID__C <> '' AND FNC_MainAppointment__c = true ORDER BY ACTIVITYDATE__C DESC");
                }
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lAppointments.AddRange(records.OfType<Appointment__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lAppointments;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<Programa_por_Paciente__c> GetProgramsData(string sid)
        {
            List<Programa_por_Paciente__c> lprograma = new List<Programa_por_Paciente__c>();
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            //programa.FNC_Programa__r.Name
            StringBuilder stringBuilder = new StringBuilder("SELECT FNC_Paciente__c, FNC_Programa__r.Name FROM Programa_por_Paciente__c WHERE FNC_Paciente__c IN (");
            stringBuilder.Append(sid);
            stringBuilder.Append(")");
            string squery = "SELECT FNC_Paciente__c, FNC_Programa__r.Name, FNC_Grupo_Asmaire__c, FNC_Estado__c FROM Programa_por_Paciente__c";
            bool bdone = false;
            try
            {
                //soapClient.query(sessionHeader, queryOptions, mruHeader, null, stringBuilder.ToString(), out queryResult);
                soapClient.query(sessionHeader, queryOptions, mruHeader, null, squery, out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lprograma.AddRange(records.OfType<Programa_por_Paciente__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lprograma;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                return new List<Programa_por_Paciente__c>();
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                mruHeader = null;
            }
        }

        private string[] GetAccountIds(List<Account> laccounts)
        {
            List<string> lid = new List<string>();
            foreach (var item in laccounts)
            {
                lid.Add($"'{item.Id}'");
            }
            return lid.ToArray();
        }

        public string GetAccounts()
        {
            List<Account> laccounts = new List<Account>();
            StringBuilder saccounts = new StringBuilder();
            List<Programa_por_Paciente__c> lprogramas = new List<Programa_por_Paciente__c>();
            List<Programa_por_Paciente__c> ltmp = null;
            string svascular = string.Empty, soncologia = string.Empty, sairepoc = string.Empty, sasmaire = string.Empty, sexfumaire = string.Empty, srexpira = string.Empty;
            string sgrupoasmaire = string.Empty, sestado = string.Empty;
            string[] sresult = null;
            string[] scolumns = new string[] { "ID_ACCOUNT", "NAME", "FIRSTNAME_C__PC", "SECONDNAME__PC", "FIRSTSURNAME__PC", "SECONDSURNAME__PC", "DOCUMENTNUMBER__C", "DOCUMENTTYPE__C",
                                                "AGE__PC", "AGE2__PC", "PERSONMOBILEPHONE", "PERSONHOMEPHONE", "PERSONBIRTHDATE", "ADDRESS__C", "GENDER__PC", "PROGRAMAS_ESPECIALES__C",
                                                "PERSONEMAIL", "FECHAADICION"
                                                };
            //saccounts.AppendLine(string.Join(";", scolumns));
            try
            {
                laccounts = this.GetAccountsData(laccounts);
                sresult = this.GetAccountIds(laccounts);
                lprogramas = this.GetProgramsData(string.Empty);
                /*int totalItems = sresult.Length;
                int blockSize = 1000;                 
                int startIndex = 0; 
                while (startIndex < totalItems)
                {
                    int currentBlockSize = Math.Min(blockSize, totalItems - startIndex);
                    string[] asearch = new string[currentBlockSize];
                    Array.Copy(sresult, startIndex, asearch, 0, currentBlockSize);
                    string currentNames = String.Join(",", asearch);
                    lprogramas.AddRange(this.GetProgramsData(currentNames));
                    startIndex += blockSize;
                }*/
                foreach (var item in laccounts)
                {
                    svascular = soncologia = sairepoc = sasmaire = sexfumaire = srexpira = "0";
                    ltmp = lprogramas.FindAll(x => x.FNC_Paciente__c == item.Id);
                    if (ltmp != null)
                    {
                        foreach (Programa_por_Paciente__c programa in ltmp)
                        {
                            if (programa.FNC_Programa__r.Name == "VASCULAR PULMONAR")
                            {
                                svascular = "1";
                            }
                            else if (programa.FNC_Programa__r.Name == "AIREPOC")
                            {
                                sairepoc = "1";
                            }
                            else if (programa.FNC_Programa__r.Name == "NEUMOLOGÍA ONCOLÓGICA")
                            {
                                soncologia = "1";
                            }
                            else if (programa.FNC_Programa__r.Name == "EXFUMAIRE")
                            {
                                sexfumaire = "1";
                            }
                            else if (programa.FNC_Programa__r.Name == "REXPIRA")
                            {
                                srexpira = "1";
                            }
                            else if (programa.FNC_Programa__r.Name == "ASMAIRE")
                            {
                                sasmaire = "1";
                            }
                            sgrupoasmaire = programa.FNC_Grupo_Asmaire__c;
                            sestado = programa.FNC_Estado__c;
                        }
                    }
                    scolumns = new string[]
                    {
                        item.Id,
                        Tools.ReplaceChars(item.Name),
                        Tools.ReplaceChars(item.FirstName_c__pc),
                        Tools.ReplaceChars(item.SecondName__pc),
                        Tools.ReplaceChars(item.FirstSurname__pc),
                        Tools.ReplaceChars(item.SecondSurname__pc),
                        item.DocumentNumber__c,
                        item.DocumentType__c,
                        item.Age__pc,
                        item.Age2__pc.ToString(),
                        Tools.ReplaceChars(item.PersonMobilePhone),
                        Tools.ReplaceChars(item.PersonHomePhone),
                        item.PersonBirthdate.HasValue ? item.PersonBirthdate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        Tools.ReplaceChars(item.Address__c),
                        item.Gender__pc,
                        item.Programas_Especiales__c,
                        Tools.ReplaceChars(item.PersonEmail),
                        //DateTime.Today.ToString("yyyy-MM-dd"),
                        item.Programas_DateIn__c.HasValue ? item.Programas_DateIn__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        item.Programas_DateOut__c.HasValue ? item.Programas_DateOut__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        string.Empty,
                        sairepoc,
                        sasmaire,
                        sexfumaire,
                        soncologia,
                        svascular,
                        sgrupoasmaire,
                        sestado
                    };
                    saccounts.Append($"{string.Join(";", scolumns)}\n");
                    //saccounts.AppendLine(string.Join(";", scolumns));
                }
                return saccounts.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<Account> GetAccountsData(List<Account> laccounts)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            bool bdone = false;
            stringBuilder.Append("SELECT ID, NAME, FirstName_c__pc, SecondName__pc, FirstSurname__pc, SecondSurname__pc, DocumentNumber__c, DocumentType__c,  Age__pc, Age2__pc, PersonMobilePhone" +
                ", PersonHomePhone, PersonBirthdate, Address__c, Gender__pc, Programas_Especiales__c, PersonEmail, Programas_DateIn__c, Programas_DateOut__c FROM Account" +
                " WHERE LastModifiedDate = LAST_N_DAYS:60 AND RecordTypeId = '012o0000000p4VKAAY' ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        laccounts.AddRange(records.OfType<Account>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return laccounts;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        /*private List<ProgramaPaciente__c> GetProgramPatientData(SoapClient soapClient, QueryOptions queryOptions, SessionHeader sessionHeader, MruHeader mruHeader)
        {

        }
        */
        public string GetProductsByGroup()
        {
            List<ProductsByGroup__c> lproductsbygroup = new List<ProductsByGroup__c>();
            StringBuilder sproducts = new StringBuilder();
            string[] scolumns = new string[] { "ID_GROUP", "GROUPID__RNAME", "GROUPID__RCODE__C", "PRODUCTCODE__C", "PRODUCTID__RNAME__C", "PRODUCTID__RISNOPOS__C", "CENTRO_DE_COSTOS__RNAME", "CENTRO_DE_COSTOS__RCODE__C" };
            string soldproducts = string.Empty;
            //sproducts.AppendLine(string.Join(";", scolumns));
            try
            {
                lproductsbygroup = this.GetProductsByGroupData(lproductsbygroup);
                foreach (var item in lproductsbygroup)
                {
                    scolumns = new string[]
                    {
                        item.Grupo_por_Plan__r.Grupo__c,
                        Tools.ReplaceChars(item.Grupo_por_Plan__r.Grupo__r.Name),
                        item.Grupo_por_Plan__r.Grupo__r.Code__c,
                        Tools.ReplaceChars(item.Tarifa_concepto_producto__r.ProductId__r.Name),
                        item.Tarifa_concepto_producto__r.ProductId__r.Name__c,
                        item.Tarifa_concepto_producto__r.ProductId__r.IsNoPos__c.ToString(),
                        Tools.ReplaceChars(item.Tarifa_concepto_producto__r.CostCenterId__r.Name),
                        Tools.ReplaceChars(item.Tarifa_concepto_producto__r.CostCenterId__r.Code__c),
                        item.Grupo_por_Plan__r.Plan__c,
                        item.Tarifa_concepto_producto__r.ConceptId__r.FunctionalUnit__r.Code__c
                    };
                    sproducts.AppendLine(string.Join(";", scolumns));
                }
                soldproducts = this.GetProductsByGroup(true);
                sproducts.Append(soldproducts);
                return sproducts.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<ProductsByGroup__c> GetProductsByGroupData(List<ProductsByGroup__c> lproductsbygroup)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            bool bdone = false;
            stringBuilder.Append("SELECT Id, Grupo_por_Plan__r.Grupo__c, Grupo_por_Plan__r.Plan__c, Grupo_por_Plan__r.Grupo__r.Name, Grupo_por_Plan__r.Grupo__r.Code__c, " +
                   "Tarifa_concepto_producto__r.ProductId__r.Name, Tarifa_concepto_producto__r.ProductId__r.Name__c, Tarifa_concepto_producto__r.ProductId__r.IsNoPos__c, " +
                   "Tarifa_concepto_producto__r.CostCenterId__r.Name, Tarifa_concepto_producto__r.CostCenterId__r.Code__c, Tarifa_concepto_producto__c,  Tarifa_concepto_producto__r.ConceptId__r.FunctionalUnit__r.Code__c " +
                   "FROM ProductsByGroup__c WHERE Tarifa_concepto_producto__c <> '' AND Grupo_por_Plan__c <> ''");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lproductsbygroup.AddRange(records.OfType<ProductsByGroup__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lproductsbygroup;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetProductsByGroup(bool bflag)
        {
            List<ProductsByGroup__c> lproductsbygroup = new List<ProductsByGroup__c>();
            StringBuilder sproducts = new StringBuilder();
            string[] scolumns = new string[] { "ID_GROUP", "GROUPID__RNAME", "GROUPID__RCODE__C", "PRODUCTCODE__C", "PRODUCTID__RNAME__C", "PRODUCTID__RISNOPOS__C", "CENTRO_DE_COSTOS__RNAME", "CENTRO_DE_COSTOS__RCODE__C" };
            //sproducts.AppendLine(string.Join(";", scolumns));
            try
            {
                lproductsbygroup = this.GetProductsByGroupData(lproductsbygroup, true);
                foreach (var item in lproductsbygroup)
                {
                    scolumns = new string[]
                    {
                        item.GroupId__c,
                        Tools.ReplaceChars(item.GroupId__r.Name),
                        item.GroupId__r.Code__c,
                        Tools.ReplaceChars(item.ProductId__r.Name),
                        item.ProductId__r.Name__c,
                        item.ProductId__r.IsNoPos__c.ToString(),
                        Tools.ReplaceChars(item.Centro_de_Costos__r.Name),
                        Tools.ReplaceChars(item.Centro_de_Costos__r.Code__c),
                        "''",
                        "''"
                    };
                    sproducts.AppendLine(string.Join(";", scolumns));
                }
                return sproducts.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<ProductsByGroup__c> GetProductsByGroupData(List<ProductsByGroup__c> lproductsbygroup, bool bflag)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            stringBuilder.Append("SELECT Id, GroupId__c, GroupId__r.Name, GroupId__r.Code__c, ProductId__r.Name, ProductId__r.Name__c, ProductId__r.IsNoPos__c, Centro_de_Costos__r.Name, Centro_de_Costos__r.Code__c " +
                "FROM ProductsByGroup__c WHERE Tarifa_concepto_producto__c = '' AND Grupo_por_Plan__c = '' AND GroupId__c <> '' AND ProductId__c <> '' ORDER BY ID ASC");
            bool bdone = false;
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lproductsbygroup.AddRange(records.OfType<ProductsByGroup__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lproductsbygroup;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string   GetAuthUsage()
        {
            List<Use_autorization__c> lusageauth = new List<Use_autorization__c>();
            StringBuilder susage = new StringBuilder();
            string[] scolumns = new string[] { "ID_USE_AUTORIZATION", "NAME", "ID_APPOIMENT", "CENTRO_DE_COSTOS__RNAME", "CENTRO_DE_COSTOS__RCODE__C", "PRODUCT_NAME__C"
                                            , "CODIGO_PRODUCTO__C", "CONCEPTOS__C", "ES_FACTURABLE__C", "ES_PROCEDIMIENTO__C", "GRUPO_DE_SERVICIOS__RNAME", "GRUPO_DE_SERVICIOS__RCODE__C"
                                            , "NO_CARGO__C", "TARIFA__RNAME", "TARIFA__RCODE__C", "TOTAL_SIN_DTO__C", "UNIDAD_FUNCIONAL__RNAME", "UNIDAD_FUNCIONAL__RCODE__C" };
            //susage.AppendLine(string.Join(";", scolumns));
            try
            {
                lusageauth = this.GetAuthUsageData(lusageauth);
                foreach (var item in lusageauth)
                {
                    if (item.Centro_de_Costos__r != null && item.Producto__r != null && item.Tarifa__r != null && item.Unidad_Funcional__r != null)
                    {
                        scolumns = new string[]
                        {
                            item.Id,
                            item.Name,
                            item.Cita__c,
                            item.Centro_de_Costos__r.Name,
                            Tools.ReplaceChars(item.Centro_de_Costos__r.Code__c),
                            item.Producto__r.Name__c,
                            Tools.ReplaceChars(item.Producto__r.Name),
                            item.Conceptos__c,
                            item.Facturable__c,
                            item.Producto__r.IsProcedure__c.ToString(),
                            (item.Grupo_de_Servicios__r != null) ? item.Grupo_de_Servicios__r.Name : string.Empty,
                            (item.Grupo_de_Servicios__r != null) ? item.Grupo_de_Servicios__r.Code__c : string.Empty,
                            item.No_Cargo__c,
                            item.Tarifa__r.Name,
                            Tools.ReplaceChars(item.Tarifa__r.Code__c),
                            item.Total_sin_Dto__c.ToString(),
                            item.Unidad_Funcional__r.Name,
                            Tools.ReplaceChars(item.Unidad_Funcional__r.Code__c)
                        };
                        susage.AppendLine(string.Join(";", scolumns));
                    }


                }
                return susage.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<Use_autorization__c> GetAuthUsageData(List<Use_autorization__c> lusageauth)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            bool bdone = false;
            stringBuilder.Append("SELECT Id, Name, Cita__c, Centro_de_Costos__r.Name, Centro_de_Costos__r.Code__c, Producto__r.Name__c, Producto__r.Name, Conceptos__c,  Facturable__c" +
                ", Producto__r.IsProcedure__c, Grupo_de_Servicios__r.Name, Grupo_de_Servicios__r.Code__c, No_Cargo__c, Tarifa__r.Name, Tarifa__r.Code__c, Total_sin_Dto__c, Unidad_Funcional__r.Name" +
                //", Unidad_Funcional__r.Code__c FROM Use_autorization__c WHERE LastModifiedDate = LAST_N_DAYS:35 ORDER BY ID");
                ", Unidad_Funcional__r.Code__c FROM Use_autorization__c WHERE LastModifiedDate = THIS_YEAR ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lusageauth.AddRange(records.OfType<Use_autorization__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lusageauth;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetAssesments()
        {
            List<Assesment__c> lassesment = new List<Assesment__c>();
            StringBuilder sassesment = new StringBuilder();
            string[] scolumns = new string[] { "ID_ASSESMENT", "NAME", "ID_APPOINTMENT", "ID_PATIENT", "AGREEMENTNAME__C", "AGREEMENTID__RCODE__C", "PLANNAME__C", "PLANID__RCODE__C"
                                            , "ISCLOSEDHISTORY__C", "TYPEOFCARE__C", "ASSESMENTDATE__C", "CLOSEDATE__C", "CREATEDBYNAME", "CLOSEDBYUSER__RNAME", "CLOSEDBYUSER__RDOCUMENTNUMBER__C"
                                            , "MAINPROFESSIONALID__RNAME" };
            try
            {
                lassesment = this.GetAssesmentData(lassesment);
                foreach (var item in lassesment)
                {
                    scolumns = new string[]
                    {
                        item.Id,
                        item.Name,
                        item.AppointmentId__c,
                        item.PatientId__c,
                        item.AgreementName__c,
                        item.AgreementId__c != null ? item.AgreementId__r.Code__c : string.Empty,
                        item.PlanName__c,
                        item.PlanId__c != null ? item.PlanId__r.Code__c : string.Empty,
                        item.IsClosedHistory__c.ToString(),
                        item.TypeOfCare__c,
                        item.AssesmentDate__c.Value.ToString("yyyy-MM-dd"),
                        item.CloseDate__c.HasValue ?  item.CloseDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        item.CreatedBy.Name,
                        item.ClosedByUser__c != null ? item.ClosedByUser__r.Name : string.Empty,
                        item.ClosedByUser__c != null ? item.ClosedByUser__r.DocumentNumber__c : string.Empty,
                        item.MainProfessionalId__c != null ? item.MainProfessionalId__r.Name : string.Empty,
                    };
                    sassesment.AppendLine(string.Join(";", scolumns));

                }
                return sassesment.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<Assesment__c> GetAssesmentData(List<Assesment__c> lassesment)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            stringBuilder.Append("SELECT ID, NAME, APPOINTMENTID__C, PATIENTID__C, AGREEMENTNAME__C, AGREEMENTID__R.CODE__C, PLANNAME__C, PLANID__R.HEALTHCAREPLANID__R.CODE__C,ISCLOSEDHISTORY__C" +
                ", TYPEOFCARE__C,  ASSESMENTDATE__C, CLOSEDATE__C, CREATEDBY.NAME, CLOSEDBYUSER__R.NAME, CLOSEDBYUSER__R.DOCUMENTNUMBER__C,  MAINPROFESSIONALID__R.NAME " +
                "FROM ASSESMENT__C WHERE ASSESMENTDATE__C >= LAST_N_MONTHS:1 ORDER BY ID ASC");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                bool bdone = false;
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lassesment.AddRange(records.OfType<Assesment__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lassesment;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetPrescriptions()
        {
            List<Prescription__c> lprescriptions = new List<Prescription__c>();
            List<Prescripcion2022__c> prescripcion2022s = new List<Prescripcion2022__c>();
            StringBuilder sprescriptions = new StringBuilder();
            string[] scolumns = new string[] { "ACTIVEINGREDIENT__C", "BIBLIOGRAPHY__C", "COMMENTS__C", "COMPOSITION__C", "CREATEDDATE", "DESIREDTHERAPEUTICALEFFECT__C", "DOSAGE__C"
                                            , "FREQUENCY__C", "MEDICALRECORDID__C", "MEDICINENAME__C", "MEDICINEID__RISPOS__C", "MEDICINEUSAGEJUSTIFICATION__C", "NOPOS_TYPE__C"
                                            , "NOPOS_URGENCY__C", "OUTOFFORMULA__C", "POSALTERNATIVE__C", "POS_ALTERNATIVEUSED__C", "PRESENTATION1__C", "PRESENTATION__C", "QUANTITY__C"
                                            , "SERVICECODE__C", "SUPPLYMETHOD__C", "TRADEMARK__C" };
            try
            {
                lprescriptions = this.GetPrescriptionsData(lprescriptions);
                foreach (var item in lprescriptions)
                {
                    scolumns = new string[]
                    {
                        (item.ActiveIngredient__c != null) ? Tools.ReplaceChars(item.ActiveIngredient__c) : string.Empty,
                        (item.Bibliography__c != null) ? Tools.ReplaceChars(item.Bibliography__c) : string.Empty,
                        string.Empty,
                        (item.Composition__c != null) ? Tools.ReplaceChars(item.Composition__c) : string.Empty,
                        item.CreatedDate.Value.ToString("yyyy-MM-dd"),
                        (item.DesiredTherapeuticalEffect__c != null) ? Tools.ReplaceChars(item.DesiredTherapeuticalEffect__c) : string.Empty,
                        item.Dosage__c,
                        item.MedicalRecordId__c,
                        (item.MedicineName__c != null) ? Tools.ReplaceChars(item.MedicineName__c) : string.Empty,
                        item.MedicineId__r.IsPOS__c.ToString(),
                        (item.MedicineUsageJustification__c != null) ? Tools.ReplaceChars(item.MedicineUsageJustification__c) : string.Empty,
                        item.NOPOS_Type__c,
                        item.NOPOS_Urgency__c,
                        item.OutOfFormula__c.ToString(),
                        (item.PosAlternative__c != null) ? Tools.ReplaceChars(item.PosAlternative__c) : string.Empty,
                        (item.POS_AlternativeUsed__c != null) ? Tools.ReplaceChars(item.POS_AlternativeUsed__c) : string.Empty,
                        (item.Presentation1__c != null) ? Tools.ReplaceChars(item.Presentation1__c) : string.Empty,
                        (item.Presentation__c != null) ? Tools.ReplaceChars(item.Presentation__c) : string.Empty,
                        item.Quantity__c.ToString(),
                        item.ServiceCode__c,
                        (item.SupplyMethod__c != null) ? Tools.ReplaceChars(item.SupplyMethod__c) : string.Empty,
                        (item.TradeMark__c != null) ? Tools.ReplaceChars(item.TradeMark__c) : string.Empty,
                        item.Frequency__c.ToString(),
                        item.MedicalRecordId__c
                    };
                    sprescriptions.AppendLine(string.Join("|", scolumns));
                }
                prescripcion2022s = this.GetPrescriptionsData(prescripcion2022s);
                foreach (var item in prescripcion2022s)
                {
                    if (item.FNC_Medicamento__r != null)
                    {
                        scolumns = new string[]
                        {
                            (item.FNC_Medicamento__r.FNC_PrincipiosActivos__c != null) ? Tools.ReplaceChars(item.FNC_Medicamento__r.FNC_PrincipiosActivos__c) : string.Empty,
                            string.Empty,
                            string.Empty,
                            (item.FNC_Medicamento__r.FNC_Concentraciones__c != null) ? Tools.ReplaceChars(item.FNC_Medicamento__r.FNC_Concentraciones__c) : string.Empty,
                            item.CreatedDate.Value.ToString("yyyy-MM-dd"),
                            string.Empty,
                            (item.FNC_DosisNumero__c.HasValue) ? item.FNC_DosisNumero__c.Value.ToString() : string.Empty,
                            item.FNC_Consulta__c,
                            item.FNC_Medicamento__r.FNC_Medicamento__c,
                            (item.FNC_Medicamento__r.FNC_MedicamentoPOS__c.HasValue) ? item.FNC_Medicamento__r.FNC_MedicamentoPOS__c.Value.ToString() : string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            string.Empty,
                            (item.FNC_Medicamento__r.FNC_FormaFarmaceutica__c != null) ? item.FNC_Medicamento__r.FNC_FormaFarmaceutica__c : string.Empty,
                            string.Empty,
                            item.Cantidad__c.ToString(),
                            string.Empty,
                            item.FNC_ViaAdministracion__c,
                            item.FNC_Medicamento__r.FNC_NombreComercial__c,
                            item.FNC_Cada__c.ToString(),
                            item.FNC_Consulta__c,
                        };
                        sprescriptions.AppendLine(string.Join("|", scolumns));
                    }

                }
                return sprescriptions.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<Prescription__c> GetPrescriptionsData(List<Prescription__c> lprescriptions)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            stringBuilder.Append("SELECT ACTIVEINGREDIENT__C, BIBLIOGRAPHY__C, COMMENTS__C, COMPOSITION__C, CREATEDDATE, DESIREDTHERAPEUTICALEFFECT__C, DOSAGE__C" +
                ", FREQUENCY__C, MEDICALRECORDID__C, MEDICINENAME__C, MEDICINEID__R.ISPOS__C, MEDICINEUSAGEJUSTIFICATION__C, NOPOS_TYPE__C, NOPOS_URGENCY__C, OUTOFFORMULA__C" +
                ", POSALTERNATIVE__C,POS_ALTERNATIVEUSED__C, PRESENTATION1__C, PRESENTATION__C, QUANTITY__C, SERVICECODE__C, SUPPLYMETHOD__C, TRADEMARK__C FROM PRESCRIPTION__C" +
                " WHERE CREATEDDATE >= LAST_N_MONTHS:2 ORDER BY ID ASC");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                bool bdone = false;
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lprescriptions.AddRange(records.OfType<Prescription__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lprescriptions;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<Prescripcion2022__c> GetPrescriptionsData(List<Prescripcion2022__c> lprescriptions)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            stringBuilder.Append("SELECT FNC_Medicamento__r.FNC_PrincipiosActivos__c, FNC_Recomedacion__c, FNC_Medicamento__r.FNC_Concentraciones__c, CREATEDDATE, FNC_DosisNumero__c" +
                ", FNC_Cada__c, FNC_Consulta__c, FNC_Medicamento__r.FNC_Medicamento__c, FNC_Medicamento__r.FNC_MedicamentoPOS__c" +
                ", FNC_Medicamento__r.FNC_FormaFarmaceutica__c, Cantidad__c, FNC_ViaAdministracion__c, FNC_Medicamento__r.FNC_NombreComercial__c FROM Prescripcion2022__c" +
                " WHERE CREATEDDATE >= LAST_N_MONTHS:2 ORDER BY ID ASC");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                bool bdone = false;
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lprescriptions.AddRange(records.OfType<Prescripcion2022__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lprescriptions;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetOrderTest()
        {
            List<OrderedTest__c> lorders = new List<OrderedTest__c>();
            StringBuilder sprescriptions = new StringBuilder();
            string[] scolumns = new string[] { "ID, NAME, CREATEDDATE, MEDICALRECORDID__C, SERVICEUSAGEJUSTIFICATION__C, SERVICEID__R.NAME,  SERVICEID__R.NAMEFNC__C, SERVICEID__R.CODE__C, SERVICEID__R.CODECUPS__C" };
            string sservicename = string.Empty, scode = string.Empty, sfnc = string.Empty;
            try
            {
                lorders = this.GetOrdersData(lorders);
                foreach (var item in lorders)
                {
                    if (item.FCN_Nombre__r != null)
                    {
                        sservicename = item.FCN_Nombre__r.CodeCups__c;
                        scode = item.FCN_Nombre__r.Name;
                        sfnc = item.FCN_Nombre__r.NameFnc__c;
                    }
                    else if (item.ServiceId__r != null)
                    {
                        sservicename = item.ServiceId__r.CodeCups__c;
                        scode = item.ServiceId__r.Name;
                        sfnc = item.ServiceId__r.NameFnc__c;
                    }
                    else
                    {
                        sservicename = scode = sfnc = string.Empty;
                    }
                    scolumns = new string[]
                    {
                        item.Id,
                        item.Name,
                        item.CreatedDate.Value.ToString("yyyy-MM-dd"),
                        item.MedicalRecordId__c,
                        (item.ServiceUsageJustification__c != null) ? Tools.ReplaceChars(item.ServiceUsageJustification__c) : string.Empty,
                        sservicename,
                        sfnc,
                        scode,
                        scode
                    };
                    sprescriptions.AppendLine(string.Join("|", scolumns));
                }
                return sprescriptions.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<OrderedTest__c> GetOrdersData(List<OrderedTest__c> lorders)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            stringBuilder.Append("SELECT ID, NAME, CREATEDDATE, MEDICALRECORDID__C, SERVICEUSAGEJUSTIFICATION__C, FCN_Nombre__r.NAME,  FCN_Nombre__r.NameFnc__c, FCN_Nombre__r.CodeCups__c, ServiceId__r.Name, ServiceId__r.NameFnc__c, ServiceId__r.CodeCups__c FROM ORDEREDTEST__C WHERE CREATEDDATE = LAST_N_DAYS:200 ORDER BY ID ASC");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                bool bdone = false;
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lorders.AddRange(records.OfType<OrderedTest__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lorders;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetDianosis()
        {
            List<DiagnosisByMedRecord__c> ldiagnosis = new List<DiagnosisByMedRecord__c>();
            StringBuilder sdiagnosis = new StringBuilder();
            string[] scolumns = new string[] { "ID", "NAME", "CREATEDBY.NAME", "DIAGNOSISDATE__C", "DIAGNOSISID__R.NAME", "ICD_CODE__C", "MEDICALRECORDID__C", "ORDERNUMBER__C", "TYPE__C" };
            try
            {
                ldiagnosis = this.GetDiagnosisData(ldiagnosis);
                foreach (var item in ldiagnosis)
                {
                    scolumns = new string[]
                    {
                        item.Id,
                        item.Name,
                        item.CreatedBy.Name,
                        item.DiagnosisDate__c.Value.ToString("yyyy-MM-dd"),
                        item.DiagnosisId__r.Description__c,
                        item.DiagnosisId__r.Name,
                        item.MedicalRecordId__c,
                        item.OrderNumber__c.ToString(),
                        item.Type__c
                    };
                    sdiagnosis.AppendLine(string.Join("|", scolumns));
                }
                return sdiagnosis.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<DiagnosisByMedRecord__c> GetDiagnosisData(List<DiagnosisByMedRecord__c> ldiagnosis)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            stringBuilder.Append("SELECT ID, NAME, CREATEDBY.NAME, DIAGNOSISDATE__C, DIAGNOSISID__R.Description__c, DIAGNOSISID__R.NAME, MEDICALRECORDID__C,  ORDERNUMBER__C, TYPE__C FROM DIAGNOSISBYMEDRECORD__C WHERE DIAGNOSISDATE__C >= LAST_N_MONTHS:12 ORDER BY ID ASC");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                bool bdone = false;
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        ldiagnosis.AddRange(records.OfType<DiagnosisByMedRecord__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return ldiagnosis;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetPRFP()
        {
            List<PFT__c> lpft = new List<PFT__c>();
            StringBuilder spft = new StringBuilder();
            string[] scolumns = new string[] { "ID", "NAME", "APPOINTMENTID__C", "CREATEDDATE", "APPROVALDATE__C", "TESTDATE__C", "STATUS__C", "LASTMODIFIEDBY.NAME", "PATIENTID__C" };
            try
            {
                lpft = this.GetPFPData(lpft);
                foreach (var item in lpft)
                {
                    scolumns = new string[]
                    {
                        item.Id,
                        item.Name,
                        item.AppointmentId__c,
                        (item.CreatedDate.HasValue) ? item.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        (item.ApprovalDate__c.HasValue) ? item.ApprovalDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        (item.TestDate__c.HasValue) ? item.TestDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        item.Status__c,
                        (item.LastModifiedBy != null) ? item.LastModifiedBy.Name : string.Empty,
                        item.PatientId__c
                    };
                    spft.AppendLine(string.Join(";", scolumns));
                }
                return spft.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<PFT__c> GetPFPData(List<PFT__c> lpft)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            if (lpft.Count == 0)
            {
                stringBuilder.Append("SELECT ID, NAME, APPOINTMENTID__C, CREATEDDATE, APPROVALDATE__C, TESTDATE__C, STATUS__C, LASTMODIFIEDBY.NAME, PATIENTID__C FROM PFT__C WHERE CREATEDDATE >= LAST_N_MONTHS:3 ORDER BY ID ASC LIMIT 2000");
            }
            else
            {
                stringBuilder.Append("SELECT ID, NAME, APPOINTMENTID__C, CREATEDDATE, APPROVALDATE__C, TESTDATE__C, STATUS__C, LASTMODIFIEDBY.NAME, PATIENTID__C FROM PFT__C WHERE CREATEDDATE >= LAST_N_MONTHS:3");
                stringBuilder.Append(" AND ID > '");
                stringBuilder.Append(lpft.LastOrDefault().Id);
                stringBuilder.Append("' ORDER BY ID LIMIT 2000");
            }
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lpft.AddRange(records.OfType<PFT__c>().ToList());
                    return GetPFPData(lpft);
                }
                else
                {
                    return lpft;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetRHB()
        {
            List<RehabSession__c> lrhb = new List<RehabSession__c>();
            StringBuilder srhb = new StringBuilder();
            string[] scolumns = new string[] { "ID", "NAME", "APPOINTMENTID__C", "CREATEDDATE", "APPROVALDATE__C", "TESTDATE__C", "STATUS__C", "LASTMODIFIEDBY.NAME", "PATIENTID__C" };
            try
            {
                lrhb = this.GetRHBData(lrhb);
                foreach (var item in lrhb)
                {
                    scolumns = new string[]
                    {
                        item.Id,
                        item.Name,
                        item.AppointmentId__c,
                        (item.CreatedDate.HasValue) ? item.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        (item.ApprovalDate__c.HasValue) ? item.ApprovalDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        (item.TestDate__c.HasValue) ? item.TestDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        item.Status__c,
                        (item.LastModifiedBy != null) ? item.LastModifiedBy.Name : string.Empty,
                        item.PatientId__c
                    };
                    srhb.AppendLine(string.Join(";", scolumns));
                }
                return srhb.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<RehabSession__c> GetRHBData(List<RehabSession__c> lrhb)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            if (lrhb.Count == 0)
            {
                stringBuilder.Append("SELECT ID, NAME, APPOINTMENTID__C, CREATEDDATE, APPROVALDATE__C, TESTDATE__C, STATUS__C, LASTMODIFIEDBY.NAME, PATIENTID__C FROM REHABSESSION__C WHERE CREATEDDATE >= LAST_N_MONTHS:3 ORDER BY ID ASC LIMIT 2000");
            }
            else
            {
                stringBuilder.Append("SELECT ID, NAME, APPOINTMENTID__C, CREATEDDATE, APPROVALDATE__C, TESTDATE__C, STATUS__C, LASTMODIFIEDBY.NAME, PATIENTID__C FROM REHABSESSION__C WHERE CREATEDDATE >= LAST_N_MONTHS:3");
                stringBuilder.Append(" AND ID > '");
                stringBuilder.Append(lrhb.LastOrDefault().Id);
                stringBuilder.Append("' ORDER BY ID LIMIT 2000");
            }
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lrhb.AddRange(records.OfType<RehabSession__c>().ToList());
                    return GetRHBData(lrhb);
                }
                else
                {
                    return lrhb;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public string GetSleepTest()
        {
            List<SleepTest__c> lsleep = new List<SleepTest__c>();
            StringBuilder ssleep = new StringBuilder();
            string[] scolumns = new string[] { "ID", "NAME", "APPOINTMENTID__C", "CREATEDDATE", "APPROVALDATE__C", "TESTDATE__C", "STATUS__C", "LASTMODIFIEDBY.NAME", "PATIENTID__C" };
            try
            {
                lsleep = this.GetSleepData(lsleep);
                foreach (var item in lsleep)
                {
                    scolumns = new string[]
                    {
                        item.Id,
                        item.Name,
                        item.AppointmentId__c,
                        (item.CreatedDate.HasValue) ? item.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        (item.ApprovalDate__c.HasValue) ? item.ApprovalDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        (item.TestDate__c.HasValue) ? item.TestDate__c.Value.ToString("yyyy-MM-dd") : string.Empty,
                        item.Status__c,
                        (item.LastModifiedBy != null) ? item.LastModifiedBy.Name : string.Empty,
                        item.PatientId__c,
                        (item.LastModifiedDate.HasValue) ? item.LastModifiedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                    };
                    ssleep.AppendLine(string.Join(";", scolumns));
                }
                return ssleep.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<SleepTest__c> GetSleepData(List<SleepTest__c> lsleep)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            stringBuilder.Append("SELECT ID, NAME, APPOINTMENTID__C, CREATEDDATE, APPROVALDATE__C, TESTDATE__C, STATUS__C, LASTMODIFIEDBY.NAME, PATIENTID__C, LASTMODIFIEDDATE FROM SLEEPTEST__C WHERE CREATEDDATE = LAST_N_DAYS:365 ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                bool bdone = false;
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lsleep.AddRange(records.OfType<SleepTest__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lsleep;
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }            
        }

        public string GetPlan()
        {
            List<Plan__c> lplan = new List<Plan__c>();
            StringBuilder splan = new StringBuilder();
            string[] scolumns = new string[] { "ID", "AGREEMENTID__C", "COMPANYNAME__C", "HEALTHCAREPLANID__C", "NAME", "TIPO_PLAN__C", "TIPO_TARIFA__C", "AGREEMENTID__R.CODE__C", "HEALTHCAREPLANID__R.CODE__C" };
            try
            {
                lplan = this.GetPlanData(lplan);
                foreach (var item in lplan)
                {
                    scolumns = new string[]
                    {
                        item.Id,
                        item.AgreementId__c,
                        item.CompanyName__c,
                        item.HealthCarePlanId__c,
                        item.Name,
                        item.Tipo_Plan__c,
                        item.Tipo_Tarifa__c,
                        Tools.ReplaceChars(item.AgreementId__r.Code__c),
                        Tools.ReplaceChars(item.HealthCarePlanId__r.Code__c),
                    };
                    splan.AppendLine(string.Join(";", scolumns));
                }
                return splan.ToString();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
        }

        private List<Plan__c> GetPlanData(List<Plan__c> lplan)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            if (lplan.Count == 0)
            {
                stringBuilder.Append("SELECT ID, AGREEMENTID__C, COMPANYNAME__C, HEALTHCAREPLANID__C, NAME, TIPO_PLAN__C, TIPO_TARIFA__C, AGREEMENTID__R.CODE__C, HEALTHCAREPLANID__R.CODE__C FROM PLAN__C WHERE ISACTIVO__C = true ORDER BY ID ASC LIMIT 2000");
            }
            else
            {
                stringBuilder.Append("SELECT ID, AGREEMENTID__C, COMPANYNAME__C, HEALTHCAREPLANID__C, NAME, TIPO_PLAN__C, TIPO_TARIFA__C, AGREEMENTID__R.CODE__C, HEALTHCAREPLANID__R.CODE__C FROM PLAN__C WHERE ISACTIVO__C = true");
                stringBuilder.Append(" AND ID > '");
                stringBuilder.Append(lplan.LastOrDefault().Id);
                stringBuilder.Append("' ORDER BY ID LIMIT 2000");
            }
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    lplan.AddRange(records.OfType<Plan__c>().ToList());
                    return GetPlanData(lplan);
                }
                else
                {
                    return lplan;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }
        #endregion

        #region Métodos integración sistema de consentimientos informados

        public void MarkConsent(string sSession, string sUrl, string sAppointment, string sStatus)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SaveResult[] results = null;
            LimitInfo[] limitInfos = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            StringBuilder stringBuilder = new StringBuilder("SELECT Id FROM Appointment__c WHERE Name = '");
            stringBuilder.Append(sAppointment);
            stringBuilder.Append("'");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    Appointment__c appointment = records[0] as Appointment__c;
                    appointment.ConsentApproved__cSpecified = true;
                    appointment.ConsentApproved__c = (sStatus.ToUpper() == "SI");
                    sObject[] oUpdate = new sObject[] { appointment };
                    soapClient.update(sessionHeader, null, mruHeader, null, null, null, null, null, null, null, packageVersions, null, null, oUpdate, out limitInfos, out results);
                    if (!results[0].success)
                    {
                        ApplicationException applicationException = new ApplicationException(results[0].errors[0].message);
                        LogError.WriteError("Application", "WSInspira", applicationException);
                    }

                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
            }
        }

        public List<Consentimiento> GetDataForConsent(string sSession, string sUrl, string sid)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            Consentimiento consentimiento = null;
            List<Consentimiento> lconsentimientos = new List<Consentimiento>();
            StringBuilder stringBuilder = new StringBuilder("SELECT Id, Name, GroupId__c, PlanId__c, ActivityDate__c, StartDatetime__c, EndDatetime__c, RoomId__r.WaitingRoomId__c, CompanionName__c");
            stringBuilder.Append(", CompanionPhone__c, CostCenterId__r.Code__c, AgreementCode__c, PlanCode__c, ProgramId__r.Code__c, PlanId__r.RateId__c, TurnNumber__c, WhatId__c, WhatId__r.FirstName_c__pc");
            stringBuilder.Append(", WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.PersonBirthdate, WhatId__r.DocumentType__c, WhatId__r.DocumentNumber__c");
            stringBuilder.Append(", WhatId__r.Gender__pc, WhatId__r.MaritalStatus__pc, WhatId__r.Ocupation__pc, WhatId__r.ResidentialArea__pc, WhatId__r.ExternalId__c, WhatId__r.Address__c,  WhatId__r.Phone");
            stringBuilder.Append(", WhatId__r.PersonHomePhone, WhatId__r.PersonMobilePhone, WhatId__r.PersonEmail, WhatId__r.CreatedDate, WhatId__r.EconomicLevel__pc, WhatId__r.State__r.Code__c, WhatId__r.City__r.Code__c");
            stringBuilder.Append(", WhatId__r.ERP_Code__c, WhatId__r.Age2__pc, AgendaId__c, AgendaId__r.CostCenterId__r.Code__c, OwnerId, Owner.Name, AgreementId__r.Name, AgreementId__r.CompanyId__r.DocumentNumber__c");
            stringBuilder.Append(", PlanId__r.Name, PlanId__r.HealthCarePlanId__r.Code__c, Multicompany__c, AuthorizationCode__c, GroupId__r.Name, PatientAttended__c FROM Appointment__c WHERE Name = '");
            stringBuilder.Append(sid);
            stringBuilder.Append("' LIMIT 1");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    sObject[] records = queryResult.records;
                    Appointment__c appointment = records[0] as Appointment__c;
                    stringBuilder.Clear();
                    stringBuilder.Append("SELECT Tarifa_concepto_producto__r.ProductId__c, Tarifa_concepto_producto__r.ProductId__r.Name__c, Tarifa_concepto_producto__r.ProductId__r.Name, " +
                                        "Grupo_por_Plan__r.Grupo__c, Tarifa_concepto_producto__r.CostCenterId__r.Code__c FROM ProductsByGroup__c WHERE Grupo_por_Plan__r.Grupo__c = '");
                    stringBuilder.Append(appointment.GroupId__c);
                    stringBuilder.Append("' AND Grupo_por_Plan__r.Plan__c = '");
                    stringBuilder.Append(appointment.PlanId__c);
                    stringBuilder.Append("' AND Tarifa_concepto_producto__r.ProductId__r.Name IN ('890371', '890271', '890372', '890272')");
                    soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                    if (queryResult.size > 0)
                    {
                        records = queryResult.records;
                        foreach (var item in records)
                        {
                            ProductsByGroup__c productsByGroup__C = item as ProductsByGroup__c;
                            consentimiento = new Consentimiento()
                            {
                                sappointmemt = appointment.Name,
                                sdocument = appointment.WhatId__r.DocumentNumber__c,
                                sdocumenttype = appointment.WhatId__r.DocumentType__c,
                                sfirstname = appointment.WhatId__r.FirstName_c__pc,
                                shabeasdata = (appointment.WhatId__r.HabeasData__c != null) ? appointment.WhatId__r.HabeasData__c : string.Empty,
                                dappointmentdate = appointment.ActivityDate__c.Value,
                                ssecondsurname = (appointment.WhatId__r.SecondSurname__pc != null) ? appointment.WhatId__r.SecondSurname__pc : string.Empty,
                                ssurname = appointment.WhatId__r.SecondSurname__pc,
                                iage = Convert.ToInt32(appointment.WhatId__r.Age2__pc),
                                sid = appointment.Id,
                                sservicename = productsByGroup__C.Tarifa_concepto_producto__r.ProductId__r.Name__c,
                                ssecondname = (appointment.WhatId__r.SecondName__pc != null) ? appointment.WhatId__r.SecondName__pc : string.Empty,
                                scups = productsByGroup__C.Tarifa_concepto_producto__r.ProductId__r.Name,
                            };
                            lconsentimientos.Add(consentimiento);
                        }
                    }
                }
                return lconsentimientos;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
        }

        #endregion

        #region Nuevos métodos de sincronización entre Inspira y Servinte utilizando directamente el componenrte sin el WS

        #region Sincronizar convenios

        /// <summary>
        /// Método para actualizar los convenios
        /// </summary>
        /// <param name="lConvenios"></param>
        /// <returns></returns>
        public List<InspiraTemporal> ActualizaConvenios(List<InspiraTemporal> lConvenios)
        {
            //List<Account> laccounts = this.getcom
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            List<Account> lcompanies = this.GetCompanies(soapClient);
            List<Agreement__c> lagreements = this.GetAgreements(false);
            List<sObject> lInsert = new List<sObject>();
            List<sObject> lUpdate = new List<sObject>();
            string sid = string.Empty;
            Account account = null;
            Agreement__c agreement = null;
            string srecordtype = this.GetCompanyRecordType(soapClient);
            string sagreementid = string.Empty;
            this.lListInsert = new List<sObject>();
            this.lListUpdate = new List<sObject>();
            foreach (var item in lConvenios)
            {
                sid = this.GetCompanyId(lcompanies, item.iparametro3.ToString());
                if (string.IsNullOrEmpty(sid))
                {
                    account = new Account()
                    {
                        RecordTypeId = srecordtype,
                        DocumentType__c = "NIT",
                        DocumentNumber__c = item.iparametro3.ToString(),
                        Address__c = item.sparametro1,
                        Phone = item.sparametro2,
                        CompanyType__c = "OTRAS",
                        ERP_Code__c = item.scod,
                        Name = item.snombre,
                        IsActive__cSpecified = true,
                        IsActive__c = true,
                    };
                    lInsert.Add(account);
                    this.lListInsert.Add(account);
                }
            }
            if (lInsert.Count > 0) this.InsertValues(lInsert, soapClient, sessionHeader, "Agreement");
            lInsert.Clear();
            this.lListInsert.Clear();
            lcompanies = this.GetCompanies(soapClient);
            foreach (var item in lConvenios)
            {
                sid = this.GetCompanyId(lcompanies, item.iparametro3.ToString());
                sagreementid = this.GetAgreementByAccount(sid, lagreements);
                if (!string.IsNullOrEmpty(sid))
                {
                    agreement = new Agreement__c()
                    {
                        Code__c = item.scod,
                        Name = item.snombre,
                        IsActivo__cSpecified = true,
                        IsActivo__c = true,
                        Tipo_de_Convenio__c = "E-Empresas",
                        StartDate__cSpecified = true,
                        StartDate__c = DateTime.Now,
                        CompanyId__c = sid
                    };
                    if (!string.IsNullOrEmpty(sagreementid))
                    {
                        agreement.Id = sagreementid;
                        lUpdate.Add(agreement);
                        this.lListUpdate.Add(agreement);
                    }
                    else
                    {
                        lInsert.Add(agreement);
                        this.lListInsert.Add(account);
                    }
                }
            }
            if (lInsert.Count > 0) this.InsertValues(lInsert, soapClient, sessionHeader, "Agreement");
            if (lUpdate.Count > 0) this.UpdateValues(lUpdate, soapClient, sessionHeader, "Agreement");
            lagreements = this.GetAgreements(true);
            for (int i = 0; i < lConvenios.Count; i++)
            {
                lConvenios[i].sid = this.GetAgreementd(lagreements, lConvenios[i].scod);
            }
            return lConvenios;
        }

        private List<Account> GetCompanies(SoapClient soapClient)
        {
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            StringBuilder stringBuilder = new StringBuilder("SELECT Id, DocumentNumber__c FROM Account WHERE RecordType.SobjectType = 'Account' AND RecordType.Name IN ('Empresa', 'Organization', 'Business')");
            soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
            if (queryResult.size > 0)
            {
                sObject[] records = queryResult.records;
                return records.OfType<Account>().ToList();
            }
            return new List<Account>();
        }

        /// <summary>
        /// Método para obtener el id de una empresa por el nit de la misma buscando en el listado total de empresas
        /// </summary>
        /// <param name="scompany">String nit de la empresa</param>
        /// <param name="lcompanies">Lista genérica del objeto Account con las empresas</param>
        /// <returns>Id de la empresa</returns>
        private string SearchCompanyByNit(string scompany, List<Account> lcompanies)
        {
            Account account = lcompanies.FirstOrDefault(x => x.DocumentNumber__c == scompany);
            return (account != null) ? account.Id : string.Empty;
        }

        private string GetAgreementByAccount(string scompany, List<Agreement__c> lagreements)
        {
            Agreement__c agreement = lagreements.FirstOrDefault(x => x.CompanyId__c == scompany);
            return (agreement != null) ? agreement.Id : string.Empty;
        }

        #endregion

        #endregion

        #region Métodos para obtener la información de los soportes de las atenciones prestadas

        public List<Appointment__c> GetAppointmentDataFromName(string snames)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            bool bdone = false;
            List<Appointment__c> lappointments = new List<Appointment__c>();
            stringBuilder.Append("SELECT Id, Name, CostCenterId__r.Code__c, CostCenterId__c, GroupId__r.Name FROM Appointment__c WHERE Name IN (");
            stringBuilder.Append(snames);
            stringBuilder.Append(")  ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lappointments.AddRange(records.OfType<Appointment__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                return lappointments;
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        public List<Charge> GetAppointmentSupports(List<Charge> lcharges, string snames)        
        {
            List<ChargeDetail> lstTmp = new List<ChargeDetail>();
            List<Appointment__c> lappointments = new List<Appointment__c>();
            List<string> lstrehabSessions = new List<string>();
            List<string> lstassesments = new List<string>();
            List<string> lstpft = new List<string>();
            List<string> lstallergys = new List<string>();
            List<string> lstsleep = new List<string>();
            int blockSize = 1000;
            int startIndex = 0;
            int currentBlockSize = 0;
            string[] asearch = null;
            string currentNames = string.Empty;
            int totalItems = 0;
            string[] aresult = null;
            List<string> currentBlock = null;
            List<string> excludeNames = new List<string>
            {
                "ERGO 60",
                "ERGO RHB",
                "PRUEBA DE EJERCICIO CARDIO - PULMONAR(ERGOESPIROMETRIA)",
            };
            try
            {
                aresult = snames.Split(',');                 
                totalItems = aresult.Length; // Total de elementos en el arreglo
                blockSize = 1000; // Tamaño del bloque
                startIndex = 0; // Índice de inicio para cada bloque
                while (startIndex < totalItems)
                {
                    currentBlockSize = Math.Min(blockSize, totalItems - startIndex);
                    asearch = new string[currentBlockSize];
                    Array.Copy(aresult, startIndex, asearch, 0, currentBlockSize);
                    currentNames = String.Join(",", asearch);
                    lappointments.AddRange(this.GetAppointmentDataFromName(currentNames));
                    startIndex += blockSize;
                }
                foreach (Appointment__c appointment in lappointments)
                {
                    if (!excludeNames.Any(name => appointment.GroupId__r.Name.Contains(name)))
                    {
                        lstassesments.Add($"'{appointment.Id}'");
                    }
                    if (appointment.CostCenterId__c != null)
                    {
                        /*if (appointment.GroupId__r.Name.Contains("CONSULTA") || appointment.GroupId__r.Name.Contains("LECTURA") 
                            || appointment.GroupId__r.Name.Contains("EDUCACI") || appointment.GroupId__r.Name.Contains("CONTROL") || appointment.GroupId__r.Name.Contains("INMUNOTERAPIA")
                            || appointment.GroupId__r.Name.Contains("PRUEBA INTRAEPIDÉRMICA") || appointment.GroupId__r.Name.Contains("SESION PSICOLOGÍA") 
                            || appointment.GroupId__r.Name.Contains("SESION EVALUACI") || appointment.GroupId__r.Name.Contains("SESION NUTRICIÓN") || appointment.GroupId__r.Name.Contains("SESION TALLER"))
                        {
                            lstassesments.Add("'" + appointment.Id + "'");
                        }
                        else if (appointment.CostCenterId__r.Code__c.EndsWith("00"))
                        {
                            lstassesments.Add("'" + appointment.Id + "'");
                        }
                        else */
                        if (appointment.CostCenterId__r.Code__c.StartsWith("41"))
                        {
                            lstpft.Add("'" + appointment.Id + "'"); 
                        }
                        else if (appointment.CostCenterId__r.Code__c.StartsWith("42"))
                        {
                            lstallergys.Add("'" + appointment.Id + "'");
                        }
                        else if (appointment.CostCenterId__r.Code__c.StartsWith("40"))
                        {
                            lstsleep.Add("'" + appointment.Id + "'");
                        }
                        else if (appointment.CostCenterId__r.Code__c.StartsWith("50"))
                        {
                            lstrehabSessions.Add("'" + appointment.Id + "'");
                        }
                        else if (appointment.CostCenterId__r.Code__c.EndsWith("00"))
                        {
                            lstassesments.Add("'" + appointment.Id + "'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);                
            }
            if (lstassesments.Count > 0)
            {
                startIndex = 0;                 
                totalItems = lstassesments.Count; 
                // Procesar en bloques de 1000 elementos
                while (startIndex < totalItems)
                {
                    currentBlockSize = Math.Min(blockSize, totalItems - startIndex);
                    currentBlock = lstassesments.GetRange(startIndex, currentBlockSize);
                    try
                    {
                        var notes = this.GetAssementNotes(currentBlock);
                        // Agregar los resultados a la lista temporal (lstTmp)
                        lstTmp.AddRange(notes);
                    }
                    catch (Exception ex)
                    {
                        LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                    }                    
                    // Avanzar al siguiente bloque
                    startIndex += blockSize;
                }
            }
            if (lstpft.Count > 0)
            {
                try
                {
                    startIndex = 0; // Índice de inicio para cada bloque
                    totalItems = lstpft.Count; // Total de elementos en la lista
                    while (startIndex < totalItems)
                    {
                        // Calcular el tamaño del bloque actual (puede ser menor a 1000 si es el último bloque)
                        currentBlockSize = Math.Min(blockSize, totalItems - startIndex);
                        // Obtener el bloque actual usando GetRange
                        currentBlock = lstpft.GetRange(startIndex, currentBlockSize);
                        // Llamar al método para procesar el bloque actual
                        var notes = this.GetPftTests(currentBlock);
                        // Agregar los resultados a la lista temporal (lstTmp)
                        lstTmp.AddRange(notes);
                        // Avanzar al siguiente bloque
                        startIndex += blockSize;
                    }
                }
                catch (Exception)
                {

                    throw;
                }
            }
            if (lstsleep.Count > 0)
            {
                try
                {
                    startIndex = 0; // Índice de inicio para cada bloque
                    totalItems = lstsleep.Count; // Total de elementos en la lista
                    while (startIndex < totalItems)
                    {
                        // Calcular el tamaño del bloque actual (puede ser menor a 1000 si es el último bloque)
                        currentBlockSize = Math.Min(blockSize, totalItems - startIndex);
                        // Obtener el bloque actual usando GetRange
                        currentBlock = lstsleep.GetRange(startIndex, currentBlockSize);
                        // Llamar al método para procesar el bloque actual
                        var notes = this.GetSleepTests(currentBlock);
                        // Agregar los resultados a la lista temporal (lstTmp)
                        lstTmp.AddRange(notes);
                        // Avanzar al siguiente bloque
                        startIndex += blockSize;
                    }
                }
                catch (Exception ex)
                {
                    LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);                    
                }
            }
            if (lstrehabSessions.Count > 0)
            {
                try
                {
                    
                    startIndex = 0; // Índice de inicio para cada bloque
                    totalItems = lstrehabSessions.Count; // Total de elementos en la lista
                    while (startIndex < totalItems)
                    {
                        // Calcular el tamaño del bloque actual (puede ser menor a 1000 si es el último bloque)
                        currentBlockSize = Math.Min(blockSize, totalItems - startIndex);
                        // Obtener el bloque actual usando GetRange
                        currentBlock = lstrehabSessions.GetRange(startIndex, currentBlockSize);
                        // Llamar al método para procesar el bloque actual
                        var notes = this.GetRhbTests(currentBlock);
                        // Agregar los resultados a la lista temporal (lstTmp)
                        lstTmp.AddRange(notes);
                        // Avanzar al siguiente bloque
                        startIndex += blockSize;
                    }
                }
                catch (Exception ex)
                {
                    LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);                    
                }                
            }
            if (lstallergys.Count > 0)
            {
                try
                {
                    startIndex = 0; // Índice de inicio para cada bloque
                    totalItems = lstallergys.Count; // Total de elementos en la lista
                    while (startIndex < totalItems)
                    {
                        // Calcular el tamaño del bloque actual (puede ser menor a 1000 si es el último bloque)
                        currentBlockSize = Math.Min(blockSize, totalItems - startIndex);
                        // Obtener el bloque actual usando GetRange
                        currentBlock = lstallergys.GetRange(startIndex, currentBlockSize);
                        // Llamar al método para procesar el bloque actual
                        var notes = this.GetAllergyTests(currentBlock);
                        // Agregar los resultados a la lista temporal (lstTmp)
                        lstTmp.AddRange(notes);
                        // Avanzar al siguiente bloque
                        startIndex += blockSize;
                    }
                }
                catch (Exception ex)
                {
                    LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                }
            }
            lcharges = this.SetFileValues(lcharges, lstTmp);
            return lcharges;
        }

        private List<Charge> SetFileValues(List<Charge> lcharges, List<ChargeDetail> lstTmp)
        {
            ChargeDetail chargeDetail = null;
            for (int i = 0; i < lcharges.Count; i++)
            {
                lcharges[i].ldetail = new List<ChargeDetail>();
                foreach (ChargeDetail detail in lstTmp)
                {
                    if (detail.scode == lcharges[i].sappointment)
                    {
                        chargeDetail = new ChargeDetail()
                        {
                            sservice = detail.sservice,
                            sconcept = detail.sconcept,
                            scostcenter = detail.scostcenter,
                            snit = detail.snit,
                            stype = detail.stype,
                            sgroupname = detail.sgroupname.Replace(",", " "),
                            itype = detail.itype,
                            scode = detail.scode
                        };
                        lcharges[i].ldetail.Add(chargeDetail);
                    }
                }
            }
            return lcharges;
        }

        private List<ChargeDetail> GetAssementNotes(List<string> lstpft)
        {
            List<ChargeDetail> lstDetail = new List<ChargeDetail>();
            ChargeDetail chargeDetail = null;
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            string sid = string.Join(",", lstpft);
            bool bdone = false;
            List<Assesment__c> lappointments = new List<Assesment__c>();
            stringBuilder.Append("SELECT Id, Status__c, AppointmentId__c, AppointmentId__r.Name, AppointmentId__r.GroupId__r.Name, CreatedDate, AppointmentId__r.CostcenterId__r.Name FROM Assesment__c WHERE AppointmentId__c IN (");
            stringBuilder.Append(sid);
            stringBuilder.Append(") AND (NOT Name LIKE 'Informe de procedimiento Inmunoterapia%') ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lappointments.AddRange(records.OfType<Assesment__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                foreach (Assesment__c pft in lappointments)
                {
                    chargeDetail = new ChargeDetail()
                    {
                        scode = pft.AppointmentId__r.Name,
                        sservice = pft.Id,
                        stype = "Assesment",
                        sconcept = pft.Status__c,
                        scostcenter = (pft.CreatedDate.HasValue) ? pft.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        //snit = (pft.Status__c != "Rechazado") ? "Prueba cargada" : "Pendiente carga",
                        itype = (pft.Status__c != "Cerrado") ? 1 : 2,
                        snit = (pft.AppointmentId__r.CostCenterId__r != null) ? pft.AppointmentId__r.CostCenterId__r.Name : "COAD - CONSULTA",
                        sgroupname = pft.AppointmentId__r.GroupId__r.Name,
                    };
                    lstDetail.Add(chargeDetail);
                }
                return lstDetail;
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }


        private List<ChargeDetail> GetPftTests(List<string> lstpft)
        {
            List<ChargeDetail> lstDetail = new List<ChargeDetail>();
            ChargeDetail chargeDetail = null;
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            string sid = string.Join(",", lstpft);
            bool bdone = false;
            List<PFT__c> lappointments = new List<PFT__c>();
            stringBuilder.Append("SELECT Id, Status__c, AppointmentId__c, ApprovalDate__c, Filename__c, AppointmentId__r.Name, AppointmentId__r.GroupId__r.Name, CreatedDate, AppointmentId__r.CostcenterId__r.Name FROM PFT__c WHERE AppointmentId__c IN (");
            stringBuilder.Append(sid);
            stringBuilder.Append(")  ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lappointments.AddRange(records.OfType<PFT__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                foreach (PFT__c pft in lappointments)
                {
                    chargeDetail = new ChargeDetail()
                    {
                        scode = pft.AppointmentId__r.Name,
                        sservice = pft.Filename__c,
                        stype = "Prfp",
                        sconcept = pft.Status__c,                        
                        scostcenter = (pft.CreatedDate.HasValue) ? pft.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        //snit = (pft.Status__c != "Rechazado") ? "Prueba cargada" : "Pendiente carga",
                        itype = (pft.Status__c != "Rechazado") ? 1 : 2,
                        snit = pft.AppointmentId__r.CostCenterId__r.Name,
                        sgroupname = pft.AppointmentId__r.GroupId__r.Name,                        
                    };
                    lstDetail.Add(chargeDetail);
                }
                return lstDetail;
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<ChargeDetail> GetRhbTests(List<string> lstrehabSessions)
        {
            List<ChargeDetail> lstDetail = new List<ChargeDetail>();
            ChargeDetail chargeDetail = null;
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            string sid = string.Join(",", lstrehabSessions);
            bool bdone = false;
            List<RehabSession__c> lappointments = new List<RehabSession__c>();
            stringBuilder.Append("SELECT Id, Status__c, AppointmentId__c, ApprovalDate__c, Filename__c, AppointmentId__r.Name, AppointmentId__r.GroupId__r.Name, CreatedDate, AppointmentId__r.CostCenterId__r.Name FROM RehabSession__c WHERE AppointmentId__c IN (");
            stringBuilder.Append(sid);
            stringBuilder.Append(")  ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lappointments.AddRange(records.OfType<RehabSession__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                foreach (RehabSession__c pft in lappointments)
                {
                    chargeDetail = new ChargeDetail()
                    {
                        scode = pft.AppointmentId__r.Name,
                        sservice = pft.Filename__c,
                        stype = "Rehab",
                        sconcept = pft.Status__c,
                        scostcenter = (pft.CreatedDate.HasValue) ? pft.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        //snit = (pft.Status__c != "Rechazado") ? "Prueba cargada" : "Pendiente carga",
                        itype = (pft.Status__c != "Rechazado") ? 1 : 2,
                        snit = pft.AppointmentId__r.CostCenterId__r.Name,
                        sgroupname = pft.AppointmentId__r.GroupId__r.Name,
                    };
                    lstDetail.Add(chargeDetail);
                }
                return lstDetail;
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<ChargeDetail> GetSleepTests(List<string> lstsleep)
        {
            List<ChargeDetail> lstDetail = new List<ChargeDetail>();
            ChargeDetail chargeDetail = null;
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            string sid = string.Join(",", lstsleep);
            bool bdone = false;
            List<SleepTest__c> lappointments = new List<SleepTest__c>();
            stringBuilder.Append("SELECT Id, Status__c, AppointmentId__c, ApprovalDate__c, Filename__c, AppointmentId__r.Name, AppointmentId__r.GroupId__r.Name, CreatedDate, AppointmentId__r.CostCenterId__r.Name FROM SleepTest__c WHERE AppointmentId__c IN (");
            stringBuilder.Append(sid);
            stringBuilder.Append(")  ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lappointments.AddRange(records.OfType<SleepTest__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                foreach (SleepTest__c pft in lappointments)
                {
                    chargeDetail = new ChargeDetail()
                    {
                        scode = pft.AppointmentId__r.Name,
                        sservice = pft.Filename__c,
                        stype = "Sleep",
                        sconcept = pft.Status__c,
                        scostcenter = (pft.CreatedDate.HasValue) ? pft.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        //snit = (pft.Status__c != "Rechazado") ? "Prueba cargada" : "Pendiente carga",
                        itype = (pft.Status__c != "Rechazado") ? 1 : 2,
                        snit = pft.AppointmentId__r.CostCenterId__r.Name,
                        sgroupname = pft.AppointmentId__r.GroupId__r.Name,
                    };
                    lstDetail.Add(chargeDetail);
                }
                return lstDetail;
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        private List<ChargeDetail> GetAllergyTests(List<string> lstallergies)
        {
            List<ChargeDetail> lstDetail = new List<ChargeDetail>();
            ChargeDetail chargeDetail = null;
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
            PackageVersion[] packageVersions = null;
            SessionHeader sessionHeader = new SessionHeader() { sessionId = sSession };
            StringBuilder stringBuilder = new StringBuilder();
            soapClient.Endpoint.Address = new System.ServiceModel.EndpointAddress(sUrl);
            string sid = string.Join(",", lstallergies);
            bool bdone = false;            
            List<Allergy__c> lappointments = new List<Allergy__c>();
            stringBuilder.Append("SELECT Id, Status__c, AppointmentId__c, ApprovalDate__c, FileName__c, AppointmentId__r.Name, AppointmentId__r.GroupId__r.Name, CreatedDate, AppointmentId__r.CostCenterId__r.Name FROM Allergy__c WHERE AppointmentId__c IN (");
            stringBuilder.Append(sid);
            stringBuilder.Append(") AND FileName__c  ORDER BY ID");
            try
            {
                soapClient.query(sessionHeader, queryOptions, mruHeader, packageVersions, stringBuilder.ToString(), out queryResult);
                if (queryResult.size > 0)
                {
                    while (!bdone)
                    {
                        sObject[] records = queryResult.records;
                        lappointments.AddRange(records.OfType<Allergy__c>().ToList());
                        if (queryResult.done)
                        {
                            bdone = true;
                        }
                        else
                        {
                            soapClient.queryMore(sessionHeader, queryOptions, queryResult.queryLocator, out queryResult);
                        }
                    }
                }
                foreach (Allergy__c pft in lappointments)
                {
                    chargeDetail = new ChargeDetail()
                    {
                        scode = pft.AppointmentId__r.Name,
                        sservice = pft.FileName__c,
                        stype = "Aler",
                        sconcept = pft.Status__c,
                        scostcenter = (pft.CreatedDate.HasValue) ? pft.CreatedDate.Value.ToString("yyyy-MM-dd") : string.Empty,
                        //snit = (pft.Status__c != "Rechazado") ? "Prueba cargada" : "Pendiente carga",
                        itype = (pft.Status__c != "Rechazado") ? 1 : 2,
                        snit = pft.AppointmentId__r.CostCenterId__r.Name,
                        sgroupname = pft.AppointmentId__r.GroupId__r.Name,
                    };
                    lstDetail.Add(chargeDetail);
                }
                return lstDetail;
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "CreaSoporteCargos", ex);
                throw;
            }
            finally
            {
                soapClient = null;
                sessionHeader = null;
                queryOptions = null;
                packageVersions = null;
                mruHeader = null;
            }
        }

        #endregion
    }
}
