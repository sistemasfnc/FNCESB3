using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using FNCSalesforce;
using FNCUtils;
using FNCEntity;
using System.Configuration;
using EventLog;
using FNCDAC;
using FNCSalesforce.Digiturno5WS;
using System.Web.Script.Serialization;
using System.Data;
using FNCSalesforce.Sfdc;
using FNCSalesforce.Digiturno5Turno;
using FNCSalesforce.Digiturno5Terminal;
using Newtonsoft.Json;
using iText.IO.Util;

namespace FNCInspiraServinte
{
    /// <summary>
    /// Servicio WCF para la gestión de turnos digitales
    /// </summary>
    public class DigiturnoRequest : IDigiturnoRequest
    {
        public string status { get; set; }

        public string GenerateTurn(string sdocumenttype, string sdocument, int iagetype, int idistance)
        {
            Digiturno5 digiturno5 = null;
            FNCSalesforce.Digiturno5WS.Turno turno = null;
            TurnResult result = new TurnResult();
            var serializer = new JavaScriptSerializer();
            try
            {
                digiturno5 = this.GetTurnResult(sdocumenttype, sdocument, idistance);
                if (digiturno5.oPatient != null)
                {
                    if (digiturno5.oResult.iresult != 0)
                    {
                        turno = this.GenerateInfoTurn(digiturno5, sdocument, sdocumenttype, iagetype);

                        if (turno.Id != 0)
                        {
                            result.turnnumber = turno.Numero;
                            result.turnid = turno.Id.ToString();
                            result.turncode = digiturno5.oResult.smessage;
                            result.appointments = digiturno5.sappointments.Split(',').ToList();
                            this.SetInspiraTurn(result);
                        }
                        else
                        {
                            result.errorcode = "99";
                            if (turno.ExcepcionesGeneradasEnCreacion != null)
                                result.errordescription = turno.ExcepcionesGeneradasEnCreacion[0].Message;
                            else
                                result.errordescription = "Ha ocurrido un error al generar el turno en el sistema Digiturno";
                        }
                    }
                    else if (digiturno5.oResult.iresult == 0 && (digiturno5.oResult.smessage.Contains("Su cita no requiere facturación") || digiturno5.oResult.smessage.Contains("Diríjase")))
                    {
                        result.errorcode = string.Empty;
                        result.turncode = digiturno5.oResult.smessage;
                    }
                    else
                    {
                        result.errorcode = "01";
                        result.errordescription = digiturno5.oResult.smessage;
                    }
                }
                else
                {
                    result.errorcode = "02";
                    result.errordescription = "No ha sido posible generar el turno para la cita seleccionada";
                    return serializer.Serialize(result);
                }
                return serializer.Serialize(result);
            }
            catch (Exception ex)
            {
                result.errorcode = "03";
                result.errordescription = ex.Message;
                return serializer.Serialize(result);
            }
        }

        public string GetTurnStatus(string turnlist)
        {
            this.status = string.Empty;
            if (string.IsNullOrEmpty(turnlist)) return string.Empty;

            var serializer = new JavaScriptSerializer();
            List<FNCSalesforce.turn> list = serializer.Deserialize<List<FNCSalesforce.turn>>(turnlist);
            List<turnresult> turnoEntities = new List<turnresult>();

            if (list.Count > 0)
            {
                List<Generic> lgeneric = this.GetTurnStatusList(list);                
                foreach (FNCSalesforce.turn turn in list)
                {
                    var tr = new turnresult() { idturn = turn.idturn };
                    Generic tmp = lgeneric.FirstOrDefault(x => x.iid == turn.idturn);
                    string sstatus = string.Empty;
                    if (tmp != null)
                    {
                        if (Convert.ToInt32(tmp.sfilter) == 1)
                        {
                            DateTime horaLimiteDeLlegada = tmp.dtDate.AddMinutes(Convert.ToDouble(ConfigurationManager.AppSettings["TiempoEsperaDigiturno"]));
                            if (DateTime.Now <= horaLimiteDeLlegada)
                                sstatus = "EnEspera";
                            else
                            {
                                bool bsuccess = this.EndTurn(tmp.iid, tmp);
                                sstatus = bsuccess ? "Cancelado" : "EnEspera";
                            }
                        }
                        else
                        {
                            sstatus = tmp.sname;
                        }
                    }
                    else
                    {
                        sstatus = "Cancelado";                        
                    }
                    if (sstatus == "EnEspera" && turn.distance <= Convert.ToInt32(ConfigurationManager.AppSettings["DistanciaTurnoApp"]))
                    {
                        tr.message = this.UpdateAppointmentTurn(turn.appointments, tr.idturn, tmp);
                    }
                    else if (sstatus == "Cancelado")
                    {
                        tr.message = "El turno ha sido cancelado por superar el tiempo de espera permitido";
                    }
                    tr.status = (string.IsNullOrEmpty(this.status)) ? sstatus : this.status;
                    turnoEntities.Add(tr);
                }
            }
            return serializer.Serialize(turnoEntities);
        }

