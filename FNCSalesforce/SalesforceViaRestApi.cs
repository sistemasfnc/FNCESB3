using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Security.Policy;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;
using EventLog;
using FNCDAC;
using FNCEntity;
using FNCSalesforce.Sfdc;
using FNCUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Web;
using FNCSalesforce.InspiraSynapseWS;
using System.Data.SqlClient;
using System.Net.NetworkInformation;

namespace FNCSalesforce
{
    public class SalesforceViaRestApi : IDisposable
    {
        public string sLogingEndPoint { get; set; }
        public string sApiEndpoint { get; set; }

        private readonly HttpClient httpClient;

        public Generic salesforceSession { get; set; }

        public bool battended { get; set; }

        public string sdirection { get; set; }

        //public const string LoginEndpoint = "https://test.salesforce.com/services/oauth2/token";
        //public const string ApiEndpoint = "/services/data/v36.0/"; //Use your org's version number

        public SalesforceViaRestApi()
        {
            this.httpClient = new HttpClient();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        }

        #region Métodos para invocar el API de Salesforce

        public void DoLogin(string sUser, string sPassword, string sClientId, string sClienteSecret)
        {
            HttpContent httpContent = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"grant_type", "password"},
                {"client_id", sClientId},
                {"client_secret", sClienteSecret},
                {"username", sUser},
                {"password", sPassword}
            });
            HttpResponseMessage message = httpClient.PostAsync(this.sLogingEndPoint + "/services/oauth2/token", httpContent).Result;
            string response = message.Content.ReadAsStringAsync().Result;
            JObject obj = JObject.Parse(response);
            this.salesforceSession = new Generic()
            {
                scode = (string)obj["access_token"],
                sname = (string)obj["instance_url"],
            };
        }

        private string QueryRecordAsync(HttpClient client, string queryMessage)
        {
            string restQuery = $"{this.salesforceSession.sname}{this.sApiEndpoint}query?q={queryMessage}";
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, restQuery);
            request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            try
            {
                HttpResponseMessage response = client.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    return response.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    throw new Exception($"Error en la solicitud HTTP: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private string QueryRecordsWithPagination(HttpClient client, string queryMessage, int batchSize)
        {
            List<string> results = new List<string>();
            string nextRecordsUrl = null;
            do
            {
                string query = string.IsNullOrEmpty(nextRecordsUrl) ? queryMessage : $"{this.salesforceSession.sname}{this.sApiEndpoint}{nextRecordsUrl}";
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, query);
                request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                try
                {
                    HttpResponseMessage response = client.SendAsync(request).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResponse = response.Content.ReadAsStringAsync().Result;
                        results.Add(jsonResponse);
                        // Verifica si hay una URL para la siguiente página en la respuesta
                        nextRecordsUrl = ParseNextRecordsUrlFromResponse(jsonResponse);
                    }
                    else
                    {
                        throw new Exception($"Error en la solicitud HTTP: {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    throw ex;
                }

            } while (!string.IsNullOrEmpty(nextRecordsUrl));

            // Aquí puedes procesar y combinar los resultados de todas las páginas si es necesario
            string combinedResult = CombineResults(results);

            return combinedResult;
        }

        private string ParseNextRecordsUrlFromResponse(string jsonResponse)
        {
            // Analiza el JSON para obtener la URL de la siguiente página (si existe)
            // Esto dependerá de la estructura de la respuesta de Salesforce
            // Ejemplo: busca una clave como "nextRecordsUrl" en el JSON

            // Supongamos que el formato es el siguiente:
            // { "nextRecordsUrl": "/services/data/vXX.X/query/01gXXXXXXXXXXXXXXX-2000" }

            JObject response = JObject.Parse(jsonResponse);
            if (response.TryGetValue("nextRecordsUrl", out JToken nextRecordsUrlToken))
            {
                string nextRecordsUrl = nextRecordsUrlToken.Value<string>();
                return nextRecordsUrl;
            }

            return null;  // Si no hay una URL de siguiente página
        }

        private string CombineResults(List<string> results)
        {
            // Combina y procesa los resultados de todas las páginas si es necesario
            // Esto dependerá de la estructura de los datos que estés recuperando

            // Aquí, simplemente concatenamos los resultados en una sola cadena
            StringBuilder combinedResult = new StringBuilder();
            foreach (string result in results)
            {
                combinedResult.Append(result);
            }

            return combinedResult.ToString();
        }

        private bool UpdateObjectAsync(string objectName, string objectId, string updateMessage)
        {
            try
            {
                string updateUrl = $"{this.salesforceSession.sname}{this.sApiEndpoint}sobjects/{objectName}/{objectId}?_HttpMethod=PATCH";
                HttpRequestMessage requestUpdate = new HttpRequestMessage(HttpMethod.Post, updateUrl);
                HttpContent contentUpdate = new StringContent(updateMessage, Encoding.UTF8, "application/xml");
                requestUpdate.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
                requestUpdate.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                requestUpdate.Content = contentUpdate;
                HttpResponseMessage response = httpClient.SendAsync(requestUpdate).Result;
                // Verifica si la actualización fue exitosa
                if (response.IsSuccessStatusCode)
                {
                    string sresponse = response.Content.ReadAsStringAsync().Result;
                    return (!string.IsNullOrEmpty(sresponse));
                }
                else
                {
                    // Maneja los errores de actualización si es necesario
                    string errorMessage = response.Content.ReadAsStringAsync().Result;
                    throw new Exception($"Error en la actualización del objeto {objectName}: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error en la actualización del objeto {objectName}: {ex.Message}");
            }
        }

        private bool CreateRecordAsync(string objectName, string createMessage)
        {
            try
            {
                string createUrl = $"{this.salesforceSession.sname}{this.sApiEndpoint}sobjects/{objectName}/";
                HttpRequestMessage requestCreate = new HttpRequestMessage(HttpMethod.Post, createUrl);
                HttpContent contentCreate = new StringContent(createMessage, Encoding.UTF8, "application/xml");
                requestCreate.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
                requestCreate.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
                requestCreate.Content = contentCreate;
                HttpResponseMessage response = httpClient.SendAsync(requestCreate).Result;
                // Verifica si la creación fue exitosa
                if (response.IsSuccessStatusCode)
                {
                    string sresponse = response.Content.ReadAsStringAsync().Result;
                    return (!string.IsNullOrEmpty(sresponse));
                }
                else
                {
                    // Maneja los errores de creación si es necesario
                    string errorMessage = response.Content.ReadAsStringAsync().Result;
                    throw new Exception($"Error en la creación del objeto {objectName}: {errorMessage}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error en la creación del objeto {objectName}: {ex.Message}");
            }
        }

        #endregion

        #region Métodos de integración con el Digiturno5 para consultar el paciente en Salesforce

        private void UpdateAppointmentForTurnSelection(Appointment__c appointment, SalesforceIntegrator integrator)
        {
            StringBuilder stringBuilder = new StringBuilder("<root>");
            stringBuilder.Append("<IsRemoteTurn__c>true</IsRemoteTurn__c>");
            stringBuilder.Append("<noTurnSelect__c>true</noTurnSelect__c>");
            stringBuilder.Append("</root>");
            bool bUpdate = this.UpdateObjectAsync("Appointment__c", appointment.Id, stringBuilder.ToString());
        }

        public List<Generic> UpdateListAppoinments(List<Appointment> appointments)
        {
            string appointmentIds = string.Join("','", appointments.Select(a => a.id));
            List<Generic> list = new List<Generic>();
            List<Appointment__c> lappointments = null;
            SalesforceIntegrator integrator = new SalesforceIntegrator();
            StringBuilder query = new StringBuilder("SELECT Id, PlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c" +
                ", GroupId__r.Needspreassessment__c, FNC_RequierePreconsulta__c, FNC_prefacturado__c, ServiceBilled__c, WhatId__r.Age2__pc " +
                ", AgendaId__r.Name, AgreementId__r.Name, StartDatetime__c FROM Appointment__c WHERE Id IN ('");
            query.Append(appointmentIds);
            query.Append("')");
            string jsonResponse = this.QueryRecordAsync(this.httpClient, query.ToString());
            SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
            lappointments = response.Records;
            foreach (Appointment__c appointment in lappointments)
            {
                this.battended = false;
                Generic generic = new Generic()
                {
                    scode = appointment.Id,
                };
                generic.sname = this.UpdateAppointment(appointment, integrator, lappointments);
                generic.iid = this.battended ? 1 : 0;
                generic.sextra1 = this.sdirection;
                list.Add(generic);
            }
            return list;
        }

        private string UpdateAppointment(Appointment__c appointment, SalesforceIntegrator integrator, List<Appointment__c> lappointments, bool bisremote = false)
        {
            /*if (integrator.ValidateAppointment(appointment, bisremote, 0) == 0)
            {
                return "Cita vencida";
            }*/
            if (appointment.ServiceBilled__c.Value)
            {
                this.sdirection = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, true, true, appointment.PlanName__c, appointment.IsCoPayment__c.Value, appointment.AgendaId__r.Name, appointment.AgreementId__r.Name);
                return "Cita facturada";
            }
            try
            {
                // Estado por defecto - siempre inicia como "Asistió"
                string sStatus = "Asistió";

                // Obtener valores con validaciones de null
                bool bCopayment = appointment.IsCoPayment__c.HasValue ? appointment.IsCoPayment__c.Value : true;
                bool bNeedsPre = (appointment.GroupId__r.Needspreassessment__c.HasValue) ? appointment.GroupId__r.Needspreassessment__c.Value : false;
                bool bAppointmentPre = (appointment.FNC_RequierePreconsulta__c.HasValue) ? appointment.FNC_RequierePreconsulta__c.Value : false;
                bool bPrefacturado = (appointment.FNC_prefacturado__c != null) ? appointment.FNC_prefacturado__c.Value : false;
                if (bPrefacturado)
                {
                    if (appointment.prefacturadoApp__c.HasValue)
                    {
                        bPrefacturado = (appointment.prefacturadoApp__c.Value && bPrefacturado);
                    }
                }
                // Determinar si necesita factura
                bool bNeedsInvoice = integrator.NeedsInvoice(
                    appointment.PlanName__c,
                    appointment.GroupId__r?.Name,
                    appointment.AuthorizationCode__c,
                    bCopayment,
                    appointment.WhatId__r.Age2__pc.HasValue ? appointment.WhatId__r.Age2__pc.Value : 0
                );

                // Determinar si necesita pre-consulta
                bool bNeedsPreassessment = this.NeeedsPreAssessment(
                    appointment.GroupId__r?.Name,
                    Convert.ToInt32(appointment.WhatId__r?.Age2__pc ?? 0),
                    appointment.AgendaId__r?.Name,
                    lappointments,
                    (bNeedsPre && bAppointmentPre)
                );

                // Construir el XML de actualización
                StringBuilder stringBuilder = BuildBaseUpdateXml();

                // Determinar el estado final y campos adicionales
                AppendStatusSpecificFields(stringBuilder, bNeedsInvoice, bNeedsPreassessment, bisremote, bPrefacturado, out sStatus);

                // Cerrar XML con el estado final
                stringBuilder.Append($"<Status__c>{sStatus}</Status__c>");
                stringBuilder.Append("</root>");

                if (sStatus == "Facturada" || sStatus == "Pre Consulta")
                {
                    this.sdirection = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, false, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name, appointment.AgreementId__r.Name);
                }

                // Actualizar en Salesforce
                bool bUpdateResult = this.UpdateObjectAsync("Appointment__c", appointment.Id, stringBuilder.ToString());

                // Log opcional para debugging
                LogAppointmentUpdate(appointment, sStatus, bNeedsInvoice, bNeedsPreassessment, bUpdateResult);
                return $"Cita {sStatus}";
            }
            catch (Exception ex)
            {
                // Manejo de errores
                LogError.WriteMessage("Application", "WSInspira", $"Error updating appointment {appointment.Id}: {ex.Message}");
                throw;
            }
        }

        private StringBuilder BuildBaseUpdateXml()
        {
            StringBuilder stringBuilder = new StringBuilder("<root>");
            string currentDateTime = DateTime.Now.AddHours(5).ToString("yyyy-MM-ddTHH:mm:ss") + ".000Z";

            stringBuilder.Append("<PatientAttended__c>true</PatientAttended__c>");
            stringBuilder.Append("<noTurnSelect__c>true</noTurnSelect__c>");
            stringBuilder.Append($"<AttendedStartDatetime__c>{currentDateTime}</AttendedStartDatetime__c>");
            stringBuilder.Append($"<BilledStartDate__c>{currentDateTime}</BilledStartDate__c>");

            return stringBuilder;
        }

        private void AppendStatusSpecificFields(StringBuilder stringBuilder, bool bNeedsInvoice, bool bNeedsPreassessment,
                                              bool bisremote, bool bPrefacturado, out string sStatus)
        {
            string currentDateTime = DateTime.Now.AddHours(5).ToString("yyyy-MM-ddTHH:mm:ss") + ".000Z";

            // Lógica de estados basada en las condiciones
            if (bNeedsInvoice && bNeedsPreassessment)
            {
                // Caso: Necesita factura Y preconsulta
                sStatus = "Pre Consulta";
                AppendInvoicedFields(stringBuilder, currentDateTime);
                stringBuilder.Append("<patient_pre__c>true</patient_pre__c>");
            }
            else if ((bNeedsInvoice || bPrefacturado) && !bNeedsPreassessment && !bisremote)
            {
                // Caso: Necesita factura (o ya está prefacturado) pero NO preconsulta y NO es remoto
                sStatus = "Facturada";
                AppendInvoicedFields(stringBuilder, currentDateTime);
            }
            else if (bNeedsInvoice && bisremote)
            {
                // Caso: Necesita factura Y es remoto
                sStatus = "Prefacturado";
                stringBuilder.Append("<FNC_prefacturado__c>true</FNC_prefacturado__c>");
                if (bisremote)
                {
                    stringBuilder.Append("<prefacturadoApp__c>true</prefacturadoApp__c>");
                }
            }
            else
            {
                // Caso por defecto: mantiene "Asistió"
                sStatus = "Asistió";
                this.battended = true;
            }
        }

        private void AppendInvoicedFields(StringBuilder stringBuilder, string currentDateTime)
        {
            stringBuilder.Append("<PatientWaiting__c>true</PatientWaiting__c>");
            stringBuilder.Append("<FNC_prefacturado__c>false</FNC_prefacturado__c>");
            stringBuilder.Append($"<WaitingStartDate__c>{currentDateTime}</WaitingStartDate__c>");
            stringBuilder.Append($"<BilledEndDate__c>{currentDateTime}</BilledEndDate__c>");
            stringBuilder.Append("<ServiceBilled__c>true</ServiceBilled__c>");
        }

        private void LogAppointmentUpdate(Appointment__c appointment, string status, bool needsInvoice, bool needsPreassessment, bool updateResult)
        {
            string logMessage = $"Appointment Update - " +
                               $"Patient: {appointment.WhatId__r?.DocumentNumber__c}, " +
                               $"Group: {appointment.GroupId__r?.Name}, " +
                               $"Agenda: {appointment.AgendaId__r?.Name}, " +
                               $"Status: {status}, " +
                               $"Needs Invoice: {needsInvoice}, " +
                               $"Needs Preassessment: {needsPreassessment}, " +
                               $"Update Result: {updateResult}";

            LogError.WriteMessage("Application", "WSInspira", logMessage);
        }

        public void SetTurnAppointments(List<string> appointments, string turnnumber, int turnid)
        {
            StringBuilder stringBuilder = new StringBuilder();
            foreach (string appointment in appointments)
            {
                try
                {
                    stringBuilder.Append("<root>");
                    stringBuilder.Append($"<TurnNumber__c>{turnnumber}</TurnNumber__c>");
                    stringBuilder.Append($"<MotivoNoTurno__c>{turnid}</MotivoNoTurno__c>");
                    stringBuilder.Append("</root>");
                    this.UpdateObjectAsync("Appointment__c", appointment, stringBuilder.ToString());
                    stringBuilder.Clear();
                }
                catch (Exception ex)
                {
                    LogError.WriteError("Application", "WSInspira", ex);
                }
            }
        }

        public Digiturno5 GetPatientForApp(string sDocumentType, string sDocument, string sConnection, bool bisinlocation, int idistance)
        {
            SalesforceIntegrator integrator = new SalesforceIntegrator();
            Digiturno5 digiturno5 = new Digiturno5();
            int iQue = 0;
            bool bNeedsInvoice = false;
            bool bIsVip = false;
            StringBuilder sQuery = new StringBuilder();
            StringBuilder sResumen = new StringBuilder();
            int ifirstque = 0;
            bool bCopayment = false;
            List<Appointment__c> lappointments = null;
            int iProcedure = -1;
            List<string> appointments = new List<string>();
            try
            {
                //Query que trae la información de las citas del paciente enviado desde el Digiturno (Atril). Se traen las citas en estado Asignada, Confirmada y Prefacturado ordernadas por hora de llegada                
                sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.DocumentNumber__c");
                sQuery.Append(", WhatId__r.DocumentType__c, PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
                //Descomentar esta línea para el paso a producción de Inspira 2.0
                sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c, FNC_prefacturado__c, GroupId__r.Description__c, GroupId__r.Code__c" +
                    ", WhatId__r.State__r.Name, WhatId__r.City__r.Name, WhatId__r.Gender__pc, WhatId__r.Phone, WhatId__r.Address__c" +
                    ", AgreementId__r.Code__c, WhatId__r.PersonEmail, WhatId__r.PersonBirthdate, PlanId__c, CostCenterId__c, CostCenterId__r.Code__c, PlanId__r.RateId__c, ins2_categoria__c, FNC_RequierePreconsulta__c" +
                    " FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                //Comentar esta línea para el paso a producción de Inspira 2.0
                //sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                sQuery.Append(sDocument);
                sQuery.Append("' AND WhatId__r.DocumentType__c = '");
                sQuery.Append(sDocumentType);
                sQuery.Append("' AND ActivityDate__c = ");
                sQuery.Append(DateTime.Now.ToString("yyyy-MM-dd"));
                //sQuery.Append("2024-09-05");
                //Descomentar esta línea para el paso a producción de Inspira 2.0
                sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                //Comentar esta línea para el paso a producción de Inspira 2.0
                //sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado', 'Facturada', 'En Atención') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                string jsonResponse = this.QueryRecordAsync(this.httpClient, sQuery.ToString());
                SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
                lappointments = response.Records;
                //Si el paciente tiene citas activas recorremos las citas, validamos la hora de llegada y damos asistencia y facturado a las citas que correspondan
                if (lappointments.Count > 0)
                {
                    int i = 0;
                    int j = 0;
                    foreach (Appointment__c appointment in lappointments)
                    {
                        //Se valida si el paciente es de salas de cirugía
                        if (appointment.AgendaId__r.Name.Contains("SALA"))
                        {
                            iProcedure = j;
                            //LogError.WriteMessage("Application", "WSInspira", "Paciente para enviar a synapse:" + appointment.WhatId__r.DocumentNumber__c);
                        }
                        //Se valida hora de asistencia de la cita, si la hora de llegada del paciente es menor igual o mayor igual a 10 minutos de la hora de llegada de la cita se retorna cola urgente, si no se retorna cola general
                        iQue = integrator.ValidateAppointment(appointment, true, idistance);
                        //Si la cita es válida tiene cola permitida
                        if (iQue != 0)
                        {
                            //Se asigna la primer cola para que en el caso de que el paciente tenga cita urgente se entregue turno urgente
                            if (i == 0) ifirstque = iQue;
                            //Se actualiza el estado de la cita a asistió o a facturado
                            this.UpdateAppointmentForTurnSelection(appointment, integrator);
                            appointments.Add(appointment.Id);
                            i++;
                        }
                        j++;
                    }
                    if (i > 0)
                    {
                        Appointment__c appointment = (iProcedure == -1) ? lappointments[0] : lappointments[iProcedure];
                        PatientCiel oPatient = new PatientCiel()
                        {
                            sfirstname = appointment.WhatId__r.FirstName_c__pc,
                            ssecondname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondName__pc)) ? string.Empty : appointment.WhatId__r.SecondName__pc,
                            sfirstsurname = appointment.WhatId__r.FirstSurname__pc,
                            ssecondsurname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondSurname__pc)) ? string.Empty : appointment.WhatId__r.SecondSurname__pc,
                            iplan = this.GetPlanAsync(appointment.PlanId__c),
                            iunit = integrator.GetUnit(appointment.PlanName__c, appointment.GroupId__r.Name, bNeedsInvoice, appointment.AgendaId__r.Name.ToUpper(), appointment.WhatId__r.Age2__pc.Value),
                            iattendance = ifirstque,
                        };
                        digiturno5.oPatient = oPatient;
                        digiturno5.sappointments = string.Join(",", appointments);
                        //En caso de que todo vaya bien, se obtiene el direccionamiento del paciente para mostrar el mensaje en la máquina de turnos acerca de dónde debe dirigirse para facturación o atención
                        digiturno5.oResult = new Result()
                        {
                            iresult = 1,
                            smessage = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
                            iroom = integrator.GetRoom(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
                        };
                    }
                    //Si no existen citas validas
                    else if (i == 0)
                    {
                        digiturno5.oResult = new Result() { iresult = 0, smessage = FNCSalesforce.Properties.Settings.Default.MessageTooLate };
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
                digiturno5.oResult = new Result() { iresult = 0, smessage = "Ha ocurrido un error al consultar las citas del paciente" };
            }
            return digiturno5;
        }

        /// <summary>
        /// Método que verifica en Salesforce si un paciente tiene citas, retorna el objeto Digiturno5 con la información del paciente y los datos de la cola en caso de encontrar paciente que cumpla 
        /// con las condiciones
        /// </summary>
        /// <param name="sDocumentType">String tipo de documento del paciente</param>
        /// <param name="sDocument">String documento</param>
        /// <param name="sConnection">Strin cadena de conexión</param>
        /// <param name="sappointment">Strin id de la cita</param>
        /// <returns>Objeto Digiturno5</returns>
        public Digiturno5 GetPatientAsync(string sDocumentType, string sDocument, string sConnection, string sappointment = "")
        {
            SalesforceIntegrator integrator = new SalesforceIntegrator();
            Digiturno5 digiturno5 = new Digiturno5();
            int iQue = 0;
            bool bNeedsInvoice = false;
            bool bIsVip = false;
            StringBuilder sQuery = new StringBuilder();
            StringBuilder sResumen = new StringBuilder();
            int ifirstque = 0;
            bool bCopayment = false;
            bool bQue = true, bIsRHB = false;
            List<Appointment__c> lappointments = null;
            List<string> appointments = new List<string>();
            int iProcedure = -1;
            try
            {
                if (string.IsNullOrEmpty(sappointment))
                {
                    //Query que trae la información de las citas del paciente enviado desde el Digiturno (Atril). Se traen las citas en estado Asignada, Confirmada y Prefacturado ordernadas por hora de llegada                
                    sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.DocumentNumber__c");
                    sQuery.Append(",  WhatId__r.DocumentType__c, PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
                    //Descomentar esta línea para el paso a producción de Inspira 2.0
                    sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c, FNC_prefacturado__c, GroupId__r.Description__c, GroupId__r.Code__c" +
                        ", WhatId__r.State__r.Name, WhatId__r.City__r.Name, WhatId__r.Gender__pc, WhatId__r.Phone, WhatId__r.Address__c" +
                        ", AgreementId__r.Code__c, WhatId__r.PersonEmail, WhatId__r.PersonBirthdate, PlanId__c, CostCenterId__c, CostCenterId__r.Code__c, PlanId__r.RateId__c, ins2_categoria__c, FNC_RequierePreconsulta__c" +
                        " FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                    //Comentar esta línea para el paso a producción de Inspira 2.0
                    //sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                    sQuery.Append(sDocument);
                    sQuery.Append("' AND WhatId__r.DocumentType__c = '");
                    sQuery.Append(sDocumentType);
                    sQuery.Append("' AND ActivityDate__c = ");
                    sQuery.Append(DateTime.Now.ToString("yyyy-MM-dd"));
                    //sQuery.Append("2024-09-05");
                    //Descomentar esta línea para el paso a producción de Inspira 2.0
                    sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                    //Comentar esta línea para el paso a producción de Inspira 2.0
                    //sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado', 'Facturada', 'En Atención') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                }
                else
                {
                    sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.DocumentNumber__c");
                    sQuery.Append(",  WhatId__r.DocumentType__c, PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
                    //Descomentar esta línea para el paso a producción de Inspira 2.0
                    sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c, FNC_prefacturado__c, GroupId__r.Description__c, GroupId__r.Code__c" +
                        ", WhatId__r.State__r.Name, WhatId__r.City__r.Name, WhatId__r.Gender__pc, WhatId__r.Phone, WhatId__r.Address__c" +
                        ", AgreementId__r.Code__c, WhatId__r.PersonEmail, WhatId__r.PersonBirthdate, PlanId__c, CostCenterId__c, CostCenterId__r.Code__c, PlanId__r.RateId__c, ins2_categoria__c, FNC_RequierePreconsulta__c" +
                        " FROM Appointment__c WHERE Id ='");
                    sQuery.Append(sappointment);
                    sQuery.Append("'");
                    sQuery.Append("' AND ActivityDate__c = ");
                    sQuery.Append(DateTime.Now.ToString("yyyy-MM-dd"));
                    sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                }
                string jsonResponse = this.QueryRecordAsync(this.httpClient, sQuery.ToString());
                SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
                lappointments = response.Records;
                //Si el paciente tiene citas activas recorremos las citas, validamos la hora de llegada y damos asistencia y facturado a las citas que correspondan
                if (lappointments.Count > 0)
                {
                    int i = 0;
                    int j = 0;
                    foreach (Appointment__c appointment in lappointments)
                    {
                        //Se valida si el paciente es de salas de cirugía
                        if (appointment.AgendaId__r.Name.Contains("SALA"))
                        {
                            iProcedure = j;
                            //LogError.WriteMessage("Application", "WSInspira", "Paciente para enviar a synapse:" + appointment.WhatId__r.DocumentNumber__c);
                        }
                        //Se valida hora de asistencia de la cita, si la hora de llegada del paciente es menor igual o mayor igual a 10 minutos de la hora de llegada de la cita se retorna cola urgente, si no se retorna cola general
                        iQue = integrator.ValidateAppointment(appointment);
                        //Si la cita es válida tiene cola permitida
                        if (iQue != 0)
                        {
                            //Se asigna la primer cola para que en el caso de que el paciente tenga cita urgente se entregue turno urgente
                            if (i == 0) ifirstque = iQue;
                            //Se actualiza el estado de la cita a asistió o a facturado
                            this.UpdateAppointment(appointment, integrator, lappointments);
                            i++;
                        }
                        j++;
                    }
                    //Se vuelven a recorrer las citas, esta vez para validar si es necesario entregar turno o no. En caso de que haya una cita que requiere pasar por cajas se entrega turno
                    foreach (Appointment__c appointment in lappointments)
                    {
                        iQue = integrator.ValidateAppointment(appointment);
                        if (iQue != 0)
                        {
                            sResumen.AppendLine(appointment.GroupId__r.Description__c);
                            bCopayment = appointment.IsCoPayment__c.HasValue ? appointment.IsCoPayment__c.Value : false;
                            bNeedsInvoice = integrator.NeedsInvoice(appointment.PlanName__c, appointment.GroupId__r.Name, appointment.AuthorizationCode__c, bCopayment, appointment.WhatId__r.Age2__pc.Value);
                            appointments.Add(appointment.Id);
                            if (bNeedsInvoice || appointment.FNC_prefacturado__c.Value)
                            {
                                bQue = false;
                            }
                            else if (integrator.IsRhb(appointment))
                            {
                                bQue = false;
                                bIsRHB = true;
                                break;
                            }
                            else
                            {
                                bQue = true;
                                break;
                            }
                        }
                    }
                    digiturno5.sappointments = string.Join(",", appointments);
                    //Si existen citas válidas y hay que pasar por caja
                    if (i > 0 && bQue)
                    {
                        Appointment__c appointment = (iProcedure == -1) ? lappointments[0] : lappointments[iProcedure];
                        PatientCiel oPatient = new PatientCiel()
                        {
                            sfirstname = appointment.WhatId__r.FirstName_c__pc,
                            ssecondname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondName__pc)) ? string.Empty : appointment.WhatId__r.SecondName__pc,
                            sfirstsurname = appointment.WhatId__r.FirstSurname__pc,
                            ssecondsurname = (string.IsNullOrEmpty(appointment.WhatId__r.SecondSurname__pc)) ? string.Empty : appointment.WhatId__r.SecondSurname__pc,
                            iplan = this.GetPlanAsync(appointment.PlanId__c),
                            iunit = integrator.GetUnit(appointment.PlanName__c, appointment.GroupId__r.Name, bNeedsInvoice, appointment.AgendaId__r.Name.ToUpper(), appointment.WhatId__r.Age2__pc.Value),
                            iattendance = ifirstque,
                        };

                        digiturno5.oPatient = oPatient;
                        //En caso de que todo vaya bien, se obtiene el direccionamiento del paciente para mostrar el mensaje en la máquina de turnos acerca de dónde debe dirigirse para facturación o atención
                        digiturno5.oResult = new Result()
                        {
                            iresult = 1,
                            smessage = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
                            iroom = integrator.GetRoom(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
                        };
                    }
                    //Si existen citas válidas y no hay que pasar por cajas
                    else if (i > 0 && !bQue)
                    {
                        digiturno5.oPatient = new PatientCiel();
                        string smessage = string.Empty;
                        if (!bIsRHB)
                        {
                            smessage = "Su cita no requiere facturación, diríjase directamente a su lugar de atención \n";
                        }
                        else
                        {
                            smessage = "Diríjase al Piso 4 Caja Rehabilitación Pulmonar \n";
                        }
                        StringBuilder stringBuilder = new StringBuilder(smessage);
                        if (sResumen.Length > 0 && !bIsRHB)
                        {
                            stringBuilder.Append("Resumen de Servicios: \n" + sResumen.ToString());
                        }
                        digiturno5.oResult = new Result() { iresult = 0, smessage = stringBuilder.ToString() };
                        return digiturno5;
                    }
                    //Si no existen citas validas
                    else if (i == 0)
                    {
                        digiturno5.oResult = new Result() { iresult = 0, smessage = FNCSalesforce.Properties.Settings.Default.MessageTooLate };
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
                digiturno5.oResult = new Result() { iresult = 0, smessage = "Ha ocurrido un error al consultar las citas del paciente" };
            }
            //Pasé este fragmento de código para este sitio para garantizar que se genere turno por si la conexión con Synapse falla
            if (iProcedure > -1)
            {
                try
                {
                    int iresult = this.SendAppointmentToSynapse(lappointments[iProcedure]);
                }
                catch (Exception ex)
                {
                    LogError.WriteError("Application", "WSInspira", ex);
                }
            }
            return digiturno5;
        }

        public bool NeeedsPreAssessment(string sGroup, int iAge, string sSchedule, List<Appointment__c> lappointments, bool bNeedsPre)
        {
            /*bool bFlag = false;
            //if (iAge < 18)
            {
                for (int i = 0; i < lappointments.Count && !bFlag; i++)
                {
                    Appointment__c appointment = lappointments[i];
                    bFlag = (appointment.AgendaId__r.Name.ToUpper().Contains("VMAX") || appointment.GroupId__r.Name.Contains("PSICOLOGIA") || appointment.AgendaId__r.Name.ToUpper().Contains("PFP"));
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
            //else if (iAge < 18 && bNeedsPre)
            if (bNeedsPre)
            {
                return true;
            }
            return false;   */
            return bNeedsPre;
        }

        private int GetPlanAsync(string sIdPlan)
        {
            StringBuilder sQuery = new StringBuilder("SELECT HealthCarePlanId__r.Name, Tipo_Tarifa__c FROM Plan__c WHERE Id = '");
            sQuery.Append(sIdPlan);
            sQuery.Append("'");
            string jsonResponse = this.QueryRecordAsync(this.httpClient, sQuery.ToString());
            SalesforceResponse<Plan__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Plan__c>>(jsonResponse);
            List<Plan__c> lPlans = response.Records;
            if (lPlans.Count != 0)
            {
                Plan__c healthCarePlan = lPlans[0];
                if (healthCarePlan.HealthCarePlanId__r.Name.Contains("FCI"))
                {
                    if (healthCarePlan.HealthCarePlanId__r.Name.Contains("ECOPETROL"))
                    {
                        return 14;
                    }
                    else
                    {
                        return 11;
                    }
                }
                else if (healthCarePlan.Tipo_Tarifa__c == "PREPAGADA" || healthCarePlan.Tipo_Tarifa__c == "PARTICULAR") return 14;
                else if (healthCarePlan.Tipo_Tarifa__c == "PBS" || healthCarePlan.Tipo_Tarifa__c.Contains("ARL")) return 13;
                else return 13;
            }
            return 13;
        }

        #endregion

        #region Métodos para cargos de programas

        private List<ProductsByGroup__c> GetProductsInfo(string sgroup, string splan, string scostcenter, string srate, string sunit, string splanname, string scategory = "")
        {
            StringBuilder stringBuilder = new StringBuilder();
            List<ProductsByGroup__c> productsByGroups = new List<ProductsByGroup__c>();
            try
            {
                if (!splanname.Contains("PROTOCOLO"))
                {
                    stringBuilder.Append("SELECT Grupo_por_Plan__r.Grupo__c, Grupo_por_Plan__r.Grupo__r.Name, Grupo_por_Plan__r.Plan__c, Tarifa_concepto_producto__r.CostCenterId__c" +
                        ", Tarifa_concepto_producto__r.ConceptId__c" + ", Tarifa_concepto_producto__r.RateId__c, Tarifa_concepto_producto__r.RateId__r.Code__c" +
                        ", Tarifa_concepto_producto__r.ProductId__c, Tarifa_concepto_producto__r.ProductId__r.Name, Tarifa_concepto_producto__r.ProductId__r.Name__c" +
                        ", Tarifa_concepto_producto__r.CostCenterId__r.Code__c, Tarifa_concepto_producto__r.RateId__r.Name, Tarifa_concepto_producto__r.Value__c, Tarifa_concepto_producto__r.ConceptId__r.Code__c FROM ProductsByGroup__c");
                    stringBuilder.Append(" WHERE Tarifa_concepto_producto__r.RateId__c = '" + srate + "' AND Grupo_por_Plan__c <> '' AND Grupo_por_Plan__r.Grupo__c = '" + sgroup
                                            + "' AND Grupo_por_Plan__r.Plan__c = '" + splan + "' AND Tarifa_concepto_producto__r.CostCenterId__r.Code__c IN('" + scostcenter + "')");
                    //" AND Tarifa_concepto_producto__r.ConceptId__r.FunctionalUnit__r.Code__c = '" + sunit + "' AND Grupo_por_Plan__r.FNC_Categoria__c = '" + scategory + "'"););
                    if (!string.IsNullOrEmpty(sunit))
                    {
                        stringBuilder.Append(" AND Tarifa_concepto_producto__r.ConceptId__r.FunctionalUnit__r.Code__c = '" + sunit + "'");
                    }
                    if (!string.IsNullOrEmpty(scategory))
                    {
                        stringBuilder.Append(" AND Grupo_por_Plan__r.FNC_Categoria__c = '" + scategory + "'");
                    }
                }
                else
                {
                    stringBuilder.Append("SELECT Grupo_por_Plan__r.Grupo__c, Grupo_por_Plan__r.Grupo__r.Name, Grupo_por_Plan__r.Plan__c, Tarifa_concepto_producto__r.CostCenterId__c" +
                        ", Tarifa_concepto_producto__r.ConceptId__c" + ", Tarifa_concepto_producto__r.RateId__c, Tarifa_concepto_producto__r.RateId__r.Code__c" +
                        ", Tarifa_concepto_producto__r.ProductId__c, Tarifa_concepto_producto__r.ProductId__r.Name, Tarifa_concepto_producto__r.ProductId__r.Name__c" +
                        ", Tarifa_concepto_producto__r.CostCenterId__r.Code__c, Tarifa_concepto_producto__r.RateId__r.Name, Tarifa_concepto_producto__r.Value__c, Tarifa_concepto_producto__r.ConceptId__r.Code__c FROM ProductsByGroup__c");
                    stringBuilder.Append(" WHERE Tarifa_concepto_producto__r.RateId__c = '" + srate + "' AND Grupo_por_Plan__c <> '' AND Grupo_por_Plan__r.Grupo__c = '" + sgroup
                                            + "' AND Grupo_por_Plan__r.Plan__c = '" + splan + "'");
                }
                var jsonResponse = this.QueryRecordAsync(this.httpClient, stringBuilder.ToString());
                SalesforceResponse<ProductsByGroup__c> response = JsonConvert.DeserializeObject<SalesforceResponse<ProductsByGroup__c>>(jsonResponse);
                if (response.Records.Count > 0)
                {
                    productsByGroups.AddRange(response.Records);
                    //return GetProductsInfo(productsByGroups, sgroup, splan, scostcenter);
                }
                return productsByGroups;
            }
            catch (Exception ex)
            {
                LogError.WriteError("CargoProgramas", "Application", ex);
                throw;
            }
            finally
            {
                stringBuilder = null;
            }
        }

        /// <summary>
        /// Método que obtiene los productos por grupo anteriores a la versión 2.0 de inspira
        /// </summary>
        /// <param name="sgroupname">String nombre del grupo</param>
        /// <returns>Lista genérica con los productos por grupo</returns>
        private List<ProductsByGroup__c> GetOldProducts(string sgroupname)
        {
            List<ProductsByGroup__c> productsByGroup__Cs = new List<ProductsByGroup__c>();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("SELECT Id, GroupId__c, GroupId__r.Name, GroupId__r.Code__c, ProductId__r.Name, ProductId__r.Name__c, ProductId__r.IsNoPos__c" +
                                " , Centro_de_Costos__r.Name, Centro_de_Costos__r.Code__c, Centro_de_Costos__c, ProductId__c FROM ProductsByGroup__c WHERE GroupId__r.Name LIKE '" + sgroupname + "%'");
            try
            {
                var jsonResponse = this.QueryRecordAsync(this.httpClient, HttpUtility.UrlEncode(stringBuilder.ToString(), Encoding.UTF8));
                SalesforceResponse<ProductsByGroup__c> response = JsonConvert.DeserializeObject<SalesforceResponse<ProductsByGroup__c>>(jsonResponse);
                if (response.Records.Count > 0)
                {
                    productsByGroup__Cs.AddRange(response.Records);
                    //return GetProductsInfo(productsByGroups, sgroup, splan, scostcenter);
                }
                return productsByGroup__Cs;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                stringBuilder = null;
            }
        }

        public List<ServintePatient> GetPatientsforPrograms(string sInitialDate, string sFinalDate, string sErrorFile, bool bIsFamisanar = false, string sId = "")
        {
            List<Appointment__c> lAppointments = new List<Appointment__c>();
            List<ServintePatient> lservintePatients = new List<ServintePatient>();
            ServintePatient servintePatient = null;
            List<ProductsByGroup__c> productsByGroups = new List<ProductsByGroup__c>();
            string sunit = string.Empty;
            string scostcenter = string.Empty;
            string[] arrayList = null;
            string sfunctionalunit = string.Empty;
            string sType = "Programas";
            ArrayList serrors = new ArrayList();
            List<ServiceRequest> serviceRequests = new List<ServiceRequest>();
            StringBuilder stringBuilder = new StringBuilder("SELECT Id, WhatId__c, WhatId__r.Address__c, WhatId__r.DocumentNumber__c, WhatId__r.DocumentType__c, WhatId__r.FirstName, WhatId__r.FirstName_c__pc");
            stringBuilder.Append(", WhatId__r.FirstSurname__pc, WhatId__r.Gender__pc, WhatId__r.MiddleName, WhatId__r.Ocupation__pc, WhatId__r.PersonBirthdate, WhatId__r.PersonEmail ,WhatId__r.PersonMobilePhone");
            stringBuilder.Append(", WhatId__r.SecondName__pc, WhatId__r.SecondSurname__pc, GroupId__c, GroupId__r.Name, PlanId__r.Name, PlanId__r.HealthCarePlanId__r.Code__c, PlanId__c");
            stringBuilder.Append(", PlanId__r.AgreementId__r.Code__c, PlanId__r.AgreementId__r.Name, PlanId__r.RateId__r.Code__c, WhatId__r.Age2__pc, ActivityDate__c");
            stringBuilder.Append(", AgendaId__r.ProfessionalId__r.DocumentNumber__c, PlanId__r.RateId__r.Name, PlanId__r.HealthCarePlanId__r.Name, PlanId__r.RateId__c, ScheduleId__r.FNC_CentroCostos__c " +
                                " , AgendaId__r.Name, ins2_categoria__c FROM Appointment__c WHERE PatientAttended__c = true AND FNC_MainAppointment__c = true");
            if (!string.IsNullOrEmpty(sId))
            {
                stringBuilder.Append(" AND Id IN (" + sId + ")");
            }
            else
            {
                if (bIsFamisanar)
                {
                    stringBuilder.Append(" AND GroupId__r.Statistics__c = true");
                    stringBuilder.Append(" AND (PlanId__r.Name LIKE '%FAMISANAR AIREPOC%')");
                    stringBuilder.Append(" AND ActivityDate__c >= " + sInitialDate + " AND ActivityDate__c <= " + sFinalDate);
                }
                else
                {
                    /*stringBuilder.Append(" AND (PlanId__r.Name LIKE '%PROTOCOLO%' OR PlanId__r.Name LIKE '%SANITAS AIREPOC%' OR PlanId__r.Name LIKE '%VASCU%' OR PlanId__r.Name LIKE '%HTP%' OR PlanId__r.Name LIKE '%COOMEVA MP ASMAIRE%'");
                    stringBuilder.Append(" OR GroupId__r.Name LIKE '%VALORACIÓN ANESTESIA%' OR GroupId__r.Name LIKE '%VALORACIÓN FBC%' OR PlanId__r.Name LIKE '%PROTOCOLO%'");
                    stringBuilder.Append(" OR GroupId__r.Name LIKE '%VALORACIÓN ANESTESIA%' OR GroupId__r.Name LIKE '%VALORACIÓN FBC%'");
                    stringBuilder.Append(" OR PlanId__r.Name LIKE '%SANITAS ASMAIRE%' OR PlanId__r.Name LIKE '%COOMEVA AIREPOC%' OR PlanId__r.Name LIKE '%ALIANSALUD OXIGENAR%'");
                    stringBuilder.Append(" OR PlanId__r.Name LIKE '%COLMEDICA AIREPOC%' OR PlanId__r.Name LIKE '%FNC INDICE INTEGRACIÓN UCI%' OR PlanId__r.Name LIKE '%FNC SANITAS VMI%'");
                    stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC VENTIL MECANICA INTERMITEN%' OR PlanId__r.Name LIKE '%FNC ECOPETROL ASMAIRE%' OR PlanId__r.Name LIKE '%FNC ECOPETROL AIREPOC%'");
                    stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC SURA EPS VMI%' OR PlanId__r.Name LIKE '%FNC SURA VMI%' OR PlanId__r.Name LIKE 'FNC ASMAIRE%'");
                    //stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC COOMEVA MP AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES ASMAIRE%') " +
                    //" AND AgendaId__r.Name <> 'INVESTIGACION'");
                    stringBuilder.Append(" OR PlanId__r.Name LIKE '%FNC COOMEVA MP AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES AIREPOC%' OR PlanId__r.Name LIKE '%FNC SANITAS PES ASMAIRE%')");
                    */
                    stringBuilder.Append(" AND PlanId__r.HealthCarePlanId__r.Code__c = '434'");
                    stringBuilder.Append(" AND WhatId__r.DocumentNumber__c NOT IN ('INVEST', 'BLOQUEO')");
                    stringBuilder.Append(" AND ActivityDate__c >= " + sInitialDate + " AND ActivityDate__c <= " + sFinalDate);
                    //stringBuilder.Append(" AND Id NOT IN ('a036e00000z6MtgAAE', 'a036e00000rHT8DAAW', 'a036e00000z34MKAAY', 'a036e00000z354lAAA', 'a036e00000rHQFSAA4', 'a036e00000zSbXVAA0', 'a036e00000zUrz1AAC', 'a036e00000z3GgbAAE', 'a036e00000z4ejfAAA', 'a036e00000zVOkTAAW', 'a036e00000z4Mq6AAE', 'a036e00000z4gbkAAA', 'a036e00000zSc5jAAC', 'a036e00000z354zAAA', 'a036e00000z3Gv8AAE', 'a036e00000rI2AaAAK', 'a036e00000rIMNlAAO', 'a036e00000rJ3auAAC', 'a036e00000z4ejkAAA', 'a036e00000z4nCCAAY', 'a036e00000z4nCEAAY', 'a036e00000z5w7DAAQ', 'a036e00000zUuz1AAC', 'a036e00000rJY9MAAW', 'a036e00000zU9e3AAC', 'a036e00000zUsYuAAK', 'a036e00000z3537AAA', 'a036e00000rJY9eAAG', 'a036e00000rJYA5AAO', 'a036e00000rKZNxAAO', 'a036e00000zVcrCAAS', 'a036e00000rJXobAAG', 'a036e00000rHAYeAAO', 'a036e00000zUqTrAAK', 'a036e00000rJ0HbAAK', 'a036e00000rIPExAAO', 'a036e00000rI1tsAAC', 'a036e00000rJYAIAA4', 'a036e00000rJYAmAAO', 'a036e00000rJYB0AAO', 'a036e00000rJYBAAA4', 'a036e00000rIsOoAAK', 'a036e00000rJXoUAAW', 'a036e00000rJ2hUAAS', 'a036e00000rJXsKAAW', 'a036e00000zUNSNAA4', 'a036e00000z605CAAQ', 'a036e00000rJYBIAA4', 'a036e00000rJYBLAA4', 'a036e00000rKLJ4AAO', 'a036e00000z5zlFAAQ', 'a036e00000zUa1tAAC', 'a036e00000rJZrUAAW', 'a036e00000rJmXHAA0', 'a036e00000zUNOiAAO', 'a036e00000zTMsSAAW', 'a036e00000rJY5oAAG', 'a036e00000z35RaAAI', 'a036e00000z35RgAAI', 'a036e00000z63LfAAI', 'a036e00000zUoFqAAK', 'a036e00000rJXsqAAG', 'a036e00000rJY5wAAG', 'a036e00000z5zlLAAQ', 'a036e00000rJXoDAAW', 'a036e00000z5znAAAQ', 'a036e00000rHnYCAA0', 'a036e00000rHnXoAAK', 'a036e00000rHnXqAAK', 'a036e00000z5znMAAQ', 'a036e00000rJXsvAAG', 'a036e00000zU9dmAAC', 'a036e00000zUaaEAAS', 'a036e00000rJY5zAAG', 'a036e00000rKFfYAAW', 'a036e00000rJY6IAAW', 'a036e00000rJY6KAAW', 'a036e00000rJY6MAAW', 'a036e00000rJY6NAAW', 'a036e00000z4fElAAI', 'a036e00000z4fEsAAI', 'a036e00000z4fEvAAI', 'a036e00000z4fF0AAI', 'a036e00000zVvsoAAC', 'a036e00000z4fF4AAI', 'a036e00000rJXopAAG', 'a036e00000z34EIAAY', 'a036e00000rIuXeAAK', 'a036e00000rIxnMAAS', 'a036e00000z4ejeAAA', 'a036e00000rJXq7AAG', 'a036e00000rJXqZAAW', 'a036e00000rJbCeAAK', 'a036e00000z34MJAAY', 'a036e00000rJY72AAG', 'a036e00000rJY73AAG', 'a036e00000rJY79AAG', 'a036e00000z35CkAAI', 'a036e00000rJkVSAA0', 'a036e00000z60CAAAY', 'a036e00000z60CBAAY', 'a036e00000z6N2OAAU', 'a036e00000rJZqbAAG', 'a036e00000zUtL7AAK', 'a036e00000zUuomAAC', 'a036e00000zUtMDAA0', 'a036e00000z4elqAAA', 'a036e00000rJY4bAAG', 'a036e00000rINNNAA4', 'a036e00000rJ0tUAAS', 'a036e00000rKEOlAAO', 'a036e00000zUZs0AAG', 'a036e00000z344uAAA', 'a036e00000z344yAAA', 'a036e00000zUqVzAAK', 'a036e00000zSbUQAA0', 'a036e00000zUNOHAA4', 'a036e00000zUIGtAAO', 'a036e00000zUNOjAAO', 'a036e00000rJY3MAAW', 'a036e00000z3iWeAAI', 'a036e00000rJY3OAAW', 'a036e00000zVuDRAA0', 'a036e00000z43SwAAI', 'a036e00000z4f5GAAQ', 'a036e00000zVvrzAAC', 'a036e00000z4egtAAA', 'a036e00000rJXqmAAG', 'a036e00000zVv2yAAC', 'a036e00000z34T5AAI', 'a036e00000rIUbjAAG', 'a036e00000rJY3QAAW', 'a036e00000rJY3RAAW', 'a036e00000z63UaAAI', 'a036e00000z5w7BAAQ', 'a036e00000z5w98AAA', 'a036e00000z605HAAQ', 'a036e00000z605KAAQ', 'a036e00000z6MzbAAE', 'a036e00000zUr6FAAS', 'a036e00000zVvooAAC', 'a036e00000rJXqwAAG', 'a036e00000zUb5sAAC', 'a036e00000rJXrCAAW', 'a036e00000rJXtSAAW', 'a036e00000rJXrKAAW', 'a036e00000z34TBAAY', 'a036e00000rJW2CAAW', 'a036e00000rJXufAAG', 'a036e00000rK2HEAA0', 'a036e00000rJY41AAG', 'a036e00000rJY48AAG', 'a036e00000rJY49AAG', 'a036e00000rJY4GAAW', 'a036e00000rJY4JAAW', 'a036e00000z6N0BAAU', 'a036e00000zVQqnAAG', 'a036e00000rHQYZAA4', 'a036e00000rJY7uAAG', 'a036e00000rJY7vAAG', 'a036e00000rJXrWAAW', 'a036e00000rJXrpAAG', 'a036e00000rJZr0AAG', 'a036e00000rJW2MAAW', 'a036e00000rJY4WAAW', 'a036e00000rJY4hAAG', 'a036e00000rJjM4AAK', 'a036e00000rJjMCAA0', 'a036e00000z4nVXAAY', 'a036e00000z6N0pAAE', 'a036e00000zUoN4AAK', 'a036e00000z3iZqAAI', 'a036e00000rJeD8AAK', 'a036e00000rJizYAAS', 'a036e00000rJjGTAA0', 'a036e00000rJjKGAA0', 'a036e00000z4nHJAAY', 'a036e00000z5znJAAQ', 'a036e00000z34CcAAI', 'a036e00000zVvrmAAC', 'a036e00000rK1W3AAK', 'a036e00000rK2I2AAK', 'a036e00000rHnc7AAC', 'a036e00000z34b1AAA', 'a036e00000zVBWXAA4', 'a036e00000z34b2AAA', 'a036e00000zVBZkAAO', 'a036e00000zUuuDAAS', 'a036e00000z4ex9AAA', 'a036e00000z35gfAAA', 'a036e00000zViZ6AAK', 'a036e00000z3H92AAE', 'a036e00000z4fJ5AAI', 'a036e00000z4fJEAAY', 'a036e00000zVj8IAAS', 'a036e00000rHnchAAC', 'a036e00000rHphsAAC', 'a036e00000rHzYlAAK', 'a036e00000rJYBhAAO', 'a036e00000zScc3AAC', 'a036e00000zUa2sAAC', 'a036e00000rJYBvAAO', 'a036e00000rJYByAAO', 'a036e00000z3ifvAAA', 'a036e00000z60F0AAI', 'a036e000010bxNEAAY', 'a036e00000rJYCrAAO', 'a036e00000rJYCsAAO', 'a036e00000z35iFAAQ', 'a036e000010bcEXAAY', 'a036e00000zVibwAAC', 'a036e00000zVA84AAG', 'a036e00000rJYDGAA4', 'a036e00000rJYDMAA4', 'a036e00000rJYDUAA4', 'a036e00000rJYDVAA4', 'a036e00000rJYDaAAO', 'a036e00000z4nILAAY', 'a036e00000z4nIOAAY', 'a036e00000z4nITAAY', 'a036e00000z4nIUAAY', 'a036e00000z6MyDAAU', 'a036e00000zVvqIAAS', 'a036e00000z34vSAAQ', 'a036e00000z600SAAQ', 'a036e00000rJYDnAAO', 'a036e00000zVAC5AAO', 'a036e00000rJs4nAAC', 'a036e00000rJz7KAAS', 'a036e00000z35bTAAQ', 'a036e00000z34ajAAA', 'a036e00000z4exHAAQ', 'a036e00000z4exLAAQ', 'a036e00000z4exOAAQ', 'a036e00000rHgG9AAK', 'a036e00000z5w4uAAA', 'a036e00000rJY10AAG', 'a036e00000z4fGUAAY', 'a036e00000rKfj6AAC', 'a036e00000z43exAAA', 'a036e00000z35ZjAAI', 'a036e00000zVgKoAAK', 'a036e00000rJXz2AAG', 'a036e00000rJWdAAAW', 'a036e00000zUoJQAA0', 'a036e00000rJY1DAAW', 'a036e00000rJY1FAAW', 'a036e00000rJY1jAAG', 'a036e00000rJY1mAAG', 'a036e00000zVgLOAA0', 'a036e00000z3H63AAE', 'a036e00000rJgrUAAS', 'a036e00000rHQErAAO', 'a036e00000rJXzdAAG', 'a036e00000rJXyoAAG', 'a036e00000z5zzFAAQ', 'a036e00000rJY25AAG', 'a036e00000rJY29AAG', 'a036e00000rJjQRAA0', 'a036e00000rJjQdAAK', 'a036e00000z4fGBAAY', 'a036e00000zVgvjAAC', 'a036e00000zVgvlAAC', 'a036e00000zVgvuAAC', 'a036e00000z4fGWAAY', 'a036e00000rI4uPAAS', 'a036e00000rJXwJAAW', 'a036e00000rIR1jAAG', 'a036e00000z34bdAAA', 'a036e00000rJ3aPAAS', 'a036e00000z34asAAA', 'a036e00000rJXysAAG', 'a036e00000rJxMYAA0', 'a036e00000z6N3sAAE', 'a036e00000rJYBqAAO', 'a036e00000zViv1AAC', 'a036e00000rJfT5AAK', 'a036e00000zUNUsAAO', 'a036e00000zUNQEAA4', 'a036e00000z5w2UAAQ', 'a036e00000zVNB0AAO', 'a036e00000zVN9yAAG', 'a036e00000rJmfkAAC', 'a036e00000z35ZdAAI', 'a036e00000z35ZhAAI', 'a036e00000rKWztAAG', 'a036e00000rJXwGAAW', 'a036e00000z5zxjAAA', 'a036e00000rJXzYAAW', 'a036e00000z34vPAAQ', 'a036e000010c0YaAAI', 'a036e00000z60HCAAY', 'a036e00000rJYEQAA4', 'a036e00000rJXwzAAG', 'a036e00000rJXx9AAG', 'a036e00000rJXxCAAW', 'a036e00000rJY0FAAW', 'a036e00000z4ezyAAA', 'a036e00000z4ezoAAA', 'a036e00000z4f06AAA', 'a036e00000rJYEVAA4', 'a036e00000z60HHAAY', 'a036e00000rJXxOAAW', 'a036e00000rJXxhAAG', 'a036e00000rJboQAAS', 'a036e00000z34jXAAQ', 'a036e00000zSbu5AAC', 'a036e00000zVO1EAAW', 'a036e00000z5wINAAY', 'a036e00000rJYEeAAO', 'a036e00000rJYEtAAO', 'a036e00000rJc2dAAC', 'a036e00000rJc2sAAC', 'a036e00000z34bfAAA', 'a036e00000z4eu8AAA', 'a036e00000rJroYAAS', 'a036e00000rJs8uAAC', 'a036e00000z4AgbAAE', 'a036e00000z34ZOAAY', 'a036e00000zVFvbAAG', 'a036e00000z34lPAAQ', 'a036e00000zSbvoAAC', 'a036e00000z34lSAAQ', 'a036e00000zVFuvAAG', 'a036e00000rJYFjAAO', 'a036e00000rJjP2AAK', 'a036e00000zVBUoAAO', 'a036e00000zVFwGAAW', 'a036e00000z35gYAAQ', 'a036e00000z36RIAAY', 'a036e000010cGX8AAM', 'a036e00000zUNXWAA4', 'a036e00000z36RfAAI', 'a036e00000zUIiUAAW', 'a036e00000z4feXAAQ', 'a036e00000rH9HjAAK', 'a036e000010cGMEAA2', 'a036e000010cGLzAAM', 'a036e000010cGXGAA2', 'a036e000010cHp3AAE', 'a036e000010cGMdAAM', 'a036e000010cGNQAA2', 'a036e000010cGNSAA2', 'a036e000010cGNbAAM', 'a036e000010cGNhAAM', 'a036e00000z36RHAAY', 'a036e00000z36S4AAI', 'a036e00000z4mnKAAQ', 'a036e00000z4nuoAAA', 'a036e00000z4nuqAAA', 'a036e00000z5wYSAAY', 'a036e00000z60cMAAQ', 'a036e00000z6NBTAA2', 'a036e000010cHq0AAE', 'a036e000010cGYbAAM', 'a036e000010cGYTAA2', 'a036e000010cGYfAAM', 'a036e000010csKFAAY', 'a036e000010czWyAAI', 'a036e000010cGMHAA2', 'a036e00000rH60BAAS', 'a036e00000z36C1AAI', 'a036e00000rH61kAAC', 'a036e00000z36BnAAI', 'a036e00000rHAyoAAG', 'a036e00000rHHcgAAG', 'a036e00000rHHchAAG', 'a036e00000rHgLiAAK', 'a036e00000zUoVlAAK', 'a036e00000rHgLjAAK', 'a036e00000rJjSVAA0', 'a036e00000z36BuAAI', 'a036e00000z36CAAAY', 'a036e00000z36ADAAY', 'a036e000010cHeOAAU', 'a036e00000z36C4AAI', 'a036e00000z4fYoAAI', 'a036e00000z4noaAAA', 'a036e00000z4nobAAA', 'a036e00000zUa3TAAS', 'a036e00000z60VdAAI', 'a036e00000zSciMAAS', 'a036e00000zVkCdAAK', 'a036e00000zVkBJAA0', 'a036e00000zSciNAAS', 'a036e00000z35qsAAA', 'a036e00000yuZqnAAE', 'a036e00000z35xsAAA', 'a036e00000z35xtAAA', 'a036e00000z35xwAAA', 'a036e00000zUvEmAAK', 'a036e00000zVqpsAAC', 'a036e00000z35quAAA', 'a036e00000zVkCxAAK', 'a036e00000z3HBmAAM', 'a036e00000zSjVqAAK', 'a036e00000zVqohAAC', 'a036e00000z35zBAAQ', 'a036e00000z5wMFAAY', 'a036e00000z43mRAAQ', 'a036e00000zUabAAAS', 'a036e00000rK1y1AAC', 'a036e00000z4fMXAAY', 'a036e000010c3VaAAI', 'a036e00000z4fMpAAI', 'a036e00000z4fMkAAI', 'a036e00000z4fMoAAI', 'a036e00000zVkCMAA0', 'a036e00000zVkkRAAS', 'a036e00000z4fPrAAI', 'a036e00000z4niaAAA', 'a036e00000z5wM6AAI', 'a036e00000zUNVFAA4', 'a036e000010cDtpAAE', 'a036e000010cDtiAAE', 'a036e000010cDtnAAE', 'a036e00000zUoSjAAK', 'a036e00000zUvEsAAK', 'a036e00000zUabDAAS', 'a036e00000zVqnkAAC', 'a036e000010bx2vAAA', 'a036e000010cDtrAAE', 'a036e000010cDtyAAE', 'a036e00000z60JGAAY', 'a036e00000z60JJAAY', 'a036e00000rKGO6AAO', 'a036e00000rJYGYAA4', 'a036e00000zVkCcAAK', 'a036e00000rIYyWAAW', 'a036e00000rIsCsAAK', 'a036e00000rJYGIAA4', 'a036e00000zUNVQAA4', 'a036e00000rJYIdAAO', 'a036e00000zSjWUAA0', 'a036e00000rJYHLAA4', 'a036e00000z35z1AAA', 'a036e00000z35z4AAA', 'a036e00000zUNVlAAO', 'a036e00000rJYI4AAO', 'a036e00000rJYIGAA4', 'a036e00000rJYIIAA4', 'a036e000010cDtTAAU', 'a036e00000rHneZAAS', 'a036e000010c4GCAAY', 'a036e00000rJNIaAAO', 'a036e00000rJjPuAAK', 'a036e00000rKFadAAG', 'a036e00000yuZpaAAE', 'a036e00000z35p2AAA', 'a036e00000z35p7AAA', 'a036e00000rJYIeAAO', 'a036e00000rJYIfAAO', 'a036e00000rJurWAAS', 'a036e00000rJzc1AAC', 'a036e00000z35zAAAQ', 'a036e00000rIUdIAAW', 'a036e00000rJVhtAAG', 'a036e00000rJzezAAC', 'a036e00000rJYL7AAO', 'a036e000010cGGYAA2', 'a036e00000rH1viAAC', 'a036e00000z4fVjAAI', 'a036e00000rHUThAAO', 'a036e00000z4fVfAAI', 'a036e00000rIMWIAA4', 'a036e00000z3658AAA', 'a036e00000z4fVXAAY', 'a036e00000z4fVZAAY', 'a036e000010cGGNAA2', 'a036e00000z4nlmAAA', 'a036e000010cGGXAA2', 'a036e00000z60TDAAY', 'a036e000010cGGKAA2', 'a036e000010cGGUAA2', 'a036e000010cGGPAA2', 'a036e000010cGGVAA2', 'a036e000010cGGqAAM', 'a036e000010cGH1AAM', 'a036e000010cGH5AAM', 'a036e000010cGHiAAM', 'a036e000010cGHkAAM', 'a036e000010cGHsAAM', 'a036e000010cGI2AAM', 'a036e00000z36JmAAI', 'a036e00000rH7tNAAS', 'a036e00000rH8B5AAK', 'a036e00000rH8BTAA0', 'a036e000010cGSAAA2', 'a036e00000rJRijAAG', 'a036e00000z36JoAAI', 'a036e00000rJYPfAAO', 'a036e00000rJjSsAAK', 'a036e00000rJjSwAAK', 'a036e00000rJjSyAAK', 'a036e00000rJjT3AAK', 'a036e00000rJkPWAA0', 'a036e00000z36IDAAY', 'a036e00000z36KMAAY', 'a036e000010cHizAAE', 'a036e000010cGRNAA2', 'a036e00000z5wVXAAY', 'a036e00000z5wVhAAI', 'a036e000010cGRJAA2', 'a036e00000rIUcUAAW', 'a036e00000z36WeAAI', 'a036e000010cGS7AAM', 'a036e000010cHjfAAE', 'a036e000010cz3sAAA', 'a036e000010clcFAAQ', 'a036e000010cGRvAAM', 'a036e000010cGRxAAM', 'a036e000010cGS2AAM', 'a036e000010cGSWAA2', 'a036e00000z6MwrAAE', 'a036e00000z6Mw3AAE', 'a036e00000z4fGSAAY', 'a036e00000rHgGOAA0', 'a036e00000z35bZAAQ', 'a036e00000z34E5AAI', 'a036e00000zUrxnAAC', 'a036e00000z34EJAAY', 'a036e00000rJXsTAAW', 'a036e00000z34T1AAI', 'a036e00000z353IAAQ', 'a036e00000z354nAAA', 'a036e00000zVOkEAAW', 'a036e00000z354pAAA', 'a036e00000zSc5YAAS', 'a036e00000zVOmNAAW', 'a036e00000rHQFPAA4', 'a036e00000rJY9fAAG', 'a036e00000z34EMAAY', 'a036e00000z4sRYAAY', 'a036e00000z354vAAA', 'a036e00000z4f2LAAQ', 'a036e00000rIMNnAAO', 'a036e00000rJY9jAAG', 'a036e00000z4ejaAAA', 'a036e00000zVvpEAAS', 'a036e00000zUsYwAAK', 'a036e00000z4f2QAAQ', 'a036e00000z4f2SAAQ', 'a036e00000zVvrfAAC', 'a036e00000z6MyhAAE', 'a036e00000z6MzFAAU', 'a036e00000zTOApAAO', 'a036e00000z6MrwAAE', 'a036e00000rHgVxAAK', 'a036e00000rJXshAAG', 'a036e00000z43RqAAI', 'a036e00000z5w7AAAQ', 'a036e00000rJYABAA4', 'a036e00000rH5ACAA0', 'a036e00000rHgKAAA0', 'a036e00000z344sAAA', 'a036e00000rHnX5AAK', 'a036e00000rINHkAAO', 'a036e00000z34NiAAI', 'a036e00000z35CiAAI', 'a036e00000rI1tyAAC', 'a036e00000rJYADAA4', 'a036e00000rJYAKAA4', 'a036e00000rJYAoAAO', 'a036e00000rJYAwAAO', 'a036e00000rJYB1AAO', 'a036e00000rJYB3AAO', 'a036e00000rJYB7AAO', 'a036e00000rJZb1AAG', 'a036e00000rIMAnAAO', 'a036e00000rJZlHAAW', 'a036e00000rIdgqAAC', 'a036e00000rJZlvAAG', 'a036e00000rJXsRAAW', 'a036e00000rIMKUAA4', 'a036e00000rJY5YAAW', 'a036e00000rJYBDAA4', 'a036e00000rJYBGAA4', 'a036e00000z4fF3AAI', 'a036e00000rJXnkAAG', 'a036e00000rJXsZAAW', 'a036e00000rJY5fAAG', 'a036e00000zVA8vAAG', 'a036e00000zScByAAK', 'a036e00000zUaa3AAC', 'a036e00000z4fExAAI', 'a036e00000zVvsiAAC', 'a036e00000z35RZAAY', 'a036e00000z35RfAAI', 'a036e00000rJXnzAAG', 'a036e00000rJXo1AAG', 'a036e00000rJXqCAAW', 'a036e00000zUNOmAAO', 'a036e00000rJXsnAAG', 'a036e00000z432dAAA', 'a036e00000zSjT5AAK', 'a036e00000zUaa6AAC', 'a036e00000z35SIAAY', 'a036e00000z35SOAAY', 'a036e00000zVcdmAAC', 'a036e00000z4fEqAAI', 'a036e00000rJXoJAAW', 'a036e00000rHnXfAAK', 'a036e00000rHnXnAAK', 'a036e00000rI29DAAS', 'a036e00000z4ejYAAQ', 'a036e00000rJXqOAAW', 'a036e00000rJXt0AAG', 'a036e00000rJXt5AAG', 'a036e00000rJXt9AAG', 'a036e00000rJXtCAAW', 'a036e00000rJY6QAAW', 'a036e00000z4fErAAI', 'a036e00000zVvsqAAC', 'a036e00000rJXoPAAW', 'a036e00000rJXoaAAG', 'a036e00000rJXocAAG', 'a036e00000rJXogAAG', 'a036e00000rJXolAAG', 'a036e00000rIQoYAAW', 'a036e00000rIQoZAAW', 'a036e00000rIyesAAC', 'a036e00000rJ4IvAAK', 'a036e00000rJmRmAAK', 'a036e00000rJmXMAA0', 'a036e00000rJmXWAA0', 'a036e00000rJmpiAAC', 'a036e00000rJY3bAAG', 'a036e00000zVhULAA0', 'a036e00000rJY77AAG', 'a036e00000rJY7CAAW', 'a036e00000rJY7DAAW', 'a036e00000rJY7IAAW', 'a036e00000rJkh9AAC', 'a036e00000z6N2EAAU', 'a036e00000z6N2NAAU', 'a036e00000zU9duAAC', 'a036e00000rJXp0AAG', 'a036e00000rJXp2AAG', 'a036e00000rJXqDAAW', 'a036e00000z49ONAAY', 'a036e00000zVfdVAAS', 'a036e00000rJdu1AAC', 'a036e00000rKEOCAA4', 'a036e00000z35ClAAI', 'a036e00000z344qAAA', 'a036e00000zUqTFAA0', 'a036e00000zUqViAAK', 'a036e00000zUqYkAAK', 'a036e00000zUqVyAAK', 'a036e00000z344zAAA', 'a036e00000rJdjmAAC', 'a036e00000zSjPvAAK', 'a036e00000z63JOAAY', 'a036e00000z4nERAAY', 'a036e00000z63LiAAI', 'a036e00000zVA8dAAG', 'a036e00000z4f57AAA', 'a036e00000z4f5EAAQ', 'a036e00000z4f5FAAQ', 'a036e00000zVvs7AAC', 'a036e00000z345fAAA', 'a036e00000z345hAAA', 'a036e00000z3GeEAAU', 'a036e00000z6Mr0AAE', 'a036e00000rJXqaAAG', 'a036e00000zUsGyAAK', 'a036e00000rJY3TAAW', 'a036e00000z4nU7AAI', 'a036e00000z5w9PAAQ', 'a036e00000z6MrUAAU', 'a036e00000zUNNfAAO', 'a036e00000rJXrMAAW', 'a036e00000rJ0FGAA0', 'a036e00000rJY3dAAG', 'a036e00000rJY42AAG', 'a036e00000rJY44AAG', 'a036e00000rJY4kAAG', 'a036e00000z6N0JAAU', 'a036e00000rJY7tAAG', 'a036e00000rJY81AAG', 'a036e00000rJY85AAG', 'a036e00000rJY88AAG', 'a036e00000rJXrSAAW', 'a036e00000rJXseAAG', 'a036e00000rJkgaAAC', 'a036e00000rJXrjAAG', 'a036e00000rJXrmAAG', 'a036e00000rJW2NAAW', 'a036e00000zSbhEAAS', 'a036e00000rJY4XAAW', 'a036e00000rJY4YAAW', 'a036e00000rJjM5AAK', 'a036e00000z34CZAAY', 'a036e00000z34NkAAI', 'a036e00000z34E4AAI', 'a036e00000z34T9AAI', 'a036e00000z34CYAAY', 'a036e00000z34EPAAY', 'a036e00000rJkVNAA0', 'a036e00000z4f2gAAA', 'a036e00000z4f2fAAA', 'a036e00000rHQFEAA4', 'a036e00000rHQFVAA4', 'a036e00000rJY9ZAAW', 'a036e00000rHQFDAA4', 'a036e00000z34b5AAA', 'a036e00000z4mgIAAQ', 'a036e00000z3GqKAAU', 'a036e00000z4ex5AAA', 'a036e00000z4f05AAA', 'a036e00000z4nKcAAI', 'a036e00000z5w2GAAQ', 'a036e00000z5w2RAAQ', 'a036e00000z35i5AAA', 'a036e00000z3H96AAE', 'a036e00000z4fJ6AAI', 'a036e00000zViv2AAC', 'a036e00000z4fJCAAY', 'a036e00000rHncwAAC', 'a036e00000z4fJPAAY', 'a036e00000z60HFAAY', 'a036e00000zUNUtAAO', 'a036e00000zVjNhAAK', 'a036e00000z60EuAAI', 'a036e00000rJYCbAAO', 'a036e00000rJYCcAAO', 'a036e00000rJYCjAAO', 'a036e00000rJYCtAAO', 'a036e00000rJYCvAAO', 'a036e00000zViZ5AAK', 'a036e00000z35iJAAQ', 'a036e00000z35iKAAQ', 'a036e00000z35iMAAQ', 'a036e00000zVvqFAAS', 'a036e00000zVvqOAAS', 'a036e00000rJYDEAA4', 'a036e00000rJYDXAA4', 'a036e00000rJYDeAAO', 'a036e00000z4nIPAAY', 'a036e00000z6MvsAAE', 'a036e00000rJCo3AAG', 'a036e00000rJY0sAAG', 'a036e00000rJYDiAAO', 'a036e00000z35bUAAQ', 'a036e00000z34aoAAA', 'a036e00000rHUSmAAO', 'a036e00000z4ex1AAA', 'a036e00000z4exPAAQ', 'a036e00000rHnZtAAK', 'a036e00000rJY13AAG', 'a036e00000rJY1CAAW', 'a036e00000z4fGYAAY', 'a036e00000z4fGZAAY', 'a036e00000z35ZlAAI', 'a036e00000z35ZtAAI', 'a036e00000z35ZuAAI', 'a036e00000zVgJtAAK', 'a036e00000z63PpAAI', 'a036e00000rIY4FAAW', 'a036e00000rJ6PgAAK', 'a036e00000rJtrvAAC', 'a036e00000rJWcvAAG', 'a036e00000rJY23AAG', 'a036e00000z35bVAAQ', 'a036e00000zVgOTAA0', 'a036e00000z4As4AAE', 'a036e00000z4fG9AAI', 'a036e00000rHHYJAA4', 'a036e00000rJXySAAW', 'a036e00000rJeVVAA0', 'a036e00000rJXylAAG', 'a036e00000z5w2HAAQ', 'a036e00000rJY1uAAG', 'a036e00000rJY2CAAW', 'a036e00000rJY2FAAW', 'a036e00000rJY2GAAW', 'a036e00000rJdvEAAS', 'a036e00000z4fGPAAY', 'a036e00000z4fGaAAI', 'a036e000010buvFAAQ', 'a036e00000z5wFcAAI', 'a036e00000z60F7AAI', 'a036e00000rHnZIAA0', 'a036e00000zVBWvAAO', 'a036e00000z5zzAAAQ', 'a036e00000rJXyvAAG', 'a036e00000z5w2KAAQ', 'a036e00000rJs49AAC', 'a036e00000rJtqOAAS', 'a036e00000rJzHeAAK', 'a036e00000z34txAAA', 'a036e00000rINSoAAO', 'a036e00000rINSrAAO', 'a036e00000z60HAAAY', 'a036e00000rJXxlAAG', 'a036e00000rJNoUAAW', 'a036e00000z5w2EAAQ', 'a036e00000rJY0rAAG', 'a036e00000zUNRCAA4', 'a036e00000zUZr7AAG', 'a036e00000rJXwBAAW', 'a036e00000z34tzAAA', 'a036e00000z34v9AAA', 'a036e00000z34vCAAQ', 'a036e00000zSc0rAAC', 'a036e00000z34vJAAQ', 'a036e00000rKU8rAAG', 'a036e00000rIycLAAS', 'a036e00000rJYE5AAO', 'a036e00000rJXzOAAW', 'a036e00000rJXzZAAW', 'a036e00000rJXzaAAG', 'a036e00000rJXzeAAG', 'a036e00000rJXzqAAG', 'a036e00000zVNBdAAO', 'a036e00000z34vMAAQ', 'a036e00000zVNC5AAO', 'a036e00000rJYEPAA4', 'a036e00000zTOBgAAO', 'a036e00000rJXwQAAW', 'a036e00000rJXx4AAG', 'a036e00000rJXxAAAW', 'a036e00000rJY0jAAG', 'a036e00000rJkUPAA0', 'a036e00000rJsdnAAC', 'a036e00000z4ezvAAA', 'a036e00000zUNUoAAO', 'a036e00000rJYEUAA4', 'a036e00000z60H7AAI', 'a036e00000rJYEZAA4', 'a036e00000rJXxRAAW', 'a036e00000rJXxjAAG', 'a036e00000rJXxkAAG', 'a036e00000zUZr6AAG', 'a036e00000z34jZAAQ', 'a036e00000rJYEdAAO', 'a036e00000rJYExAAO', 'a036e00000rJYFeAAO', 'a036e00000z4eu7AAA', 'a036e00000rJrqAAAS', 'a036e00000zVBWnAAO', 'a036e00000z34lOAAQ', 'a036e00000zVFysAAG', 'a036e00000zVFvIAAW', 'a036e00000zSbviAAC', 'a036e00000z34lWAAQ', 'a036e00000rJYFlAAO', 'a036e00000rJYFrAAO', 'a036e00000rJjP4AAK', 'a036e00000rJjPDAA0', 'a036e00000rJjPHAA0', 'a036e00000z4sKKAAY', 'a036e00000zSbm5AAC', 'a036e00000z34ldAAA', 'a036e00000zVFvPAAW', 'a036e00000z35gOAAQ', 'a036e00000z36QpAAI', 'a036e00000z60cYAAQ', 'a036e000010cGM5AAM', 'a036e000010cGLpAAM', 'a036e000010cGMMAA2', 'a036e00000rH9elAAC', 'a036e00000rI7aFAAS', 'a036e00000yua4PAAQ', 'a036e000010cGMbAAM', 'a036e000010cGNRAA2', 'a036e000010cGNWAA2', 'a036e000010cGNXAA2', 'a036e000010cGNaAAM', 'a036e000010cNYYAA2', 'a036e000010cPYpAAM', 'a036e000010cUcdAAE', 'a036e00000z36RMAAY', 'a036e00000z3HQ5AAM', 'a036e00000z4femAAA', 'a036e000010cGX7AAM', 'a036e00000z4mnAAAQ', 'a036e000010cGX4AAM', 'a036e00000z6NB9AAM', 'a036e000010biEeAAI', 'a036e000010cGWuAAM', 'a036e000010cGWxAAM', 'a036e000010cGXEAA2', 'a036e000010cGXtAAM', 'a036e000010cGXyAAM', 'a036e000010cGYhAAM', 'a036e000010cGMNAA2', 'a036e00000rH61jAAC', 'a036e00000rH6rAAAS', 'a036e00000rH6rBAAS', 'a036e00000rH6rCAAS', 'a036e00000rHejIAAS', 'a036e00000rJjSLAA0', 'a036e00000rJjSRAA0', 'a036e00000rJohdAAC', 'a036e000010cHfhAAE', 'a036e00000yuZypAAE', 'a036e000010bjn4AAA', 'a036e00000z3HKiAAM', 'a036e00000z3HKxAAM', 'a036e00000z43slAAA', 'a036e00000z4noHAAQ', 'a036e00000z5wStAAI', 'a036e00000z60VXAAY', 'a036e00000z60VfAAI', 'a036e00000z6N8vAAE', 'a036e00000z35qtAAA', 'a036e00000zScm2AAC', 'a036e00000yt0hQAAQ', 'a036e00000z4B1dAAE', 'a036e00000z35xyAAA', 'a036e000010c3owAAA', 'a036e00000zVkAfAAK', 'a036e00000z4fMrAAI', 'a036e00000z35z2AAA', 'a036e00000zVqnhAAC', 'a036e00000z35z7AAA', 'a036e00000z5wMKAAY', 'a036e00000zVkYoAAK', 'a036e00000z4fMgAAI', 'a036e00000z4fMhAAI', 'a036e00000zVkYqAAK', 'a036e00000zVkYsAAK', 'a036e00000zVkkrAAC', 'a036e00000z4fPsAAI', 'a036e00000zVr6YAAS', 'a036e00000z4fQCAAY', 'a036e00000z4niZAAQ', 'a036e00000z60JHAAY', 'a036e00000z5wMAAAY', 'a036e00000z60NOAAY', 'a036e00000z60NSAAY', 'a036e00000z63jcAAA', 'a036e000010cDtXAAU', 'a036e00000zUNVuAAO', 'a036e00000zUab5AAC', 'a036e00000zVrTbAAK', 'a036e000010cDtwAAE', 'a036e000010cDtzAAE', 'a036e000010cDu0AAE', 'a036e000010cDu2AAE', 'a036e000010cDu3AAE', 'a036e00000rHndtAAC', 'a036e00000rJYGWAA4', 'a036e00000zUNVPAA4', 'a036e000010cDu5AAE', 'a036e000010cDuqAAE', 'a036e00000z35qrAAA', 'a036e00000rHndlAAC', 'a036e00000rIYyUAAW', 'a036e00000z60JCAAY', 'a036e00000rJYGKAA4', 'a036e00000rJYGQAA4', 'a036e00000rJYGZAA4', 'a036e00000zUNVLAA4', 'a036e00000z60JEAAY', 'a036e00000rK1tGAAS', 'a036e00000rJYGoAAO', 'a036e00000z5wMJAAY', 'a036e000010cDtVAAU', 'a036e000010cNHaAAM', 'a036e00000rJYGtAAO', 'a036e000010c0jNAAQ', 'a036e00000rJYHbAAO', 'a036e00000rJYHhAAO', 'a036e00000rJYHmAAO', 'a036e00000rJYIDAA4', 'a036e00000rJYIEAA4', 'a036e00000rJYIJAA4', 'a036e00000rJjPgAAK', 'a036e00000rJjPjAAK', 'a036e00000rJjPoAAK', 'a036e00000rJjPsAAK', 'a036e000010cNdFAAU', 'a036e00000rIMSAAA4', 'a036e00000rJW7dAAG', 'a036e00000rJv3SAAS', 'a036e00000rKFbHAAW', 'a036e00000zVkYpAAK', 'a036e00000z4fMtAAI', 'a036e00000zVkkqAAC', 'a036e00000z35p5AAA', 'a036e00000rJjHHAA0', 'a036e000010cGIAAA2', 'a036e000010cGIBAA2', 'a036e000010cPWzAAM', 'a036e000010cPbFAAU', 'a036e000010cijHAAQ', 'a036e00000rJVcsAAG', 'a036e00000rJVL2AAO', 'a036e000010cAPkAAM', 'a036e000010cAPlAAM', 'a036e00000rJb7cAAC', 'a036e00000rJb7dAAC', 'a036e00000z37YbAAI', 'a036e00000z37YcAAI', 'a036e000010cAPhAAM', 'a036e00000z3650AAA', 'a036e000010cGHFAA2', 'a036e00000zVAHRAA4', 'a036e000010cV87AAE', 'a036e00000z3653AAA', 'a036e00000rH2XUAA0', 'a036e00000rHUTgAAO', 'a036e00000rHUTiAAO', 'a036e00000z4fVVAAY', 'a036e00000z4fVUAAY', 'a036e00000rHUTzAAO', 'a036e00000z4fVgAAI', 'a036e00000rJYL6AAO', 'a036e00000rKU1lAAG', 'a036e00000z363QAAQ', 'a036e00000z363VAAQ', 'a036e00000z43qzAAA', 'a036e00000z4B40AAE', 'a036e00000z4nllAAA', 'a036e00000z4nlnAAA', 'a036e00000z5wPaAAI', 'a036e00000z60TFAAY', 'a036e00000z60TIAAY', 'a036e000010cGGSAA2', 'a036e00000z6N83AAE', 'a036e000010c0l9AAA', 'a036e000010cHUzAAM', 'a036e000010cGGLAA2', 'a036e000010cGH8AAM', 'a036e00000rH7cIAAS', 'a036e00000rH7caAAC', 'a036e00000z36JzAAI', 'a036e000010cGRdAAM', 'a036e00000z4fbgAAA', 'a036e00000rH7zmAAC', 'a036e00000rHzbyAAC', 'a036e00000rIMYfAAO', 'a036e000010cGRLAA2', 'a036e00000rItTaAAK', 'a036e00000rJjT0AAK', 'a036e00000z36JvAAI', 'a036e00000rJkbQAAS', 'a036e00000rJohTAAS', 'a036e00000rK3toAAC', 'a036e00000z36IeAAI', 'a036e00000z36IhAAI', 'a036e00000z36KHAAY', 'a036e00000z36KLAAY', 'a036e00000z36KPAAY', 'a036e00000z36KQAAY', 'a036e00000z4nrkAAA', 'a036e000010cGRKAA2', 'a036e00000z60YzAAI', 'a036e00000z60Z0AAI', 'a036e000010cHjlAAE', 'a036e00000rIUdwAAG', 'a036e00000z6NAEAA2', 'a036e00000z6NAVAA2', 'a036e00000z6NAXAA2', 'a036e000010cGRXAA2', 'a036e000010cGS8AAM', 'a036e000010cGcKAAU', 'a036e000010crtWAAQ', 'a036e000010cGSTAA2', 'a036e000010cGScAAM', 'a036e000010coFvAAI', 'a036e00000rHT8CAAW', 'a036e00000rHUTSAA4', 'a036e00000rHgGYAA0', 'a036e00000rHglCAAS', 'a036e00000zSbT1AAK', 'a036e00000zUs0IAAS', 'a036e00000z353FAAQ', 'a036e00000z353GAAQ', 'a036e00000z353aAAA', 'a036e00000z354rAAA', 'a036e00000rHQFGAA4', 'a036e00000rHQFRAA4', 'a036e00000rJY9oAAG', 'a036e00000rKFZuAAO', 'a036e00000zUrxyAAC', 'a036e00000z3GgfAAE', 'a036e00000z354uAAA', 'a036e00000z4f2JAAQ', 'a036e00000zVcrHAAS', 'a036e00000rJOOtAAO', 'a036e00000zVcrEAAS', 'a036e00000z4AUcAAM', 'a036e00000z4nCDAAY', 'a036e00000z5vtiAAA', 'a036e00000z4f2TAAQ', 'a036e00000z4f2VAAQ', 'a036e00000z4f2oAAA', 'a036e00000rJY9RAAW', 'a036e00000rJYBCAA4', 'a036e00000rJY9aAAG', 'a036e00000z6MseAAE', 'a036e00000zVvrbAAC', 'a036e00000rJY4IAAW', 'a036e00000rJY9cAAG', 'a036e00000rJY9iAAG', 'a036e00000rJY9kAAG', 'a036e00000z344oAAA', 'a036e00000z344pAAA', 'a036e00000rJXsaAAG', 'a036e00000rJCqaAAG', 'a036e00000rJYAFAA4', 'a036e00000rJYAlAAO', 'a036e00000rJYAvAAO', 'a036e00000rJYAxAAO', 'a036e00000rJYAyAAO', 'a036e00000rJYB8AAO', 'a036e00000rJXjzAAG', 'a036e00000rJ3fnAAC', 'a036e00000rJc8sAAC', 'a036e00000rJYBEAA4', 'a036e00000rJYBKAA4', 'a036e00000rJYBMAA4', 'a036e00000rK2I7AAK', 'a036e00000z35SPAAY', 'a036e00000zUNNbAAO', 'a036e00000rJXsVAAW', 'a036e00000rJXsbAAG', 'a036e00000z3lyXAAQ', 'a036e00000z63WuAAI', 'a036e00000rJY5nAAG', 'a036e00000yuZhiAAE', 'a036e00000z35QfAAI', 'a036e00000z35QgAAI', 'a036e00000z35RBAAY', 'a036e00000zUv5hAAC', 'a036e00000zVcZhAAK', 'a036e00000zUa23AAC', 'a036e00000rHAaTAAW', 'a036e00000rJXskAAG', 'a036e00000zVECkAAO', 'a036e00000rJXsoAAG', 'a036e00000rJY5uAAG', 'a036e00000zVcaXAAS', 'a036e00000z3H2rAAE', 'a036e00000zSbOJAA0', 'a036e00000rJXo7AAG', 'a036e00000rJXqJAAW', 'a036e00000rIMBzAAO', 'a036e00000rJXtAAAW', 'a036e00000zScFcAAK', 'a036e00000rJY6OAAW', 'a036e00000rJY6PAAW', 'a036e00000z4fEdAAI', 'a036e00000zVdAmAAK', 'a036e00000z4fEkAAI', 'a036e00000z4fF1AAI', 'a036e00000rJXojAAG', 'a036e00000zSbUAAA0', 'a036e00000z4elmAAA', 'a036e00000rJmVUAA0', 'a036e00000rJmZwAAK', 'a036e00000rJY70AAG', 'a036e00000z35CjAAI', 'a036e00000z6N2CAAU', 'a036e00000z6N2PAAU', 'a036e00000zSjUHAA0', 'a036e00000zU9dvAAC', 'a036e00000rJXosAAG', 'a036e00000rJXovAAG', 'a036e00000rJXoxAAG', 'a036e00000z4egrAAA', 'a036e00000rJXqIAAW', 'a036e00000z5vtPAAQ', 'a036e00000rJdk1AAC', 'a036e00000zUtMdAAK', 'a036e00000zUuoyAAC', 'a036e00000rHHZCAA4', 'a036e00000rJxJeAAK', 'a036e00000zVhUMAA0', 'a036e00000rINNLAA4', 'a036e00000z354wAAA', 'a036e00000rJY3eAAG', 'a036e00000rJudDAAS', 'a036e00000rKBKHAA4', 'a036e00000z35CgAAI', 'a036e00000zScCFAA0', 'a036e00000zSbMHAA0', 'a036e00000rJZqvAAG', 'a036e00000z3iIsAAI', 'a036e00000rJXqNAAW', 'a036e00000rJXqPAAW', 'a036e00000rJXqWAAW', 'a036e00000z4elrAAA', 'a036e00000z4eltAAA', 'a036e00000z4nETAAY', 'a036e00000z6MtYAAU', 'a036e00000zVfh0AAC', 'a036e00000rJY3IAAW', 'a036e00000z63UbAAI', 'a036e00000z4sRzAAI', 'a036e00000zVvs0AAC', 'a036e00000z4f59AAA', 'a036e00000zUqVWAA0', 'a036e00000z5vrLAAQ', 'a036e00000z63HUAAY', 'a036e00000z5vteAAA', 'a036e00000rJXqjAAG', 'a036e00000rJXqtAAG', 'a036e00000zVA2vAAG', 'a036e00000zVv2rAAC', 'a036e00000rINIjAAO', 'a036e00000rJY3SAAW', 'a036e00000z4nU8AAI', 'a036e00000z63X2AAI', 'a036e00000z6MzpAAE', 'a036e00000rJXrAAAW', 'a036e00000rJ3g2AAC', 'a036e00000rJW2TAAW', 'a036e00000rJW2IAAW', 'a036e00000rJW2JAAW', 'a036e00000rJyp5AAC', 'a036e00000rJY4UAAW', 'a036e00000rJY4VAAW', 'a036e00000zUv1RAAS', 'a036e00000zVQqoAAG', 'a036e00000rHnbgAAC', 'a036e00000rJY7wAAG', 'a036e00000rJY86AAG', 'a036e00000rJY8AAAW', 'a036e00000rJXrZAAW', 'a036e00000rJXraAAG', 'a036e00000rJkLOAA0', 'a036e00000rJmbxAAC', 'a036e00000rJjM0AAK', 'a036e00000z6N0eAAE', 'a036e00000rJay6AAC', 'a036e00000rJbPgAAK', 'a036e00000z34EFAAY', 'a036e00000z34CbAAI', 'a036e00000zUtM4AAK', 'a036e00000z4nHLAAY', 'a036e00000rJogBAAS', 'a036e00000z354qAAA', 'a036e00000zSc91AAC', 'a036e00000rK1VJAA0', 'a036e00000rK2IgAAK', 'a036e00000rK5HlAAK', 'a036e00000rJY9YAAW', 'a036e00000rHHaIAAW', 'a036e00000z34b4AAA', 'a036e00000z34b8AAA', 'a036e00000zVBUYAA4', 'a036e00000z4exQAAQ', 'a036e00000z35bXAAQ', 'a036e00000rHmQzAAK', 'a036e00000z35ggAAA', 'a036e00000z35giAAA', 'a036e00000z35gnAAA', 'a036e00000z35i7AAA', 'a036e00000zUoQnAAK', 'a036e00000zViXBAA0', 'a036e00000z35iEAAQ', 'a036e00000zViXXAA0', 'a036e00000z35iRAAQ', 'a036e00000z4fIzAAI', 'a036e00000z4fJBAAY', 'a036e00000zViv4AAC', 'a036e000010c0boAAA', 'a036e00000z4fJFAAY', 'a036e00000rJYC7AAO', 'a036e00000z5wFfAAI', 'a036e00000rHnceAAC', 'a036e00000z4fGMAAY', 'a036e00000zVgvkAAC', 'a036e00000rJYBuAAO', 'a036e00000z60FDAAY', 'a036e00000z4fJOAAY', 'a036e00000zVj8JAAS', 'a036e00000zVj8KAAS', 'a036e00000z60F2AAI', 'a036e00000zSjUmAAK', 'a036e00000zVjNiAAK', 'a036e000010bwjVAAQ', 'a036e000010bxTgAAI', 'a036e000010c0WUAAY', 'a036e00000rJYC5AAO', 'a036e00000rJYCYAA4', 'a036e00000rJYCwAAO', 'a036e00000z35iGAAQ', 'a036e00000z4euCAAQ', 'a036e00000z4euMAAQ', 'a036e00000z6MwhAAE', 'a036e00000rJY1GAAW', 'a036e00000zUoKZAA0', 'a036e00000rJYD1AAO', 'a036e00000rJYDIAA4', 'a036e00000rJYDZAA4', 'a036e00000rJYDcAAO', 'a036e00000z4nISAAY', 'a036e00000rJY2LAAW', 'a036e00000z600UAAQ', 'a036e00000rJY0tAAG', 'a036e00000z600TAAQ', 'a036e00000rJs4TAAS', 'a036e00000rJuozAAC', 'a036e00000rJXwKAAW', 'a036e00000z34ZPAAY', 'a036e00000rH71TAAS', 'a036e00000rHUSpAAO', 'a036e00000rHUT3AAO', 'a036e00000rJY14AAG', 'a036e00000z35ZrAAI', 'a036e00000z35bPAAQ', 'a036e00000z35bQAAQ', 'a036e00000rJXygAAG', 'a036e00000rJY01AAG', 'a036e00000z34lZAAQ', 'a036e00000z4erSAAQ', 'a036e00000z34lIAAQ', 'a036e00000rJuhFAAS', 'a036e00000rJY1YAAW', 'a036e00000rJY1aAAG', 'a036e00000rJY1bAAG', 'a036e00000rJY1dAAG', 'a036e00000rHQExAAO', 'a036e00000rHQEzAAO', 'a036e00000rJXyVAAW', 'a036e00000rJXybAAG', 'a036e00000rJXymAAG', 'a036e00000z63PjAAI', 'a036e00000zUwnZAAS', 'a036e00000rJY26AAG', 'a036e00000rJY2BAAW', 'a036e00000rJjQVAA0', 'a036e00000z4fGOAAY', 'a036e00000zVgvsAAC', 'a036e00000z5wFmAAI', 'a036e00000rHHb3AAG', 'a036e00000zUNQLAA4', 'a036e00000rIMFsAAO', 'a036e00000rJ3acAAC', 'a036e00000z5zzEAAQ', 'a036e00000rJmYFAA0', 'a036e00000rJxA8AAK', 'a036e00000zVNAaAAO', 'a036e00000yuZUEAA2', 'a036e00000z34twAAA', 'a036e000010bwjpAAA', 'a036e00000zSjUlAAK', 'a036e00000rJrkMAAS', 'a036e00000rJXz7AAG', 'a036e00000rJXz9AAG', 'a036e00000zVNAVAA4', 'a036e00000z34vEAAQ', 'a036e00000z35iOAAQ', 'a036e00000zUZrDAAW', 'a036e00000zUZrCAAW', 'a036e00000rJY2DAAW', 'a036e00000rJY2KAAW', 'a036e00000z600fAAA', 'a036e00000rJXzfAAG', 'a036e00000rJXzgAAG', 'a036e00000rJXzhAAG', 'a036e00000zSc2RAAS', 'a036e00000z4gZFAAY', 'a036e00000z3GsNAAU', 'a036e00000z60H9AAI', 'a036e00000rJXwoAAG', 'a036e00000rJXx2AAG', 'a036e00000rJXx6AAG', 'a036e00000rJXzuAAG', 'a036e00000rJs9dAAC', 'a036e00000z34lNAAQ', 'a036e00000z4ezuAAA', 'a036e00000z4ezxAAA', 'a036e00000zVvr7AAC', 'a036e00000rJYESAA4', 'a036e00000rJYETAA4', 'a036e00000zUNUqAAO', 'a036e00000rJYEWAA4', 'a036e00000rJYEaAAO', 'a036e00000rJXxPAAW', 'a036e00000rJY1kAAG', 'a036e00000rJs1AAAS', 'a036e00000rJXxbAAG', 'a036e00000zUNQSAA4', 'a036e00000yuZTAAA2', 'a036e00000z34jYAAQ', 'a036e00000z34jeAAA', 'a036e00000zUIreAAG', 'a036e00000z34lGAAQ', 'a036e00000zVFv7AAG', 'a036e00000zVA7bAAG', 'a036e00000z43hkAAA', 'a036e00000rJYEpAAO', 'a036e00000rJYFdAAO', 'a036e00000rJYFgAAO', 'a036e00000rJYFhAAO', 'a036e00000zVBZlAAO', 'a036e00000rJrmXAAS', 'a036e00000zVA7gAAG', 'a036e00000z34lVAAQ', 'a036e00000rJYFoAAO', 'a036e00000rJYFyAAO', 'a036e00000rJYFzAAO', 'a036e00000rJjPAAA0', 'a036e00000rJjPCAA0', 'a036e00000rJjPFAA0', 'a036e00000rJjioAAC', 'a036e00000z34atAAA', 'a036e00000z34awAAA', 'a036e00000z34leAAA', 'a036e00000zVFvQAAW', 'a036e00000z34liAAA', 'a036e00000z35iQAAQ', 'a036e00000rKG5nAAG', 'a036e00000z4fJAAAY', 'a036e00000z35gSAAQ', 'a036e00000z35gXAAQ', 'a036e00000z36RCAAY', 'a036e00000z60cSAAQ', 'a036e00000rH8zLAAS', 'a036e00000rH90nAAC', 'a036e00000z36RJAAY', 'a036e00000z36QlAAI', 'a036e00000rH91HAAS', 'a036e00000z4fejAAA', 'a036e000010cGNcAAM', 'a036e000010cGMBAA2', 'a036e000010cGMTAA2', 'a036e00000rH9ehAAC', 'a036e000010cGNeAAM', 'a036e000010cGNiAAM', 'a036e00000z36QhAAI', 'a036e000010cr8BAAQ', 'a036e00000z36RAAAY', 'a036e00000z4fehAAA', 'a036e000010ckzQAAQ', 'a036e00000z4mnOAAQ', 'a036e00000z4nurAAA', 'a036e00000z6NAtAAM', 'a036e00000z6NBAAA2', 'a036e000010cHp1AAE', 'a036e000010cGX3AAM', 'a036e000010cGXzAAM', 'a036e000010cGYXAA2', 'a036e000010cGYgAAM', 'a036e000010cGYkAAM', 'a036e00000z36C2AAI', 'a036e00000rH65VAAS', 'a036e00000z4fYUAAY', 'a036e00000z4fYVAAY', 'a036e00000rH6OhAAK', 'a036e00000rH6boAAC', 'a036e00000z36C9AAI', 'a036e00000rIPV6AAO', 'a036e00000rJYNMAA4', 'a036e00000rJjSMAA0', 'a036e00000rJjSOAA0', 'a036e00000rJjSPAA0', 'a036e000010cHcqAAE', 'a036e00000rK3tiAAC', 'a036e00000z36AOAAY', 'a036e000010bjn3AAA', 'a036e00000z36BzAAI', 'a036e00000zUoVcAAK', 'a036e00000z5wSqAAI', 'a036e00000z6N99AAE', 'a036e00000z6N9GAAU', 'a036e00000z6N9TAAU', 'a036e000010cGM0AAM', 'a036e00000zVkCGAA0', 'a036e00000z35qpAAA', 'a036e00000z35qqAAA', 'a036e00000zSjWMAA0', 'a036e00000z4fPpAAI', 'a036e00000z4fMbAAI', 'a036e00000z4fMdAAI', 'a036e00000z4fMmAAI', 'a036e00000z4fMnAAI', 'a036e00000zVr6cAAC', 'a036e000010cDtCAAU', 'a036e00000z6N5eAAE', 'a036e000010bvdfAAA', 'a036e000010bveZAAQ', 'a036e000010c3BSAAY', 'a036e000010cDthAAE', 'a036e00000z60NJAAY', 'a036e00000z6N6SAAU', 'a036e00000z6N6UAAU', 'a036e00000z6N6aAAE', 'a036e00000zUNVsAAO', 'a036e000010cDtQAAU', 'a036e000010cDtPAAU', 'a036e00000zUabEAAS', 'a036e000010cDtvAAE', 'a036e000010cDu9AAE', 'a036e000010cDueAAE', 'a036e00000rIYyYAAW', 'a036e00000rHiTvAAK', 'a036e00000rHne2AAC', 'a036e00000rJYGHAA4', 'a036e00000rJYGaAAO', 'a036e00000rJYGbAAO', 'a036e00000rKFTUAA4', 'a036e00000rJYGrAAO', 'a036e000010cDtgAAE', 'a036e00000rJYHMAA4', 'a036e00000rJYHOAA4', 'a036e00000rJYHaAAO', 'a036e00000rJYHdAAO', 'a036e00000rJYHkAAO', 'a036e00000rJYHpAAO', 'a036e00000rHEu2AAG', 'a036e000010cDtdAAE', 'a036e00000rJYI5AAO', 'a036e00000rJYI8AAO', 'a036e00000rJYIAAA4', 'a036e00000rJbVIAA0', 'a036e00000rJjPkAAK', 'a036e00000rJjPqAAK', 'a036e000010cNcqAAE', 'a036e00000zUNVMAA4', 'a036e00000z4fMfAAI', 'a036e00000z35qmAAA', 'a036e00000rJjQvAAK', 'a036e00000zUNVqAAO', 'a036e000010cGI4AAM', 'a036e000010cGIHAA2', 'a036e000010cGIIAA2', 'a036e00000rJVL5AAO', 'a036e00000rHn2dAAC', 'a036e00000rINmEAAW', 'a036e00000z37Y8AAI', 'a036e00000rJVcqAAG', 'a036e00000z37Y7AAI', 'a036e00000rJVL6AAO', 'a036e00000rJb7ZAAS', 'a036e000010cAPnAAM', 'a036e00000rJb7kAAC', 'a036e000010cAPpAAM', 'a036e00000z37YXAAY', 'a036e00000z37YaAAI', 'a036e00000z37YeAAI', 'a036e00000z363NAAQ', 'a036e00000z60TTAAY', 'a036e000010cGGWAA2', 'a036e00000rH1wxAAC', 'a036e00000rH1zuAAC', 'a036e00000z4fVWAAY', 'a036e00000z4fViAAI', 'a036e00000z4fVaAAI', 'a036e00000z4fVbAAI', 'a036e00000z363OAAQ', 'a036e00000z3HHwAAM', 'a036e00000z4meMAAQ', 'a036e00000z4mePAAQ', 'a036e00000z60TBAAY', 'a036e000010cGGIAA2', 'a036e000010cHVYAA2', 'a036e00000z60TMAAY', 'a036e00000z6N7zAAE', 'a036e00000z6N85AAE', 'a036e000010biUMAAY', 'a036e000010c44FAAQ', 'a036e000010cGH3AAM', 'a036e000010cGH7AAM', 'a036e000010cGHKAA2', 'a036e000010cGHhAAM', 'a036e000010cGHtAAM', 'a036e00000z36JtAAI', 'a036e00000rH7cZAAS', 'a036e000010cHhjAAE', 'a036e00000z4fbcAAA', 'a036e00000rH8AyAAK', 'a036e00000rHB0XAAW', 'a036e000010cGS5AAM', 'a036e00000rJjSuAAK', 'a036e00000rJjT4AAK', 'a036e00000rJjT6AAK', 'a036e000010cHitAAE', 'a036e00000z36INAAY', 'a036e00000z36IdAAI', 'a036e00000z36IfAAI', 'a036e000010clc5AAA', 'a036e00000z4nDVAAY', 'a036e000010cGRTAA2', 'a036e000010c0v4AAA', 'a036e00000z4nrlAAA', 'a036e00000z5wVPAAY', 'a036e000010cGROAA2', 'a036e00000zUFu5AAG', 'a036e00000z60Z4AAI', 'a036e00000z36WfAAI', 'a036e000010cHuMAAU', 'a036e00000zUNXqAAO', 'a036e00000zUNXzAAO', 'a036e000010cGRVAA2', 'a036e000010cz2jAAA', 'a036e00000z4nw4AAA', 'a036e000010cGcMAAU', 'a036e000010cGcXAAU', 'a036e000010cGRgAAM', 'a036e000010cGRlAAM', 'a036e000010cGSRAA2', 'a036e000010cGSeAAM', 'a036e000010cGShAAM', 'a036e000010cGSlAAM', 'a036e000010cbCnAAI', 'a036e000010crtgAAA', 'a036e00000rJbn4AAC', 'a036e00000z6MzHAAU', 'a036e00000z4fGdAAI', 'a036e00000zUrzGAAS', 'a036e00000zUs0SAAS', 'a036e00000rJXsmAAG', 'a036e00000rJXsrAAG', 'a036e00000z34NfAAI', 'a036e00000z353CAAQ', 'a036e00000z354oAAA', 'a036e00000zSc5LAAS', 'a036e00000rJY9qAAG', 'a036e00000rHnc8AAC', 'a036e00000rHncDAAS', 'a036e00000z34ELAAY', 'a036e00000z354yAAA', 'a036e00000z4mlXAAQ', 'a036e00000z3GvCAAU', 'a036e00000rHncFAAS', 'a036e00000rJW9FAAW', 'a036e00000rJY9mAAG', 'a036e00000rIxg1AAC', 'a036e00000z4ejdAAA', 'a036e00000z4nCAAAY', 'a036e00000z4nCBAAY', 'a036e00000zVvraAAC', 'a036e00000z4f2RAAQ', 'a036e00000z6039AAA', 'a036e00000z6Mz9AAE', 'a036e00000zVPa3AAG', 'a036e00000zVPa4AAG', 'a036e00000zVPa5AAG', 'a036e00000z6MsDAAU', 'a036e00000zVhQUAA0', 'a036e00000rJY3NAAW', 'a036e00000z354mAAA', 'a036e00000rJY9hAAG', 'a036e00000rKZRjAAO', 'a036e00000rJYA7AAO', 'a036e00000rJYAAAA4', 'a036e00000rJXnlAAG', 'a036e00000rHnXCAA0', 'a036e00000rIMD5AAO', 'a036e00000rJXsMAAW', 'a036e00000rIMDMAA4', 'a036e00000zU9cxAAC', 'a036e00000rJdj8AAC', 'a036e00000rHnbTAAS', 'a036e00000rJY5hAAG', 'a036e00000rJYAnAAO', 'a036e00000rJYAsAAO', 'a036e00000rJYAuAAO', 'a036e00000rJYB6AAO', 'a036e00000zUZryAAG', 'a036e00000rIdaEAAS', 'a036e00000rJXo9AAG', 'a036e00000z4egnAAA', 'a036e00000zVvomAAC', 'a036e00000z34NoAAI', 'a036e00000rJXsLAAW', 'a036e00000rJXsNAAW', 'a036e00000zSbZZAA0', 'a036e00000zUwtSAAS', 'a036e00000rKEM1AAO', 'a036e00000rKFPXAA4', 'a036e00000z605DAAQ', 'a036e00000z4fF2AAI', 'a036e00000z35SKAAY', 'a036e00000rKVbeAAG', 'a036e00000rKZPPAA4', 'a036e00000z5zlBAAQ', 'a036e00000zUNNjAAO', 'a036e00000rJXsUAAW', 'a036e00000rJY5iAAG', 'a036e00000zScTJAA0', 'a036e00000z35RcAAI', 'a036e00000rJXnrAAG', 'a036e00000z5zlKAAQ', 'a036e00000rJXo3AAG', 'a036e00000rHnXdAAK', 'a036e00000z63LgAAI', 'a036e00000z5vw0AAA', 'a036e00000rJXslAAG', 'a036e00000zSjQOAA0', 'a036e00000z605MAAQ', 'a036e00000rJY5tAAG', 'a036e00000z35SHAAY', 'a036e00000z35SLAAY', 'a036e00000z3H2vAAE', 'a036e00000z3H33AAE', 'a036e00000z43YEAAY', 'a036e00000rJds0AAC', 'a036e00000rJXoBAAW', 'a036e00000rJZkeAAG', 'a036e00000rHnXgAAK', 'a036e00000rJXrhAAG', 'a036e00000rHnYhAAK', 'a036e00000rI2F7AAK', 'a036e00000rJXsyAAG', 'a036e00000zUuoxAAC', 'a036e00000z5vwDAAQ', 'a036e00000rJZr5AAG', 'a036e00000rJY6BAAW', 'a036e00000rJY6FAAW', 'a036e00000zVvsdAAC', 'a036e00000z4fEpAAI', 'a036e00000z4fEoAAI', 'a036e00000z4fEyAAI', 'a036e00000zUuncAAC', 'a036e00000rJXoSAAW', 'a036e00000zUFhUAAW', 'a036e00000rJXoVAAW', 'a036e00000rJXohAAG', 'a036e00000rJXokAAG', 'a036e00000z34EEAAY', 'a036e00000rJXq3AAG', 'a036e00000rJj16AAC', 'a036e00000rJkWGAA0', 'a036e00000rJY75AAG', 'a036e00000rJY7EAAW', 'a036e00000z6N2QAAU', 'a036e00000rJXoqAAG', 'a036e00000z3447AAA', 'a036e00000z34NnAAI', 'a036e00000rK8wmAAC', 'a036e00000z35CnAAI', 'a036e00000z35CfAAI', 'a036e00000zVQC3AAO', 'a036e00000z35ChAAI', 'a036e00000zVXr6AAG', 'a036e00000zUv17AAC', 'a036e00000zVQBbAAO', 'a036e00000zVcYZAA0', 'a036e00000zVdAKAA0', 'a036e00000zUqUyAAK', 'a036e00000zSbQ9AAK', 'a036e00000rJXqTAAW', 'a036e00000zUoEzAAK', 'a036e00000z4elvAAA', 'a036e00000z4nESAAY', 'a036e00000z6Mt0AAE', 'a036e00000zU9cgAAC', 'a036e00000z43RzAAI', 'a036e00000rJY3KAAW', 'a036e00000zVfgtAAC', 'a036e00000zVQ9YAAW', 'a036e00000z4f50AAA', 'a036e00000z4f52AAA', 'a036e00000zSbQZAA0', 'a036e00000z5vtgAAA', 'a036e00000rJXrQAAW', 'a036e00000z42yHAAQ', 'a036e00000rJXqoAAG', 'a036e00000rJXqsAAG', 'a036e00000zUb5rAAC', 'a036e00000rJY3VAAW', 'a036e00000rJY3ZAAW', 'a036e00000z6MziAAE', 'a036e00000z6MzvAAE', 'a036e00000zUqTUAA0', 'a036e00000zUr6HAAS', 'a036e00000zVv20AAC', 'a036e00000rJXrBAAW', 'a036e00000rJXrEAAW', 'a036e00000rJXrHAAW', 'a036e00000rJXrPAAW', 'a036e00000rJXrRAAW', 'a036e00000z34T8AAI', 'a036e00000rJY3vAAG', 'a036e00000rJY3wAAG', 'a036e00000rJY47AAG', 'a036e00000rJY4AAAW', 'a036e00000rJY4CAAW', 'a036e00000rJXrTAAW', 'a036e00000rJXrXAAW', 'a036e00000rJXrbAAG', 'a036e00000rJXreAAG', 'a036e00000rJXrgAAG', 'a036e00000rJXsdAAG', 'a036e00000rJW2OAAW', 'a036e00000z34T6AAI', 'a036e00000rJY4cAAG', 'a036e00000zVfdTAAS', 'a036e00000rJY4pAAG', 'a036e00000rJjM1AAK', 'a036e00000rJY8BAAW', 'a036e00000rJyvIAAS', 'a036e00000rIthaAAC', 'a036e00000z43SuAAI', 'a036e00000rJjAKAA0', 'a036e00000rJoejAAC', 'a036e00000rJsFiAAK', 'a036e00000zSc62AAC', 'a036e00000rJynOAAS', 'a036e00000rK2HZAA0', 'a036e00000rK93NAAS', 'a036e00000rJY9TAAW', 'a036e00000z4ewvAAA', 'a036e00000z5w2TAAQ', 'a036e00000z5zz8AAA', 'a036e00000z35gcAAA', 'a036e00000zUvA0AAK', 'a036e00000z4fIyAAI', 'a036e00000z4fJTAAY', 'a036e00000z4fJKAAY', 'a036e00000rJYBkAAO', 'a036e00000rJYBpAAO', 'a036e00000rJYBsAAO', 'a036e00000z4fJLAAY', 'a036e00000rJYBzAAO', 'a036e00000rJYC1AAO', 'a036e00000zUv8vAAC', 'a036e00000rJYCVAA4', 'a036e00000rJYCkAAO', 'a036e00000rJYClAAO', 'a036e00000zViYdAAK', 'a036e00000zVibuAAC', 'a036e00000z35iLAAQ', 'a036e00000z4etzAAA', 'a036e00000z4euGAAQ', 'a036e00000zVvqEAAS', 'a036e00000z4euHAAQ', 'a036e00000z4euLAAQ', 'a036e00000zVvqHAAS', 'a036e00000z6MwZAAU', 'a036e00000zUa2WAAS', 'a036e00000rJYD0AAO', 'a036e00000rJYD8AAO', 'a036e00000rJYDAAA4', 'a036e00000rJYDDAA4', 'a036e00000rJYDJAA4', 'a036e00000rJYDNAA4', 'a036e00000rJYDPAA4', 'a036e00000rJYDTAA4', 'a036e00000rJYDYAA4', 'a036e00000zVC6mAAG', 'a036e00000z4nIMAAY', 'a036e00000z4nINAAY', 'a036e00000z6MviAAE', 'a036e00000zU9dGAAS', 'a036e00000rJY0uAAG', 'a036e00000rJYDhAAO', 'a036e00000zVvqRAAS', 'a036e00000rHUT5AAO', 'a036e00000rHUT6AAO', 'a036e00000z4exNAAQ', 'a036e00000zUa2RAAS', 'a036e00000rJujVAAS', 'a036e00000rJx90AAC', 'a036e00000z34lUAAQ', 'a036e00000rJbxOAAS', 'a036e00000rJVv7AAG', 'a036e00000rJXyOAAW', 'a036e00000rJtuRAAS', 'a036e00000rJzX8AAK', 'a036e00000zUa2XAAS', 'a036e00000rJY1eAAG', 'a036e00000rJY1iAAG', 'a036e00000z4fGcAAI', 'a036e00000rHHYFAA4', 'a036e00000rHnZ8AAK', 'a036e00000rJXyQAAW', 'a036e00000rJXyTAAW', 'a036e00000rJXyUAAW', 'a036e00000rJXydAAG', 'a036e00000rJXyjAAG', 'a036e00000rJXywAAG', 'a036e00000rJY2HAAW', 'a036e00000rJjQQAA0', 'a036e00000rJjQTAA0', 'a036e00000rJjQaAAK', 'a036e00000zVgvrAAC', 'a036e00000z4fGbAAI', 'a036e00000z5wFkAAI', 'a036e00000z35gmAAA', 'a036e00000z35iBAAQ', 'a036e00000rHndFAAS', 'a036e00000rIR1kAAG', 'a036e00000rJXyuAAG', 'a036e00000z5w2IAAQ', 'a036e00000rJXyxAAG', 'a036e00000z34tNAAQ', 'a036e000010bvaLAAQ', 'a036e000010bxI4AAI', 'a036e00000rHzZAAA0', 'a036e00000rI2HmAAK', 'a036e00000rJXw3AAG', 'a036e00000rJXw4AAG', 'a036e00000rJXw7AAG', 'a036e00000rJXz0AAG', 'a036e00000z5zzQAAQ', 'a036e00000z34v7AAA', 'a036e00000z34vFAAQ', 'a036e00000zVNB6AAO', 'a036e00000z35a6AAA', 'a036e00000rIfQlAAK', 'a036e00000rJYEHAA4', 'a036e00000z49o3AAA', 'a036e00000rJYEIAA4', 'a036e00000rJXwHAAW', 'a036e00000rJY20AAG', 'a036e00000zUoKXAA0', 'a036e00000rJXzbAAG', 'a036e00000z4gZEAAY', 'a036e00000z34vKAAQ', 'a036e00000z34vLAAQ', 'a036e00000zVNEnAAO', 'a036e00000zSc2pAAC', 'a036e00000zUa2tAAC', 'a036e00000rJYENAA4', 'a036e00000rJYERAA4', 'a036e00000rJXwqAAG', 'a036e00000rJY16AAG', 'a036e00000rJXx8AAG', 'a036e00000rJXxBAAW', 'a036e00000rJXyqAAG', 'a036e00000rJfIZAA0', 'a036e00000rJXzsAAG', 'a036e00000rJY0DAAW', 'a036e00000rJY0GAAW', 'a036e00000rJY0HAAW', 'a036e00000rJjIyAAK', 'a036e00000z4exIAAQ', 'a036e00000rJsbNAAS', 'a036e00000z34lHAAQ', 'a036e00000rJv26AAC', 'a036e00000z3GsgAAE', 'a036e00000z4ezlAAA', 'a036e00000z4ezzAAA', 'a036e00000z4f0CAAQ', 'a036e00000rJYEXAA4', 'a036e00000rJXxLAAW', 'a036e00000rJXxmAAG', 'a036e00000zUNQTAA4', 'a036e00000z34jhAAA', 'a036e00000z34lLAAQ', 'a036e00000zVFyrAAG', 'a036e00000zUa2cAAC', 'a036e00000z5wIPAAY', 'a036e00000rJYEwAAO', 'a036e00000rJrmcAAC', 'a036e00000z34lQAAQ', 'a036e00000z34lRAAQ', 'a036e00000zVFtlAAG', 'a036e00000rJYFnAAO', 'a036e00000rJYFpAAO', 'a036e00000rJYFtAAO', 'a036e00000rJYG0AAO', 'a036e00000rJjP0AAK', 'a036e00000zVBVlAAO', 'a036e00000z34arAAA', 'a036e00000zVBWqAAO', 'a036e00000z3Gq6AAE', 'a036e00000z3Gq8AAE', 'a036e00000rKFYXAA4', 'a036e00000rKaJ1AAK', 'a036e00000z4fJ9AAI', 'a036e00000z35gMAAQ', 'a036e00000z35gQAAQ', 'a036e00000z35gRAAQ', 'a036e00000z35gWAAQ', 'a036e00000z36R9AAI', 'a036e00000z36RGAAY', 'a036e00000z5wYQAAY', 'a036e000010cGWvAAM', 'a036e00000rH8zGAAS', 'a036e000010cGMCAA2', 'a036e00000zUa3OAAS', 'a036e000010cGM9AAM', 'a036e00000zUa3WAAS', 'a036e00000zUa3bAAC', 'a036e00000zUa3fAAC', 'a036e00000rH90sAAC', 'a036e00000z4feWAAQ', 'a036e00000z4feZAAQ', 'a036e00000rH9HpAAK', 'a036e00000rH9HqAAK', 'a036e000010cGLoAAM', 'a036e000010cGLqAAM', 'a036e000010cGLsAAM', 'a036e000010cGMOAA2', 'a036e000010cGMXAA2', 'a036e000010cGMZAA2', 'a036e00000rH9ejAAC', 'a036e00000rK3tzAAC', 'a036e000010cGNZAA2', 'a036e000010cGNlAAM', 'a036e00000z4feiAAA', 'a036e00000z36REAAY', 'a036e00000z4feOAAQ', 'a036e00000z4febAAA', 'a036e000010clD1AAI', 'a036e00000z4nupAAA', 'a036e00000z5wYWAAY', 'a036e00000z6NAxAAM', 'a036e00000z6NB5AAM', 'a036e000010cGWmAAM', 'a036e000010cGWoAAM', 'a036e000010cGX1AAM', 'a036e000010cHp2AAE', 'a036e000010cGX6AAM', 'a036e000010cGXCAA2', 'a036e000010cGXHAA2', 'a036e000010cGXdAAM', 'a036e000010cGXgAAM', 'a036e000010cGXkAAM', 'a036e000010cGXrAAM', 'a036e000010cGXuAAM', 'a036e000010cGYYAA2', 'a036e000010cGMKAA2', 'a036e00000z36BwAAI', 'a036e00000z4fYTAAY', 'a036e00000rH6OeAAK', 'a036e000010cGM2AAM', 'a036e00000rJjSTAA0', 'a036e00000rJjSWAA0', 'a036e00000z36AJAAY', 'a036e00000z36BiAAI', 'a036e00000z4fYLAAY', 'a036e00000z4noZAAQ', 'a036e00000zVkCBAA0', 'a036e000010c0gOAAQ', 'a036e00000zScjhAAC', 'a036e00000zVqocAAC', 'a036e00000z35qvAAA', 'a036e00000z4fMuAAI', 'a036e00000z4fMvAAI', 'a036e00000z4fPqAAI', 'a036e00000z4fPtAAI', 'a036e00000z4fQ3AAI', 'a036e00000z4fQDAAY', 'a036e00000z4fQFAAY', 'a036e00000zUoSqAAK', 'a036e000010cDtkAAE', 'a036e000010cDtfAAE', 'a036e000010cDtbAAE', 'a036e00000zVrTcAAK', 'a036e00000zVrTdAAK', 'a036e000010c3j6AAA', 'a036e000010cDtYAAU', 'a036e000010cDu1AAE', 'a036e00000z60J7AAI', 'a036e00000rJYGfAAO', 'a036e000010cDu4AAE', 'a036e000010cDu6AAE', 'a036e000010cDurAAE', 'a036e000010cDuuAAE', 'a036e00000rJYGsAAO', 'a036e00000z4fMlAAI', 'a036e00000rIxoeAAC', 'a036e00000rJYGTAA4', 'a036e00000rJYGUAA4', 'a036e00000rJYHVAA4', 'a036e00000z5wKvAAI', 'a036e00000z5wL6AAI', 'a036e00000z35yxAAA', 'a036e000010cDtEAAU', 'a036e00000z5wL7AAI', 'a036e00000rJYGwAAO', 'a036e00000rJYHKAA4', 'a036e00000rJYHQAA4', 'a036e00000rJYHTAA4', 'a036e00000rJYHUAA4', 'a036e00000rJYHXAA4', 'a036e00000rJYHfAAO', 'a036e00000rJYHnAAO', 'a036e00000rJYI3AAO', 'a036e00000rJYIBAA4', 'a036e00000rJjPfAAK', 'a036e00000rJjPmAAK', 'a036e00000rJjPpAAK', 'a036e00000z60NTAAY', 'a036e00000zUabCAAS', 'a036e00000rHnebAAC', 'a036e00000rJjPwAAK', 'a036e00000rKgmkAAC', 'a036e00000z4fMcAAI', 'a036e00000z4fMiAAI', 'a036e00000zVkYxAAK', 'a036e00000rJYIqAAO', 'a036e000010cDtNAAU', 'a036e00000rK1vHAAS', 'a036e00000rK2IHAA0', 'a036e000010cGI7AAM', 'a036e000010cGIDAA2', 'a036e000010cUbQAAU', 'a036e00000rJb7bAAC', 'a036e00000rJb7fAAC', 'a036e000010cAPjAAM', 'a036e00000z37YZAAY', 'a036e00000zSjX6AAK', 'a036e00000z364zAAA', 'a036e00000z4fVQAAY', 'a036e00000rHUTjAAO', 'a036e00000z4fVTAAY', 'a036e000010cHYIAA2', 'a036e00000rINYoAAO', 'a036e000010cGGRAA2', 'a036e00000z363PAAQ', 'a036e00000z364vAAA', 'a036e000010cHVGAA2', 'a036e000010cNHVAA2', 'a036e000010cSfjAAE', 'a036e00000z43r0AAA', 'a036e00000z4fVYAAY', 'a036e00000z4meJAAQ', 'a036e00000z4meNAAQ', 'a036e000010cGGMAA2', 'a036e000010cGGJAA2', 'a036e000010cGGQAA2', 'a036e000010cGHBAA2', 'a036e000010cGHDAA2', 'a036e000010cGHIAA2', 'a036e000010cGHmAAM', 'a036e000010cGHnAAM', 'a036e000010cGHpAAM', 'a036e00000rH7cMAAS', 'a036e00000z36JrAAI', 'a036e000010cGRZAA2', 'a036e000010cGRbAAM', 'a036e00000z4fbdAAA', 'a036e00000rH7zfAAC', 'a036e00000z4fbqAAA', 'a036e00000z4fbsAAA', 'a036e00000rIMYcAAO', 'a036e00000rINbeAAG', 'a036e000010cGSBAA2', 'a036e00000rJjT1AAK', 'a036e00000rJjT2AAK', 'a036e00000rJjT5AAK', 'a036e00000z36IaAAI', 'a036e00000z36JuAAI', 'a036e00000z3HO2AAM', 'a036e00000z3HO6AAM', 'a036e00000z3ixgAAA', 'a036e00000z43xNAAQ', 'a036e00000z4nrRAAQ', 'a036e00000z5wVVAAY', 'a036e00000z5wVkAAI', 'a036e000010cHhoAAE', 'a036e00000zUvNIAA0', 'a036e000010cGRWAA2', 'a036e00000zSjYCAA0', 'a036e000010cGRPAA2', 'a036e00000zUNXyAAO', 'a036e00000z36WkAAI', 'a036e000010d2doAAA', 'a036e000010cGRUAA2', 'a036e000010cpAfAAI', 'a036e000010cGRqAAM', 'a036e000010cGSNAA2', 'a036e000010cGSOAA2', 'a036e000010cGSQAA2', 'a036e000010cGSVAA2', 'a036e000010cGSoAAM', 'a036e000010cGSqAAM', 'a036e000010coMQAAY', 'a036e00000z6Ms2AAE', 'a036e00000z6MscAAE', 'a036e00000z6MxpAAE', 'a036e00000rHT8BAAW', 'a036e00000z4fGFAAY', 'a036e00000zV1WYAA0', 'a036e00000zVOkIAAW', 'a036e00000rJY9pAAG', 'a036e00000zUrxxAAC', 'a036e00000z3GgjAAE', 'a036e00000z5znLAAQ', 'a036e00000zVCleAAG', 'a036e00000z4f2KAAQ', 'a036e00000z35SJAAY', 'a036e00000rJY9gAAG', 'a036e00000rJ3ayAAC', 'a036e00000zVvpCAAS', 'a036e00000z4ejcAAA', 'a036e00000z4ejiAAA', 'a036e00000z4ejjAAA', 'a036e00000z4nC9AAI', 'a036e00000zVvrcAAC', 'a036e00000z6MyrAAE', 'a036e00000z6MytAAE', 'a036e00000rJY9UAAW', 'a036e00000zUZrzAAG', 'a036e00000z6MsFAAU', 'a036e00000rJY4oAAG', 'a036e00000zUZs4AAG', 'a036e00000rJY9lAAG', 'a036e00000rJYA6AAO', 'a036e00000z344vAAA', 'a036e00000rHAYhAAO', 'a036e00000rIyVWAA0', 'a036e00000rJ3ekAAC', 'a036e00000rJYACAA4', 'a036e00000rJYAEAA4', 'a036e00000rJYAMAA4', 'a036e00000rJYB4AAO', 'a036e00000rJYB5AAO', 'a036e00000zTS8sAAG', 'a036e00000rJkmOAAS', 'a036e00000rJZreAAG', 'a036e00000rIybQAAS', 'a036e00000rJYBFAA4', 'a036e00000rJYBHAA4', 'a036e00000rJYBJAA4', 'a036e00000rJZoVAAW', 'a036e00000rK1j1AAC', 'a036e00000zScRqAAK', 'a036e00000zVcZSAA0', 'a036e00000rKcs3AAC', 'a036e00000rJXneAAG', 'a036e00000rJXnmAAG', 'a036e00000rJZl7AAG', 'a036e00000rJXsYAAW', 'a036e00000rJY5eAAG', 'a036e00000rJY5gAAG', 'a036e00000rJY5mAAG', 'a036e00000zUNSAAA4', 'a036e00000z35QeAAI', 'a036e00000z35RCAAY', 'a036e00000z5vrUAAQ', 'a036e00000rJXsjAAG', 'a036e00000zUNOcAAO', 'a036e00000rJY5pAAG', 'a036e00000rJs8LAAS', 'a036e00000rJY5rAAG', 'a036e00000zUaaGAAS', 'a036e00000rJbDcAAK', 'a036e00000z35SMAAY', 'a036e00000rJXo6AAG', 'a036e00000rJXoFAAW', 'a036e00000zUNOIAA4', 'a036e00000zTSBZAA4', 'a036e00000rJXt3AAG', 'a036e00000zUb5qAAC', 'a036e00000rJXtEAAW', 'a036e00000rJY5xAAG', 'a036e00000rJY5yAAG', 'a036e00000rJY6HAAW', 'a036e00000rJY80AAG', 'a036e00000zVdApAAK', 'a036e00000zVdAoAAK', 'a036e00000z4fEtAAI', 'a036e00000zVvseAAC', 'a036e00000zVvsnAAC', 'a036e00000rJXoXAAW', 'a036e00000rJXoeAAG', 'a036e00000zUa28AAC', 'a036e00000rIdphAAC', 'a036e00000z4ejXAAQ', 'a036e00000rIsLpAAK', 'a036e00000z4elnAAA', 'a036e00000z34NtAAI', 'a036e00000rJmVZAA0', 'a036e00000z34NhAAI', 'a036e00000rJY7GAAW', 'a036e00000rJY7JAAW', 'a036e00000z6N2IAAU', 'a036e00000rJXowAAG', 'a036e00000rJXp3AAG', 'a036e00000rJbtRAAS', 'a036e00000rJc3MAAS', 'a036e00000rJdstAAC', 'a036e00000rJXq9AAG', 'a036e00000zUNOAAA4', 'a036e00000rJXqEAAW', 'a036e00000rJXqFAAW', 'a036e00000zSbZxAAK', 'a036e00000z34NrAAI', 'a036e00000z432fAAA', 'a036e00000rHnarAAC', 'a036e00000rJY3HAAW', 'a036e00000rIY4rAAG', 'a036e00000zVOltAAG', 'a036e00000rKBIpAAO', 'a036e00000rKEPPAA4', 'a036e00000zVQAuAAO', 'a036e00000zUunQAAS', 'a036e00000z344rAAA', 'a036e00000z344wAAA', 'a036e00000zUqVVAA0', 'a036e00000z4eldAAA', 'a036e00000z63LdAAI', 'a036e00000zUuyuAAC', 'a036e00000zVfguAAC', 'a036e00000z602zAAA', 'a036e00000z63WzAAI', 'a036e00000z4f51AAA', 'a036e00000z4f53AAA', 'a036e00000z4f5BAAQ', 'a036e00000z4f5CAAQ', 'a036e00000z345jAAA', 'a036e00000z4egsAAA', 'a036e00000z4n9HAAQ', 'a036e00000rJXqYAAW', 'a036e00000z5vtcAAA', 'a036e00000z4nU5AAI', 'a036e00000z4nU6AAI', 'a036e00000z6MzeAAE', 'a036e00000z6MznAAE', 'a036e00000rJXqxAAG', 'a036e00000rJXrDAAW', 'a036e00000rJXrIAAW', 'a036e00000rJXrOAAW', 'a036e00000rIUevAAG', 'a036e00000rJXuiAAG', 'a036e00000rJW2HAAW', 'a036e00000rJkcxAAC', 'a036e00000rJY40AAG', 'a036e00000rJY4BAAW', 'a036e00000z6N0DAAU', 'a036e00000zVQ9XAAW', 'a036e00000zVQqpAAG', 'a036e00000zVQqqAAG', 'a036e00000rJY83AAG', 'a036e00000rJY84AAG', 'a036e00000rJXrYAAW', 'a036e00000rJW2FAAW', 'a036e00000rJXrfAAG', 'a036e00000rJXrkAAG', 'a036e00000rJW2PAAW', 'a036e00000rJW2QAAW', 'a036e00000z5w7VAAQ', 'a036e00000z3H0oAAE', 'a036e00000zUoNDAA0', 'a036e00000z35CmAAI', 'a036e00000zVA2QAAW', 'a036e00000z34ECAAY', 'a036e00000z34CWAAY', 'a036e00000zSbZTAA0', 'a036e00000z34CdAAI', 'a036e00000zVA5MAAW', 'a036e00000rJXqQAAW', 'a036e00000rJXqHAAW', 'a036e00000z354tAAA', 'a036e00000rK1XLAA0', 'a036e00000rINR7AAO', 'a036e00000z35RbAAI', 'a036e00000rHnc6AAC', 'a036e00000zVBUpAAO', 'a036e00000z34b3AAA', 'a036e00000zVBWYAA4', 'a036e00000z34beAAA', 'a036e00000rJzIrAAK', 'a036e00000z35gbAAA', 'a036e00000z3H9IAAU', 'a036e00000zViuzAAC', 'a036e00000z4fGNAAY', 'a036e00000rIZr1AAG', 'a036e00000zUNUuAAO', 'a036e00000zUa35AAC', 'a036e000010bjsIAAQ', 'a036e000010c0V2AAI', 'a036e00000rJYC9AAO', 'a036e00000rJYCiAAO', 'a036e00000rJYCmAAO', 'a036e00000rJYCuAAO', 'a036e00000z3GmzAAE', 'a036e00000z4ex0AAA', 'a036e00000zVC6kAAG', 'a036e00000zVC6lAAG', 'a036e00000z4euFAAQ', 'a036e00000zVsVJAA0', 'a036e00000rHnaKAAS', 'a036e00000rJY18AAG', 'a036e00000rHsLCAA0', 'a036e00000rJY15AAG', 'a036e00000rHzWeAAK', 'a036e00000rJYDCAA4', 'a036e00000rJYDbAAO', 'a036e00000z4euOAAQ', 'a036e00000z4nIJAAY', 'a036e00000z4nIRAAY', 'a036e00000z6MveAAE', 'a036e00000z6Mw9AAE', 'a036e00000rJY0nAAG', 'a036e00000rJY0zAAG', 'a036e00000rJYDjAAO', 'a036e00000rJYDkAAO', 'a036e00000rJYDpAAO', 'a036e00000rJYDqAAO', 'a036e00000rJYDrAAO', 'a036e00000z35a3AAA', 'a036e00000rK1ZDAA0', 'a036e00000rJXwRAAW', 'a036e00000zVBnFAAW', 'a036e00000z34lXAAQ', 'a036e00000rI094AAC', 'a036e00000z600aAAA', 'a036e00000rJY12AAG', 'a036e00000rJY19AAG', 'a036e00000rJY1BAAW', 'a036e00000z4fGTAAY', 'a036e00000z35ZiAAI', 'a036e00000z35bNAAQ', 'a036e00000zVgKpAAK', 'a036e00000zUPQEAA4', 'a036e00000z34jdAAA', 'a036e00000rItdKAAS', 'a036e00000rJXyhAAG', 'a036e00000zUa2dAAC', 'a036e00000rJymBAAS', 'a036e00000zUa2ZAAS', 'a036e00000zVgOSAA0', 'a036e00000zVgvwAAC', 'a036e00000rHHYHAA4', 'a036e00000rHQEpAAO', 'a036e00000rJXwFAAW', 'a036e00000rJXyRAAW', 'a036e00000rJXycAAG', 'a036e00000rJXyiAAG', 'a036e00000z5zzJAAQ', 'a036e00000rJY1vAAG', 'a036e00000rJY2IAAW', 'a036e00000rJY2PAAW', 'a036e00000rJjQNAA0', 'a036e00000z60EwAAI', 'a036e00000z34ayAAA', 'a036e00000rJXypAAG', 'a036e00000z3iSKAAY', 'a036e00000zUv98AAC', 'a036e000010biEsAAI', 'a036e00000rJYBwAAO', 'a036e00000rJYBxAAO', 'a036e00000rJYEbAAO', 'a036e00000z4fJ7AAI', 'a036e00000rINSqAAO', 'a036e00000zUNUnAAO', 'a036e00000rJCNgAAO', 'a036e00000zU9d4AAC', 'a036e00000z5w2VAAQ', 'a036e00000z34v8AAA', 'a036e00000z34vBAAQ', 'a036e00000rJYC6AAO', 'a036e00000rJYE1AAO', 'a036e00000zTaKCAA0', 'a036e00000rJXwNAAW', 'a036e00000rJXwOAAW', 'a036e00000rJXzTAAW', 'a036e00000rJXzcAAG', 'a036e00000rJXziAAG', 'a036e00000rJXzmAAG', 'a036e00000rJXznAAG', 'a036e00000zVN9kAAG', 'a036e00000z3GsMAAU', 'a036e00000z3GsUAAU', 'a036e00000z60HBAAY', 'a036e00000zVA7CAAW', 'a036e00000rJXx1AAG', 'a036e00000rJXx3AAG', 'a036e00000rJXx5AAG', 'a036e00000zVBnHAAW', 'a036e00000rJXztAAG', 'a036e00000rJY0IAAW', 'a036e00000rJY0JAAW', 'a036e00000z4exJAAQ', 'a036e00000rJkUoAAK', 'a036e00000z34jVAAQ', 'a036e00000zVvrBAAS', 'a036e00000z4f00AAA', 'a036e00000zVvrDAAS', 'a036e00000z3EqcAAE', 'a036e00000z34lFAAQ', 'a036e00000zVFyoAAG', 'a036e00000z34lJAAQ', 'a036e00000zVFvcAAG', 'a036e00000zSjS8AAK', 'a036e00000rJrplAAC', 'a036e00000rJrqKAAS', 'a036e00000rJs6UAAS', 'a036e00000rJseCAAS', 'a036e00000z34ZNAAY', 'a036e00000zVFvHAAW', 'a036e00000zVFwAAAW', 'a036e00000rJYFuAAO', 'a036e00000rJjP1AAK', 'a036e00000rJjP8AAK', 'a036e00000zSbnXAAS', 'a036e00000z34laAAA', 'a036e00000z34lgAAA', 'a036e00000z3Gq4AAE', 'a036e00000z35gTAAQ', 'a036e00000z35gVAAQ', 'a036e00000zUa3QAAS', 'a036e00000zVZLRAA4', 'a036e000010bvZmAAI', 'a036e00000z4feYAAQ', 'a036e000010cGMQAA2', 'a036e000010cGMYAA2', 'a036e000010cGMaAAM', 'a036e00000rHzcPAAS', 'a036e00000zUoXIAA0', 'a036e00000rIMZfAAO', 'a036e000010cHopAAE', 'a036e000010cGNTAA2', 'a036e000010cGNUAA2', 'a036e000010cGNgAAM', 'a036e000010cGNkAAM', 'a036e00000z36QZAAY', 'a036e00000z4M59AAE', 'a036e000010bxFZAAY', 'a036e000010cGXAAA2', 'a036e000010cGWtAAM', 'a036e000010cGXDAA2', 'a036e000010cGWnAAM', 'a036e000010cGXsAAM', 'a036e000010cGXvAAM', 'a036e000010cGYZAA2', 'a036e000010cympAAA', 'a036e000010d0OVAAY', 'a036e000010d0ZKAAY', 'a036e00000z36BjAAI', 'a036e00000z36BtAAI', 'a036e00000rH612AAC', 'a036e00000rH6OdAAK', 'a036e00000zUNXPAA4', 'a036e000010cGM4AAM', 'a036e000010cGMDAA2', 'a036e00000rJjSJAA0', 'a036e00000rJmfpAAC', 'a036e00000zUa3hAAC', 'a036e00000z4fYKAAY', 'a036e00000z36AFAAY', 'a036e00000z36AHAAY', 'a036e00000z36AKAAY', 'a036e00000z4fYMAAY', 'a036e00000z4noJAAQ', 'a036e00000z5wSrAAI', 'a036e000010cGLyAAM', 'a036e00000z60ViAAI', 'a036e00000z60VkAAI', 'a036e00000zUNXSAA4', 'a036e00000z35qnAAA', 'a036e00000zUIawAAG', 'a036e00000zVqodAAC', 'a036e00000z3HBaAAM', 'a036e00000z4n2ZAAQ', 'a036e00000z4B0PAAU', 'a036e00000z3ineAAA', 'a036e00000z4fMjAAI', 'a036e00000z4fMqAAI', 'a036e00000z4fMxAAI', 'a036e00000z4fPwAAI', 'a036e00000z4fQ5AAI', 'a036e00000z4fQAAAY', 'a036e00000z4fQEAAY', 'a036e000010cDtoAAE', 'a036e00000z6N5LAAU', 'a036e00000z5wMCAAY', 'a036e00000z60NUAAY', 'a036e000010cDtOAAU', 'a036e00000z6N70AAE', 'a036e00000zUNVrAAO', 'a036e00000zUabFAAS', 'a036e000010cDtRAAU', 'a036e000010cDtZAAU', 'a036e000010cDtxAAE', 'a036e00000rJYGhAAO', 'a036e000010cDu7AAE', 'a036e000010cDuBAAU', 'a036e00000z35qjAAA', 'a036e00000rHTUdAAO', 'a036e00000rIYyXAAW', 'a036e00000rJYGOAA4', 'a036e00000z5wKqAAI', 'a036e00000rJYGeAAO', 'a036e00000rJYGiAAO', 'a036e00000rJYGlAAO', 'a036e00000z5wL2AAI', 'a036e00000rHnePAAS', 'a036e00000rJYHWAA4', 'a036e00000rHO0JAAW', 'a036e00000rJYI2AAO', 'a036e00000rJYI6AAO', 'a036e00000rJYI7AAO', 'a036e00000rJYIFAA4', 'a036e00000rJYIHAA4', 'a036e00000rJjPiAAK', 'a036e00000rJjPrAAK', 'a036e000010cDtmAAE', 'a036e000010cDtKAAU', 'a036e00000rK264AAC', 'a036e00000yuZpbAAE', 'a036e00000z35p4AAA', 'a036e00000z4fQ9AAI', 'a036e000010cGIFAA2', 'a036e000010ciXwAAI', 'a036e00000rJUmwAAG', 'a036e000010cAPmAAM', 'a036e000010cAPoAAM', 'a036e00000z37YWAAY', 'a036e00000z37YYAAY', 'a036e000010cGHMAA2', 'a036e00000rH2XKAA0', 'a036e00000z4fVRAAY', 'a036e00000z4fVeAAI', 'a036e00000z5wPlAAI', 'a036e00000rJYL4AAO', 'a036e00000z363WAAQ', 'a036e00000z3HHjAAM', 'a036e000010cUl8AAE', 'a036e00000z4nlTAAQ', 'a036e00000zUFt7AAG', 'a036e000010cGGOAA2', 'a036e000010cHUQAA2', 'a036e00000z60TJAAY', 'a036e00000z63m4AAA', 'a036e00000zSjX0AAK', 'a036e000010cGGTAA2', 'a036e000010cGH9AAM', 'a036e000010cGHCAA2', 'a036e000010cGHxAAM', 'a036e00000z36KSAAY', 'a036e00000z36JxAAI', 'a036e00000z36K0AAI', 'a036e00000rH7coAAC', 'a036e00000z36JwAAI', 'a036e00000zVALLAA4', 'a036e00000rH7zeAAC', 'a036e00000z4fbeAAA', 'a036e00000rH7zgAAC', 'a036e00000rH8BUAA0', 'a036e00000z4fbtAAA', 'a036e00000z60ZCAAY', 'a036e00000rJdy3AAC', 'a036e00000rJjT9AAK', 'a036e00000z4fboAAA', 'a036e00000z4fbpAAA', 'a036e00000rKEP5AAO', 'a036e00000rKXDqAAO', 'a036e00000z36IJAAY', 'a036e00000z36IiAAI', 'a036e00000z5wVRAAY', 'a036e000010cGS9AAM', 'a036e000010cGRcAAM', 'a036e000010cHigAAE', 'a036e00000z6N9vAAE', 'a036e00000z36WhAAI', 'a036e00000z36WiAAI', 'a036e000010cHxGAAU', 'a036e00000z3HSzAAM', 'a036e00000z4msSAAQ', 'a036e000010cGcNAAU', 'a036e000010cGcOAAU', 'a036e000010cGcTAAU', 'a036e000010cGRQAA2', 'a036e000010cGRoAAM', 'a036e000010cGRrAAM', 'a036e000010cGRtAAM', 'a036e000010cGRuAAM', 'a036e000010cGRwAAM', 'a036e000010cGSPAA2', 'a036e000010cGSSAA2', 'a036e000010cGSYAA2', 'a036e000010cGSbAAM', 'a036e000010cGSjAAM', 'a036e000010cGSmAAM', 'a036e000010cmBwAAI', 'a036e00000z4fGCAAY', 'a036e00000rHUTRAA4', 'a036e00000rHUTVAA4', 'a036e00000zUrxjAAC', 'a036e00000zUs0AAAS', 'a036e00000z34E9AAI', 'a036e00000z34T0AAI', 'a036e00000z353JAAQ', 'a036e00000zVOmRAAW', 'a036e00000zVOmMAAW', 'a036e00000rJY9dAAG', 'a036e00000rHncEAAS', 'a036e00000rJY9QAAW', 'a036e00000z34EOAAY', 'a036e00000z4mVSAAY', 'a036e00000z3GgrAAE', 'a036e00000z3iIvAAI', 'a036e00000z4AUaAAM', 'a036e00000z3Gv0AAE', 'a036e00000z3GvSAAU', 'a036e00000rHzYUAA0', 'a036e00000z35RYAAY', 'a036e00000rJY9IAAW', 'a036e00000z4ejhAAA', 'a036e00000zUsYvAAK', 'a036e00000z5vtfAAA', 'a036e00000z4f2PAAQ', 'a036e00000z6MyfAAE', 'a036e00000zUNTyAAO', 'a036e00000zSjUBAA0', 'a036e00000zUZrxAAG', 'a036e00000rJY9XAAW', 'a036e00000z5znKAAQ', 'a036e00000zVv2eAAC', 'a036e00000z34NgAAI', 'a036e00000zVhUFAA0', 'a036e00000z3iWhAAI', 'a036e00000rJXo2AAG', 'a036e00000zUa26AAC', 'a036e00000rJXooAAG', 'a036e00000zUa1rAAC', 'a036e00000rHnXEAA0', 'a036e00000rHnXFAA0', 'a036e00000z34NlAAI', 'a036e00000rIi6sAAC', 'a036e00000rIwAfAAK', 'a036e00000rJkgpAAC', 'a036e00000rHeHCAA0', 'a036e00000rJYALAA4', 'a036e00000rJYAqAAO', 'a036e00000rJYArAAO', 'a036e00000rJYB2AAO', 'a036e00000rItjxAAC', 'a036e00000rJ0GLAA0', 'a036e00000rJUi5AAG', 'a036e00000rJXsQAAW', 'a036e00000zUNSDAA4', 'a036e00000rJY5jAAG', 'a036e00000rINO4AAO', 'a036e00000rIti0AAC', 'a036e00000rKFPmAAO', 'a036e00000z3GyQAAU', 'a036e00000rJYBNAA4', 'a036e00000rJxIlAAK', 'a036e00000rJXniAAG', 'a036e00000rJXnoAAG', 'a036e00000rJXsSAAW', 'a036e00000z5vvqAAA', 'a036e00000zUb5mAAC', 'a036e00000rJdiyAAC', 'a036e00000rJXsWAAW', 'a036e00000rJXscAAG', 'a036e00000zU9dcAAC', 'a036e00000rJY5lAAG', 'a036e00000zVvspAAC', 'a036e00000z4fEwAAI', 'a036e00000z35ReAAI', 'a036e00000z35RhAAI', 'a036e00000zUv5RAAS', 'a036e00000rJXsgAAG', 'a036e00000rKBJdAAO', 'a036e00000zVcaSAAS', 'a036e00000zVcdhAAC', 'a036e00000z35SQAAY', 'a036e00000zVcYfAAK', 'a036e00000z5zlMAAQ', 'a036e00000zSbO7AAK', 'a036e00000zTMntAAG', 'a036e00000rJXoAAAW', 'a036e00000rJXoCAAW', 'a036e00000zUa1sAAC', 'a036e00000rJXoOAAW', 'a036e00000rINH3AAO', 'a036e00000rJXqRAAW', 'a036e00000rIMC0AAO', 'a036e00000rJXtRAAW', 'a036e00000rK954AAC', 'a036e00000rJY6DAAW', 'a036e00000rJY6JAAW', 'a036e00000rJY6LAAW', 'a036e00000z4fEnAAI', 'a036e00000z4fEmAAI', 'a036e00000zVdAnAAK', 'a036e00000z4fEuAAI', 'a036e00000zVvsfAAC', 'a036e00000z4fEzAAI', 'a036e00000rJXoYAAW', 'a036e00000rJXofAAG', 'a036e00000rJXodAAG', 'a036e00000rJXoiAAG', 'a036e00000rJXomAAG', 'a036e00000z34EHAAY', 'a036e00000rIrqNAAS', 'a036e00000z4eloAAA', 'a036e00000rJmazAAC', 'a036e00000rJmhfAAC', 'a036e00000rJY3YAAW', 'a036e00000rJY74AAG', 'a036e00000rK96HAAS', 'a036e00000rJY7MAAW', 'a036e00000rJfQZAA0', 'a036e00000rJXorAAG', 'a036e00000rJXouAAG', 'a036e00000z4egqAAA', 'a036e00000rJbyHAAS', 'a036e00000yuZD0AAM', 'a036e00000z344AAAQ', 'a036e00000z344mAAA', 'a036e00000zUqUAAA0', 'a036e00000zUunqAAC', 'a036e00000rJXqGAAW', 'a036e00000z34NjAAI', 'a036e00000zV1WyAAK', 'a036e00000z34NsAAI', 'a036e00000z354xAAA', 'a036e00000z344tAAA', 'a036e00000zSbPUAA0', 'a036e00000rJXqMAAW', 'a036e00000z4nEOAAY', 'a036e00000z4nEPAAY', 'a036e00000z6MteAAE', 'a036e00000rJY3DAAW', 'a036e00000rJY3EAAW', 'a036e00000z4f54AAA', 'a036e00000z4f56AAA', 'a036e00000z4nU2AAI', 'a036e00000z3452AAA', 'a036e00000z345eAAA', 'a036e00000z345iAAA', 'a036e00000zUsH4AAK', 'a036e00000zUtkpAAC', 'a036e00000zVv2xAAC', 'a036e00000rIUalAAG', 'a036e00000rJj3QAAS', 'a036e00000rJY3WAAW', 'a036e00000rJY3XAAW', 'a036e00000rJbAVAA0', 'a036e00000z4nU3AAI', 'a036e00000zSjPPAA0', 'a036e00000zVv1yAAC', 'a036e00000rJXrGAAW', 'a036e00000rJW2GAAW', 'a036e00000rJXugAAG', 'a036e00000rJY46AAG', 'a036e00000rJY4DAAW', 'a036e00000rJY4EAAW', 'a036e00000rJY4FAAW', 'a036e00000rHnboAAC', 'a036e00000rJXrdAAG', 'a036e00000rJXriAAG', 'a036e00000rJXrnAAG', 'a036e00000rJXrqAAG', 'a036e00000rJXuoAAG', 'a036e00000rJXupAAG', 'a036e00000rJW2RAAW', 'a036e00000rJXurAAG', 'a036e00000rJY4dAAG', 'a036e00000rJY4gAAG', 'a036e00000rJY4iAAG', 'a036e00000rJY4jAAG', 'a036e00000rJY4nAAG', 'a036e00000rJY4qAAG', 'a036e00000rJjLyAAK', 'a036e00000rJjM3AAK', 'a036e00000rJjM6AAK', 'a036e00000rJjM8AAK', 'a036e00000rJjMAAA0', 'a036e00000rJjMBAA0', 'a036e00000zUoNBAA0', 'a036e00000zVA8wAAG', 'a036e00000z34EAAAY', 'a036e00000z34T7AAI', 'a036e00000zV5yhAAC', 'a036e00000rJ0HNAA0', 'a036e00000zUsGzAAK', 'a036e00000zTa0MAAS', 'a036e00000zVBWSAA4', 'a036e00000zVBVXAA4', 'a036e00000z4Af2AAE', 'a036e00000z4ex4AAA', 'a036e00000z35gZAAQ', 'a036e00000z35gaAAA', 'a036e00000z35gdAAA', 'a036e00000z35gjAAA', 'a036e00000z35i6AAA', 'a036e00000zViXTAA0', 'a036e00000zViWtAAK', 'a036e00000z4fJ8AAI', 'a036e00000z4fJDAAY', 'a036e00000z5wFoAAI', 'a036e00000rJYC0AAO', 'a036e00000rJYC4AAO', 'a036e000010bb9MAAQ', 'a036e000010c0pLAAQ', 'a036e00000z5wFaAAI', 'a036e00000rJYCRAA4', 'a036e00000rJYCgAAO', 'a036e00000rJYCnAAO', 'a036e00000rJYCpAAO', 'a036e00000rJYCqAAO', 'a036e00000rJYCyAAO', 'a036e00000z35iHAAQ', 'a036e00000zVibvAAC', 'a036e00000zVBVaAAO', 'a036e00000z3GmfAAE', 'a036e00000rHUSnAAO', 'a036e00000zVvqDAAS', 'a036e00000z4euEAAQ', 'a036e00000z5zzCAAQ', 'a036e00000zUNRMAA4', 'a036e00000rHzWiAAK', 'a036e00000rJYDHAA4', 'a036e00000rJYDKAA4', 'a036e00000rJYDLAA4', 'a036e00000rJYDOAA4', 'a036e00000rJYDSAA4', 'a036e00000rJYDfAAO', 'a036e00000z4nIKAAY', 'a036e00000z4nIQAAY', 'a036e00000z6MwDAAU', 'a036e00000zSblRAAS', 'a036e00000rJY0vAAG', 'a036e00000zUNRFAA4', 'a036e00000rJYDmAAO', 'a036e00000rJYDoAAO', 'a036e00000rJdfQAAS', 'a036e00000rJoetAAC', 'a036e00000rJXwIAAW', 'a036e00000z34bZAAQ', 'a036e00000z4ex2AAA', 'a036e00000z4exGAAQ', 'a036e00000rJY2MAAW', 'a036e00000rJY21AAG', 'a036e00000rJY1AAAW', 'a036e00000z600bAAA', 'a036e00000z35ZkAAI', 'a036e00000z35ZqAAI', 'a036e00000z35a7AAA', 'a036e00000zUNRNAA4', 'a036e00000z34vAAAQ', 'a036e00000zUa2aAAC', 'a036e00000rJtrlAAC', 'a036e00000rJts5AAC', 'a036e00000rJXykAAG', 'a036e00000zVgK7AAK', 'a036e00000z3H67AAE', 'a036e00000z3ifyAAA', 'a036e00000rHQEoAAO', 'a036e00000rHna9AAC', 'a036e00000rJXynAAG', 'a036e00000rJY2AAAW', 'a036e00000z600eAAA', 'a036e00000rJY2NAAW', 'a036e00000rJY2OAAW', 'a036e00000rJjQMAA0', 'a036e00000rJkkcAAC', 'a036e00000z4fGHAAY', 'a036e00000z4fGXAAY', 'a036e00000z4naeAAA', 'a036e00000z35iNAAQ', 'a036e00000rIMFpAAO', 'a036e00000rINKvAAO', 'a036e00000rJ94LAAS', 'a036e00000z5w2FAAQ', 'a036e00000rJXyzAAG', 'a036e00000rJklBAAS', 'a036e00000rJrtfAAC', 'a036e00000rJskVAAS', 'a036e00000rJuhAAAS', 'a036e00000z5w4qAAA', 'a036e00000rJxKrAAK', 'a036e00000z6N3MAAU', 'a036e00000z6N3aAAE', 'a036e00000z6N3qAAE', 'a036e000010biOCAAY', 'a036e00000rHndHAAS', 'a036e00000rJYELAA4', 'a036e00000zUoIMAA0', 'a036e00000zUFkIAAW', 'a036e00000z5zzHAAQ', 'a036e00000zTO7vAAG', 'a036e00000z34u0AAA', 'a036e00000z60EzAAI', 'a036e00000zUNUvAAO', 'a036e00000zVAE9AAO', 'a036e00000zU9d2AAC', 'a036e00000zSbnvAAC', 'a036e00000rJXzNAAW', 'a036e00000rJY1UAAW', 'a036e00000rJXzUAAW', 'a036e00000rJY22AAG', 'a036e00000rJXzjAAG', 'a036e00000rJXzlAAG', 'a036e00000rJXzoAAG', 'a036e00000rJXzpAAG', 'a036e00000zSc2dAAC', 'a036e00000z34vQAAQ', 'a036e00000zUNUjAAO', 'a036e00000rJXwvAAG', 'a036e00000rJdnWAAS', 'a036e00000rJY0EAAW', 'a036e00000rJbvhAAC', 'a036e00000zVvr8AAC', 'a036e00000z4f04AAA', 'a036e00000z5w4pAAA', 'a036e00000z6MyIAAU', 'a036e00000zUoQuAAK', 'a036e00000z60HJAAY', 'a036e00000rJXxMAAW', 'a036e00000rJXxaAAG', 'a036e00000rJXxiAAG', 'a036e00000zVFvJAAW', 'a036e00000z4ex6AAA', 'a036e00000z34tyAAA', 'a036e00000z34jfAAA', 'a036e00000zUvAJAA0', 'a036e00000rJYEsAAO', 'a036e00000rJYEuAAO', 'a036e00000rJYF1AAO', 'a036e000010c0ZJAAY', 'a036e00000z4exAAAQ', 'a036e00000z34azAAA', 'a036e00000z34ZMAAY', 'a036e00000z34jaAAA', 'a036e00000rJYFiAAO', 'a036e00000rJYFxAAO', 'a036e00000zVBVjAAO', 'a036e00000zW1QwAAK', 'a036e00000z34lhAAA', 'a036e00000rJkjaAAC', 'a036e00000z35gPAAQ', 'a036e00000z35gUAAQ', 'a036e000010cGXIAA2', 'a036e000010cGX2AAM', 'a036e00000zUoVeAAK', 'a036e00000z36R8AAI', 'a036e00000z4fekAAA', 'a036e000010bwixAAA', 'a036e000010cGMIAA2', 'a036e000010cGMLAA2', 'a036e000010cGMRAA2', 'a036e00000rH9eiAAC', 'a036e00000z36R5AAI', 'a036e00000rINbvAAG', 'a036e000010cGWyAAM', 'a036e00000rJezVAAS', 'a036e000010cGNOAA2', 'a036e000010cGNjAAM', 'a036e000010ciHZAAY', 'a036e000010bvaaAAA', 'a036e000010cz1dAAA', 'a036e00000z4mnCAAQ', 'a036e00000z4nujAAA', 'a036e00000z4nukAAA', 'a036e000010cGWzAAM', 'a036e000010cHo2AAE', 'a036e000010cGXFAA2', 'a036e000010cGXcAAM', 'a036e000010cGXeAAM', 'a036e000010cGYWAA2', 'a036e000010cGYcAAM', 'a036e000010cGYdAAM', 'a036e000010cHnoAAE', 'a036e000010cNI5AAM', 'a036e000010cNIYAA2', 'a036e000010cUfIAAU', 'a036e00000rH62TAAS', 'a036e00000rH65UAAS', 'a036e00000z4fYWAAY', 'a036e00000zUoVjAAK', 'a036e00000rIMXJAA4', 'a036e00000rIMXVAA4', 'a036e00000rJjSHAA0', 'a036e00000rJjSNAA0', 'a036e00000rJjSSAA0', 'a036e00000z36AEAAY', 'a036e00000z36AIAAY', 'a036e000010cUdmAAE', 'a036e000010cl0dAAA', 'a036e00000z4mi1AAA', 'a036e00000z4noIAAQ', 'a036e00000z4noKAAQ', 'a036e00000z60VcAAI', 'a036e00000zTaVZAA0', 'a036e000010c3SbAAI', 'a036e00000rKEQSAA4', 'a036e00000z35xvAAA', 'a036e00000z35xxAAA', 'a036e00000z35xzAAA', 'a036e00000z35yzAAA', 'a036e00000z35z0AAA', 'a036e00000zVkCtAAK', 'a036e00000z35qxAAA', 'a036e00000z4B0QAAU', 'a036e00000z4fMsAAI', 'a036e00000z3HEVAA2', 'a036e000010cDtcAAE', 'a036e00000z4fMeAAI', 'a036e00000z4n2bAAA', 'a036e00000zVr6TAAS', 'a036e00000z4fQ0AAI', 'a036e00000z4fQ4AAI', 'a036e00000z6N5cAAE', 'a036e00000zVkSOAA0', 'a036e00000z5wMBAAY', 'a036e00000z5wMLAAY', 'a036e000010cDtLAAU', 'a036e00000zUNVkAAO', 'a036e00000zUNVnAAO', 'a036e000010cDttAAE', 'a036e000010cDuwAAE', 'a036e00000rHTLNAA4', 'a036e00000rIY8OAAW', 'a036e00000z35qlAAA', 'a036e00000zSci3AAC', 'a036e00000rIZMhAAO', 'a036e00000rJYGcAAO', 'a036e00000rJYGkAAO', 'a036e00000rJYGqAAO', 'a036e00000z60NVAAY', 'a036e00000rJYHNAA4', 'a036e00000rJYHRAA4', 'a036e00000rJYHgAAO', 'a036e00000rJYHlAAO', 'a036e00000rJYI1AAO', 'a036e00000z35z3AAA', 'a036e00000rHneJAAS', 'a036e00000rJYICAA4', 'a036e00000rJjPeAAK', 'a036e00000rJjPhAAK', 'a036e00000rHneXAAS', 'a036e000010cNcMAAU', 'a036e00000z5wMIAAY', 'a036e00000zUNVpAAO', 'a036e00000yuZpYAAU', 'a036e00000z35p6AAA', 'a036e00000z5wM1AAI', 'a036e00000zUNViAAO', 'a036e00000rJupiAAC', 'a036e00000rK1iPAAS', 'a036e000010cDtjAAE', 'a036e000010cGI6AAM', 'a036e000010cGI9AAM', 'a036e00000rJb7aAAC', 'a036e00000rJb7lAAC', 'a036e00000z37YdAAI', 'a036e000010cGGHAA2', 'a036e00000z3657AAA', 'a036e00000rH1w7AAC', 'a036e00000rH1wyAAC', 'a036e00000z364xAAA', 'a036e00000z4fVPAAY', 'a036e00000rItgDAAS', 'a036e00000z363UAAQ', 'a036e00000z4fVNAAY', 'a036e000010cGHNAA2', 'a036e00000z60THAAY', 'a036e00000zTODaAAO', 'a036e000010cHYSAA2', 'a036e00000z60TRAAY', 'a036e00000z6N89AAE', 'a036e000010cGGZAA2', 'a036e000010cGH6AAM', 'a036e000010cGHfAAM', 'a036e000010cGHjAAM', 'a036e000010cGHrAAM', 'a036e000010cGHyAAM', 'a036e000010cGI0AAM', 'a036e000010cGI1AAM', 'a036e00000rH7dMAAS', 'a036e00000z36JpAAI', 'a036e00000rH7tMAAS', 'a036e00000rH7tOAAS', 'a036e00000rH7zdAAC', 'a036e00000z4fbaAAA', 'a036e00000z4fbfAAA', 'a036e00000z4fbhAAA', 'a036e00000z4fbkAAA', 'a036e00000z5wVdAAI', 'a036e000010cGRIAA2', 'a036e00000rJjStAAK', 'a036e00000rJohiAAC', 'a036e00000z36IEAAY', 'a036e00000z36IGAAY', 'a036e00000z36IHAAY', 'a036e00000z36IcAAI', 'a036e00000z36IgAAI', 'a036e00000z36KIAAY', 'a036e00000z36KKAAY', 'a036e00000z36KOAAY', 'a036e00000z3HOAAA2', 'a036e00000z5wVTAAY', 'a036e000010cGRSAA2', 'a036e00000z5wVcAAI', 'a036e000010cGS6AAM', 'a036e00000z6MkCAAU', 'a036e00000z6NACAA2', 'a036e00000rIUdpAAG', 'a036e00000zSjYEAA0', 'a036e000010cGSCAA2', 'a036e00000zUNXwAAO', 'a036e000010cHjNAAU', 'a036e00000z6NBzAAM', 'a036e000010cGcPAAU', 'a036e000010cGcRAAU', 'a036e000010cGcSAAU', 'a036e000010bjv0AAA', 'a036e000010cGRnAAM', 'a036e000010cGRhAAM', 'a036e000010cGRmAAM', 'a036e000010cGS1AAM', 'a036e000010cGSUAA2', 'a036e000010cGSXAA2', 'a036e000010cGSaAAM', 'a036e000010cGSdAAM', 'a036e000010cGSpAAM', 'a036e000010coxWAAQ', 'a036e00000z6Mx6AAE', 'a036e00000z6MxzAAE', 'a036e00000rHHabAAG', 'a036e00000z4fGEAAY', 'a036e00000z34E6AAI', 'a036e00000zUrybAAC', 'a036e00000z34EBAAY', 'a036e00000zUs0OAAS', 'a036e00000z34EKAAY', 'a036e00000z34NmAAI', 'a036e00000rJXuhAAG', 'a036e00000rJXujAAG', 'a036e00000rJXukAAG', 'a036e00000z353BAAQ', 'a036e00000z353DAAQ', 'a036e00000z353HAAQ', 'a036e00000zSnDLAA0', 'a036e00000zVOkHAAW', 'a036e00000z354sAAA', 'a036e00000zUuyjAAC', 'a036e00000rHQFNAA4', 'a036e00000rJW90AAG', 'a036e00000rHQFQAA4', 'a036e00000rJY9nAAG', 'a036e00000rHQFTAA4', 'a036e00000rHQFUAA4', 'a036e00000rJY9OAAW', 'a036e00000z43RrAAI', 'a036e00000z4AhuAAE', 'a036e00000rJ3b2AAC', 'a036e00000zVfdaAAC', 'a036e00000zSjUGAA0', 'a036e00000rJY9VAAW', 'a036e00000z63axAAA', 'a036e00000z63JKAAY', 'a036e00000z6Ms6AAE', 'a036e00000z6MtaAAE', 'a036e00000z6MsWAAU', 'a036e00000rHnYEAA0', 'a036e00000rI7wKAAS', 'a036e00000zVPa6AAG', 'a036e00000z63UcAAI', 'a036e00000rJY3UAAW', 'a036e00000z3538AAA', 'a036e00000z3539AAA', 'a036e00000rJY9bAAG', 'a036e00000zU9e5AAC', 'a036e00000rKZNEAA4', 'a036e00000rJYA2AAO', 'a036e00000z344BAAQ', 'a036e00000z344nAAA', 'a036e00000rINHiAAO', 'a036e00000rJXsiAAG', 'a036e00000zTCoBAAW', 'a036e00000rIMKQAA4', 'a036e00000rJYAzAAO', 'a036e00000rHnXOAA0', 'a036e00000rHyq5AAC', 'a036e00000rIrfxAAC', 'a036e00000z3GdqAAE', 'a036e00000rJXnqAAG', 'a036e00000z5vvrAAA', 'a036e00000rJY5aAAG', 'a036e00000zUaa7AAC', 'a036e00000rJCqfAAG', 'a036e00000z4AmuAAE', 'a036e00000rJY5ZAAW', 'a036e00000z35R9AAI', 'a036e00000rJXnpAAG', 'a036e00000rJZrtAAG', 'a036e00000rJXsfAAG', 'a036e00000zVcaZAAS', 'a036e00000z35RdAAI', 'a036e00000z35SDAAY', 'a036e00000zVcZTAA0', 'a036e00000zUa27AAC', 'a036e00000rJXo4AAG', 'a036e00000rJXo5AAG', 'a036e00000z5zlOAAQ', 'a036e00000rJXqKAAW', 'a036e00000zUNOBAA4', 'a036e00000z3iLUAAY', 'a036e00000rJY5qAAG', 'a036e00000zUaaCAAS', 'a036e00000rJY5vAAG', 'a036e00000zUv5dAAC', 'a036e00000zVcYbAAK', 'a036e00000zVcZfAAK', 'a036e00000rJXoMAAW', 'a036e00000rJXoNAAW', 'a036e00000rJXqVAAW', 'a036e00000rJkN5AAK', 'a036e00000rJXqSAAW', 'a036e00000rJdjXAAS', 'a036e00000rJXsuAAG', 'a036e00000rJkfmAAC', 'a036e00000zUup1AAC', 'a036e00000rJY69AAG', 'a036e00000rJY6AAAW', 'a036e00000rJY6EAAW', 'a036e00000rJY6GAAW', 'a036e00000rJY6xAAG', 'a036e00000rJY6zAAG', 'a036e00000z4fEgAAI', 'a036e00000z4fEhAAI', 'a036e00000z4nXQAAY', 'a036e00000z5vrRAAQ', 'a036e00000zVA2XAAW', 'a036e00000rJbNzAAK', 'a036e00000rJdehAAC', 'a036e00000rJdkVAAS', 'a036e00000rJkVcAAK', 'a036e00000rJmU2AAK', 'a036e00000rJme3AAC', 'a036e00000rHAjVAAW', 'a036e00000rJY78AAG', 'a036e00000rJY7AAAW', 'a036e00000rJY7BAAW', 'a036e00000rJY7HAAW', 'a036e00000rJY7LAAW', 'a036e00000z6N2aAAE', 'a036e00000zUNU2AAO', 'a036e00000rJXoyAAG', 'a036e00000rJXozAAG', 'a036e00000rJb31AAC', 'a036e00000rJbpYAAS', 'a036e00000z5vtWAAQ', 'a036e00000rJXqBAAW', 'a036e00000rJXqLAAW', 'a036e00000z63JJAAY', 'a036e00000zUtKHAA0', 'a036e00000z34NqAAI', 'a036e00000zUb5vAAC', 'a036e00000rJY3aAAG', 'a036e00000rIRdMAAW', 'a036e00000zUoM6AAK', 'a036e00000rKFSvAAO', 'a036e00000z35CoAAI', 'a036e00000zUqVoAAK', 'a036e00000z3450AAA', 'a036e00000rJXqUAAW', 'a036e00000zVvpgAAC', 'a036e00000z4eluAAA', 'a036e00000z4nEQAAY', 'a036e00000z5vvtAAA', 'a036e00000rJY3FAAW', 'a036e00000rJY3GAAW', 'a036e00000rJY3JAAW', 'a036e00000rJY3LAAW', 'a036e00000z4f4sAAA', 'a036e00000zVvryAAC', 'a036e00000z4f5AAAQ', 'a036e00000z3451AAA', 'a036e00000z345gAAA', 'a036e00000z3Ge2AAE', 'a036e00000z3Ge6AAE', 'a036e00000z4n9EAAQ', 'a036e00000z4n9GAAQ', 'a036e00000z5vrOAAQ', 'a036e00000z5vrQAAQ', 'a036e00000z6MrKAAU', 'a036e00000rJXqXAAW', 'a036e00000zUsH0AAK', 'a036e00000zUuo1AAC', 'a036e00000rJXqkAAG', 'a036e00000zUb5uAAC', 'a036e00000zVv31AAC', 'a036e00000z34T2AAI', 'a036e00000z34T3AAI', 'a036e00000rIUcwAAG', 'a036e00000z6MzhAAE', 'a036e00000zUr6GAAS', 'a036e00000zVv1zAAC', 'a036e00000rJXtBAAW', 'a036e00000rJXrLAAW', 'a036e00000rJXrNAAW', 'a036e00000rJXuqAAG', 'a036e00000rIUewAAG', 'a036e00000yszvCAAQ', 'a036e00000rJY3uAAG', 'a036e00000rJY3yAAG', 'a036e00000rJY4HAAW', 'a036e00000zSjTEAA0', 'a036e00000rI8YVAA0', 'a036e00000rJY89AAG', 'a036e00000rJXroAAG', 'a036e00000rJXumAAG', 'a036e00000rJXunAAG', 'a036e00000zSbhQAAS', 'a036e00000rJY4lAAG', 'a036e00000rJjLzAAK', 'a036e00000rJzC0AAK', 'a036e00000z3H0rAAE', 'a036e00000rHnbIAAS', 'a036e00000rJY5kAAG', 'a036e00000z43SvAAI', 'a036e00000rJbJTAA0', 'a036e00000z34CeAAI', 'a036e00000z4nHKAAY', 'a036e00000rJXqdAAG', 'a036e00000rJjMEAA0', 'a036e00000rJkV8AAK', 'a036e00000rJY9rAAG', 'a036e00000z35RiAAI', 'a036e00000rHHaLAAW', 'a036e00000zVBZfAAO', 'a036e00000z34bbAAA', 'a036e00000z3GqWAAU', 'a036e00000z35geAAA', 'a036e00000zViWcAAK', 'a036e00000z35iDAAQ', 'a036e00000z3H8qAAE', 'a036e00000z3H9MAAU', 'a036e00000z43hdAAA', 'a036e00000z4fIxAAI', 'a036e00000z60F6AAI', 'a036e00000rJYBiAAO', 'a036e00000rJYBjAAO', 'a036e00000z4fJRAAY', 'a036e00000z4fJSAAY', 'a036e00000zVjNfAAK', 'a036e00000rJYC2AAO', 'a036e00000rJYC3AAO', 'a036e00000z5wFYAAY', 'a036e00000rJYCZAA4', 'a036e00000rJYCfAAO', 'a036e00000rJYChAAO', 'a036e00000rJYCxAAO', 'a036e00000zScdKAAS', 'a036e00000zVuVsAAK', 'a036e000010bb6rAAA', 'a036e00000z4eu2AAA', 'a036e00000z4euDAAQ', 'a036e00000z6MwlAAE', 'a036e00000z6Mx7AAE', 'a036e00000z6MxDAAU', 'a036e00000rJYDWAA4', 'a036e00000rJYDdAAO', 'a036e00000rJYDgAAO', 'a036e00000z4nIHAAY', 'a036e00000z4nIIAAY', 'a036e00000z4nIVAAY', 'a036e00000rIMIEAA4', 'a036e00000rJCoDAAW', 'a036e00000rJumAAAS', 'a036e00000zVvqPAAS', 'a036e00000z4exKAAQ', 'a036e00000z4exMAAQ', 'a036e00000z34lYAAQ', 'a036e00000rJY11AAG', 'a036e00000zUa2YAAS', 'a036e00000z4fGVAAY', 'a036e00000z35ZeAAI', 'a036e00000z35ZfAAI', 'a036e00000z35ZgAAI', 'a036e00000z35ZnAAI', 'a036e00000z35ZpAAI', 'a036e00000z35ZsAAI', 'a036e00000z35a5AAA', 'a036e00000z34jcAAA', 'a036e00000rIXseAAG', 'a036e00000rJts6AAC', 'a036e00000rJXyPAAW', 'a036e00000z5w4rAAA', 'a036e00000z5w4sAAA', 'a036e00000rJY1cAAG', 'a036e00000rJY1pAAG', 'a036e00000rJY1rAAG', 'a036e00000z35bWAAQ', 'a036e00000z4mwkAAA', 'a036e00000z60F3AAI', 'a036e00000z43f1AAA', 'a036e00000rJXwDAAW', 'a036e00000zTMqVAAW', 'a036e00000rHQF0AAO', 'a036e00000rJXyWAAW', 'a036e00000rJXyYAAW', 'a036e00000rJXyZAAW', 'a036e00000rJXzkAAG', 'a036e00000rJb0NAAS', 'a036e00000rJY28AAG', 'a036e00000rJY2JAAW', 'a036e00000rJYEcAAO', 'a036e00000rHnZCAA0', 'a036e00000rJsHUAA0', 'a036e00000z63PgAAI', 'a036e00000rJuIzAAK', 'a036e00000rJyoXAAS', 'a036e00000zVNAZAA4', 'a036e00000z60F9AAI', 'a036e00000z63dOAAQ', 'a036e00000zUNUlAAO', 'a036e00000rJXw6AAG', 'a036e00000rJXw9AAG', 'a036e00000rJXzAAAW', 'a036e00000z4sPJAAY', 'a036e00000zVN9VAAW', 'a036e00000zVNAxAAO', 'a036e00000zVNBwAAO', 'a036e00000z34vIAAQ', 'a036e00000rINSuAAO', 'a036e00000rJ6oRAAS', 'a036e00000rJXwEAAW', 'a036e00000z3EqOAAU', 'a036e00000rJXwLAAW', 'a036e00000rJeA6AAK', 'a036e00000rJXwMAAW', 'a036e00000rJXzQAAW', 'a036e00000zVNEmAAO', 'a036e00000zUNUmAAO', 'a036e00000rJXwxAAG', 'a036e00000rJXx7AAG', 'a036e00000rJXzrAAG', 'a036e00000rJts0AAC', 'a036e00000z3GsZAAU', 'a036e00000z4AgeAAE', 'a036e00000z600WAAQ', 'a036e00000z6MxfAAE', 'a036e00000zUXDzAAO', 'a036e00000zUa2wAAC', 'a036e00000zUoQvAAK', 'a036e00000zUa33AAC', 'a036e00000rJXxNAAW', 'a036e00000rJXxQAAW', 'a036e00000rJv2yAAC', 'a036e00000z34jgAAA', 'a036e00000zUvACAA0', 'a036e00000rJYEqAAO', 'a036e00000rJYEvAAO', 'a036e00000rJYEyAAO', 'a036e00000rJmnFAAS', 'a036e00000zVBUVAA4', 'a036e00000zSbvnAAC', 'a036e00000rJYFmAAO', 'a036e00000rJYFqAAO', 'a036e00000rJYFsAAO', 'a036e00000rJYFwAAO', 'a036e00000rJjP7AAK', 'a036e00000rJjPBAA0', 'a036e00000rJjPEAA0', 'a036e00000z34jbAAA', 'a036e00000z34amAAA', 'a036e00000zVBUnAAO', 'a036e00000z3Gq5AAE', 'a036e00000rJkmyAAC', 'a036e00000rJmf6AAC', 'a036e00000rJomJAAS', 'a036e00000zScbzAAC', 'a036e000010c0cIAAQ', 'a036e000010cGWwAAM', 'a036e000010cGX9AAM', 'a036e000010cGM1AAM', 'a036e00000zUa3UAAS', 'a036e000010cGMAAA2', 'a036e00000rH91BAAS', 'a036e00000z36RKAAY', 'a036e00000rH9HiAAK', 'a036e00000z441FAAQ', 'a036e000010bve4AAA', 'a036e000010cGMPAA2', 'a036e000010cGMVAA2', 'a036e000010cGMWAA2', 'a036e00000rH9ekAAC', 'a036e000010cGX0AAM', 'a036e000010cGMfAAM', 'a036e000010cGMhAAM', 'a036e000010cGNMAA2', 'a036e00000z36QsAAI', 'a036e00000z36RBAAY', 'a036e00000z36RFAAY', 'a036e00000z36RNAAY', 'a036e00000z441IAAQ', 'a036e00000z4feLAAQ', 'a036e00000z4feUAAQ', 'a036e00000z4mnBAAQ', 'a036e000010cm3xAAA', 'a036e00000zUoXGAA0', 'a036e000010cGWpAAM', 'a036e000010cGXBAA2', 'a036e000010cGXiAAM', 'a036e000010cGXlAAM', 'a036e000010cGXmAAM', 'a036e000010cGXxAAM', 'a036e000010cGYUAA2', 'a036e000010cGYVAA2', 'a036e000010cHnSAAU', 'a036e000010cGMGAA2', 'a036e000010cl0nAAA', 'a036e00000rH6OfAAK', 'a036e00000z36BxAAI', 'a036e00000rJj9vAAC', 'a036e00000rJjSKAA0', 'a036e000010c3uOAAQ', 'a036e00000rJjSUAA0', 'a036e00000rJrqPAAS', 'a036e00000z43t0AAA', 'a036e00000z36AGAAY', 'a036e00000z36AMAAY', 'a036e00000z36ANAAY', 'a036e00000z4fYNAAY', 'a036e00000z4fYpAAI', 'a036e00000z6N91AAE', 'a036e00000zUNXMAA4', 'a036e00000z35qoAAA', 'a036e00000zSnDgAAK', 'a036e000010cDtlAAE', 'a036e00000z35xuAAA', 'a036e00000z35y0AAA', 'a036e00000z3HByAAM', 'a036e00000z4B0SAAU', 'a036e00000z3inZAAQ', 'a036e00000z4fMaAAI', 'a036e00000zVkksAAC', 'a036e00000z4fN0AAI', 'a036e00000z4fN1AAI', 'a036e00000zVkYyAAK', 'a036e00000z4fN2AAI', 'a036e000010bveUAAQ', 'a036e00000zVr6UAAS', 'a036e00000z4fQ6AAI', 'a036e00000zVr6ZAAS', 'a036e00000z5wM8AAI', 'a036e00000z4nfUAAQ', 'a036e00000z6N5KAAU', 'a036e000010bwinAAA', 'a036e00000z5wMHAAY', 'a036e000010cDtUAAU', 'a036e000010cDtWAAU', 'a036e00000z63jhAAA', 'a036e00000z6N6qAAE', 'a036e00000zUNVjAAO', 'a036e000010cJJGAA2', 'a036e00000rJYGdAAO', 'a036e00000rJYGnAAO', 'a036e000010cDu8AAE', 'a036e00000rHQ7lAAG', 'a036e00000z4B0RAAU', 'a036e00000rJYGLAA4', 'a036e00000rJYGPAA4', 'a036e00000rJYGSAA4', 'a036e00000rJYGXAA4', 'a036e00000rJYGgAAO', 'a036e00000rJYGjAAO', 'a036e00000zUoRiAAK', 'a036e00000rJYGpAAO', 'a036e00000z5wL5AAI', 'a036e000010cDtFAAU', 'a036e000010cDteAAE', 'a036e00000rJYHYAA4', 'a036e00000rJYHjAAO', 'a036e00000rHNcQAAW', 'a036e00000rHneLAAS', 'a036e00000zUab4AAC', 'a036e00000rJjPdAAK', 'a036e00000rJjPtAAK', 'a036e00000rIMSGAA4', 'a036e00000rINV0AAO', 'a036e00000yuZpcAAE', 'a036e00000z35p1AAA', 'a036e00000z35p3AAA', 'a036e00000rJznIAAS', 'a036e00000rKEQXAA4', 'a036e000010cGI5AAM', 'a036e000010cGI8AAM', 'a036e000010cGICAA2', 'a036e000010cGIEAA2', 'a036e000010cNjrAAE', 'a036e00000z37Y6AAI', 'a036e00000rJb7YAAS', 'a036e00000z37YVAAY', 'a036e00000rJYL5AAO', 'a036e000010cGHvAAM', 'a036e00000z3655AAA', 'a036e00000z3656AAA', 'a036e00000zVAHLAA4', 'a036e00000rH2XRAA0', 'a036e00000z4fVkAAI', 'a036e00000z4fVOAAY', 'a036e00000z4fVcAAI', 'a036e00000z4fVdAAI', 'a036e00000rHUU1AAO', 'a036e00000rJog1AAC', 'a036e000010bjmVAAQ', 'a036e000010bjmTAAQ', 'a036e000010bjmUAAQ', 'a036e00000z4fVlAAI', 'a036e00000z4meRAAQ', 'a036e00000z5wPmAAI', 'a036e00000z60TGAAY', 'a036e000010cHWKAA2', 'a036e000010cHXgAAM', 'a036e000010cGGGAA2', 'a036e000010cGHAAA2', 'a036e000010cGHEAA2', 'a036e000010cGHqAAM', 'a036e000010cGHwAAM', 'a036e000010cGHzAAM', 'a036e000010cGRRAA2', 'a036e000010cGSDAA2', 'a036e00000z36JsAAI', 'a036e00000rH7dwAAC', 'a036e00000zVALGAA4', 'a036e000010cGRYAA2', 'a036e00000rH7zhAAC', 'a036e00000z4fbiAAA', 'a036e00000rH7znAAC', 'a036e00000rH8BSAA0', 'a036e00000rJYPgAAO', 'a036e00000rJjSzAAK', 'a036e00000rJjT8AAK', 'a036e00000rKEPKAA4', 'a036e00000z36IFAAY', 'a036e00000z36IKAAY', 'a036e00000z36IbAAI', 'a036e000010cHiUAAU', 'a036e00000z36KJAAY', 'a036e00000zSjYFAA0', 'a036e00000z4fbVAAQ', 'a036e00000z4fbWAAQ', 'a036e00000z5wVSAAY', 'a036e000010cGRMAA2', 'a036e00000z60ZAAAY', 'a036e00000rIUcTAAW', 'a036e00000z6NARAA2', 'a036e00000z36WgAAI', 'a036e00000zUoWZAA0', 'a036e000010cGRpAAM', 'a036e000010cGRsAAM', 'a036e00000z6MxBAAU', 'a036e00000z4fGDAAY', 'a036e00000z4fGRAAY', 'a036e00000rHgGNAA0', 'a036e00000z34EDAAY', 'a036e00000zSbTsAAK', 'a036e00000z34EQAAY', 'a036e00000rJXspAAG', 'a036e00000rJXtcAAG', 'a036e00000z3530AAA', 'a036e00000z353EAAQ', 'a036e00000rHQFHAA4', 'a036e00000z4mVUAAY', 'a036e00000z4ejgAAA', 'a036e00000zVOkUAAW', 'a036e00000z3Gv1AAE', 'a036e00000z3GvOAAU', 'a036e00000rJY9HAAW', 'a036e00000z4ejbAAA', 'a036e00000zVvpHAAS', 'a036e00000z5vtbAAA', 'a036e00000z5znCAAQ', 'a036e00000z4f2UAAQ', 'a036e00000zTMs1AAG', 'a036e00000zVA8XAAW', 'a036e00000rJY9WAAW', 'a036e00000z6Ms4AAE', 'a036e00000z6Ms8AAE', 'a036e00000z6MtdAAE', 'a036e00000zUNO9AAO', 'a036e00000rI7wPAAS', 'a036e00000z353AAAQ', 'a036e00000rJuh0AAC', 'a036e00000rJXonAAG', 'a036e00000z3449AAA', 'a036e00000rHgKBAA0', 'a036e00000rHnXMAA0', 'a036e00000zU9chAAC', 'a036e00000rJXtDAAW', 'a036e00000rIMKZAA4', 'a036e00000rJYAGAA4', 'a036e00000rJYAHAA4', 'a036e00000rJYB9AAO', 'a036e00000zUa24AAC', 'a036e00000zVA0XAAW', 'a036e00000rIyGtAAK', 'a036e00000rJXsOAAW', 'a036e00000rJXsPAAW', 'a036e00000rJZpeAAG', 'a036e00000rJY5sAAG', 'a036e00000rJYBBAA4', 'a036e00000rJYBOAA4', 'a036e00000zUNNcAAO', 'a036e00000rJXnjAAG', 'a036e00000rJXnnAAG', 'a036e00000rJZrZAAW', 'a036e00000rJY6RAAW', 'a036e00000zUNSJAA4', 'a036e00000z43SoAAI', 'a036e00000z35RAAAY', 'a036e00000zScOmAAK', 'a036e00000zVcalAAC', 'a036e00000z35SGAAY', 'a036e00000rJXnyAAG', 'a036e00000z5vrTAAQ', 'a036e00000z5vrWAAQ', 'a036e00000rJXo0AAG', 'a036e00000z5vrSAAQ', 'a036e00000rJXssAAG', 'a036e00000zSjQNAA0', 'a036e00000rKFUhAAO', 'a036e00000zScRYAA0', 'a036e00000z35SRAAY', 'a036e00000rJXo8AAG', 'a036e00000z4ejZAAQ', 'a036e00000rI45EAAS', 'a036e00000rJXsxAAG', 'a036e00000rJXt8AAG', 'a036e00000rJXtUAAW', 'a036e00000rJY6CAAW', 'a036e00000z4nXPAAY', 'a036e00000z4nXRAAY', 'a036e00000z60C7AAI', 'a036e00000zUa21AAC', 'a036e00000rJXoRAAW', 'a036e00000rJXoZAAW', 'a036e00000zUNNeAAO', 'a036e00000rIQoXAAW', 'a036e00000z34EGAAY', 'a036e00000z4elpAAA', 'a036e00000z34NpAAI', 'a036e00000rJdi5AAC', 'a036e00000z4AWBAA2', 'a036e00000rJmUCAA0', 'a036e00000rJY71AAG', 'a036e00000rJY7FAAW', 'a036e00000rJY7KAAW', 'a036e00000rJj1nAAC', 'a036e00000z6N2AAAU', 'a036e00000zUNTqAAO', 'a036e00000rJXotAAG', 'a036e00000rJXp1AAG', 'a036e00000rJdhCAAS', 'a036e00000rJdjcAAC', 'a036e00000z3448AAA', 'a036e00000z344CAAQ', 'a036e00000z5znDAAQ', 'a036e00000zUtOFAA0', 'a036e00000zUtMkAAK', 'a036e00000rHnawAAC', 'a036e00000rJY4mAAG', 'a036e00000zSc5RAAS', 'a036e00000z35BMAAY', 'a036e00000zScEMAA0', 'a036e00000zVcrIAAS', 'a036e00000zSbURAA0', 'a036e00000z63JNAAY', 'a036e00000z4elbAAA', 'a036e00000z5zqTAAQ', 'a036e00000rK2JtAAK', 'a036e00000z35CpAAI', 'a036e00000z35CtAAI', 'a036e00000zUqTZAA0', 'a036e00000z4AS6AAM', 'a036e00000z4egfAAA', 'a036e00000z4n9FAAQ', 'a036e00000z4n9IAAQ', 'a036e00000zUuo3AAC', 'a036e00000rJXqcAAG', 'a036e00000zUup5AAC', 'a036e00000rIUd4AAG', 'a036e00000rIUbiAAG', 'a036e00000z5w79AAA', 'a036e00000zVhUHAA0', 'a036e00000zSjSfAAK', 'a036e00000z6034AAA', 'a036e00000z4nU4AAI', 'a036e00000z4nUAAAY', 'a036e00000z605LAAQ', 'a036e00000z6MzdAAE', 'a036e00000zUNNlAAO', 'a036e00000zUa22AAC', 'a036e00000rJXqvAAG', 'a036e00000rJXrFAAW', 'a036e00000rJXrJAAW', 'a036e00000z5vtZAAQ', 'a036e00000z34TAAAY', 'a036e00000rJ0J9AAK', 'a036e00000rJXulAAG', 'a036e00000rJY3cAAG', 'a036e00000rJY4eAAG', 'a036e00000zUNSBAA4', 'a036e00000zUXCLAA4', 'a036e00000rJY7xAAG', 'a036e00000rJY7yAAG', 'a036e00000rJY7zAAG', 'a036e00000rJY82AAG', 'a036e00000rJXrcAAG', 'a036e00000rJdjNAAS', 'a036e00000rJY4aAAG', 'a036e00000rJjM2AAK', 'a036e00000rJjM7AAK', 'a036e00000z4nVYAAY', 'a036e00000zUaaBAAS', 'a036e00000z34ENAAY', 'a036e00000rJdmgAAC', 'a036e00000z34CXAAY', 'a036e00000rJXqAAAW', 'a036e00000rJXqbAAG', 'a036e00000zUsH6AAK', 'a036e00000z34CaAAI', 'a036e00000rJjMDAA0', 'a036e00000rJzGlAAK', 'a036e00000rK1aoAAC', 'a036e00000rK1zGAAS', 'a036e00000rK2aYAAS', 'a036e00000rK8yvAAC', 'a036e00000zUNTmAAO', 'a036e00000zVBVYAA4', 'a036e00000z34bcAAA', 'a036e00000z3GqCAAU', 'a036e00000z4mgKAAQ', 'a036e00000z4ex8AAA', 'a036e00000z4exBAAQ', 'a036e00000z4f07AAA', 'a036e00000zVvrEAAS', 'a036e00000z4nKdAAI', 'a036e00000z35ghAAA', 'a036e00000zViXRAA0', 'a036e00000z4fJ0AAI', 'a036e00000rHncdAAC', 'a036e00000rJYBrAAO', 'a036e00000z60HKAAY', 'a036e00000z63gRAAQ', 'a036e00000zVjNgAAK', 'a036e00000rJYBtAAO', 'a036e00000z60EtAAI', 'a036e00000rJYCaAAO', 'a036e00000rJYCeAAO', 'a036e00000rJYCoAAO', 'a036e00000z35iIAAQ', 'a036e00000z3EqNAAU', 'a036e00000z4eu0AAA', 'a036e00000z4euAAAQ', 'a036e00000z4euBAAQ', 'a036e00000z4euKAAQ', 'a036e00000zVvqNAAS', 'a036e00000z4euNAAQ', 'a036e00000z5zzNAAQ', 'a036e00000z6MxXAAU', 'a036e00000z6MwjAAE', 'a036e00000rI87JAAS', 'a036e00000rJYCzAAO', 'a036e00000rJYD7AAO', 'a036e00000rJYD9AAO', 'a036e00000rJYDBAA4', 'a036e00000z5vzTAAQ', 'a036e00000z6MvjAAE', 'a036e00000zUZr3AAG', 'a036e00000z600dAAA', 'a036e00000rIrwzAAC', 'a036e00000rJYDlAAO', 'a036e00000rJog6AAC', 'a036e00000z34lKAAQ', 'a036e00000rHUT7AAO', 'a036e00000z34lbAAA', 'a036e00000rIR2EAAW', 'a036e00000z63S6AAI', 'a036e00000rJmXqAAK', 'a036e00000rJY17AAG', 'a036e00000zUNRLAA4', 'a036e00000z35ZoAAI', 'a036e00000rJXyNAAW', 'a036e00000rJXz8AAG', 'a036e00000z34jWAAQ', 'a036e00000rJXz1AAG', 'a036e00000zVA8BAAW', 'a036e00000rJY1EAAW', 'a036e00000rJY1ZAAW', 'a036e00000rJY1nAAG', 'a036e00000rJY1sAAG', 'a036e00000rJY1tAAG', 'a036e00000z35baAAA', 'a036e00000z35bbAAA', 'a036e00000z3H5vAAE', 'a036e00000z34lfAAA', 'a036e00000rHHYGAA4', 'a036e00000rJedqAAC', 'a036e00000rJj7DAAS', 'a036e00000rJXyeAAG', 'a036e00000rJY2QAAW', 'a036e00000z34vOAAQ', 'a036e00000rJjQSAA0', 'a036e00000z4fGKAAY', 'a036e00000z4fGLAAY', 'a036e00000z4nafAAA', 'a036e00000z5wFbAAI', 'a036e00000rHndLAAS', 'a036e00000rJYEKAA4', 'a036e00000z35gLAAQ', 'a036e00000rJXxgAAG', 'a036e00000zSbpHAAS', 'a036e00000z34auAAA', 'a036e00000z5w2JAAQ', 'a036e00000rJXyrAAG', 'a036e00000rJXyyAAG', 'a036e00000rJkkhAAC', 'a036e00000z34vRAAQ', 'a036e00000rJypuAAC', 'a036e00000rJz88AAC', 'a036e00000z34tMAAQ', 'a036e000010bibgAAA', 'a036e000010bq6pAAA', 'a036e00000rHnclAAC', 'a036e00000rHncoAAC', 'a036e000010bvVAAAY', 'a036e000010c0dzAAA', 'a036e00000rHndKAAS', 'a036e00000rINSsAAO', 'a036e00000rJXytAAG', 'a036e00000rJXwAAAW', 'a036e00000rJXz4AAG', 'a036e00000rJXz5AAG', 'a036e00000z5zzOAAQ', 'a036e00000rJXz6AAG', 'a036e00000z5zzRAAQ', 'a036e00000z34u1AAA', 'a036e00000rJYEYAA4', 'a036e00000rIycKAAS', 'a036e00000z35iPAAQ', 'a036e00000zUa2xAAC', 'a036e00000rJXwCAAW', 'a036e00000zSbnjAAC', 'a036e00000rJXxfAAG', 'a036e00000rJXzPAAW', 'a036e00000rJXzSAAW', 'a036e00000rJXzVAAW', 'a036e00000rJzXcAAK', 'a036e00000rJXzWAAW', 'a036e00000rJXzXAAW', 'a036e00000z34vNAAQ', 'a036e00000rJYEJAA4', 'a036e00000z60H8AAI', 'a036e00000rJYEOAA4', 'a036e00000zTaKBAA0', 'a036e00000zUNQOAA4', 'a036e00000rJXwtAAG', 'a036e00000rJXxDAAW', 'a036e00000rJY27AAG', 'a036e00000rJXzxAAG', 'a036e00000rJytuAAC', 'a036e00000z3GsYAAU', 'a036e00000z4ezwAAA', 'a036e00000zVvr9AAC', 'a036e00000z4f03AAA', 'a036e00000z4f09AAA', 'a036e00000z4f0GAAQ', 'a036e00000z6MxUAAU', 'a036e00000zUNQRAA4', 'a036e00000z34lTAAQ', 'a036e00000z34lMAAQ', 'a036e00000zVA8JAAW', 'a036e00000zVO1FAAW', 'a036e00000zUa36AAC', 'a036e00000rJYF3AAO', 'a036e00000rJil4AAC', 'a036e00000z4eu9AAA', 'a036e00000zVFw9AAG', 'a036e00000rJYFkAAO', 'a036e00000rJYFvAAO', 'a036e00000rJjP3AAK', 'a036e00000rJjP5AAK', 'a036e00000rJjP9AAK', 'a036e00000z34akAAA', 'a036e00000z34alAAA', 'a036e00000zVBVBAA4', 'a036e00000z34avAAA', 'a036e00000z34axAAA', 'a036e00000zVFtoAAG', 'a036e00000zVFz4AAG', 'a036e00000rJzgvAAC', 'a036e00000rK8xCAAS', 'a036e00000z35gNAAQ', 'a036e00000zUa3RAAS', 'a036e000010cGM6AAM', 'a036e000010cGMFAA2', 'a036e00000zUvK3AAK', 'a036e00000z36QmAAI', 'a036e00000rH9HhAAK', 'a036e000010cGM8AAM', 'a036e000010cGMSAA2', 'a036e000010cGMUAA2', 'a036e00000zUNYVAA4', 'a036e00000rINc2AAG', 'a036e00000z36QxAAI', 'a036e00000rKX08AAG', 'a036e000010cGMcAAM', 'a036e000010cGMeAAM', 'a036e000010cGMgAAM', 'a036e000010cGNKAA2', 'a036e000010cGNVAA2', 'a036e000010cGNYAA2', 'a036e000010cGNfAAM', 'a036e000010ckzBAAQ', 'a036e00000z4mn8AAA', 'a036e00000z4mn9AAA', 'a036e00000zUvOJAA0', 'a036e00000z60cWAAQ', 'a036e00000z4nulAAA', 'a036e00000z4nunAAA', 'a036e000010cHooAAE', 'a036e000010cGX5AAM', 'a036e000010cGXpAAM', 'a036e000010cGY0AAM', 'a036e000010cGYaAAM', 'a036e000010cGYeAAM', 'a036e000010cGYiAAM', 'a036e000010cGYjAAM', 'a036e000010cHpKAAU', 'a036e00000z36BmAAI', 'a036e000010cGMJAA2', 'a036e00000z36BoAAI', 'a036e00000z36BkAAI', 'a036e00000z36C3AAI', 'a036e00000rH6bnAAC', 'a036e00000rH6rDAAS', 'a036e00000z36C5AAI', 'a036e00000z36C8AAI', 'a036e00000z60VaAAI', 'a036e00000rIMXQAA4', 'a036e00000z60VlAAI', 'a036e00000rJjSXAA0', 'a036e00000rJohJAAS', 'a036e00000rKFXPAA4', 'a036e00000z36ACAAY', 'a036e00000z36ALAAY', 'a036e00000z36ByAAI', 'a036e00000z3HKhAAM', 'a036e000010c4DwAAI', 'a036e00000z5ZImAAM', 'a036e000010cGM7AAM', 'a036e00000z5wSkAAI', 'a036e00000z5wSnAAI', 'a036e000010cGM3AAM', 'a036e00000rK3seAAC', 'a036e00000z35ywAAA', 'a036e00000zVkkTAAS', 'a036e00000zTaMSAA0', 'a036e000010c4LWAAY', 'a036e00000z4fPoAAI', 'a036e00000z4fMyAAI', 'a036e00000z4fMwAAI', 'a036e00000z4nibAAA', 'a036e00000z6N5QAAU', 'a036e00000zVkkSAAS', 'a036e000010c3ryAAA', 'a036e00000z5wMOAAY', 'a036e00000z60NQAAY', 'a036e00000z6N6sAAE', 'a036e00000z6N6uAAE', 'a036e00000zTOCjAAO', 'a036e00000zUab9AAC', 'a036e00000zVrTeAAK', 'a036e00000zVrTfAAK', 'a036e000010cDtMAAU', 'a036e000010cDtSAAU', 'a036e000010cDtaAAE', 'a036e000010cDtsAAE', 'a036e000010cDtuAAE', 'a036e000010cDusAAE', 'a036e000010cDutAAE', 'a036e00000z35qwAAA', 'a036e00000rJYGuAAO', 'a036e00000rJYGJAA4', 'a036e00000rJYGNAA4', 'a036e00000rJYGRAA4', 'a036e00000z5wKtAAI', 'a036e00000z5wKxAAI', 'a036e00000rJYGmAAO', 'a036e00000z5wKwAAI', 'a036e00000rKX0mAAG', 'a036e00000z60NLAAY', 'a036e000010cDtDAAU', 'a036e00000rJYHZAA4', 'a036e00000rJYHeAAO', 'a036e00000rJYHiAAO', 'a036e00000rJYHoAAO', 'a036e000010cEsOAAU', 'a036e00000rHEu5AAG', 'a036e00000z35z5AAA', 'a036e00000rHneSAAS', 'a036e00000rJYI9AAO', 'a036e00000rJjPvAAK', 'a036e00000rJz2vAAC', 'a036e00000rJz9GAAS', 'a036e00000z4fMzAAI', 'a036e00000rJYIoAAO', 'a036e000010cDukAAE', 'a036e00000z4fQ7AAI', 'a036e00000rJjQuAAK', 'a036e00000rJUlFAAW', 'a036e00000rJb7gAAC', 'a036e00000rH1wwAAC', 'a036e00000rH1zgAAC', 'a036e00000z363XAAQ', 'a036e00000z43r5AAA', 'a036e00000yuZxdAAE', 'a036e00000z4fVSAAY', 'a036e00000rHUTwAAO', 'a036e00000rHUTyAAO', 'a036e00000rHUU0AAO', 'a036e00000z4fVhAAI', 'a036e00000rJCuFAAW', 'a036e00000rJofmAAC', 'a036e00000rK1k9AAC', 'a036e00000rKX69AAG', 'a036e00000yuZxfAAE', 'a036e00000z363MAAQ', 'a036e00000z3HHzAAM', 'a036e00000z4meSAAQ', 'a036e00000z4meQAAQ', 'a036e000010cHYTAA2', 'a036e000010cGH2AAM', 'a036e000010cGH4AAM', 'a036e000010cGHGAA2', 'a036e000010cGHLAA2', 'a036e000010cGHoAAM', 'a036e000010cGHuAAM', 'a036e000010cGI3AAM', 'a036e00000zUvNUAA0', 'a036e00000rH7cbAAC', 'a036e00000rH7dHAAS', 'a036e00000rH7tSAAS', 'a036e00000z4fbbAAA', 'a036e00000z4fblAAA', 'a036e00000z4fbjAAA', 'a036e00000z4fbrAAA', 'a036e00000zSjYDAA0', 'a036e00000z60ZBAAY', 'a036e00000rJYPhAAO', 'a036e00000rJjSvAAK', 'a036e00000rJjSxAAK', 'a036e00000rJjT7AAK', 'a036e00000rJzq8AAC', 'a036e00000z36JqAAI', 'a036e000010cGRaAAM', 'a036e00000z36IIAAY', 'a036e00000z36ILAAY', 'a036e00000z36IMAAY', 'a036e00000z36KNAAY', 'a036e00000z36KRAAY', 'a036e00000z5wVbAAI', 'a036e00000z60Z8AAI', 'a036e00000zUoWUAA0', 'a036e00000z6N9yAAE', 'a036e00000zUNY0AAO', 'a036e00000z36WjAAI', 'a036e00000z4msXAAQ', 'a036e000010cGcWAAU', 'a036e000010cGS4AAM', 'a036e000010cGRiAAM', 'a036e000010cGRkAAM', 'a036e000010cGS0AAM', 'a036e000010cGSfAAM', 'a036e000010cGSgAAM', 'a036e000010cGSkAAM', 'a036e000010cGSnAAM', 'a036e000010cqudAAA')");
                }
            }
            //stringBuilder.Append(" AND Name IN ('AP-0045388632', 'AP-0045388632', 'AP-0045101181')");          
            try
            {
                var jsonResponse = this.QueryRecordAsync(this.httpClient, HttpUtility.UrlEncode(stringBuilder.ToString(), Encoding.UTF8));
                SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
                if (response.Records.Count > 0)
                {
                    lAppointments = response.Records;
                    foreach (var item in lAppointments)
                    {
                        sType = "Programas";
                        if (item.PlanId__r.Name.Contains("ASMAIRE"))
                        {
                            arrayList = new string[] { "5205", "0005", "0105", "4105", "5005" };
                        }
                        else if (item.PlanId__r.Name.Contains("AIREP"))
                        {
                            arrayList = new string[] { "5206", "0006", "4106", "5006", "0606", "1106" };
                        }
                        else if (item.PlanId__r.Name.Contains("VASCU") || item.PlanId__r.Name.Contains("HTP") || item.PlanId__r.Name.Contains("MVPU"))
                        {
                            arrayList = new string[] { "5009", "4111", "2103", "4400", "4401", "4402", "4403" };
                        }
                        else if (item.PlanId__r.Name.Contains("OXIGENAR"))
                        {
                            arrayList = new string[] { "0008", "0000" };
                        }
                        else if (item.PlanId__r.Name.Contains("FNC SURA VMI") || item.PlanId__r.Name.Contains("FNC VENTIL MECANICA INTERMITEN") || item.PlanId__r.Name.Contains("FNC SURA EPS VMI")
                            || item.PlanId__r.Name.Contains("VMI"))
                        {
                            arrayList = new string[] { "4501", "4101" };
                        }
                        else if (item.GroupId__r.Name.Contains("VALORACIÓN FBC") || item.GroupId__r.Name.Contains("VALORACIÓN ANESTESIA"))
                        {
                            arrayList = new string[] { "3001" };
                            sType = "Intervencionismo";
                        }
                        else if (item.PlanId__r.Name.Contains("PROTOCOLO"))
                        {
                            arrayList = new string[] { "7167", "7140", "7142", "7143", "7144", "7148", "7149", "7150", "7152", "7155", "7166", "7175", "7180", "7181", "713H", "7145", "7158"
                                , "7147", "7170", "7171", "7173", "7153", "7178", "713A", "714A", "714B", "713B", "713C", "713D", "7182", "713E", "713F", "713G", "713J", "7183", "7184", "713L"
                                , "713I", "713N", "713Ñ", "713O", "713P", "713K", "713M", "7185" };
                            sType = "Investigacion";
                        }
                        else
                        {
                            arrayList = new string[] { item.ScheduleId__r.FNC_CentroCostos__r.Code__c };
                        }
                        scostcenter = string.Join("', '", arrayList);
                        sfunctionalunit = (item.WhatId__r.Age2__pc >= 18) ? "1100" : "1200";
                        //sfunctionalunit = "1100";
                        productsByGroups = this.GetProductsInfo(item.GroupId__c, item.PlanId__c, scostcenter, item.PlanId__r.RateId__c, sfunctionalunit, item.PlanId__r.Name);
                        if (productsByGroups.Count == 0)
                        {
                            productsByGroups = this.GetOldProducts("(" + item.GroupId__r.Name);
                            serviceRequests = this.WrapServiceRequest(productsByGroups, item.PlanId__r.RateId__c, sfunctionalunit, item.PlanId__r.Name);
                        }
                        else
                        {
                            serviceRequests = this.WrapServiceRequest(productsByGroups);
                            serviceRequests = serviceRequests.GroupBy(x => new { x.sservice, x.sconcept, x.scostcenter }).Select(y => y.First()).ToList();
                        }
                        //productsTmp = productsByGroups.FindAll(x => x.GroupId__c == item.Grupo_por_Plan__r.Grupo__c && x.Grupo_por_Plan__r.Plan__r.RateId__c == item.PlanId__r.RateId__c && x.Tarifa_concepto_producto__r.CostCenterId__c == item.ScheduleId__r.FNC_CentroCostos__c);
                        if (serviceRequests.Count > 0)
                        {
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
                                sunit = sfunctionalunit,
                                sattentiontype = "2",
                                sservicetype = "28",
                                ddate = item.ActivityDate__c.Value,
                                lservices = new List<ServiceRequest>(),
                                sthird = item.AgendaId__r.ProfessionalId__r.DocumentNumber__c,
                                scostcenter = item.PlanId__r.HealthCarePlanId__r.Name,
                            };
                            appointment.lservices = serviceRequests;
                            /*
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
                                    bisprocedure = !product.Tarifa_concepto_producto__r.ProductId__r.Name__c.Contains("CONSULTA"),
                                    sconcept = product.Tarifa_concepto_producto__r.ConceptId__r.Code__c,
                                    idiscount = 0,
                                    ivalue = Convert.ToDecimal(product.Tarifa_concepto_producto__r.Value__c)
                                };                           
                                appointment.lservices.Add(services);
                            }*/
                            sunit = (item.WhatId__r.Age2__pc >= 18) ? "1" : "3";
                            servintePatient.lappointments.Add(appointment);
                            lservintePatients.Add(servintePatient);
                        }
                        else
                        {
                            serrors.Add(item.Id + "," + item.ActivityDate__c.Value.ToString("yyyy-MM-dd") + "," + sType);
                        }
                    }
                }
                if (serrors.Count > 0)
                {
                    this.WriteFile(serrors.ToArray(), sErrorFile);
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
                stringBuilder = null;
            }
        }

        private void WriteFile(object[] stext, string sfile)
        {
            if (!File.Exists(sfile))
            {
                File.Create(sfile);
                File.AppendAllLines(sfile, stext.Cast<String>());
            }
            else
            {
                File.AppendAllLines(sfile, stext.Cast<String>());
            }
        }

        /// <summary>
        /// Método que hace el resumen de los servicios del listado de productos por grupo
        /// </summary>
        /// <param name="lproductsByGroup">Lista genérica de productos por grupo</param>
        /// <param name="srate">Strign tarifa Id</param>
        /// <param name="sunit">String código unidad funcional</param>
        /// <param name="splanname">String nombre del plan</param>
        /// <returns></returns>
        private List<ServiceRequest> WrapServiceRequest(List<ProductsByGroup__c> lproductsByGroup, string srate, string sunit, string splanname)
        {
            List<ServiceRequest> lserviceRequest = new List<ServiceRequest>();
            List<RateByConceptByProduct__c> lrateByConceptByProduct = new List<RateByConceptByProduct__c>();
            RateByConceptByProduct__c rateByConcept = null;
            StringBuilder sb = new StringBuilder();
            try
            {
                if (!splanname.Contains("PROTOCOLO"))
                {
                    sb.Append("SELECT CostCenterId__c, ProductId__c, CostCenterId__r.Code__c, ConceptId__r.Code__c, ProductId__r.Name, ProductId__r.Name__c, Value__c, RateId__r.Code__c, ConceptId__r.FunctionalUnit__r.Code__c" +
                                                " FROM RateByConceptByProduct__c WHERE RateId__c = '" + srate + "' AND ConceptId__r.FunctionalUnit__r.Code__c = '" + sunit + "'");
                }
                else
                {
                    sb.Append("SELECT CostCenterId__c, ProductId__c, CostCenterId__r.Code__c, ConceptId__r.Code__c, ProductId__r.Name, ProductId__r.Name__c, Value__c, RateId__r.Code__c, ConceptId__r.FunctionalUnit__r.Code__c" +
                                                " FROM RateByConceptByProduct__c WHERE RateId__c = '" + srate + "'");
                }
                var jsonResponse = this.QueryRecordAsync(this.httpClient, sb.ToString());
                SalesforceResponse<RateByConceptByProduct__c> response = JsonConvert.DeserializeObject<SalesforceResponse<RateByConceptByProduct__c>>(jsonResponse);
                if (response.Records.Count > 0)
                {
                    lrateByConceptByProduct.AddRange(response.Records);
                    foreach (var product in lproductsByGroup)
                    {
                        rateByConcept = (!splanname.Contains("PROTOCOLO")) ? lrateByConceptByProduct.FirstOrDefault(x => x.CostCenterId__c == product.Centro_de_Costos__c && x.ProductId__c == product.ProductId__c) : lrateByConceptByProduct.FirstOrDefault(x => x.ProductId__c == product.ProductId__c);
                        if (rateByConcept != null)
                        {
                            ServiceRequest serviceRequest = new ServiceRequest()
                            {
                                scostcenter = rateByConcept.CostCenterId__r.Code__c,
                                sconcept = rateByConcept.ConceptId__r.Code__c,
                                sservice = rateByConcept.ProductId__r.Name,
                                sservicename = rateByConcept.ProductId__r.Name__c,
                                bisprocedure = !rateByConcept.ProductId__r.Name__c.Contains("CONSULTA"),
                                ivalue = Convert.ToDecimal(rateByConcept.Value__c),
                                srate = rateByConcept.RateId__r.Code__c,
                                idiscount = 0,
                                iqty = 1,
                                bbilleable = true,
                            };
                            lserviceRequest.Add(serviceRequest);
                        }
                    }
                }
                return lserviceRequest;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                throw;
            }
            finally
            {
                sb = null;
                rateByConcept = null;
                lrateByConceptByProduct = null;
            }
        }

        private List<ServiceRequest> WrapServiceRequest(List<ProductsByGroup__c> lproductsByGroup)
        {
            List<ServiceRequest> lserviceRequest = new List<ServiceRequest>();
            foreach (var lproduct in lproductsByGroup)
            {
                ServiceRequest serviceRequest = new ServiceRequest()
                {
                    scostcenter = lproduct.Tarifa_concepto_producto__r.CostCenterId__r.Code__c,
                    sconcept = lproduct.Tarifa_concepto_producto__r.ConceptId__r.Code__c,
                    sservice = lproduct.Tarifa_concepto_producto__r.ProductId__r.Name,
                    sservicename = lproduct.Tarifa_concepto_producto__r.ProductId__r.Name__c,
                    bisprocedure = !lproduct.Tarifa_concepto_producto__r.ProductId__r.Name__c.Contains("CONSULTA"),
                    ivalue = Convert.ToDecimal(lproduct.Tarifa_concepto_producto__r.Value__c),
                    srate = lproduct.Tarifa_concepto_producto__r.RateId__r.Code__c,
                    idiscount = 0,
                    iqty = 1,
                    bbilleable = true,
                };
                lserviceRequest.Add(serviceRequest);
            }
            return lserviceRequest;
        }

        #endregion

        #region Métodos para enviar la información desde Salesforce hacia Synapse y viceversa

        /// <summary>
        /// Método asincrónico que envía la información de la cita de intervencionismo a Synapse para la integración con el PACS
        /// </summary>
        /// <param name="appointment">Objeto cita</param>
        /// <returns>Entero con el resultado de la transacción asincrónica</returns>
        public int SendAppointmentToSynapse(Appointment__c appointment)
        {
            int i = 1;
            SynapseEntity synapseEntity = null;
            double iage = (appointment.WhatId__r.Age2__pc.HasValue) ? appointment.WhatId__r.Age2__pc.Value : 0;
            string sunit = (iage >= 18) ? "1100" : "1200";
            string srequest = string.Empty;
            bool task = false;
            List<ProductsByGroup__c> lsProducts = this.GetProductsInfo(appointment.GroupId__c, appointment.PlanId__c, appointment.CostCenterId__r.Code__c, appointment.PlanId__r.RateId__c, string.Empty, appointment.PlanName__c, string.Empty);
            if (lsProducts.Count > 0)
            {
                lsProducts = lsProducts.FindAll(x => x.Tarifa_concepto_producto__r.CostCenterId__r.Code__c.EqualsAnyOf("3001", "3102")).Distinct().ToList();
                var result = lsProducts.GroupBy(p => new { p.Tarifa_concepto_producto__r.ProductId__r.Name, p.Tarifa_concepto_producto__r.ProductId__r.Name__c });
                foreach (var item in result)
                {
                    synapseEntity = new SynapseEntity()
                    {
                        ID_PACIENTE = appointment.WhatId__r.DocumentNumber__c,
                        APELLIDOS_PACIENTE = appointment.WhatId__r.FirstSurname__pc + " " + appointment.WhatId__r.SecondSurname__pc,
                        NOMBRES_PACIENTE = appointment.WhatId__r.FirstName_c__pc + " " + appointment.WhatId__r.SecondName__pc,
                        ID_ASEGURADORA = appointment.AgreementId__r.Code__c,
                        ACCION_ORDEN = "INSERTAR",
                        NOMBRE_ASEGURADORA = appointment.AgreementId__r.Name,
                        TIPO_DOCUMENTO = Tools.GetDocumentType(appointment.WhatId__r.DocumentType__c, true),
                        UBICACION_PACIENTE = "INSPIRA",
                        MODALIDAD = "SC",
                        TIPO_PACIENTE = "CONSULTA EXTERNA",
                        NUMERO_DE_ACCESO = appointment.WhatId__r.DocumentNumber__c + "-" + appointment.Name.ToUpper() + "-" + i.ToString(),
                        FECHA_NACIMIENTO = appointment.WhatId__r.PersonBirthdate.Value.ToString("yyyy-MM-dd"),
                        GENERO = Tools.GetGenderFromInpira(appointment.WhatId__r.Gender__pc),
                        EMAIL = string.IsNullOrEmpty(appointment.WhatId__r.PersonEmail) ? "informacion@neumologica.org" : appointment.WhatId__r.PersonEmail,
                        DIRECCION = appointment.WhatId__r.Address__c,
                        DEPARTAMENTO = (appointment.WhatId__r.State__r != null) ? appointment.WhatId__r.State__r.Name : "BOGOTÁ D.C",
                        MUNICIPIO = (appointment.WhatId__r.City__r != null) ? appointment.WhatId__r.City__r.Name : "BOGOTÁ",
                        TELEFONO = appointment.WhatId__r.Phone,
                        JUSTIFICACION = string.Empty,
                        PROFESIONAL_ORDENA = string.Empty,
                        COD_ESTUDIO = item.Key.Name,
                        DESCRIPCION_ESTUDIO = item.Key.Name__c,
                    };
                    try
                    {
                        LogError.WriteMessage("Application", "WSInspira", "Toda la información lista para enviar:" + result.ToString());
                        srequest = JsonConvert.SerializeObject(synapseEntity, Formatting.Indented);
                        task = Tools.PostJson(FNCSalesforce.Properties.Settings.Default.SynapseWSURL, srequest);
                        if (!task)
                        {
                            throw new ApplicationException("Error al consumir el ws de Synapse");
                        }
                        i++;
                    }
                    catch (Exception ex)
                    {
                        LogError.WriteError("Application", "WSInspira", ex);
                    }
                }
            }
            return 1;
        }

        /// <summary>
        /// Método para actualizar la cita con la URL del reporte y del video generados en Synapse
        /// </summary>
        /// <param name="sappointment">String id de la cita a actualizar</param>
        /// <param name="surlreport">String url del reporte de la intervención en Synapse</param>
        /// <param name="surlfile">String url del video de la intervención en Synapse</param>
        public void UpdateAppointmentUrl(string sappointment, string surlreport, string surlfile)
        {
            Appointment__c appointment = this.GetAppointmenData(sappointment);
            string sId = (appointment != null) ? appointment.Id : string.Empty;
            StringBuilder stringBuilder = new StringBuilder("<root>");
            stringBuilder.Append("<VideoUrl__c>" + HttpUtility.HtmlEncode(surlfile) + "</VideoUrl__c>");
            stringBuilder.Append("<ReportUrl__c>" + HttpUtility.HtmlEncode(surlreport) + "</ReportUrl__c>");
            stringBuilder.Append("</root>");
            if (!string.IsNullOrEmpty(sId) && sappointment.Contains("AP-"))
            {
                this.UpdateObjectAsync("Appointment__c", sId, stringBuilder.ToString());
                this.CreateDocumentRegister(appointment, surlfile);
            }
            else
            {
                this.UpdateObjectAsync("Appointment__c", sappointment, stringBuilder.ToString());
            }
        }

        private void CreateDocumentRegister(Appointment__c appointment, string surlfile)
        {
            string sdate = appointment.ActivityDatetime__c.HasValue ? appointment.ActivityDatetime__c.Value.ToString("yyyy-MM-dd") : DateTime.Now.ToString("yyyy-MM-dd");
            StringBuilder stringBuilder = new StringBuilder("<root>");
            stringBuilder.Append("<AppointmentId__c>");
            stringBuilder.Append(appointment.Id);
            stringBuilder.Append("</AppointmentId__c>");
            stringBuilder.Append("<Status__c>Aprobado</Status__c>");
            stringBuilder.Append("<Filename__c>" + surlfile + "</Filename__c>");
            stringBuilder.Append("<TestDate__c>" + sdate + "</TestDate__c>");
            stringBuilder.Append("<PatientId__c>" + appointment.WhatId__c + "</PatientId__c>");
            stringBuilder.Append("</root>");
            try
            {
                this.CreateRecordAsync("ProcedureTest__c", stringBuilder.ToString());
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSDigiturno", ex);

            }
        }

        public Appointment__c GetAppointmenData(string sappointment)
        {
            string[] acount = sappointment.Split('-');
            string sname = string.Empty;
            if (acount.Length > 1)
            {
                sname = sappointment.Substring(sappointment.IndexOf('-') + 1);
            }
            else
            {
                sname = sappointment;
            }
            StringBuilder sb = new StringBuilder("SELECT Id, ActivityDate__c, WhatId__c FROM Appointment__c WHERE Name = '");
            sb.Append(sname);
            sb.Append("'");
            try
            {
                var jsonResponse = this.QueryRecordAsync(this.httpClient, sb.ToString());
                SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
                if (response.Records.Count > 0)
                {
                    return response.Records[0];
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
            }
            return null;
        }

        #endregion

        #region Métodos para obtener información para los consentimientos informados

        public List<Consentimiento> GetDataForConsent(string sSession, string sUrl, string sid)
        {
            SoapClient soapClient = new SoapClient();
            QueryOptions queryOptions = new QueryOptions() { batchSize = 2000 };
            QueryResult queryResult = new QueryResult();
            MruHeader mruHeader = new MruHeader() { updateMru = false };
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
                var jsonResponse = this.QueryRecordAsync(this.httpClient, HttpUtility.UrlEncode(stringBuilder.ToString(), Encoding.UTF8));
                SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
                if (response.Records.Count > 0)
                {
                    Appointment__c appointment = response.Records[0];
                    stringBuilder.Clear();
                    stringBuilder.Append("SELECT Tarifa_concepto_producto__r.ProductId__c, Tarifa_concepto_producto__r.ProductId__r.Name__c, Tarifa_concepto_producto__r.ProductId__r.Name, " +
                                        "Grupo_por_Plan__r.Grupo__c, Tarifa_concepto_producto__r.CostCenterId__r.Code__c FROM ProductsByGroup__c WHERE Grupo_por_Plan__r.Grupo__c = '");
                    stringBuilder.Append(appointment.GroupId__c);
                    stringBuilder.Append("' AND Grupo_por_Plan__r.Plan__c = '");
                    stringBuilder.Append(appointment.PlanId__c);
                    stringBuilder.Append("' AND Tarifa_concepto_producto__r.ProductId__r.Name IN ('890371', '890271', '890372', '890272')");
                    jsonResponse = this.QueryRecordAsync(this.httpClient, HttpUtility.UrlEncode(stringBuilder.ToString(), Encoding.UTF8));
                    SalesforceResponse<ProductsByGroup__c> response1 = JsonConvert.DeserializeObject<SalesforceResponse<ProductsByGroup__c>>(jsonResponse);
                    if (response1.Records.Count > 0)
                    {
                        foreach (var item in response1.Records)
                        {
                            ProductsByGroup__c productsByGroup__C = item;
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

        #region Métodos para la integración con el portal de visor de pacientes

        /// <summary>
        /// Método para obtener el id del paciente mediante el cual se va a validar el acceso a la plataforma de descarga de videos
        /// </summary>
        /// <param name="sdocumenttype"></param>
        /// <param name="sdocument"></param>
        /// <param name="dborndate"></param>
        /// <param name="smail"></param>
        /// <returns></returns>
        public Patient GetPatientInfo(string sdocumenttype, string sdocument, string dborndate, string smail)
        {
            Patient patient = null;
            StringBuilder stringBuilder = new StringBuilder("SELECT Id, FirstName_c__pc, FirstSurname__pc FROM Account WHERE DocumentNumber__c = '");
            stringBuilder.Append(sdocument);
            stringBuilder.Append("' AND DocumentType__c = '");
            stringBuilder.Append(sdocumenttype);
            stringBuilder.Append("' AND PersonEmail = '");
            stringBuilder.Append(smail);
            stringBuilder.Append("' AND PersonBirthdate = ");
            stringBuilder.Append(dborndate);
            var jsonResponse = this.QueryRecordAsync(this.httpClient, HttpUtility.UrlEncode(stringBuilder.ToString(), Encoding.UTF8));
            SalesforceResponse<Account> response = JsonConvert.DeserializeObject<SalesforceResponse<Account>>(jsonResponse);
            if (response.Records.Count > 0)
            {
                Account account = response.Records[0];
                patient = new Patient()
                {
                    sfirstname = account.FirstName_c__pc,
                    ssurname = account.FirstSurname__pc,
                    sdocument = sdocument,
                    smail = smail,
                    sdocumenttype = sdocumenttype,
                };
            }
            return patient;
        }
        /*
        public string GetVideoUrl(string sid)
        {
            StringBuilder stringBuilder = new StringBuilder("SELECT Filename__c FROM "
        }
        */

        #endregion

        #region Métodos para recibir el PDF de la historia clínica

        public string GetAssessmentFormat(string assesmentId)
        {
            string restEndpoint = $"{this.salesforceSession.sname}/services/apexrest/GetAssesmentFormat";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, restEndpoint);
            request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = new StringContent(assesmentId, Encoding.UTF8, "application/json");
            try
            {
                HttpResponseMessage response = this.httpClient.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    return response.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    throw new Exception($"Error en la solicitud HTTP: {response.StatusCode} Con la consulta {assesmentId}");
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        #endregion
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }

        // =========================================================================
        // MODELOS DE ENTIDAD para la sincronización de tarifas
        // =========================================================================

        /// <summary>
        /// Entidad que representa un registro del objeto RateByConceptByProduct__c de Salesforce.
        /// Los nombres de campo con doble guión bajo son campos personalizados de Salesforce.
        /// </summary>
        public class RateByConceptByProduct
        {
            [JsonProperty("RateId__c")]
            public string RateId__c { get; set; }

            [JsonProperty("RateId__r")]
            public RelatedCode RateId__r { get; set; }

            [JsonProperty("ConceptId__c")]
            public string ConceptId__c { get; set; }

            [JsonProperty("ConceptId__r")]
            public RelatedCode ConceptId__r { get; set; }

            [JsonProperty("CostCenterId__c")]
            public string CostCenterId__c { get; set; }

            [JsonProperty("CostCenterId__r")]
            public RelatedCode CostCenterId__r { get; set; }

            [JsonProperty("ProductId__c")]
            public string ProductId__c { get; set; }

            [JsonProperty("ProductId__r")]
            public RelatedName ProductId__r { get; set; }

            // Propiedades de conveniencia para acceder a los códigos relacionados
            public string RateIdCode__c => RateId__r?.Code__c;
            public string ConceptIdCode__c => ConceptId__r?.Code__c;
            public string CostCenterIdCode__c => CostCenterId__r?.Code__c;
            public string ProductIdName => ProductId__r?.Name;
        }

        /// <summary>Objeto relacionado con campo Code__c</summary>
        public class RelatedCode
        {
            [JsonProperty("Code__c")]
            public string Code__c { get; set; }
        }

        /// <summary>Objeto relacionado con campo Name</summary>
        public class RelatedName
        {
            [JsonProperty("Name")]
            public string Name { get; set; }
        }

        /// <summary>
        /// Representa un registro nuevo detectado por el query de diferencias
        /// que debe crearse en Salesforce como RateByConceptByProduct__c.
        /// </summary>
        public class RateProductNuevo
        {
            public string IdTarifa { get; set; }  // -> RateId__c
            public string IdConcepto { get; set; }  // -> ConceptId__c
            public string IdCentro { get; set; }  // -> CostCenterId__c
            public string IdProducto { get; set; }  // -> ProductId__c
            public int Valor { get; set; }  // -> Value__c
        }

        #region Sincronización RateByConceptByProduct__c

        /// <summary>
        /// Obtiene TODOS los registros del objeto RateByConceptByProduct__c de Salesforce,
        /// manejando la paginación automática mediante nextRecordsUrl para soportar
        /// más de 2000 registros (el límite por página de la API REST de Salesforce).
        /// </summary>
        /// <returns>Lista de RateByConceptByProduct con todos los registros</returns>
        public List<RateByConceptByProduct> GetAllRateByConceptByProduct()
        {
            List<RateByConceptByProduct> todos = new List<RateByConceptByProduct>();
            string soql = "SELECT RateId__c, RateId__r.Code__c, ConceptId__c, ConceptId__r.Code__c, " +
                          "CostCenterId__c, CostCenterId__r.Code__c, ProductId__c, ProductId__r.Name " +
                          "FROM RateByConceptByProduct__c";

            // Primera página: construimos la URL de query
            string url = $"{this.salesforceSession.sname}{this.sApiEndpoint}query?q={HttpUtility.UrlEncode(soql, Encoding.UTF8)}";
            int pagina = 1;

            try
            {
                while (!string.IsNullOrEmpty(url))
                {
                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    HttpResponseMessage response = this.httpClient.SendAsync(request).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = response.Content.ReadAsStringAsync().Result;
                        throw new Exception($"Error HTTP {(int)response.StatusCode} al consultar RateByConceptByProduct__c: {error}");
                    }

                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    SalesforceResponse<RateByConceptByProduct> page =
                        JsonConvert.DeserializeObject<SalesforceResponse<RateByConceptByProduct>>(jsonResponse);

                    if (page.Records != null && page.Records.Count > 0)
                        todos.AddRange(page.Records);

                    // Si Salesforce retorna nextRecordsUrl hay más páginas
                    if (!page.Done && !string.IsNullOrEmpty(page.NextRecordsUrl))
                        url = $"{this.salesforceSession.sname}{page.NextRecordsUrl}";
                    else
                        url = null;

                    pagina++;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "SalesforceViaRestApi", ex);
                throw;
            }

            Console.WriteLine();
            return todos;
        }

        /// <summary>
        /// Crea en Salesforce los registros nuevos de RateByConceptByProduct__c encontrados
        /// como diferencias entre el ERP y lo que ya existe en Salesforce.
        /// Utiliza la Composite API para enviar lotes de hasta 25 registros por petición,
        /// reduciendo el número de llamadas HTTP.
        /// </summary>
        /// <param name="registros">Lista de registros a crear en Salesforce</param>
        public void CrearRateByConceptByProducts(List<RateProductNuevo> registros)
        {
            if (registros == null || registros.Count == 0) return;

            const int batchSize = 25;
            int total = registros.Count;
            int enviados = 0;
            string compositeUrl = $"{this.salesforceSession.sname}/services/data/v60.0/composite";
            try
            {
                for (int i = 0; i < total; i += batchSize)
                {
                    int fin = Math.Min(i + batchSize, total);
                    var lote = registros.GetRange(i, fin - i);
                    // Construimos el array de sub-requests de la Composite API
                    var subreqs = new List<object>();
                    for (int j = 0; j < lote.Count; j++)
                    {
                        RateProductNuevo r = lote[j];
                        var body = new JObject();

                        if (!string.IsNullOrEmpty(r.IdTarifa)) body["RateId__c"] = r.IdTarifa;
                        if (!string.IsNullOrEmpty(r.IdConcepto)) body["ConceptId__c"] = r.IdConcepto;
                        if (!string.IsNullOrEmpty(r.IdCentro)) body["CostCenterId__c"] = r.IdCentro;
                        if (!string.IsNullOrEmpty(r.IdProducto)) body["ProductId__c"] = r.IdProducto;
                        body["Value__c"] = r.Valor;
                        subreqs.Add(new
                        {
                            method = "POST",
                            url = "/services/data/v60.0/sobjects/RateByConceptByProduct__c/",
                            referenceId = "ref" + j,
                            body = body
                        });
                    }

                    var composite = new { allOrNone = false, compositeRequest = subreqs };
                    string payload = JsonConvert.SerializeObject(composite);

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, compositeUrl);
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);

                    HttpResponseMessage response = this.httpClient.SendAsync(request).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = response.Content.ReadAsStringAsync().Result;
                        throw new Exception($"Error HTTP {(int)response.StatusCode} en Composite POST: {error}");
                    }

                    // Verificar si algún sub-request falló individualmente
                    string respBody = response.Content.ReadAsStringAsync().Result;
                    JObject compResp = JObject.Parse(respBody);
                    JArray items = (JArray)compResp["compositeResponse"];
                    if (items != null)
                    {
                        foreach (JToken item in items)
                        {
                            int code = (int)(item["httpStatusCode"] ?? 0);
                            if (code < 200 || code >= 300)
                            {
                                string refId = (string)(item["referenceId"] ?? "?");
                                string err = item["body"]?.ToString() ?? "sin detalle";
                                LogError.WriteMessage("FNCInspira", "SalesforceViaRestApi", $"Error en sub-request {refId} (HTTP {code}): {err}");
                            }
                        }
                    }
                    enviados += lote.Count;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "SalesforceViaRestApi", ex);
                throw;
            }
        }

        // <summary>
        /// Modelo para la descarga completa de RateByConceptByProduct__c incluyendo Id y Value__c,
        /// usado para comparar valores contra el ERP y actualizar los que hayan cambiado.
        /// </summary>
        public class RateByConceptByProductValor
        {
            [JsonProperty("Id")]
            public string Id { get; set; }

            [JsonProperty("RateId__c")]
            public string RateId__c { get; set; }

            [JsonProperty("RateId__r")]
            public RelatedCode RateId__r { get; set; }

            [JsonProperty("ConceptId__c")]
            public string ConceptId__c { get; set; }

            [JsonProperty("ConceptId__r")]
            public RelatedCode ConceptId__r { get; set; }

            [JsonProperty("CostCenterId__c")]
            public string CostCenterId__c { get; set; }

            [JsonProperty("CostCenterId__r")]
            public RelatedCode CostCenterId__r { get; set; }

            [JsonProperty("ProductId__c")]
            public string ProductId__c { get; set; }

            [JsonProperty("ProductId__r")]
            public RelatedName ProductId__r { get; set; }

            [JsonProperty("Value__c")]
            public decimal? Value__c { get; set; }

            // Propiedades de conveniencia
            public string RateIdCode__c => RateId__r?.Code__c;
            public string ConceptIdCode__c => ConceptId__r?.Code__c;
            public string CostCenterIdCode__c => CostCenterId__r?.Code__c;
            public string ProductIdName => ProductId__r?.Name;
        }

        /// <summary>
        /// Modelo que representa un registro de RateByConceptByProduct__c cuyo valor
        /// difiere con el ERP y debe actualizarse en Salesforce.
        /// </summary>
        public class RateValorActualizar
        {
            public string Id { get; set; }  // Id del registro en Salesforce
            public int Valor { get; set; }  // Nuevo valor tomado del ERP -> Value__c
        }

        /// <summary>
        /// Descarga TODOS los registros de RateByConceptByProduct__c incluyendo Id y Value__c,
        /// con paginación automática. Se usa para poblar la tabla InspiraTarifasValores y
        /// posteriormente detectar qué valores cambiaron en el ERP.
        /// </summary>
        /// <returns>Lista completa de RateByConceptByProductValor</returns>
        public List<RateByConceptByProductValor> GetAllRateByConceptByProductValores()
        {
            List<RateByConceptByProductValor> todos = new List<RateByConceptByProductValor>();
            string soql = "SELECT Id, RateId__c, RateId__r.Code__c, ConceptId__c, ConceptId__r.Code__c, " +
                          "CostCenterId__c, CostCenterId__r.Code__c, ProductId__c, ProductId__r.Name, Value__c " +
                          "FROM RateByConceptByProduct__c";

            string url = $"{this.salesforceSession.sname}{this.sApiEndpoint}query?q={HttpUtility.UrlEncode(soql, Encoding.UTF8)}";
            int pagina = 1;
            try
            {
                while (!string.IsNullOrEmpty(url))
                {
                    Console.Write($"\r      Descargando página {pagina}... ({todos.Count} registros)   ");

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
                    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    HttpResponseMessage response = this.httpClient.SendAsync(request).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = response.Content.ReadAsStringAsync().Result;
                        throw new Exception($"Error HTTP {(int)response.StatusCode} al consultar RateByConceptByProduct__c (valores): {error}");
                    }

                    string jsonResponse = response.Content.ReadAsStringAsync().Result;
                    SalesforceResponse<RateByConceptByProductValor> page =
                        JsonConvert.DeserializeObject<SalesforceResponse<RateByConceptByProductValor>>(jsonResponse);

                    if (page.Records != null && page.Records.Count > 0)
                        todos.AddRange(page.Records);

                    if (!page.Done && !string.IsNullOrEmpty(page.NextRecordsUrl))
                        url = $"{this.salesforceSession.sname}{page.NextRecordsUrl}";
                    else
                        url = null;

                    pagina++;
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "SalesforceViaRestApi", ex);
                throw;
            }

            Console.WriteLine();
            return todos;
        }

        /// <summary>
        /// Actualiza en Salesforce el campo Value__c de los registros RateByConceptByProduct__c
        /// cuyos valores difieren con el ERP. Usa la Composite API en lotes de 25 con PATCH.
        /// </summary>
        /// <param name="registros">Lista de registros con Id y nuevo Valor a actualizar</param>
        public void ActualizarValoresTarifas(List<RateValorActualizar> registros)
        {
            if (registros == null || registros.Count == 0) return;

            const int batchSize = 25;
            int total = registros.Count;
            int enviados = 0;
            string compositeUrl = $"{this.salesforceSession.sname}/services/data/v60.0/composite";

            try
            {
                for (int i = 0; i < total; i += batchSize)
                {
                    int fin = Math.Min(i + batchSize, total);
                    var lote = registros.GetRange(i, fin - i);

                    var subreqs = new List<object>();
                    for (int j = 0; j < lote.Count; j++)
                    {
                        RateValorActualizar r = lote[j];
                        var body = new JObject();
                        body["Value__c"] = r.Valor;

                        subreqs.Add(new
                        {
                            method = "PATCH",
                            url = $"/services/data/v60.0/sobjects/RateByConceptByProduct__c/{r.Id}",
                            referenceId = "ref" + j,
                            body = body
                        });
                    }

                    var composite = new { allOrNone = false, compositeRequest = subreqs };
                    string payload = JsonConvert.SerializeObject(composite);

                    HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, compositeUrl);
                    request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
                    request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);

                    HttpResponseMessage response = this.httpClient.SendAsync(request).Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = response.Content.ReadAsStringAsync().Result;
                        throw new Exception($"Error HTTP {(int)response.StatusCode} en Composite PATCH valores: {error}");
                    }

                    // Verificar errores individuales por sub-request
                    string respBody = response.Content.ReadAsStringAsync().Result;
                    JObject compResp = JObject.Parse(respBody);
                    JArray items = (JArray)compResp["compositeResponse"];
                    if (items != null)
                    {
                        foreach (JToken item in items)
                        {
                            int code = (int)(item["httpStatusCode"] ?? 0);
                            if (code < 200 || code >= 300)
                            {
                                string refId = (string)(item["referenceId"] ?? "?");
                                string err = item["body"]?.ToString() ?? "sin detalle";
                                LogError.WriteMessage(
                                    "FNCInspira",
                                    "SalesforceViaRestApi",
                                    $"Error PATCH sub-request {refId} (HTTP {code}): {err}");
                                Console.WriteLine($"\n      [AVISO] Sub-request {refId} falló (HTTP {code}): {err}");
                            }
                        }
                    }

                    enviados += lote.Count;
                    Console.Write($"\r      Actualizados {enviados}/{total}...   ");
                }

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "SalesforceViaRestApi", ex);
                throw;
            }
        }

        #endregion

        #region Espacios vacíos de agenda

        // ─────────────────────────────────────────────────────────────────────────────
        // DTOs — estructuras que reflejan exactamente el JSON que devuelve
        //         FNC_EspaciosVaciosAgenda_ws en Salesforce
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Respuesta del catálogo de agendas activas
        /// GET /services/apexrest/fnc/espaciosvaciosagenda/v1
        /// </summary>
        public class RespuestaCatalogoAgendas
        {
            [JsonProperty("exitoso")]
            public bool Exitoso { get; set; }

            [JsonProperty("mensaje")]
            public string Mensaje { get; set; }

            [JsonProperty("total")]
            public int Total { get; set; }

            [JsonProperty("agendas")]
            public List<AgendaInfo> Agendas { get; set; }
        }

        public class AgendaInfo
        {
            [JsonProperty("idAgenda")]
            public string IdAgenda { get; set; }

            [JsonProperty("nombreAgenda")]
            public string NombreAgenda { get; set; }

            [JsonProperty("especialidad")]
            public string Especialidad { get; set; }

            [JsonProperty("idCentroCosto")]
            public string IdCentroCosto { get; set; }

            [JsonProperty("nombreCentroCosto")]
            public string NombreCentroCosto { get; set; }
        }

        /// <summary>
        /// Respuesta de espacios vacíos para una agenda y rango de fechas
        /// GET /services/apexrest/fnc/espaciosvaciosagenda/v1?agenda=ID&desde=...&hasta=...
        /// </summary>
        public class RespuestaEspaciosVacios
        {
            [JsonProperty("exitoso")]
            public bool Exitoso { get; set; }

            [JsonProperty("mensaje")]
            public string Mensaje { get; set; }

            [JsonProperty("idAgenda")]
            public string IdAgenda { get; set; }

            [JsonProperty("desde")]
            public string Desde { get; set; }

            [JsonProperty("hasta")]
            public string Hasta { get; set; }

            [JsonProperty("total")]
            public int Total { get; set; }

            [JsonProperty("espacios")]
            public List<EspacioVacio> Espacios { get; set; }
        }

        public class EspacioVacio
        {
            [JsonProperty("idAgenda")]
            public string IdAgenda { get; set; }

            [JsonProperty("nombreAgenda")]
            public string NombreAgenda { get; set; }

            [JsonProperty("especialidadAgenda")]
            public string EspecialidadAgenda { get; set; }

            [JsonProperty("idCentroCosto")]
            public string IdCentroCosto { get; set; }

            [JsonProperty("nombreCentroCosto")]
            public string NombreCentroCosto { get; set; }

            [JsonProperty("idConfiguracionHorario")]
            public string IdConfiguracionHorario { get; set; }

            [JsonProperty("nombreConfiguracion")]
            public string NombreConfiguracion { get; set; }

            [JsonProperty("idCategoria")]
            public string IdCategoria { get; set; }

            [JsonProperty("nombreCategoria")]
            public string NombreCategoria { get; set; }

            [JsonProperty("tipoCategoria")]
            public string TipoCategoria { get; set; }

            [JsonProperty("fecha")]
            public string Fecha { get; set; }

            [JsonProperty("dia")]
            public string Dia { get; set; }

            [JsonProperty("franjaHoraria")]
            public string FranjaHoraria { get; set; }

            [JsonProperty("horaInicioStr")]
            public string HoraInicioStr { get; set; }

            [JsonProperty("horaFinStr")]
            public string HoraFinStr { get; set; }

            [JsonProperty("horaInicio")]
            public string HoraInicio { get; set; }

            [JsonProperty("horaFin")]
            public string HoraFin { get; set; }

            [JsonProperty("duracionMinutos")]
            public int DuracionMinutos { get; set; }

            [JsonProperty("esHorarioPrincipal")]
            public bool EsHorarioPrincipal { get; set; }

            [JsonProperty("idsAgendasRelacionadasLibres")]
            public List<string> IdsAgendasRelacionadasLibres { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Métodos de integración
        // ─────────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Obtiene el catálogo completo de agendas activas desde Salesforce.
        /// Corresponde a GET /services/apexrest/fnc/espaciosvaciosagenda/v1 (sin parámetros).
        /// Este catálogo se usa para iterar después por cada agenda y descargar sus espacios vacíos.
        /// </summary>
        /// <returns>Lista de AgendaInfo con id, nombre, especialidad y centro de costo</returns>
        public List<AgendaInfo> GetCatalogoAgendas()
        {
            string url = $"{this.salesforceSession.sname}/services/apexrest/fnc/espaciosvaciosagenda/v1";
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = this.httpClient.SendAsync(request).Result;
                string json = response.Content.ReadAsStringAsync().Result;

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Error HTTP {(int)response.StatusCode} al obtener catálogo de agendas: {json}");

                RespuestaCatalogoAgendas catalogo = JsonConvert.DeserializeObject<RespuestaCatalogoAgendas>(json);

                if (!catalogo.Exitoso)
                    throw new Exception($"Salesforce reportó error en catálogo de agendas: {catalogo.Mensaje}");

                LogError.WriteMessage("ServicioDescarga", "SalesforceViaRestApi",
                    $"Catálogo de agendas obtenido: {catalogo.Total} agendas activas");

                return catalogo.Agendas ?? new List<AgendaInfo>();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "SalesforceViaRestApi", ex);
                throw;
            }
        }

        /// <summary>
        /// PASO 2 — Obtiene los espacios vacíos de UNA agenda para un rango de fechas.
        /// Llama a GET /services/apexrest/fnc/espaciosvaciosagenda/v1?agenda=ID&desde=...&hasta=...
        ///
        /// Devuelve lista vacía sin lanzar excepción cuando:
        ///   - La agenda no existe o está inactiva (HTTP 404)
        ///   - Salesforce devuelve exitoso = false
        /// En cualquier otro error HTTP lanza excepción para que el caller lo gestione.
        ///
        /// IMPORTANTE: usar rangos de máximo 7 días para evitar timeout de 120 seg.
        /// </summary>
        /// <param name="idAgenda">Id de la AgendaSetting__c en Salesforce</param>
        /// <param name="desde">Fecha inicio del rango</param>
        /// <param name="hasta">Fecha fin del rango (máx 7 días después de desde)</param>
        /// <returns>Lista de EspacioVacio. Vacía si no hay datos o la agenda no existe.</returns>
        public List<EspacioVacio> GetEspaciosVaciosPorAgenda(string idAgenda, DateTime desde, DateTime hasta)
        {
            string desdeStr = desde.ToString("yyyy-MM-dd");
            string hastaStr = hasta.ToString("yyyy-MM-dd");
            string url = $"{this.salesforceSession.sname}/services/apexrest/fnc/espaciosvaciosagenda/v1" +
                         $"?agenda={idAgenda}&desde={desdeStr}&hasta={hastaStr}";
            try
            {
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", "Bearer " + this.salesforceSession.scode);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                HttpResponseMessage response = this.httpClient.SendAsync(request).Result;
                string json = response.Content.ReadAsStringAsync().Result;

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    LogError.WriteMessage("ServicioDescarga", "SalesforceViaRestApi",
                        $"Agenda no encontrada o inactiva: {idAgenda}");
                    return new List<EspacioVacio>();
                }

                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Error HTTP {(int)response.StatusCode} para agenda {idAgenda}: {json}");

                RespuestaEspaciosVacios respuesta = JsonConvert.DeserializeObject<RespuestaEspaciosVacios>(json);

                if (!respuesta.Exitoso)
                {
                    LogError.WriteMessage("ServicioDescarga", "SalesforceViaRestApi",
                        $"Salesforce reportó error para agenda {idAgenda}: {respuesta.Mensaje}");
                    return new List<EspacioVacio>();
                }

                return respuesta.Espacios ?? new List<EspacioVacio>();
            }
            catch (Exception ex)
            {
                LogError.WriteError("ServicioDescarga", "SalesforceViaRestApi", ex);
                throw;
            }
        }

        #endregion
    }
}