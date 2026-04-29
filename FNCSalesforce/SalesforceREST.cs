using EventLog;
using FNCEntity;
using FNCSalesforce.Sfdc;
using FNCUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Tasks = System.Threading.Tasks; // Alias para evitar colisión con FNCSalesforce.Sfdc.Task

namespace FNCSalesforce
{
    /// <summary>
    /// Clase optimizada para integración con Salesforce REST API
    /// Compatible con .NET Framework 4.6.1+ y C# 7.3
    /// - Reusa HttpClient (pool de conexiones automático en .NET Framework)
    /// - Evita bloqueos con ConfigureAwait(false)
    /// - Usa JSON nativo y PATCH real
    /// - Batching con Composite API para reducir RTT
    /// </summary>
    public class SalesforceREST : IDisposable
    {
        // ===== PROPIEDADES PÚBLICAS =====
        public string sLogingEndPoint { get; set; }
        public string sApiEndpoint { get; set; }
        public SalesforceSession salesforceSession { get; set; }
        public bool battended { get; set; }
        public string sdirection { get; set; }

        // Exposición para compatibilidad con llamadas existentes: QueryRecordAsync(this.httpClient, ...)
        public HttpClient httpClient => Http;

        // ===== HTTP CLIENT ESTÁTICO (SINGLETON) =====
        private static readonly Lazy<HttpClient> _lazyHttpClient = new Lazy<HttpClient>(() =>
        {
            var handler = new WebRequestHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                UseDefaultCredentials = false
            };

            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            return client;
        });

        private static HttpClient Http => _lazyHttpClient.Value;