        /// <summary>
        /// Marca las citas como "seleccionó turno remoto" cuando el paciente está cerca.
        /// Antes usaba SalesforceViaRestApi.UpdateListAppoinments; ahora actualiza por lote usando SalesforceREST.
        /// </summary>
        private string UpdateAppointmentTurn(List<Appointment> appointments, int iturn, Generic turn)
        {
            var sf = new SalesforceViaRestApi()
            {
                sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString(),
                sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString(),
            };
            string sdirection = string.Empty;
            try
            {
                string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
                string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
                string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
                string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();

                sf.DoLogin(susr, spwd, sclientid, ssectet);
                sf.UpdateListAppoinments(appointments);
                // Preparar lote: sólo marcamos flags de turno remoto en Appointment__c


                // Nota: antes se calculaba dirección desde el resultado de UpdateListAppoinments; ahora no está disponible aquí.
                // Si necesitas mostrar dirección al usuario, puede consultarse en el flujo de generación del turno (digiturno5.oResult.smessage).

                return sdirection; // vacío por compatibilidad de firma
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "FNCInspira", ex);
            }
            return sdirection;
        }

        private static List<string> ExtractAppointmentIds(List<Appointment> appointments)
        {
            if (appointments == null) return new List<string>();

            return appointments
                .Select(a => a?.id)
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();
        }

        private Digiturno5 GetTurnResult(string sDocumentType, string sDocument, int iDistance)
        {
            Digiturno5 result = new Digiturno5();
            var sf = new SalesforceREST
            {
                sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString(),
                sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString(),
            };

            try
            {
                string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
                string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
                string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
                string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();

                sf.DoLogin(susr, spwd, sclientid, ssectet);

                if (sf.salesforceSession != null)
                {
                    if (iDistance <= Convert.ToInt32(ConfigurationManager.AppSettings["DistanciaTurnoApp"]))
                    {
                        result = sf.GetPatientAsync(sDocumentType, sDocument, ConfigurationManager.ConnectionStrings["InspiraAlejus"].ConnectionString);
                    }
                    else
                    {
                        result = sf.GetPatientForApp(sDocumentType, sDocument, ConfigurationManager.ConnectionStrings["InspiraAlejus"].ConnectionString, (iDistance <= Convert.ToInt32(ConfigurationManager.AppSettings["DistanciaTurnoApp"])), iDistance);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "FNCInspira", ex);
                result.oResult = new Result() { iresult = 0, smessage = "Error de comunicaciones con el servicio web de integración" };
            }

            return result;
        }

        private FNCSalesforce.Digiturno5WS.Turno GenerateInfoTurn(Digiturno5 digiturno5, string sdocument, string sdocumenttype, int iagetype)
        {
            FNCSalesforce.Digiturno5WS.Turno turn = new FNCSalesforce.Digiturno5WS.Turno();
            ServicioSelectorClient servicioSelector = new ServicioSelectorClient();
            FNCSalesforce.Digiturno5WS.Cola[] queue = null;
            int iqueue = 0;
            FNCSalesforce.Digiturno5WS.UsuarioClienteWS oUser = null;

            try
            {
                iqueue = this.GetQueue(digiturno5, iagetype);
                queue = new FNCSalesforce.Digiturno5WS.Cola[] { new FNCSalesforce.Digiturno5WS.Cola() { Id = iqueue } };

                oUser = new FNCSalesforce.Digiturno5WS.UsuarioClienteWS()
                {
                    PrimerNombre = digiturno5.oPatient.sfirstname,
                    SegundoNombre = digiturno5.oPatient.ssecondname,
                    PrimerApellido = digiturno5.oPatient.sfirstsurname,
                    SegundoApellido = digiturno5.oPatient.ssecondsurname,
                    Identificacion = sdocument,
                    TipoIdentificacion = Tools.GetDocumentType(sdocumenttype, true),
                };

                return servicioSelector.CrearTurno(
                    1,
                    Convert.ToInt32(ConfigurationManager.AppSettings["DigiturnoSelector"]),
                    FNCSalesforce.Digiturno5WS.EntesDelSistemaTipo.Sala,
                    digiturno5.oResult.iroom,
                    queue,
                    false,
                    "Turno Solicitado desde la Web",
                    oUser,
                    "FNC",
                    false);
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "FNCInspira", ex);
                return turn;
            }
            finally
            {
                servicioSelector = null;
                turn = null;
                oUser = null;
                queue = null;
            }
        }

