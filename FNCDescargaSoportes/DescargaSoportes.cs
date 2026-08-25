using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using EventLog;
using FNCSalesforce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace FNCDescargaSoportes
{
    class Program
    {
        // ── Settings — leer de Properties.Settings.Default ───────────────────
        static readonly string SF_LOGIN_URL = FNCDescargaSoportes.Properties.Settings.Default.SalesforceLoginURL;
        static readonly string SF_USER = FNCDescargaSoportes.Properties.Settings.Default.SalesforceUser;
        static readonly string SF_PASSWORD = FNCDescargaSoportes.Properties.Settings.Default.SalesforcePassword; // ya incluye token
        static readonly string SF_CLIENT = FNCDescargaSoportes.Properties.Settings.Default.SalesforceClient;
        static readonly string SF_SECRET = FNCDescargaSoportes.Properties.Settings.Default.SalesforceSecret;
        static readonly string SF_ENDPOINT = FNCDescargaSoportes.Properties.Settings.Default.SalesforceEndPoint;

        static readonly string AWS_ACCESS = FNCDescargaSoportes.Properties.Settings.Default.AWSKey;
        static readonly string AWS_SECRET_K = FNCDescargaSoportes.Properties.Settings.Default.AWSSecret;
        static readonly string PFP_BUCKET = FNCDescargaSoportes.Properties.Settings.Default.PfpBucket;
        static readonly string RHB_BUCKET = FNCDescargaSoportes.Properties.Settings.Default.RhbBucket;
        static readonly string SLEEP_BUCKET = FNCDescargaSoportes.Properties.Settings.Default.SleepBucket;
        static readonly string ALER_BUCKET = FNCDescargaSoportes.Properties.Settings.Default.AlerBucket;
        static readonly string OUTPUT_DIR = FNCDescargaSoportes.Properties.Settings.Default.SupportPath;
        static readonly string DATE_FROM = FNCDescargaSoportes.Properties.Settings.Default.DateFrom;
        static readonly string DATE_TO = FNCDescargaSoportes.Properties.Settings.Default.DateTo;

        static readonly string[] PLANES_SANITAS =
        {
            "FNC ALIANSALUD HTP RIESG INTER",
            "FNC ALIANSALUD HTP RIESGO ALTO",
            "FNC ALIANSALUD HTP RIESGO BAJO",
            "FNC ALIANSALUD HTP VALORACION",
            "FNC ALIANSALUD VMI",
            "FNC ALIANSALUD HTP RIESG INTER",
            "FNC ALIANSALUD HTP RIESGO ALTO",
            "FNC ALIANSALUD HTP RIESGO BAJO",
            "FNC ALIANSALUD HTP VALORACION",
            "FNC ALIANSALUD VMI",
            "FNC ECOPETROL AIREPOC LEVE-MOD",
            "FNC ECOPETROL AIREPOC SEVERO",
            "FNC ECOPETROL AIREPOC VALORACI",
            "FNC ECOPETROL ASMA SEVERO",
            "FNC ECOPETROL ASMAIRE LEVE-MOD",
            "FNC ECOPETROL VALORA ASMAIRE",
            "FNC ECOPETROL HTP VALORACION",
            "FNC ECOPETROL HTP RIESGO BAJO",
            "FNC SANITAS AIREPOC MODERADO",
            "FNC SANITAS AIREPOC SEVERO",
            "FNC SANITAS AIREPOC VALORACION",
            "FNC SANITAS ASMAIRE MODERADO",
            "FNC SANITAS ASMAIRE SEVERO",
            "FNC SANITAS ASMAIRE VALORACION",
            "FNC SANITAS HTP RIESG INTERMED",
            "FNC SANITAS HTP RIESGO ALTO",
            "FNC SANITAS HTP RIESGO BAJO",
            "FNC SANITAS HTP VALORAC INICIA",
            "FNC SANITAS AIREPOC MODERADO",
            "FNC SANITAS AIREPOC SEVERO",
            "FNC SANITAS AIREPOC VALORACION",
            "FNC SANITAS ASMAIRE MODERADO",
            "FNC SANITAS ASMAIRE SEVERO",
            "FNC SANITAS ASMAIRE VALORACION",
            "FNC SANITAS HTP RIESG INTERMED",
            "FNC SANITAS HTP RIESGO ALTO",
            "FNC SANITAS HTP RIESGO BAJO",
            "FNC SANITAS HTP VALORAC INICIA",
            "FNC SURA EPS VMI",
            "FNC SURA HTP RIESGO ALTO",
            "FNC SURA EPS VMI",
            "FNC SURA HTP RIESGO ALTO",
            "FFNC SALUD TOTAL HTP RIESG BAJ",
            "FNC SALUD TOTAL HTP RIESG INTE",
            "FNC SALUD TOTAL HTP RIESGO ALT",
            "FNC SALUD TOTAL HTP VALORACION",
        };

        static readonly string[] EXCLUDE_GROUPS = { "BLOQUEO", "INVEST" };

        static SalesforceREST sfRest;
        static AWSConnector awsConnector;

        // ────────────────────────────────────────────────────────────────────
        static void Main(string[] args)
        {
            Console.WriteLine($"=== FNCDescargaSoportes — {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            Directory.CreateDirectory(OUTPUT_DIR);

            // 1. Login Salesforce
            Console.WriteLine("\n[1/3] Autenticando en Salesforce...");
            sfRest = new SalesforceREST();
            sfRest.sLogingEndPoint = SF_LOGIN_URL;
            sfRest.sApiEndpoint = SF_ENDPOINT;
            sfRest.DoLogin(SF_USER, SF_PASSWORD, SF_CLIENT, SF_SECRET);

            if (sfRest.salesforceSession == null)
            {
                Console.WriteLine("ERROR: No se pudo autenticar en Salesforce.");
                return;
            }
            Console.WriteLine($"      OK — {sfRest.salesforceSession.InstanceUrl}");

            // 2. Obtener DISTINCT WhatId__c de los planes Sanitas
            //    y con esos IDs traer TODAS sus citas en el rango (cualquier plan)
            Console.WriteLine("\n[2/3] Obteniendo pacientes Sanitas y sus citas...");
            //List<string> pacienteIds = ObtenerPacientesSanitas();
            //Console.WriteLine($"      {pacienteIds.Count} pacientes distintos en planes Sanitas.");
            //if (pacienteIds.Count == 0) { Console.WriteLine("Sin pacientes. Fin."); return; }

            // Traer TODAS las citas de esos pacientes en el rango,
            // sin importar por qué plan fueron atendidos en cada cita
            List<AppointmentData> citas = ObtenerCitasProgramas(new List<string>());
            Console.WriteLine($"      {citas.Count} citas encontradas.");
            if (citas.Count == 0) { Console.WriteLine("Sin citas. Fin."); return; }

            // 3. Clasificar y descargar soportes
            Console.WriteLine("\n[3/3] Clasificando citas y descargando soportes...");
            awsConnector = new AWSConnector(AWS_ACCESS, AWS_SECRET_K);
            awsConnector.Connect();
            List<SupportResult> resultados = ProcesarCitas(citas);

            Console.WriteLine("\nGuardando reporte...");
            GuardarReporte(resultados);

            Console.WriteLine($"\n=== Proceso completado. Archivos en: {OUTPUT_DIR} ===");
        }

        // ────────────────────────────────────────────────────────────────────
        // PASO 2: DISTINCT WhatId__c de planes Sanitas con paginación
        // ────────────────────────────────────────────────────────────────────
        static List<string> ObtenerPacientesSanitas()
        {
            var ids = new HashSet<string>();
            string planesOr = string.Join(" OR ",
                PLANES_SANITAS.Select(p => $"PlanId__r.Name LIKE '%{p}%'"));

            string soql = $@"SELECT WhatId__c
                             FROM Appointment__c
                             WHERE ({planesOr})
                             AND ActivityDate__c >= {DATE_FROM}
                             AND ActivityDate__c <= {DATE_TO}
                             AND PatientAttended__c = true";

            foreach (var rec in QueryAll(soql))
            {
                string id = rec["WhatId__c"]?.Value<string>();
                if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
            }

            return ids.ToList();
        }

        static List<AppointmentData> ObtenerCitasProgramas(List<string> pacienteIds)
        {
            var citas = new List<AppointmentData>();
            string planesOr = string.Join(" OR ", PLANES_SANITAS.Select(p => $"PlanId__r.Name LIKE '%{p}%'"));
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string initialdate = new DateTime(DateTime.Now.Year, 7, 20).ToString("yyyy-MM-dd");
            string soql = $@"SELECT Id, WhatId__c, WhatId__r.DocumentNumber__c, GroupId__r.Name, CostCenterId__c, CostCenterId__r.Code__c, ActivityDate__c, Name FROM Appointment__c WHERE ({planesOr}) AND ActivityDate__c >= {initialdate} AND ActivityDate__c <= {today} AND PatientAttended__c = true";
            foreach (var rec in QueryAll(soql))
            {
                citas.Add(new AppointmentData
                {
                    Id = rec["Id"]?.Value<string>(),
                    WhatId = rec["WhatId__c"]?.Value<string>(),
                    DocumentNumber = rec["WhatId__r"]?["DocumentNumber__c"]?.Value<string>(),
                    GroupName = rec["GroupId__r"]?["Name"]?.Value<string>(),
                    CostCenterId = rec["CostCenterId__c"]?.Value<string>(),
                    CostCenterCode = rec["CostCenterId__r"]?["Code__c"]?.Value<string>(),
                    ActivityDate = rec["ActivityDate__c"]?.Value<string>(),
                    Name = rec["Name"]?.Value<string>(),
                });
            }
            return citas;
        }

        // ────────────────────────────────────────────────────────────────────
        // PASO 3: Todas las citas de los pacientes Sanitas en el rango
        // Se procesan en batches de 100 IDs para no exceder el límite de URL
        // de SOQL (~16K chars). Cada batch pagina via nextRecordsUrl.
        // ────────────────────────────────────────────────────────────────────
        static List<AppointmentData> ObtenerCitasPacientes(List<string> pacienteIds)
        {
            var citas = new List<AppointmentData>();
            const int batchSize = 100; // 100 IDs x 18 chars = ~1800 chars en el IN
            int totalBatches = (int)Math.Ceiling(pacienteIds.Count / (double)batchSize);
            int batchNum = 0;

            for (int i = 0; i < pacienteIds.Count; i += batchSize)
            {
                batchNum++;
                var batch = pacienteIds.Skip(i).Take(batchSize).ToList();
                string inList = string.Join(",", batch.Select(id => $"'{id}'"));

                // Sin filtro de plan — traer TODAS las citas de estos pacientes
                // sin importar por cuál plan fueron atendidos en cada cita
                string soql = $@"SELECT Id,
                                        WhatId__c,
                                        WhatId__r.DocumentNumber__c,
                                        GroupId__r.Name,
                                        CostCenterId__c,
                                        CostCenterId__r.Code__c,
                                        ActivityDate__c
                                 FROM Appointment__c
                                 WHERE WhatId__c IN ({inList})
                                 AND ActivityDate__c >= {DATE_FROM}
                                 AND ActivityDate__c <= {DATE_TO}
                                 AND PatientAttended__c = true";

                foreach (var rec in QueryAll(soql))
                {
                    citas.Add(new AppointmentData
                    {
                        Id = rec["Id"]?.Value<string>(),
                        WhatId = rec["WhatId__c"]?.Value<string>(),
                        DocumentNumber = rec["WhatId__r"]?["DocumentNumber__c"]?.Value<string>(),
                        GroupName = rec["GroupId__r"]?["Name"]?.Value<string>(),
                        CostCenterId = rec["CostCenterId__c"]?.Value<string>(),
                        CostCenterCode = rec["CostCenterId__r"]?["Code__c"]?.Value<string>(),
                        ActivityDate = rec["ActivityDate__c"]?.Value<string>(),
                        
                    });
                }

                Console.Write($"\r      Batch {batchNum}/{totalBatches} — {citas.Count} citas acumuladas...");
            }
            Console.WriteLine();
            return citas;
        }

        // ────────────────────────────────────────────────────────────────────
        // PASO 4: Clasificar y descargar
        // ────────────────────────────────────────────────────────────────────
        static List<SupportResult> ProcesarCitas(List<AppointmentData> citas)
        {
            var resultados = new List<SupportResult>();
            var lstConsultas = new List<AppointmentData>();
            var lstPft = new List<AppointmentData>();
            var lstAllergys = new List<AppointmentData>();
            var lstSleep = new List<AppointmentData>();
            var lstRehab = new List<AppointmentData>();

            foreach (var c in citas)
            {
                string code = c.CostCenterCode ?? "";
                string group = c.GroupName ?? "";

                if (!EXCLUDE_GROUPS.Any(n => group.ToUpper().Contains(n)))
                    lstConsultas.Add(c);

                if (!string.IsNullOrWhiteSpace(code))
                {
                    if (code.StartsWith("41")) lstPft.Add(c);
                    else if (code.StartsWith("42")) lstAllergys.Add(c);
                    else if (code.StartsWith("40")) lstSleep.Add(c);
                    else if (code.StartsWith("50")) lstRehab.Add(c);
                }
            }

            Console.WriteLine($"      Consultas  : {lstConsultas.Count}");
            Console.WriteLine($"      PFT        : {lstPft.Count}");
            Console.WriteLine($"      Alergología: {lstAllergys.Count}");
            Console.WriteLine($"      Sueño      : {lstSleep.Count}");
            Console.WriteLine($"      Rehab      : {lstRehab.Count}");

            int consecutivo = 1;
            // Consultas médicas: primero obtener los Assesment__c
            // (el PDF se genera con el Id del Assesment, no el de la Appointment)
            consecutivo = ProcesarConsultas(lstConsultas, ref consecutivo, resultados);
            consecutivo = ProcesarLote(lstPft, "PFT", PFP_BUCKET, ref consecutivo, resultados);
            consecutivo = ProcesarLote(lstAllergys, "ALER", ALER_BUCKET, ref consecutivo, resultados);
            consecutivo = ProcesarLote(lstSleep, "SLEEP", SLEEP_BUCKET, ref consecutivo, resultados);
            consecutivo = ProcesarLote(lstRehab, "RHB", RHB_BUCKET, ref consecutivo, resultados);

            return resultados;
        }

        static int ProcesarLote(List<AppointmentData> lote, string tipo, string bucket,
            ref int consecutivo, List<SupportResult> resultados)
        {
            int idx = 0;
            foreach (var cita in lote)
            {
                idx++;
                Console.Write($"\r      {tipo}: {idx}/{lote.Count}...");
                SupportResult r = ProcesarDesdeAWS(cita, tipo, bucket, consecutivo);
                resultados.Add(r);
                if (r.Exito) consecutivo++;
            }
            if (lote.Count > 0) Console.WriteLine();
            return consecutivo;
        }

        // ── Consultas médicas:
        //    1. Query a Assesment__c para obtener el Id del assessment por AppointmentId__c
        //    2. Con el Id del Assesment llamar al endpoint que genera el PDF
        //    (el endpoint recibe el Id de Assesment__c, NO el de Appointment__c)
        static int ProcesarConsultas(List<AppointmentData> lstConsultas,
            ref int consecutivo, List<SupportResult> resultados)
        {
            if (lstConsultas.Count == 0) return consecutivo;

            // Mapear AppointmentId → AppointmentData para cruzar después
            var citaMap = lstConsultas.ToDictionary(c => c.Id, c => c);

            // Obtener Assesments en batches de 100 IDs de Appointment
            var assesments = new List<AssesmentData>();
            const int batchSize = 100;
            int totalBatches = (int)Math.Ceiling(lstConsultas.Count / (double)batchSize);
            int batchNum = 0;

            for (int i = 0; i < lstConsultas.Count; i += batchSize)
            {
                batchNum++;
                var batch = lstConsultas.Skip(i).Take(batchSize).ToList();
                string ids = string.Join(",", batch.Select(c => $"'{c.Id}'"));

                // Igual que GetAssementNotes del código existente:
                // filtrar PCutánea y ordenar por Id
                string soql = $@"SELECT Id,
                                        Status__c,
                                        AppointmentId__c,
                                        AppointmentId__r.Name,
                                        AppointmentId__r.GroupId__r.Name,
                                        CreatedDate,
                                        AppointmentId__r.CostCenterId__r.Name
                                 FROM Assesment__c
                                 WHERE AppointmentId__c IN ({ids})
                                 AND (NOT Name LIKE 'Informe de procedimiento PCutánea%')
                                 ORDER BY Id";

                Console.Write($"\r      Assesments batch {batchNum}/{totalBatches}...");

                foreach (var rec in QueryAll(soql))
                {
                    string appointmentId = rec["AppointmentId__c"]?.Value<string>();
                    if (string.IsNullOrWhiteSpace(appointmentId)) continue;

                    // Solo procesar estados válidos (equivale al itype del código original)
                    string status = rec["Status__c"]?.Value<string>() ?? "";

                    assesments.Add(new AssesmentData
                    {
                        AssesmentId = rec["Id"]?.Value<string>(),
                        AppointmentId = appointmentId,
                        Status = status,
                        CreatedDate = rec["CreatedDate"]?.Value<string>(),
                        AppointmentName = rec["AppointmentId__r"]?["Name"]?.Value<string>()
                    });
                }
            }
            Console.WriteLine($"\r      {assesments.Count} assesments encontrados para {lstConsultas.Count} citas.");

            // Descargar PDF por cada Assesment usando su Id
            int idx = 0;
            foreach (var assesment in assesments)
            {
                idx++;
                Console.Write($"\r      CONSULTA PDF: {idx}/{assesments.Count}...");

                // Obtener datos de la cita original para el nombre del archivo
                AppointmentData cita = citaMap.ContainsKey(assesment.AppointmentId)
                    ? citaMap[assesment.AppointmentId]
                    : null;

                var result = new SupportResult
                {
                    DocumentNumber = cita?.DocumentNumber,
                    ActivityDate = cita?.ActivityDate,
                    GroupName = cita?.GroupName,
                    TipoSoporte = "CONSULTA",
                    CitaId = assesment.AppointmentId,
                    AssesmentId = assesment.AssesmentId
                };

                try
                {
                    // El endpoint recibe el Id del Assesment__c, no el de Appointment__c
                    string jsonResp = GetAssessmentFormat(assesment.AssesmentId);
                    string innerJson = JsonConvert.DeserializeObject<string>(jsonResp);
                    var sfResp = JsonConvert.DeserializeObject<SalesforceServiceResponse>(innerJson);

                    if (sfResp?.success == true && !string.IsNullOrWhiteSpace(sfResp.base64))
                    {
                        byte[] pdfBytes = Convert.FromBase64String(sfResp.base64);
                        string fileName = BuildFileName(
                            cita?.DocumentNumber,
                            cita?.ActivityDate + "_" + assesment?.AppointmentName,
                            consecutivo,
                            "HC");
                        File.WriteAllBytes(Path.Combine(OUTPUT_DIR, fileName), pdfBytes);
                        result.Exito = true;
                        result.Archivo = fileName;
                        consecutivo++;
                    }
                    else
                    {
                        result.Observacion = $"Sin PDF: {sfResp?.message}";
                    }
                }
                catch (Exception ex)
                {
                    result.Observacion = ex.Message;
                    LogError.WriteError("FNCDescargaSoportes", "ProcesarConsultas", ex);
                }

                resultados.Add(result);
            }
            Console.WriteLine();
            return consecutivo;
        }

        // ── Examen → AWS S3 (buscar por DocumentNumber_YYYYMMDD) ────────────
        static SupportResult ProcesarDesdeAWS(AppointmentData cita, string tipo, string bucket, int consecutivo)
        {
            var result = new SupportResult
            {
                DocumentNumber = cita.DocumentNumber,
                ActivityDate = cita.ActivityDate,
                GroupName = cita.GroupName,
                TipoSoporte = tipo,
                CitaId = cita.Id
            };

            try
            {
                string docNum = (cita.DocumentNumber ?? "").Trim();
                string fecha = (cita.ActivityDate ?? "").Replace("-", "");

                if (string.IsNullOrWhiteSpace(docNum) || string.IsNullOrWhiteSpace(fecha))
                {
                    result.Observacion = "Documento o fecha vacíos";
                    return result;
                }

                // Archivos en S3: {DocNum}_{YYYYMMDD}_{Consecutivo}_{CUPS}.pdf
                // Se busca con prefijo {DocNum}_{YYYYMMDD} para traer todos los
                // archivos del paciente en esa fecha sin importar CUPS ni consecutivo
                string prefijo = $"{docNum}_{fecha}";
                List<string> keys = awsConnector.ListKeys(bucket, prefijo);

                if (keys.Count == 0)
                {
                    result.Observacion = $"Sin archivos en S3 con prefijo '{prefijo}'";
                    return result;
                }

                var archivos = new List<string>();
                foreach (string key in keys)
                {
                    byte[] bytes = awsConnector.DownloadFileAsync(key, bucket);
                    if (bytes == null || bytes.Length == 0)
                    {
                        LogError.WriteMessage("FNCDescargaSoportes", "AWS",
                            $"Descarga vacía: {key}");
                        continue;
                    }

                    // Conservar nombre original del archivo de S3 para trazabilidad
                    string originalName = Path.GetFileNameWithoutExtension(key);
                    string fileName = BuildFileName(docNum, $"{fecha}_{cita.Name}", consecutivo, $"SOP_{originalName}");
                    File.WriteAllBytes(Path.Combine(OUTPUT_DIR, fileName), bytes);
                    archivos.Add(fileName);
                    consecutivo++;
                }

                if (archivos.Count > 0)
                {
                    result.Exito = true;
                    result.Archivo = string.Join(" | ", archivos);
                }
                else
                {
                    result.Observacion = $"{keys.Count} keys encontradas pero todas las descargas fallaron";
                }
            }
            catch (Exception ex)
            {
                result.Observacion = ex.Message;
                LogError.WriteError("FNCDescargaSoportes", $"AWS-{tipo}", ex);
            }

            return result;
        }

        // ────────────────────────────────────────────────────────────────────
        // Reporte CSV
        // ────────────────────────────────────────────────────────────────────
        static void GuardarReporte(List<SupportResult> resultados)
        {
            string path = Path.Combine(OUTPUT_DIR, $"Reporte_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var sb = new StringBuilder();
            sb.AppendLine("Documento,FechaActividad,Grupo,TipoSoporte,CitaId,AssesmentId,Exito,Archivo,Observacion");
            foreach (var r in resultados)
                sb.AppendLine(string.Join(",",
                    Esc(r.DocumentNumber), Esc(r.ActivityDate), Esc(r.GroupName),
                    Esc(r.TipoSoporte), Esc(r.CitaId), Esc(r.AssesmentId),
                    r.Exito ? "SI" : "NO",
                    Esc(r.Archivo), Esc(r.Observacion)));

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            Console.WriteLine($"      Reporte: {path}");
            Console.WriteLine($"      Total: {resultados.Count} | OK: {resultados.Count(r => r.Exito)} | Error: {resultados.Count(r => !r.Exito)}");
        }

        // ────────────────────────────────────────────────────────────────────
        // Helpers
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Ejecuta SOQL con paginación automática via nextRecordsUrl</summary>
        static IEnumerable<JObject> QueryAll(string soql)
        {
            // SalesforceREST.QueryRecordAsync devuelve la primera página como string.
            // Para paginar usamos el httpClient expuesto y manejamos nextRecordsUrl manualmente.
            string url = $"{sfRest.salesforceSession.InstanceUrl}{SF_ENDPOINT}query?q={Uri.EscapeDataString(soql)}";

            while (!string.IsNullOrEmpty(url))
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sfRest.salesforceSession.AccessToken);
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                var resp = sfRest.httpClient.SendAsync(req).Result;
                string body = resp.Content.ReadAsStringAsync().Result;

                if (!resp.IsSuccessStatusCode)
                {
                    LogError.WriteMessage("FNCDescargaSoportes", "QueryAll",
                        $"HTTP {(int)resp.StatusCode}: {body.Substring(0, Math.Min(300, body.Length))}");
                    yield break;
                }

                var obj = JObject.Parse(body);
                var records = obj["records"] as JArray;
                if (records != null)
                    foreach (JObject rec in records)
                        yield return rec;

                string next = obj["nextRecordsUrl"]?.Value<string>();
                url = string.IsNullOrEmpty(next)
                    ? null
                    : $"{sfRest.salesforceSession.InstanceUrl}{next}";
            }
        }

        static string GetAssessmentFormat(string assesmentId)
        {
            string url = $"{sfRest.salesforceSession.InstanceUrl}/services/apexrest/GetAssesmentFormat";
            var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sfRest.salesforceSession.AccessToken);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(assesmentId, Encoding.UTF8, "application/json");
            var resp = sfRest.httpClient.SendAsync(req).Result;
            return resp.Content.ReadAsStringAsync().Result;
        }

        static string BuildFileName(string docNum, string fecha, int consecutivo, string tipo)
        {
            string f = (fecha ?? DateTime.Today.ToString("yyyyMMdd")).Replace("-", "");
            string doc = (docNum ?? "SINDOC").Trim();
            string safe = string.Concat((tipo ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            return $"{doc}_{f}_{consecutivo:D4}_{safe}.pdf";
        }

        static string Esc(string s) =>
            s == null ? "" : $"\"{s.Replace("\"", "\"\"")}\"";
    }

    // ── Modelos ───────────────────────────────────────────────────────────────
    class AppointmentData
    {
        public string Id { get; set; }
        public string WhatId { get; set; }
        public string DocumentNumber { get; set; }
        public string GroupName { get; set; }
        public string CostCenterId { get; set; }
        public string CostCenterCode { get; set; }
        public string ActivityDate { get; set; }
        public string Name { get; set; }
    }

    class SupportResult
    {
        public string DocumentNumber { get; set; }
        public string ActivityDate { get; set; }
        public string GroupName { get; set; }
        public string TipoSoporte { get; set; }
        public string CitaId { get; set; }
        public string AssesmentId { get; set; }  // Id de Assesment__c (solo para consultas)
        public bool Exito { get; set; }
        public string Archivo { get; set; }
        public string Observacion { get; set; }
    }

    class AssesmentData
    {
        public string AssesmentId { get; set; }  // Id de Assesment__c → va al endpoint PDF
        public string AppointmentId { get; set; }  // Id de Appointment__c → para cruzar con cita
        public string Status { get; set; }
        public string CreatedDate { get; set; }
        public string AppointmentName { get; set; }
    }

    class SalesforceServiceResponse
    {
        public bool success { get; set; }
        public string base64 { get; set; }
        public string message { get; set; }
    }

    class AWSConnector
    {
        public IAmazonS3 s3Client { get; set; }
        private readonly string _accessKey;
        private readonly string _secretKey;

        public AWSConnector(string ak, string sk) { _accessKey = ak; _secretKey = sk; }

        public void Connect() =>
            s3Client = new AmazonS3Client(_accessKey, _secretKey, RegionEndpoint.USEast1);

        public byte[] DownloadFileAsync(string key, string bucket)
        {
            try
            {
                using (var resp = s3Client.GetObject(new GetObjectRequest { BucketName = bucket, Key = key }))
                using (var ms = new MemoryStream())
                {
                    resp.ResponseStream.CopyTo(ms);
                    return ms.ToArray();
                }
            }
            catch (Exception ex) { LogError.WriteError("AWS", "Download", ex); return null; }
        }

        public List<string> ListKeys(string bucket, string prefix)
        {
            var keys = new List<string>();
            string token = null;
            do
            {
                var resp = s3Client.ListObjectsV2(new ListObjectsV2Request
                { BucketName = bucket, Prefix = prefix, ContinuationToken = token });
                keys.AddRange(resp.S3Objects.Select(o => o.Key));
                token = resp.IsTruncated == true ? resp.NextContinuationToken : null;
            } while (token != null);
            return keys;
        }
    }
}