        // ===== CONSTRUCTOR =====
        public SalesforceREST()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)3072; // 3072 = Tls13 (si disponible)
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.DefaultConnectionLimit = 200;
        }

        // ========================================
        // LOGIN
        // ========================================
        public void DoLogin(string sUser, string sPassword, string sClientId, string sClienteSecret)
        {
            Tasks.Task.Run(() => DoLoginAsync(sUser, sPassword, sClientId, sClienteSecret)).GetAwaiter().GetResult();
        }

        public async Tasks.Task DoLoginAsync(string sUser, string sPassword, string sClientId, string sClienteSecret, CancellationToken ct = default(CancellationToken))
        {
            HttpResponseMessage resp = null;
            try
            {
                var httpContent = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "password",
                    ["client_id"] = sClientId,
                    ["client_secret"] = sClienteSecret,
                    ["username"] = sUser,
                    ["password"] = sPassword
                });

                resp = await Http.PostAsync(sLogingEndPoint + "/services/oauth2/token", httpContent, ct).ConfigureAwait(false);
                var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    throw new Exception(string.Format("Login Salesforce falló: {0} {1} -> {2}", (int)resp.StatusCode, resp.ReasonPhrase, json));
                }

                var obj = JObject.Parse(json);

                salesforceSession = new SalesforceSession
                {
                    AccessToken = (string)obj["access_token"],
                    InstanceUrl = (string)obj["instance_url"],
                    IssuedAt = (string)obj["issued_at"]
                };
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("Error de red al conectar con Salesforce: " + ex.Message, ex);
            }
            catch (JsonException ex)
            {
                throw new Exception("Error al parsear respuesta de Salesforce: " + ex.Message, ex);
            }
            finally
            {
                if (resp != null) resp.Dispose();
            }
        }

        // ========================================
        // QUERY
        // ========================================
        public string QueryRecordAsync(HttpClient _ignored, string queryMessage)
        {
            return Tasks.Task.Run(() => QueryStringAsync(queryMessage)).GetAwaiter().GetResult();
        }

        private async Tasks.Task<string> QueryStringAsync(string soql, CancellationToken ct = default(CancellationToken))
        {
            EnsureLogged();

            var url = string.Format("{0}{1}query?q={2}", salesforceSession.InstanceUrl, sApiEndpoint, Uri.EscapeDataString(soql));

            HttpRequestMessage req = null;
            HttpResponseMessage resp = null;
            try
            {
                req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", salesforceSession.AccessToken);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    throw new Exception(string.Format("Error HTTP {0} en query: {1}", (int)resp.StatusCode, body));
                }

                return body;
            }
            finally
            {
                if (req != null) req.Dispose();
                if (resp != null) resp.Dispose();
            }
        }

        // ========================================
        // UPDATE / CREATE
        // ========================================
        public bool UpdateObjectAsync(string objectName, string objectId, string updateMessage)
        {
            return Tasks.Task.Run(() => UpdateJsonAsync(objectName, objectId, ParseXmlToJson(updateMessage))).GetAwaiter().GetResult();
        }

        public bool CreateRecordAsync(string objectName, string createMessage)
        {
            return Tasks.Task.Run(() => CreateJsonAsync(objectName, ParseXmlToJson(createMessage))).GetAwaiter().GetResult();
        }

        private async Tasks.Task<bool> UpdateJsonAsync(string objectName, string id, JObject payload, CancellationToken ct = default(CancellationToken))
        {
            EnsureLogged();

            var url = string.Format("{0}{1}sobjects/{2}/{3}", salesforceSession.InstanceUrl, sApiEndpoint, objectName, id);

            HttpRequestMessage req = null;
            HttpResponseMessage resp = null;
            try
            {
                req = new HttpRequestMessage(new HttpMethod("PATCH"), url);
                req.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", salesforceSession.AccessToken);

                resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception(string.Format("Error al actualizar {0}/{1}: {2} {3} -> {4}", objectName, id, (int)resp.StatusCode, resp.ReasonPhrase, body));
                }

                return true; // PATCH 204
            }
            finally
            {
                if (req != null) req.Dispose();
                if (resp != null) resp.Dispose();
            }
        }

        private async Tasks.Task<bool> CreateJsonAsync(string objectName, JObject payload, CancellationToken ct = default(CancellationToken))
        {
            EnsureLogged();

            var url = string.Format("{0}{1}sobjects/{2}/", salesforceSession.InstanceUrl, sApiEndpoint, objectName);

            HttpRequestMessage req = null;
            HttpResponseMessage resp = null;
            try
            {
                req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", salesforceSession.AccessToken);

                resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception(string.Format("Error al crear {0}: {1} {2} -> {3}", objectName, (int)resp.StatusCode, resp.ReasonPhrase, body));
                }

                return true;
            }
            finally
            {
                if (req != null) req.Dispose();
                if (resp != null) resp.Dispose();
            }
        }

        // ========================================
        // COMPOSITE API (BATCHING)
        // ========================================
        private async Tasks.Task<bool> CompositePatchAsync(string objectName, IEnumerable<Tuple<string, JObject>> items, CancellationToken ct = default(CancellationToken))
        {
            EnsureLogged();

            var itemsList = items.ToList();
            if (itemsList.Count == 0) return true;

            var sub = itemsList.Select((x, i) => new
            {
                method = "PATCH",
                url = string.Format("/services/data/v60.0/sobjects/{0}/{1}", objectName, x.Item1),
                referenceId = "ref" + i.ToString(),
                body = x.Item2
            }).ToArray();

            var composite = new { allOrNone = false, compositeRequest = sub };
            var url = salesforceSession.InstanceUrl + "/services/data/v60.0/composite";

            HttpRequestMessage req = null;
            HttpResponseMessage resp = null;
            try
            {
                req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(JsonConvert.SerializeObject(composite), Encoding.UTF8, "application/json");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", salesforceSession.AccessToken);

                resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);

                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new Exception(string.Format("Composite PATCH falló: {0} {1} -> {2}", (int)resp.StatusCode, resp.ReasonPhrase, body));
                }

                return true;
            }
            finally
            {
                if (req != null) req.Dispose();
                if (resp != null) resp.Dispose();
            }
        }

        // ========================================
        // MÉTODOS PÚBLICOS OPTIMIZADOS
        // ========================================
        public void SetTurnAppointments(List<string> appointments, string turnnumber, int turnid)
        {
            if (appointments == null || appointments.Count == 0) return;
            Tasks.Task.Run(() => SetTurnAppointmentsAsync(appointments, turnnumber, turnid)).GetAwaiter().GetResult();
        }

        public async Tasks.Task SetTurnAppointmentsAsync(List<string> appointments, string turnnumber, int turnid, CancellationToken ct = default(CancellationToken))
        {
            if (appointments == null || appointments.Count == 0) return;
            const int batchSize = 25;
            for (int i = 0; i < appointments.Count; i += batchSize)
            {
                var batch = appointments.Skip(i).Take(batchSize)
                    .Select(id => Tuple.Create(id, new JObject
                    {
                        ["TurnNumber__c"] = turnnumber,
                        ["MotivoNoTurno__c"] = turnid
                    }));

                await CompositePatchAsync("Appointment__c", batch, ct).ConfigureAwait(false);
            }
        }

        private void UpdateAppointmentForTurnSelection(Appointment__c appointment, SalesforceIntegrator integrator)
        {
            StringBuilder stringBuilder = new StringBuilder("<root>");
            stringBuilder.Append("<IsRemoteTurn__c>true</IsRemoteTurn__c>");
            stringBuilder.Append("<noTurnSelect__c>true</noTurnSelect__c>");
            stringBuilder.Append("</root>");
            bool bUpdate = this.UpdateObjectAsync("Appointment__c", appointment.Id, stringBuilder.ToString());
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
                // Citas del día (usar TODAY para evitar problemas de zona horaria)
                sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.DocumentNumber__c");
                sQuery.Append(", WhatId__r.DocumentType__c, PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
                sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c, FNC_prefacturado__c, GroupId__r.Description__c, GroupId__r.Code__c" +
                    ", WhatId__r.State__r.Name, WhatId__r.City__r.Name, WhatId__r.Gender__pc, WhatId__r.Phone, WhatId__r.Address__c" +
                    ", AgreementId__r.Code__c, WhatId__r.PersonEmail, WhatId__r.PersonBirthdate, PlanId__c, CostCenterId__c, CostCenterId__r.Code__c, PlanId__r.RateId__c, ins2_categoria__c, FNC_RequierePreconsulta__c" +
                    " FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                sQuery.Append(sDocument);
                sQuery.Append("' AND WhatId__r.DocumentType__c = '");
                sQuery.Append(sDocumentType);
                sQuery.Append("' AND ActivityDate__c = TODAY");
                sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");

                string jsonResponse = this.QueryRecordAsync(this.httpClient, sQuery.ToString());
                SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
                lappointments = response.Records;

                if (lappointments.Count > 0)
                {
                    int i = 0;
                    int j = 0;
                    foreach (Appointment__c appointment in lappointments)
                    {
                        if (appointment.AgendaId__r.Name.Contains("SALA"))
                        {
                            iProcedure = j;
                        }
                        iQue = integrator.ValidateAppointment(appointment, true, idistance);
                        if (iQue != 0)
                        {
                            if (i == 0) ifirstque = iQue;
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
                        digiturno5.oResult = new Result()
                        {
                            iresult = 1,
                            smessage = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
                            iroom = integrator.GetRoom(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
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
                digiturno5.oResult = new Result() { iresult = 0, smessage = "Ha ocurrido un error al consultar las citas del paciente" };
            }
            return digiturno5;
        }

        private string UpdateAppointment(Appointment__c appointment, SalesforceIntegrator integrator, List<Appointment__c> lappointments, bool bisremote = false)
        {
            if (integrator.ValidateAppointment(appointment, true, 0) == 0)
            {
                return "Cita vencida";
            }
            if (appointment.ServiceBilled__c.Value)
            {
                this.sdirection = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, true, true, appointment.PlanName__c, appointment.IsCoPayment__c.Value, appointment.AgendaId__r.Name, appointment.AgreementId__r.Name);
                return "Cita facturada";
            }
            try
            {
                string sStatus = "Asistió";

                bool bCopayment = appointment.IsCoPayment__c.HasValue ? appointment.IsCoPayment__c.Value : true;
                bool bNeedsPre = (appointment.GroupId__r.Needspreassessment__c.HasValue) ? appointment.GroupId__r.Needspreassessment__c.Value : false;
                bool bAppointmentPre = (appointment.FNC_RequierePreconsulta__c.HasValue) ? appointment.FNC_RequierePreconsulta__c.Value : false;
                bool bPrefacturado = (appointment.FNC_prefacturado__c != null) ? appointment.FNC_prefacturado__c.Value : false;
                if (bPrefacturado && appointment.prefacturadoApp__c.HasValue)
                {
                    bPrefacturado = (appointment.prefacturadoApp__c.Value && bPrefacturado);
                }

                bool bNeedsInvoice = integrator.NeedsInvoice(
                    appointment.PlanName__c,
                    appointment.GroupId__r?.Name,
                    appointment.AuthorizationCode__c,
                    bCopayment,
                    appointment.WhatId__r.Age2__pc.HasValue ? appointment.WhatId__r.Age2__pc.Value : 0
                );

                bool bNeedsPreassessment = this.NeeedsPreAssessment(
                    appointment.GroupId__r?.Name,
                    Convert.ToInt32(appointment.WhatId__r?.Age2__pc ?? 0),
                    appointment.AgendaId__r?.Name,
                    lappointments,
                    (bNeedsPre && bAppointmentPre)
                );

                StringBuilder stringBuilder = BuildBaseUpdateXml();
                AppendStatusSpecificFields(stringBuilder, bNeedsInvoice, bNeedsPreassessment, bisremote, bPrefacturado, out sStatus);

                stringBuilder.Append($"<Status__c>{sStatus}</Status__c>");
                stringBuilder.Append("</root>");

                if (sStatus == "Facturada" || sStatus == "Pre Consulta")
                {
                    this.sdirection = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, false, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name, appointment.AgreementId__r.Name);
                }

                bool bUpdateResult = this.UpdateObjectAsync("Appointment__c", appointment.Id, stringBuilder.ToString());
                LogAppointmentUpdate(appointment, sStatus, bNeedsInvoice, bNeedsPreassessment, bUpdateResult);
                return $"Cita {sStatus}";
            }
            catch (Exception ex)
            {
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

            if (bNeedsInvoice && bNeedsPreassessment)
            {
                sStatus = "Pre Consulta";
                AppendInvoicedFields(stringBuilder, currentDateTime);
                stringBuilder.Append("<patient_pre__c>true</patient_pre__c>");
            }
            else if ((bNeedsInvoice || bPrefacturado) && !bNeedsPreassessment && !bisremote)
            {
                sStatus = "Facturada";
                AppendInvoicedFields(stringBuilder, currentDateTime);
            }
            else if (bNeedsInvoice && bisremote)
            {
                sStatus = "Prefacturado";
                stringBuilder.Append("<FNC_prefacturado__c>true</FNC_prefacturado__c>");
                if (bisremote)
                {
                    stringBuilder.Append("<prefacturadoApp__c>true</prefacturadoApp__c>");
                }
            }
            else
            {
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
            string logMessage = $"Appointment Update - Patient: {appointment.WhatId__r?.DocumentNumber__c}, Group: {appointment.GroupId__r?.Name}, Agenda: {appointment.AgendaId__r?.Name}, Status: {status}, Needs Invoice: {needsInvoice}, Needs Preassessment: {needsPreassessment}, Update Result: {updateResult}";
            LogError.WriteMessage("Application", "WSInspira", logMessage);
        }

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
                    sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.DocumentNumber__c");
                    sQuery.Append(",  WhatId__r.DocumentType__c, PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
                    sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c, FNC_prefacturado__c, GroupId__r.Description__c, GroupId__r.Code__c" +
                        ", WhatId__r.State__r.Name, WhatId__r.City__r.Name, WhatId__r.Gender__pc, WhatId__r.Phone, WhatId__r.Address__c" +
                        ", AgreementId__r.Code__c, WhatId__r.PersonEmail, WhatId__r.PersonBirthdate, PlanId__c, CostCenterId__c, CostCenterId__r.Code__c, PlanId__r.RateId__c, ins2_categoria__c, FNC_RequierePreconsulta__c" +
                        " FROM Appointment__c WHERE WhatId__r.DocumentNumber__c = '");
                    sQuery.Append(sDocument);
                    sQuery.Append("' AND WhatId__r.DocumentType__c = '");
                    sQuery.Append(sDocumentType);
                    sQuery.Append("' AND ActivityDate__c = TODAY");
                    sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                }
                else
                {
                    sQuery.Append("SELECT Name, Id, StartDatetime__c, WhatId__r.FirstName_c__pc, WhatId__r.SecondName__pc, WhatId__r.FirstSurname__pc, WhatId__r.SecondSurname__pc, WhatId__r.DocumentNumber__c");
                    sQuery.Append(",  WhatId__r.DocumentType__c, PlanId__r.HealthCarePlanId__c, PlanName__c, GroupId__r.Name, AuthorizationCode__c, IsCoPayment__c, WhatId__r.Age2__pc, AgendaId__r.Name, TurnNumber__c");
                    sQuery.Append(", AgreementId__r.Name, ServiceBilled__c, GroupId__r.Needspreassessment__c, GroupId__c, ActivityDate__c, FNC_prefacturado__c, GroupId__r.Description__c, GroupId__r.Code__c" +
                        ", WhatId__r.State__r.Name, WhatId__r.City__r.Name, WhatId__r.Gender__pc, WhatId__r.Phone, WhatId__r.Address__c" +
                        ", AgreementId__r.Code__c, WhatId__r.PersonEmail, WhatId__r.PersonBirthdate, PlanId__c, CostCenterId__c, CostCenterId__r.Code__c, PlanId__r.RateId__c, ins2_categoria__c, FNC_RequierePreconsulta__c" +
                        " FROM Appointment__c WHERE Id ='");
                    sQuery.Append(sappointment);
                    sQuery.Append("' AND ActivityDate__c = TODAY");
                    sQuery.Append(" AND Status__c IN ('Confirmada', 'Asignada', 'Prefacturado') AND FNC_MainAppointment__c = true ORDER BY StartDatetime__c");
                }

                string jsonResponse = this.QueryRecordAsync(this.httpClient, sQuery.ToString());
                SalesforceResponse<Appointment__c> response = JsonConvert.DeserializeObject<SalesforceResponse<Appointment__c>>(jsonResponse);
                lappointments = response.Records;

                if (lappointments.Count > 0)
                {
                    int i = 0;
                    int j = 0;
                    foreach (Appointment__c appointment in lappointments)
                    {
                        if (appointment.AgendaId__r.Name.Contains("SALA"))
                        {
                            iProcedure = j;
                        }
                        iQue = integrator.ValidateAppointment(appointment);
                        if (iQue != 0)
                        {
                            if (i == 0) ifirstque = iQue;
                            this.UpdateAppointment(appointment, integrator, lappointments);
                            i++;
                        }
                        j++;
                    }

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
                        digiturno5.oResult = new Result()
                        {
                            iresult = 1,
                            smessage = integrator.GetDirection(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
                            iroom = integrator.GetRoom(appointment.WhatId__r.Age2__pc.Value, appointment.GroupId__r.Name, bNeedsInvoice, bIsVip, appointment.PlanName__c, bCopayment, appointment.AgendaId__r.Name.ToUpper(), appointment.AgreementId__r.Name),
                        };
                    }
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
                            smessage = "Diríjase al Piso 3 Caja Rehabilitación Pulmonar \n";
                        }
                        StringBuilder stringBuilder = new StringBuilder(smessage);
                        if (sResumen.Length > 0 && !bIsRHB)
                        {
                            stringBuilder.Append("Resumen de Servicios: \n" + sResumen.ToString());
                        }
                        digiturno5.oResult = new Result() { iresult = 0, smessage = stringBuilder.ToString() };
                        return digiturno5;
                    }
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
            return bNeedsPre; // lógica previa comentada, se respeta salida
        }

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
        /// Envía la información de la cita de intervencionismo a Synapse (integración PACS)
        /// </summary>
        public int SendAppointmentToSynapse(Appointment__c appointment)
        {
            int i = 1;
            SynapseEntity synapseEntity = null;
            double iage = (appointment.WhatId__r.Age2__pc.HasValue) ? appointment.WhatId__r.Age2__pc.Value : 0;
            string sunit = (iage >= 18) ? "1100" : "1200";
            string srequest = string.Empty;
            bool success = false;
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
                        success = Tools.PostJson(FNCSalesforce.Properties.Settings.Default.SynapseWSURL, srequest);
                        if (!success)
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

        // ========================================
        // HELPERS
        // ========================================
        private void EnsureLogged()
        {
            if (salesforceSession == null || string.IsNullOrEmpty(salesforceSession.AccessToken) || string.IsNullOrEmpty(salesforceSession.InstanceUrl))
            {
                throw new InvalidOperationException("No hay sesión de Salesforce. Llama DoLogin primero.");
            }
        }

        private static JObject ParseXmlToJson(string xml)
        {
            var obj = new JObject();
            if (string.IsNullOrWhiteSpace(xml)) return obj;

            int idx = 0;
            while (idx < xml.Length)
            {
                int open = xml.IndexOf('<', idx);
                if (open < 0) break;

                int closeTag = xml.IndexOf('>', open + 1);
                if (closeTag < 0) break;

                var tag = xml.Substring(open + 1, closeTag - open - 1).Trim('/', ' ');
                if (string.IsNullOrEmpty(tag) || tag.Equals("root", StringComparison.OrdinalIgnoreCase)) { idx = closeTag + 1; continue; }
                var endTag = "</" + tag + ">";
                int end = xml.IndexOf(endTag, closeTag + 1, StringComparison.OrdinalIgnoreCase);
                if (end < 0) break;

                var value = xml.Substring(closeTag + 1, end - closeTag - 1);
                obj[tag] = value;
                idx = end + endTag.Length;
            }

            return obj;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }

    // ========================================
    // CLASES DE SOPORTE
    // ========================================
    public class SalesforceSession
    {
        public string AccessToken { get; set; }
        public string InstanceUrl { get; set; }
        public string IssuedAt { get; set; }
        public string TokenType { get; set; }
        public string Signature { get; set; }
    }

    public class SalesforceResponse<T>
    {
        [JsonProperty("totalSize")] public int TotalSize { get; set; }
        [JsonProperty("done")] public bool Done { get; set; }
        [JsonProperty("records")] public List<T> Records { get; set; }
        [JsonProperty("nextRecordsUrl")] public string NextRecordsUrl { get; set; }
    }

    public class SalesforceCreateResponse
    {
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("success")] public bool Success { get; set; }
        [JsonProperty("errors")] public List<SalesforceError> Errors { get; set; }
    }

    public class SalesforceError
    {
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("errorCode")] public string ErrorCode { get; set; }
        [JsonProperty("fields")] public List<string> Fields { get; set; }
    }

    public class CompositeResponse
    {
        [JsonProperty("compositeResponse")] public List<CompositeSubResponse> items { get; set; }
    }

    public class CompositeSubResponse
    {
        [JsonProperty("body")] public JObject Body { get; set; }
        [JsonProperty("httpHeaders")] public Dictionary<string, string> HttpHeaders { get; set; }
        [JsonProperty("httpStatusCode")] public int HttpStatusCode { get; set; }
        [JsonProperty("referenceId")] public string ReferenceId { get; set; }
    }
}
