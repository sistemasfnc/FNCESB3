using System.Collections.Generic;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace FNCInspiraServinte
{
    [ServiceContract]
    public interface IBuscadorEspaciosMiddleware
    {
        [OperationContract]
        [WebInvoke(Method = "POST",
                   RequestFormat = WebMessageFormat.Json,
                   ResponseFormat = WebMessageFormat.Json,
                   UriTemplate = "/Citas")]
        ResponseDataMiddleware BuscarEspacios(RequestDataMiddleware request);

        [OperationContract]
        [WebInvoke(Method = "POST",
                   RequestFormat = WebMessageFormat.Json,
                   ResponseFormat = WebMessageFormat.Json,
                   UriTemplate = "/Ayudas")]
        ResponseDataMiddleware BuscarAyudas(RequestDataMiddleware request);
    }

    [DataContract]
    public class RequestDataMiddleware
    {
        [DataMember(Name = "idPaciente")] public string IdPaciente { get; set; }
        [DataMember(Name = "idPlan")] public string IdPlan { get; set; }
        [DataMember(Name = "idConvenio")] public string IdConvenio { get; set; }
        [DataMember(Name = "tipoSubconsulta")] public string TipoSubconsulta { get; set; }
        [DataMember(Name = "idAgenda")] public string IdAgenda { get; set; }
        [DataMember(Name = "fechaInicio")] public string FechaInicio { get; set; }
        [DataMember(Name = "fechaFin")] public string FechaFin { get; set; }
        [DataMember(Name = "gruposProducto")] public List<string> GruposProducto { get; set; }
    }

    [DataContract]
    public class ResponseDataMiddleware
    {
        [DataMember(Name = "isSuccess")] public bool IsSuccess { get; set; }
        [DataMember(Name = "mensaje")] public string Mensaje { get; set; }
        [DataMember(Name = "listaEspacios")] public List<EspacioMiddleware> ListaEspacios { get; set; }
    }

    // Todos los campos que Salesforce retorna en wrapperCita
    [DataContract]
    public class EspacioMiddleware
    {
        [DataMember(Name = "tipodocumentoacudiente")] public string TipoDocumentoAcudiente { get; set; }
        [DataMember(Name = "tipoCita")] public string TipoCita { get; set; }
        [DataMember(Name = "tipoAutorizacion")] public string TipoAutorizacion { get; set; }
        [DataMember(Name = "requisitos")] public string Requisitos { get; set; }
        [DataMember(Name = "recomendaciones")] public string Recomendaciones { get; set; }
        [DataMember(Name = "planName")] public string PlanName { get; set; }
        [DataMember(Name = "plan")] public string Plan { get; set; }
        [DataMember(Name = "nombreacudiente")] public string NombreAcudiente { get; set; }
        [DataMember(Name = "intervalCalc")] public int? IntervalCalc { get; set; }
        [DataMember(Name = "interval")] public string Interval { get; set; }
        [DataMember(Name = "idConvenio")] public string IdConvenio { get; set; }
        [DataMember(Name = "idConfigHorario")] public string IdConfigHorario { get; set; }
        [DataMember(Name = "idCentroCosto")] public string IdCentroCosto { get; set; }
        [DataMember(Name = "idCategoria")] public string IdCategoria { get; set; }
        [DataMember(Name = "horaInicioString")] public string HoraInicioString { get; set; }
        [DataMember(Name = "horaInicio")] public string HoraInicio { get; set; }
        [DataMember(Name = "horaFinalString")] public string HoraFinalString { get; set; }
        [DataMember(Name = "horaFinal")] public string HoraFinal { get; set; }
        [DataMember(Name = "grupoPlan")] public string GrupoPlan { get; set; }
        [DataMember(Name = "grupo")] public string Grupo { get; set; }
        [DataMember(Name = "franjaHoraria")] public string FranjaHoraria { get; set; }
        [DataMember(Name = "fecha")] public string Fecha { get; set; }
        [DataMember(Name = "duracionGrupo")] public string DuracionGrupo { get; set; }
        [DataMember(Name = "documentoacudiente")] public string DocumentoAcudiente { get; set; }
        [DataMember(Name = "dia")] public string Dia { get; set; }
        [DataMember(Name = "descripcion")] public string Descripcion { get; set; }
        [DataMember(Name = "costoCita")] public decimal? CostoCita { get; set; }
        [DataMember(Name = "categoria")] public string Categoria { get; set; }
        [DataMember(Name = "agendasrelacionadas")] public string AgendasRelacionadas { get; set; }
        [DataMember(Name = "agendaID")] public string AgendaID { get; set; }
        [DataMember(Name = "agenda")] public string Agenda { get; set; }
    }
}