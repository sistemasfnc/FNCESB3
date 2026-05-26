using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel.Activation;
using System.Text;
using System.Web.Configuration;
using EventLog;
using FNCSalesforce;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FNCInspiraServinte
{
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class BuscadorEspaciosMiddleware : IBuscadorEspaciosMiddleware
    {
        private const int MIN_ESPACIOS = 3;
        private const int VENTANA_DIAS = 7;
        private const int MAX_ITERACIONES = 20;
        private const string SF_PATH_CITAS = "/services/apexrest/BuscarEspacios/Citas";
        private const string SF_PATH_AYUDAS = "/services/apexrest/BuscarEspacios/Ayudas";

        private static readonly string SfLoginUrl = WebConfigurationManager.AppSettings["SalesforceURL"];
        private static readonly string SfUser = WebConfigurationManager.AppSettings["SalesforceUser"];
        private static readonly string SfPassword = WebConfigurationManager.AppSettings["SalesforcePassword"];
        private static readonly string SfToken = WebConfigurationManager.AppSettings["SalesforceToken"];
        private static readonly string SfClientId = WebConfigurationManager.AppSettings["SalesforceClient"];
        private static readonly string SfSecret = WebConfigurationManager.AppSettings["SalesforceSecret"];
        private static readonly string SfEndpoint = WebConfigurationManager.AppSettings["SalesforceEndPoint"];

        public ResponseDataMiddleware BuscarEspacios(RequestDataMiddleware request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.IdPaciente) ||
                string.IsNullOrWhiteSpace(request.IdPlan) ||
                string.IsNullOrWhiteSpace(request.IdConvenio) ||
                string.IsNullOrWhiteSpace(request.TipoSubconsulta))
            {
                return Error("Faltan campos obligatorios: idPaciente, idPlan, idConvenio, tipoSubconsulta");
            }

            return System.Threading.Tasks.Task
                .Run(() => EjecutarConVentanas(request, SF_PATH_CITAS, "BuscadorEspacios"))
                .Result;
        }

        public ResponseDataMiddleware BuscarAyudas(RequestDataMiddleware request)
        {
            if (request == null ||
                string.IsNullOrWhiteSpace(request.IdPaciente) ||
                string.IsNullOrWhiteSpace(request.IdPlan) ||
                string.IsNullOrWhiteSpace(request.IdConvenio) ||
                request.GruposProducto == null || request.GruposProducto.Count == 0)
            {
                return Error("Faltan campos obligatorios: idPaciente, idPlan, idConvenio, gruposProducto");
            }

            return System.Threading.Tasks.Task
                .Run(() => EjecutarConVentanas(request, SF_PATH_AYUDAS, "BuscadorEspacios"))
                .Result;
        }

        private ResponseDataMiddleware EjecutarConVentanas(
            RequestDataMiddleware request, string sfPath, string logTag)
        {
            try
            {
                using (var sfApi = new SalesforceViaRestApi())
                {
                    sfApi.sLogingEndPoint = SfLoginUrl.TrimEnd('/');
                    sfApi.sApiEndpoint = SfEndpoint;
                    sfApi.DoLogin(SfUser, SfPassword + SfToken, SfClientId, SfSecret);

                    if (sfApi.salesforceSession == null ||
                        string.IsNullOrWhiteSpace(sfApi.salesforceSession.sname) ||
                        string.IsNullOrWhiteSpace(sfApi.salesforceSession.scode))
                        return Error("No se pudo autenticar contra Salesforce");

                    LogError.WriteMessage(logTag, "BuscadorEspacios", $"OK: {sfApi.salesforceSession.sname}");

                    DateTime fechaInicio = DateTime.Today;
                    if (!string.IsNullOrWhiteSpace(request.FechaInicio) &&
                        DateTime.TryParse(request.FechaInicio, out DateTime fi))
                        fechaInicio = fi;

                    DateTime fechaFinFija = DateTime.MinValue;
                    bool rangoFijo = !string.IsNullOrWhiteSpace(request.FechaFin) &&
                                     DateTime.TryParse(request.FechaFin, out fechaFinFija);

                    // ── Caso A: fechaFin fija ─────────────────────────────────
                    if (rangoFijo)
                    {
                        return CallSalesforce(sfApi, sfPath, request,
                            fechaInicio.ToString("yyyy-MM-dd"),
                            fechaFinFija.ToString("yyyy-MM-dd"), logTag)
                            ?? Error("Sin respuesta de Salesforce");
                    }

                    // ── Caso B: ventanas deslizantes ──────────────────────────
                    var acumulados = new List<EspacioMiddleware>();
                    DateTime desde = fechaInicio;
                    int iter = 0;

                    while (acumulados.Count < MIN_ESPACIOS && iter < MAX_ITERACIONES)
                    {
                        DateTime hasta = desde.AddDays(VENTANA_DIAS - 1);

                        LogError.WriteMessage(logTag, "Ventana",
                            $"Iter {iter + 1}: {desde:yyyy-MM-dd}→{hasta:yyyy-MM-dd}");

                        ResponseDataMiddleware resp = CallSalesforce(sfApi, sfPath, request,
                            desde.ToString("yyyy-MM-dd"), hasta.ToString("yyyy-MM-dd"), logTag);

                        if (resp?.ListaEspacios != null)
                            acumulados.AddRange(resp.ListaEspacios);

                        desde = hasta.AddDays(1);
                        iter++;
                    }

                    // Ordenar por fecha + horaInicio y recortar a MIN_ESPACIOS
                    var ordenados = acumulados
                        .OrderBy(e => e.Fecha)
                        .ThenBy(e => e.HoraInicio)
                        .Take(MIN_ESPACIOS)
                        .ToList();

                    return new ResponseDataMiddleware
                    {
                        IsSuccess = true,
                        Mensaje = $"{ordenados.Count} espacios en {iter} iteración(es)",
                        ListaEspacios = ordenados
                    };
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError(logTag, "BuscadorEspacios", ex);
                return Error(ex.Message);
            }
        }

        private ResponseDataMiddleware CallSalesforce(
            SalesforceViaRestApi sfApi, string sfPath,
            RequestDataMiddleware request,
            string desde, string hasta, string logTag)
        {
            try
            {
                string payload = JsonConvert.SerializeObject(new
                {
                    idPaciente = request.IdPaciente,
                    idPlan = request.IdPlan,
                    idConvenio = request.IdConvenio,
                    tipoSubconsulta = request.TipoSubconsulta ?? "",
                    idAgenda = request.IdAgenda ?? "",
                    fechaInicio = desde,
                    fechaFin = hasta,
                    gruposProducto = request.GruposProducto ?? new List<string>()
                });

                string url = $"{sfApi.salesforceSession.sname}{sfPath}";

                using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) })
                {
                    var req = new HttpRequestMessage(HttpMethod.Post, url);
                    req.Headers.Add("Authorization", "Bearer " + sfApi.salesforceSession.scode);
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                    HttpResponseMessage httpResp = http.SendAsync(req).Result;
                    string body = httpResp.Content.ReadAsStringAsync().Result;

                    if (!httpResp.IsSuccessStatusCode)
                    {
                        LogError.WriteMessage(logTag, "BuscadorEspacios",
                            $"HTTP {(int)httpResp.StatusCode} {desde}→{hasta}: {body.Substring(0, Math.Min(300, body.Length))}");
                        return null;
                    }

                    LogError.WriteMessage(logTag, "BuscadorEspacios", $"{desde}→{hasta}: {body.Length} bytes");

                    // Deserializar usando Newtonsoft para mapear todos los campos del wrapperCita
                    return JsonConvert.DeserializeObject<ResponseDataMiddleware>(body);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError(logTag, "BuscadorEspacios", ex);
                return null;
            }
        }

        private static ResponseDataMiddleware Error(string msg) =>
            new ResponseDataMiddleware
            {
                IsSuccess = false,
                Mensaje = msg,
                ListaEspacios = null
            };
    }
}