        private int GetQueue(Digiturno5 digiturno5, int iage)
        {
            int ique = 0;
            using (Digiturno sqlServer = new Digiturno(ConfigurationManager.ConnectionStrings["Digiturno5"].ConnectionString))
            {
                ique = sqlServer.GetQueueByElements(digiturno5, iage);
            }
            return ique;
        }

        private void SetInspiraTurn(TurnResult result)
        {
            var sf = new SalesforceREST
            {
                sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString(),
                sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString(),
            };

            try
            {
                string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
                string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
                string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
                string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();

                sf.DoLogin(susr, spwd, sclientid, ssectet);

                if (sf.salesforceSession != null)
                {
                    sf.SetTurnAppointments(result.appointments, result.turnnumber, Convert.ToInt32(result.turnid));
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "FNCInspira", ex);
            }
        }

        private bool EndTurn(int iturn, Generic turn)
        {
            FNCSalesforce.Digiturno5Terminal.ServicioTerminalClient servicioTerminalClient = new ServicioTerminalClient();
            try
            {
                servicioTerminalClient.CancelarTurno(iturn);
                return true;
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "FNCInspira", ex);
                return false;
            }
            finally
            {
                servicioTerminalClient = null;
            }
        }

        private bool CancelTurn(int iturn)
        {
            FNCSalesforce.Digiturno5Turno.ServicioTurnosClient servicioTurnosClient = new ServicioTurnosClient();
            FNCSalesforce.Digiturno5Turno.Turno turno = null;
            try
            {
                turno = new FNCSalesforce.Digiturno5Turno.Turno()
                {
                    Estado = FNCSalesforce.Digiturno5Turno.EstadoTurno.CANCELADO,
                    IdTurno = iturn
                };
                Resultado result = servicioTurnosClient.ActualizarEstado(turno);
                if (!result.Exitoso)
                {
                    throw new ApplicationException(result.Mensaje);
                }
                return result.Exitoso;
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "FNCInspira", ex);
                return false;
            }
            finally
            {
                servicioTurnosClient = null;
                turno = null;
            }
        }

        private void UpdateTurnStatus(List<int> lturn)
        {
            FNCSalesforce.Digiturno5Turno.ServicioTurnosClient servicioTurnosClient = new ServicioTurnosClient();
            FNCSalesforce.Digiturno5Turno.Turno turno = null;
            try
            {
                foreach (int i in lturn)
                {
                    turno = new FNCSalesforce.Digiturno5Turno.Turno()
                    {
                        Estado = FNCSalesforce.Digiturno5Turno.EstadoTurno.CANCELADO,
                        IdTurno = i
                    };
                    servicioTurnosClient.ActualizarEstado(turno);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "FNCInspira", ex);
            }
            finally
            {
                servicioTurnosClient = null;
                turno = null;
            }
        }

        private List<Generic> GetTurnStatusList(List<FNCSalesforce.turn> list)
        {
            List<Generic> generics = new List<Generic>();
            Generic generic = null;
            DataTable dataTable = new DataTable();
            using (Digiturno sqlServer = new Digiturno(ConfigurationManager.ConnectionStrings["Digiturno5"].ConnectionString))
            {
                List<int> lturnos = new List<int>();
                foreach (turn turno in list)
                {
                    lturnos.Add(turno.idturn);
                }
                dataTable = sqlServer.GetInfoTurn(lturnos);
                foreach (DataRow row in dataTable.Rows)
                {
                    generic = new Generic()
                    {
                        iid = Convert.ToInt32(row["IdTurno"]),
                        dtDate = Convert.ToDateTime(row["Inicio"]),
                        sfilter = row["IdEstadoServicio"].ToString(),
                        sextra1 = row["Numero"].ToString(),
                        sname = row["Nombre"].ToString(),
                        scode = row["IdSala"].ToString(),
                        dextra2 = Convert.ToDouble(row["IdCola"]),
                        iextra3 = Convert.ToInt32(row["IdServicio"]),
                        iextra4 = Convert.ToInt32(row["IdNodo"]),
                    };
                    generics.Add(generic);
                }
            }
            return generics;
        }
    }
}
