using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using EventLog;
using FNCEntity;
using FNCUtils;
using System.Data.SqlTypes;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.CodeDom;
using System.Security.Cryptography;
using static System.Net.Mime.MediaTypeNames;
using System.Threading;

namespace FNCDAC
{
    public class ServinteOracle : IDisposable
    {
        #region Propiedades de la clase

        /// <summary>
        /// String cadena de conexión a la base de datos
        /// </summary>
        public string sconnection { get; set; }

        /// <summary>
        /// Lista genérica de respuesta de ingresos
        /// </summary>
        private List<EntryResponse> lresponse { get; set; }

        /// <summary>
        /// Objeto con los parámetros de admisión
        /// </summary>
        private ParametroRip parametroRip { get; set; }

        /// <summary>
        /// Número de ingreso fuente
        /// </summary>
        private int ientry { get; set; }

        /// <summary>
        /// String tipo de transacción
        /// </summary>
        public string stype { get; set; }


        /// <summary>
        /// Lista genérica de nits y conceptos
        /// </summary>
        private List<Generic> lconnit { get; set; }

        /// <summary>
        /// Lista genérica que almacena las tarifas y los productos
        /// </summary>
        private List<TarifaProducto> lTarifaProducto { get; set; }

        private List<Generic> ldoctors { get; set; }

        #endregion

        /// <summary>
        /// Constructor de la clase
        /// </summary>
        /// <param name="sconnectionstring">String cadena de conexión a asignar</param>
        public ServinteOracle(string sconnectionstring)
        {
            this.sconnection = sconnectionstring;
            this.parametroRip = new ParametroRip();
            this.lconnit = new List<Generic>();
            this.ldoctors = new List<Generic>();
            this.GetRipsParameters();
            this.GetConceptThirds();
            this.GetAllDoctors();
        }

        /// <summary>
        /// Constructor de la clase sin parámetros
        /// </summary>
        public ServinteOracle()
        {
            this.lconnit = new List<Generic>();
        }

        #region Integracion Inspira Servinte en tiempo real

        #region Métodos públicos

        public void UpdateAyorddet(int ientry, string sappointment)
        {
            string query = "UPDATE AYORDDET SET ORDDETDE2 = :ORDDETDE2 WHERE ORDDETDOC = :ORDDETDOC AND ORDDETFUE = :ORDDETFUE";
            List<OracleParameter> parameters = new List<OracleParameter>();
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                parameters.Add(new OracleParameter("ORDDETDE2", sappointment));
                parameters.Add(new OracleParameter("ORDDETDOC", ientry));
                parameters.Add(new OracleParameter("ORDDETFUE", "03"));
                oracle.ExecuteNonQuery(query, parameters);
                parameters = null;
            }
        }

        /// <summary>
        /// Método para obtener la combinación correcta de tipo de usuario, teniendo en cuenta la empresa, el plan y el nivel del usuario, tabla INPAG
        /// </summary>
        /// <returns>DataTable con los campos seleccionados</returns>
        public DataTable GetUserTypes()
        {
            string squery = "SELECT * FROM VTIPOSDEUSUARIO";
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(squery, null);
            }
        }

        /// <summary>
        /// Método que obtiene un conjunto de datos con las autorizaciones usadas por paciente en los cargos
        /// </summary>
        /// <returns>DataTable con las autorizaciones de los pacientes</returns>
        public DataTable GetAuthorizationsDetail()
        {
            StringBuilder squery = new StringBuilder("SELECT PACTID, PACIDE, CARDETCOD, MOVOTRPLA, ORDDETDOC FROM SERVINTE.AYORDDET INNER JOIN SERVINTE.AYMOV ON MOVDOC = ORDDETDOC AND MOVFUE = ORDDETFUE");
            squery.Append(" INNER JOIN SERVINTE.AYCARDET ON MOVDOC = CARDETDOC AND MOVFUE = CARDETFUE");
            squery.Append(" INNER JOIN SERVINTE.ABPAC ON PACHIS = MOVHIS");
            squery.Append(" INNER JOIN SERVINTE.AYMOVOTR ON MOVOTRDOC = MOVDOC AND MOVOTRFUE = MOVFUE");
            squery.Append(" WHERE ORDDETFUE = '03' AND MOVANU = 0");
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(squery.ToString(), null);
            }
        }

        /// <summary>
        /// Método para crear ingresos y cargos masivos en la tabla AYMOV
        /// </summary>
        /// <param name="linspiraRequests">Lista genérica objeto integración</param>
        /// <returns></returns>
        public List<EntryResponse> GenerateEntries(List<InspiraRequest> linspiraRequests)
        {
            this.lresponse = new List<EntryResponse>();
            //int ipatient = 0;
            Oracle oracle = null;
            try
            {
                /*oracle = new Oracle();
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                foreach (InspiraRequest inspiraRequest in linspiraRequests)
                {
                    if (inspiraRequest.ientry == 0)
                    {
                        ipatient = this.PatientExists(inspiraRequest.opatient, oracle);
                        if (ipatient == 0)
                        {
                            inspiraRequest.opatient.iid = this.CreatePatient(inspiraRequest.opatient, oracle);
                        }
                        else
                        {
                            inspiraRequest.opatient.iid = ipatient;
                            this.UpdatePatient(inspiraRequest.opatient, oracle);
                        }
                        inspiraRequest.ientry = this.CreateEntry(inspiraRequest, oracle);
                    }
                    this.CreateCharge(inspiraRequest, oracle);
                }                
                oracle.Commit();*/
                return this.lresponse;
            }
            catch (Exception ex)
            {
                if (oracle.oracleTransaction != null)
                {
                    if (oracle.oracleConnection.State == ConnectionState.Open)
                    {
                        oracle.RollBack();
                    }
                }
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                oracle.Dispose();
                oracle = null;
            }
        }

        /// <summary>
        /// Método para crear los ingresos masivos de educación
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira Servinte</param>
        /// <returns>Lista genérica objeto respuesta integración</returns>
        public List<EntryResponse> GenerateEntryForEducation(InspiraRequest inspiraRequest)
        {
            this.stype = inspiraRequest.stype;
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            Oracle oracle = null;
            List<string> lpatients = new List<string>();
            try
            {
                oracle = new Oracle();
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                this.CreateUserLog(oracle);
                foreach (ServintePatient servintePatient in inspiraRequest.lpatients)
                {
                    ipatient = this.PatientExists(servintePatient, oracle);
                    if (lpatients.Count == 0)
                    {
                        if (ipatient == 0)
                        {
                            ipatient = this.CreatePatient(servintePatient, oracle);
                            this.CreatePatientExtraInformation(servintePatient, oracle);
                        }
                        else
                        {
                            this.UpdatePatient(servintePatient, oracle);
                            this.UpdatePatientExtraInformation(servintePatient, oracle);
                        }
                        lpatients.Add(servintePatient.idPaciente);

                    }
                    else if (lpatients.FindIndex(x => x == servintePatient.idPaciente) == 0 && lpatients.Count > 0)
                    {
                        if (ipatient == 0)
                        {
                            ipatient = this.CreatePatient(servintePatient, oracle);
                            this.CreatePatientExtraInformation(servintePatient, oracle);
                        }
                        else
                        {
                            this.UpdatePatient(servintePatient, oracle);
                            this.UpdatePatientExtraInformation(servintePatient, oracle);
                        }
                        lpatients.Add(servintePatient.idPaciente);
                    }
                    servintePatient.iid = ipatient;
                    foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                    {
                        this.GetEntrySequence(oracle, this.parametroRip.scia);
                        this.UpdateEntrySequence(oracle);
                        this.CreateEntry(inspiraCita, servintePatient, oracle);
                        this.CreateEntryAdditional(inspiraCita, servintePatient, oracle);
                        this.CreateEntryLog(inspiraCita, oracle);
                        this.CreateEgress(inspiraCita, oracle);
                        this.CreateEntryVars(inspiraCita, servintePatient, oracle);
                        this.CreateAuthorization(inspiraCita, oracle, false);
                        this.CreateAuthorizationDetail(inspiraCita, oracle);
                        this.CreateEntryAuditLog(inspiraCita, oracle);
                        this.CreateAttentionRip(inspiraCita, servintePatient, oracle);
                        this.CreateAttentionIde(oracle, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                        this.CreateCharge(inspiraCita, oracle);
                        this.CreateChargeDetail(inspiraCita, servintePatient, inspiraRequest.sid, oracle);
                        this.IncreaseAttentionSequence(oracle, this.parametroRip.scia);
                        this.GetRipsParameters(oracle);
                    }

                }
                oracle.Commit();
                return lresponse;
            }
            catch (Exception ex)
            {
                if (oracle.oracleTransaction != null)
                {
                    if (oracle.oracleConnection.State == ConnectionState.Open)
                    {
                        oracle.RollBack();
                    }
                }
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                oracle.Dispose();
                oracle = null;
                lpatients = null;
            }
        }

        private static readonly object EntryLock = new object();

        private static readonly SemaphoreSlim SequenceSemaphore = new SemaphoreSlim(1, 1);
        private const int MaxRetryAttempts = 5;
        private const int BaseRetryDelayMs = 100;

        /// <summary>
        /// Método para crear el cargo cuando el envío es de tipo Servicio.
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <returns></returns>


        public List<EntryResponse> GenerateEntryForService(InspiraRequest inspiraRequest)
        {
            this.stype = inspiraRequest.stype;
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            InspiraCita inspiraCita = null;
            EntryResponse entryResponse = null;
            int j = 0;
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                try
                {
                    // Obtiene las tarifas vigentes por concepto y producto
                    this.GetRatesByConceptByProduct(oDAC);
                    // Recorre todos los pacientes del request
                    foreach (ServintePatient servintePatient in inspiraRequest.lpatients)
                    {
                        servintePatient.sgender = Tools.GetGender(servintePatient.sgender);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        // Crea o actualiza el paciente en la BD
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        // Si el paciente tiene citas y no es un familiar, continúa el proceso
                        if (servintePatient.lappointments != null && !servintePatient.bisparent)
                        {
                            // Se verifica que la autorización no se haya usado previamente
                            if (!this.EntryExists(inspiraRequest, oDAC, servintePatient.safiliation) && servintePatient.lappointments.Count > 0)
                            {
                                this.CreateUserLog(oDAC);
                                inspiraCita = servintePatient.lappointments[0];
                                // Sección crítica protegida por lock para evitar conflictos de secuencia
                                const int maxRetries = 5;
                                int attempt = 0;
                                bool entryCreated = false;
                                while (attempt < maxRetries && !entryCreated)
                                {
                                    try
                                    {
                                        // Paso 1 y 2: obtener y actualizar secuencia (sin lock)
                                        this.GetEntrySequence(oDAC, this.parametroRip.scia);
                                        this.UpdateEntrySequence(oDAC);
                                        // Paso 3: intentar crear ingreso
                                        this.CreateEntry(inspiraCita, servintePatient, oDAC);
                                        entryCreated = true; // éxito, salimos del bucle
                                    }
                                    catch (Exception ex)
                                    {
                                        LogError.WriteMessage("FNCInspira", "DAC", $"Intento {attempt + 1}: {ex.GetType()} - {ex.Message}");
                                        if (ex.Message.Contains("ORA-00001"))
                                        {
                                            attempt++;
                                            Thread.Sleep(100);
                                        }
                                        else
                                        {
                                            throw;
                                        }
                                    }
                                }
                                if (!entryCreated)
                                {
                                    throw new ApplicationException("No fue posible generar el ingreso tras varios intentos debido a colisión de secuencias.");
                                }
                                // Si la cita viene asociada a otro ingreso, se obtiene ese ingreso origen
                                if (inspiraRequest.bentryassociate)
                                {
                                    this.ientry = this.GetAssociatedEntry(inspiraCita, oDAC);
                                }
                                // Inserta registros relacionados al ingreso en múltiples tablas
                                this.CreateEntryAdditional(inspiraCita, servintePatient, oDAC);
                                this.CreateEntryLog(inspiraCita, oDAC);
                                this.CreateEgress(inspiraCita, oDAC);
                                this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                                this.CreateAuthorization(inspiraCita, oDAC, false);
                                this.CreateAuthorizationDetail(inspiraCita, oDAC);
                                this.CreateEntryAuditLog(inspiraCita, oDAC);
                                attempt = 0;
                                entryCreated = false;
                                while (attempt < maxRetries && !entryCreated)
                                {
                                    try
                                    {
                                        // Leer nueva secuencia
                                        this.GetRipsParameters(oDAC); // Esto actualiza this.parametroRip.isequence
                                        this.CreateAttentionRip(inspiraCita, servintePatient, oDAC); // Usa isequence
                                        this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia); // Aumenta secuencia solo si insert fue exitoso
                                        entryCreated = true;
                                    }
                                    catch (OracleException ex)
                                    {
                                        if (ex.Number == 1) // ORA-00001: violación de llave primaria
                                        {
                                            attempt++;
                                            Thread.Sleep(100); // Esperar antes de reintentar
                                        }
                                        else
                                        {
                                            LogError.WriteError("FNCInspira", "DAC", ex);
                                            throw;
                                        }
                                    }
                                }
                                if (!entryCreated)
                                {
                                    throw new ApplicationException("No fue posible generar la atención en MSATE tras varios intentos por colisión de secuencias.");
                                }
                                this.CreateAttentionIde(
                                    oDAC,
                                    servintePatient.safiliation,
                                    Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                                // Crea el cargo principal
                                this.CreateCharge(inspiraCita, oDAC);
                                // Crea el detalle de cargos por cada servicio
                                foreach (ServiceRequest service in inspiraCita.lservices)
                                {
                                    if (service.scostcenter.EqualsAnyOf("3001", "3002", "3003", "3015", "3102", "3201", "3202", "3203", "3204") && !service.sservicename.Contains("CONSULTA"))
                                    {
                                        // Crea respuesta de ingreso para servicios seleccionados
                                        entryResponse = new EntryResponse()
                                        {
                                            idPaciente = servintePatient.idPaciente,
                                            idCargo = service.idCargo,
                                            ientry = this.parametroRip.ientry,
                                            ientrysource = (this.ientry == 0) ? this.parametroRip.ientry : this.ientry,
                                            sid = inspiraCita.sappointment,
                                            sdocument = servintePatient.sdocument,
                                            sdocumenttype = servintePatient.sdocumenttype,
                                            ddate = inspiraCita.ddate.ToString("yyyy-MM-dd"),
                                            scostcenter = service.scostcenter,
                                            sservice = service.sservice,
                                            splan = inspiraCita.splan,
                                            srate = inspiraCita.srate,
                                            ipatient = servintePatient.iid,
                                            iqty = service.iqty,
                                            icharge = 0,
                                            sconcept = service.sconcept,
                                            sauthorization = service.sauthorization,
                                            stype = this.stype,
                                            sservicename = service.sservicename,
                                            dvalue = Convert.ToInt32(service.iqty * this.GetProductValue(service)),
                                            iline = (j + 1),
                                            sunit = inspiraCita.sunit,
                                            sagreement = inspiraCita.sagreement,
                                            stemplate = inspiraCita.stemplate,
                                            sappointment = inspiraCita.sappointment,
                                            sservicegroup = inspiraCita.sservicegroup,
                                            sevent = inspiraCita.sappointment,
                                            sthird = inspiraCita.sthird,
                                        };
                                        this.lresponse.Add(entryResponse);
                                    }
                                    else
                                    {
                                        // Si el servicio no califica, se crea directamente el detalle del cargo
                                        this.CreateSingleChargeDetail(inspiraCita, service, servintePatient, inspiraRequest.sid, j, oDAC);
                                        j++;
                                    }
                                }
                                // Incrementa el consecutivo de atención y finaliza transacción
                                this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                                oDAC.Commit();
                            }
                            else
                            {
                                throw new ApplicationException("La autorización enviada ya ha sido utilizada anteriormente");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null && oDAC.oracleConnection.State == ConnectionState.Open)
                    {
                        oDAC.RollBack();
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
            }
            return lresponse;
        }



        /// <summary>
        /// Método para crear de manera masiva los cargos de investigación en Servinte
        /// </summary>
        /// <param name="inspiraRequest">Objeto integración Inspira Servinte</param>
        /// <returns>Lista genérica objeto respuesta integración</returns>
        public List<EntryResponse> GenerateEntryForInvestigation(InspiraRequest inspiraRequest)
        {
            this.stype = inspiraRequest.stype;
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            Oracle oracle = null;
            List<string> lpatients = new List<string>();
            try
            {
                oracle = new Oracle();
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                this.CreateUserLog(oracle);
                foreach (ServintePatient servintePatient in inspiraRequest.lpatients)
                {
                    ipatient = this.PatientExists(servintePatient, oracle);
                    if (lpatients.FindIndex(x => x == servintePatient.idPaciente) == 0)
                    {
                        if (ipatient == 0)
                        {
                            ipatient = this.CreatePatient(servintePatient, oracle);
                            this.CreatePatientExtraInformation(servintePatient, oracle);
                        }
                        else
                        {
                            this.UpdatePatient(servintePatient, oracle);
                            this.UpdatePatientExtraInformation(servintePatient, oracle);
                        }
                        lpatients.Add(servintePatient.idPaciente);
                    }
                    servintePatient.iid = ipatient;
                    foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                    {
                        this.GetEntrySequence(oracle, this.parametroRip.scia);
                        this.UpdateEntrySequence(oracle);
                        this.CreateEntry(inspiraCita, servintePatient, oracle);
                        this.CreateEntryAdditional(inspiraCita, servintePatient, oracle);
                        this.CreateEntryLog(inspiraCita, oracle);
                        this.CreateEgress(inspiraCita, oracle);
                        this.CreateEntryVars(inspiraCita, servintePatient, oracle);
                        //this.CreateAuthorization(inspiraCita, oracle, false);
                        //this.CreateAuthorizationDetail(inspiraCita, oracle);
                        this.CreateEntryAuditLog(inspiraCita, oracle);
                        this.CreateAttentionRip(inspiraCita, servintePatient, oracle);
                        this.CreateAttentionIde(oracle, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                        this.CreateCharge(inspiraCita, oracle);
                        this.CreateChargeDetail(inspiraCita, servintePatient, inspiraRequest.sid, oracle);
                        this.IncreaseAttentionSequence(oracle, this.parametroRip.scia);
                        this.GetRipsParameters(oracle);
                    }

                }
                oracle.Commit();
                return lresponse;
            }
            catch (Exception ex)
            {
                if (oracle.oracleTransaction != null)
                {
                    if (oracle.oracleConnection.State == ConnectionState.Open)
                    {
                        oracle.RollBack();
                    }
                }
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                oracle.Dispose();
                oracle = null;
                lpatients = null;
            }
        }

        /// <summary>
        /// Método para actualizar los cargos de un ingreso, se debe verificar que el ingreso no esté cerrado y que los cargos no estén facturados
        /// </summary>
        /// <param name="inspiraCita">Objeto Inspira Cita</param>
        /// <param name="servintePatient">Objeto Inspira Paciente</param>
        /// <param name="sidAppointment">String id de la cita</param>
        /// <returns>Verdadero si se pudo hacer la actualización, falso en caso contrario</returns>
        public List<EntryResponse> UpdateEntry(InspiraCita inspiraCita, ServintePatient servintePatient, string sidAppointment)
        {
            Oracle oracle = null;
            int iline = 0;
            try
            {
                this.lresponse = new List<EntryResponse>();
                oracle = new Oracle();
                oracle.sConnection = this.sconnection;
                oracle.Connect();                
                if (!this.EntryIsClosed(inspiraCita, oracle))
                {
                    oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                    this.UpdateCharges(inspiraCita, oracle);
                    iline = this.GetLastLine(inspiraCita, oracle) + 1;
                    foreach (ServiceRequest item in inspiraCita.lservices)
                    {
                        if (item.sconcept != "7000" && !item.scostcenter.StartsWith("30") && !item.scostcenter.StartsWith("32"))
                        {
                            this.CreateSingleChargeDetail(inspiraCita, item, servintePatient, sidAppointment, iline, oracle);
                            iline++;
                        }
                        this.CreateChargeDetail(inspiraCita, item, oracle, iline, servintePatient, true);
                        
                    }
                    oracle.Commit();
                }
                return this.lresponse;
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                if (oracle.oracleTransaction != null)
                {
                    if (oracle.oracleConnection.State == ConnectionState.Open)
                    {
                        oracle.RollBack();
                    }
                }
                throw;
            }
            finally
            { 
                oracle.Dispose();
                oracle = null;
            }
        }

        #endregion

        #region Métodos privados      

        /// <summary>
        /// Método que obtiene el ingreso asociado a una cita  para hacer cargo asociado
        /// </summary>
        /// <param name="sappointment">String id cita</param>
        /// <param name="ssource">String fuente ingreso</param>
        /// <param name="oracle">Objeto conexión a base de datos</param>
        /// <returns>Entero con el número de ingreso</returns>
        private int GetAssociatedEntry(InspiraCita inspiraCita, Oracle oracle)
        {
            string squery = "SELECT ORDDETDOC FROM AYORDDET INNER JOIN AYMOV ON MOVFUE = ORDDETFUE AND ORDDETDOC = MOVDOC WHERE ORDDETORD = :ORDDETDE2 AND ORDDETFUE = :ORDDETFUE AND MOVEST = 'A' AND ORDDETCER = :MOVCER ORDER BY ORDDETDOC FETCH FIRST 1 ROW ONLY";
            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter("ORDDETDE2", inspiraCita.sauthorization));
            parameters.Add(new OracleParameter("ORDDETFUE", this.parametroRip.ssource));
            parameters.Add(new OracleParameter("MOVCER", inspiraCita.sagreement));
            object oid = null;
            oid = oracle.GetScalar(squery, parameters, true);
            return (oid != DBNull.Value) ? Convert.ToInt32(oid) : 0;
        }

        /// <summary>
        /// Método para obtener la última línea del detalle de los cargos
        /// </summary>
        /// <param name="inspiraCita">Objeto Inspira Cita</param>
        /// <param name="oracle">Objeto conexión a la base de datos</param>
        /// <returns>Entero número de la última línea</returns>
        private int GetLastLine(InspiraCita inspiraCita, Oracle oracle)
        {
            string squery = "SELECT MAX(CARDETLIN) FROM AYCARDET WHERE CARDETDOC = :MOVDOC AND CARDETFUE = :MOVFUE";
            object oline = 0;
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            oracleParameters.Add(new OracleParameter("MOVDOC", inspiraCita.ientry));
            oracleParameters.Add(new OracleParameter("MOVFUE", inspiraCita.ientrysource));
            oline = oracle.GetScalar(squery, oracleParameters, true);
            return (oline != DBNull.Value) ? Convert.ToInt32(oline) : 0;
        }

        /// <summary>
        /// Método que inactiva los cargos de un ingreso
        /// </summary>
        /// <param name="inspiraCita">Objeto Cita Inspira</param>
        /// <param name="oracle">Objeto conexión a la base de datos</param>
        private void UpdateCharges(InspiraCita inspiraCita, Oracle oracle)
        {
            StringBuilder sb = new StringBuilder("MERGE INTO AYCARDET AC1");
            sb.Append(" USING (SELECT AC2.CARDETREG, AC2.CARDETLIN FROM AYCARDET AC2 LEFT JOIN AYCARFAC ON CARFACREG = AC2.CARDETLIN");
            sb.Append(" AND AC2.CARDETLIN = CARFACLCA WHERE AC2.CARDETDOC = :MOVDOC AND AC2.CARDETFUE = :MOVFUE AND CARFACDOC IS NULL) SRC");
            sb.Append(" ON (SRC.CARDETREG = AC1.CARDETREG AND AC1.CARDETLIN = SRC.CARDETLIN) WHEN MATCHED THEN UPDATE SET AC1.CARDETANU = 1");
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            oracleParameters.Add(new OracleParameter("MOVDOC", inspiraCita.ientry));
            oracleParameters.Add(new OracleParameter("MOVFUE", inspiraCita.ientrysource));
            oracle.ExecuteNonQuery(sb.ToString(), oracleParameters, false, true);
        }

        /// <summary>
        /// Método para validar si la autorización fue utilizada y que no permita usar otra igual
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="oracle">Objeto conexión a la base de datos</param>
        /// <param name="safiliation">String tipo de afiliación del paciente</param>
        /// <returns>Boolean verdadero si la autorización ya fue creada falso si no</returns>
        private bool EntryExists(InspiraRequest inspiraRequest, Oracle oracle, string safiliation)
        {
            if (safiliation == "P") return false;
            if (inspiraRequest.lpatients[0].lappointments.Count > 0)
            {
                if (inspiraRequest.lpatients[0].lappointments[0].lservices.Count > 0)
                {
                    string[] concepts = new string[inspiraRequest.lpatients[0].lappointments[0].lservices.Count];
                    string[] services = new string[inspiraRequest.lpatients[0].lappointments[0].lservices.Count];
                    string[] costcenters = new string[inspiraRequest.lpatients[0].lappointments[0].lservices.Count];
                    int i = 0;
                    StringBuilder stringBuilder = new StringBuilder(" SELECT COUNT(1) FROM SERVINTE.AYMOVORD INNER JOIN SERVINTE.AYMOVOTR ON MOVORDDOC = MOVOTRDOC AND MOVORDFUE = MOVOTRFUE");
                    stringBuilder.Append(" INNER JOIN SERVINTE.AYMOV ON MOVORDDOC = MOVDOC AND MOVORDFUE = MOVFUE");
                    stringBuilder.Append(" INNER JOIN SERVINTE.AYCARDET ON CARDETDOC = MOVDOC AND CARDETFUE = MOVFUE");
                    stringBuilder.Append(" INNER JOIN SERVINTE.ABPAC ON PACHIS = MOVHIS");
                    stringBuilder.Append(" WHERE MOVORDORD = :MOVORDORD AND MOVORDFUE = :MOVFUE AND MOVOTRPLA = :MOVOTRPLA AND PACIDE = :PACIDE AND PACTID = :PACTID");
                    stringBuilder.Append(" AND CARDETCOD NOT IN('933501', '939403', '990111', '991202', '991203', '0') ");
                    foreach (var item in inspiraRequest.lpatients[0].lappointments[0].lservices)
                    {
                        concepts[i] = "'" + item.sconcept + "'";
                        services[i] = "'" + item.sservice + "'";
                        costcenters[i] = "'" + item.scostcenter + "'";
                        i++;
                    }
                    stringBuilder.Append(" AND CARDETCON IN (" + String.Join(",", concepts) + ")");
                    stringBuilder.Append(" AND CARDETCCO IN (" + String.Join(",", costcenters) + ")");
                    stringBuilder.Append(" AND CARDETCOD IN (" + String.Join(",", services) + ")");
                    List<OracleParameter> oracleParameters = new List<OracleParameter>();
                    oracleParameters.Add(new OracleParameter("MOVORDORD", inspiraRequest.lpatients[0].lappointments[0].sauthorization));
                    oracleParameters.Add(new OracleParameter("PACIDE", inspiraRequest.lpatients[0].sdocument));
                    oracleParameters.Add(new OracleParameter("PACTID", inspiraRequest.lpatients[0].sdocumenttype));
                    oracleParameters.Add(new OracleParameter("MOVFUE", "03"));
                    oracleParameters.Add(new OracleParameter("MOVOTRPLA", inspiraRequest.lpatients[0].lappointments[0].splan));
                    object iExists = oracle.GetScalar(stringBuilder.ToString(), oracleParameters, true);
                    return (iExists != DBNull.Value) ? (Convert.ToInt32(iExists) != 0) : true;
                }                
            }
            return false;            
        }

        private List<InspiraTemporal> GetOpenAuthorizationsForPatient(int ipatient, string splan, string sagreement, Oracle oracle)
        {
            InspiraTemporal inspiraTemporal = null;
            List<InspiraTemporal> linspiraTemporals = new List<InspiraTemporal>();
            DataTable dataTable = new DataTable();
            StringBuilder sQuery = new StringBuilder("SELECT MOVORDORD, MOVFEC, MOVDOC FROM SERVINTE.AYMOVORD, SERVINTE.AYMOV, SERVINTE.AYMOVOTR WHERE MOVDOC = MOVORDDOC");
            sQuery.Append(" AND MOVFUE = MOVORDFUE AND MOVOTRFUE = MOVFUE AND MOVOTRDOC = MOVDOC AND MOVANU = 0 AND MOVHIS = :MOVHIS AND MOVOTRPLA = :MOVOTRPLA AND MOVCER = :MOVCER AND MOVFUE = :MOVFUE");
            sQuery.Append(" ORDER BY MOVDOC");
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            oracleParameters.Add(new OracleParameter("MOVHIS", ipatient));
            oracleParameters.Add(new OracleParameter("MOVOTRPLA", splan));
            oracleParameters.Add(new OracleParameter("MOVCER", sagreement));
            oracleParameters.Add(new OracleParameter("MOVFUE", this.parametroRip.ssource));
            dataTable = oracle.GetDataTable(sQuery.ToString(), oracleParameters, false, true);
            foreach (DataRow dataRow in dataTable.Rows)
            {
                inspiraTemporal = new InspiraTemporal()
                {
                    dfecha = Convert.ToDateTime(dataRow["MOVFEC"]),
                    iid = Convert.ToInt32(dataRow["MOVDOC"]),
                    scod = dataRow["MOVORDORD"].ToString(),
                };
                linspiraTemporals.Add(inspiraTemporal);
            }
            dataTable.Dispose();
            dataTable = null;
            sQuery = null;
            oracleParameters = null;
            return linspiraTemporals;
        }

        /// <summary>
        /// Método que verifica si un paciente existe en la base de datos
        /// </summary>
        /// <param name="servintePatient">Objeto paciente</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        /// <returns>Entero con el id del paciente</returns>
        private int PatientExists(ServintePatient servintePatient, Oracle oDAC)
        {
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            string squery = String.Empty;
            if (servintePatient.iid != 0)
            {
                squery = "SELECT PACHIS FROM ABPAC WHERE PACHIS = :PACHIS";
                oracleParameters.Add(new OracleParameter(":PACHIS", servintePatient.iid));
            }
            else
            {
                squery = "SELECT PACHIS FROM ABPAC WHERE PACIDE = :PACIDE AND PACTID = :PACTID";
                oracleParameters.Add(new OracleParameter("PACIDE", servintePatient.sdocument));
                oracleParameters.Add(new OracleParameter("PACTID", Tools.GetDocumentType(servintePatient.sdocumenttype, true)));
            }
            object oid = oDAC.GetScalar(squery, oracleParameters, true);
            return (oid != null) ? Convert.ToInt32(oid) : 0;
        }

        /// <summary>
        /// Método que verifica si un ingreso se encuentra cerrado
        /// </summary>
        /// <param name="inspiraCita">Objeto cita Inspira</param>
        /// <param name="oracle">Objeto conexión a base de datos</param>
        /// <returns>Verdadero su el ingreso está cerrado, falso si está abierto</returns>
        private bool EntryIsClosed(InspiraCita inspiraCita, Oracle oracle)
        {
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            string squery = "SELECT MOVDOC FROM AYMOV WHRE MOVDOC = :MOVDOC AND MOVFUE = :MOVFUE AND MOVEST = 'C'";
            oracleParameters.Add(new OracleParameter("MOVDOC", inspiraCita.ientry));
            oracleParameters.Add(new OracleParameter("MOVFUE", inspiraCita.ientrysource));
            object oid = oracle.GetScalar(squery, oracleParameters, false);
            return (oid != null);
        }

        /// <summary>
        /// Método que obtiene el número actual de la secuencia recibida
        /// </summary>
        /// <param name="ssquence">String nombre de la secuencia</param>
        /// <param name="oracle">Objeto manejador de la base de datos</param>
        /// <returns>Entero número en el que va la secuencia</returns>
        private int GetSequenceNextValue(string ssquence, Oracle oracle)
        {
            object oval = null;
            StringBuilder stringBuilder = new StringBuilder("SELECT ");
            stringBuilder.Append(ssquence);
            stringBuilder.Append(".NEXTVAL FROM DUAL");
            oval = oracle.GetScalar(stringBuilder.ToString(), null, true);
            return (oval != null) ? Convert.ToInt32(oval) : 0;
        }

        private int GetSequenceCurrentValue(string ssquence, Oracle oracle)
        {
            object oval = null;
            StringBuilder stringBuilder = new StringBuilder("SELECT ");
            stringBuilder.Append(ssquence);
            stringBuilder.Append(".CURRVAL FROM DUAL");
            oval = oracle.GetScalar(stringBuilder.ToString(), null, true);
            return (oval != null) ? Convert.ToInt32(oval) : 0;
        }

        /// <summary>
        /// Método para crear el paciente en la tabla ABPAC
        /// </summary>
        /// <param name="servintePatient">Objeto paciente</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        /// <returns>Entero con el id del paciente creado</returns>
        private int CreatePatient(ServintePatient servintePatient, Oracle oDAC)
        {
            int ipatient = this.GetSequenceNextValue("SQ_ABPAC_HIS", oDAC);
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abpac (pacide, pactid, pacap1, pacap2, pacnom, pacsex, pacnac, paclug, pacest, pacnma");
            sQuery.Append(", pacdir, pactel, pacbar, paczon, paccel, pacte2, paccoe, pacact, pacmun, pacnal, pacfex, pacdmn, pacnpa, pacloc, pacama");
            sQuery.Append(", pactim, pacidm, pacdrm, pactma, pacmma, pacmdm, pacpma, pacpno, pacpap, pacpti, pacpid, pacpdr, pacpat, pacpmu, pacpdm");
            sQuery.Append(", pacppa, pacfac, pacfnp, pacidb, paca1b, paca2b, pacnob, pacn2b, pacnie, paccpo, pacrpa, pachis, pacpex, paceex)");
            sQuery.Append(" VALUES (:pacide, :pactid, :pacap1, :pacap2, :pacnom, :pacsex, :pacnac, :paclug, :pacest, :pacnma");
            sQuery.Append(", :pacdir, :pactel, :pacbar, :paczon, :paccel, :pacte2, :paccoe, :pacact, :pacmun, :pacnal, :pacfex, :paccdmn, :paccnpa, (SELECT barloc FROM inbar WHERE barcod = :barcod), :pacama");
            sQuery.Append(", :pactim, :pacidm, :pacdrm, :pactma, :pacmma, :pacmdm, :pacpma, :pacpno, :pacpap, :pacpti, :pacpid, :pacpdr, :pacpat, :pacpmu, :pacpdm");
            sQuery.Append(", :pacppa, :pacfac, :pacfnp, :pacidb, :paca1b, :paca2b, :pacnob, :pacn2b, :pacnie, :paccpo, :pacrpa, :pachis, :pacpex, :paceex)");
            lParameters.Add(new OracleParameter("pacide", servintePatient.sdocument)); //Documento
            lParameters.Add(new OracleParameter("pactid", Tools.GetDocumentType(servintePatient.sdocumenttype, true))); //Tipo de documento
            lParameters.Add(new OracleParameter("pacap1", Tools.SubString(servintePatient.ssurname.ToUpper(), 15))); //Primer apellido
            if (!string.IsNullOrEmpty(servintePatient.ssecondsurname))
            {
                lParameters.Add(new OracleParameter("pacap2", Tools.SubString(servintePatient.ssecondsurname.ToUpper(), 15))); //Segundo apellido
            }
            else
            {
                lParameters.Add(new OracleParameter("pacap2", string.Empty)); //Segundo apellido
            }
            lParameters.Add(new OracleParameter("pacnom", Tools.SubString(Tools.ReplaceChars(servintePatient.sfirstname.ToUpper()), 20))); //Primer nombre
            lParameters.Add(new OracleParameter("pacsex", servintePatient.sgender.Trim())); //Género
            lParameters.Add(new OracleParameter("pacnac", servintePatient.dbirthdate)); //Fecha de nacimiento
            lParameters.Add(new OracleParameter("paclug", servintePatient.scity)); //Lugar de nacimiento
            lParameters.Add(new OracleParameter("pacest", servintePatient.smaritalstatus)); //Estado civil C = Casado D = Divorciado S = Soltero
            lParameters.Add(new OracleParameter("pacnma", DBNull.Value)); //Nombre de la madre
            lParameters.Add(new OracleParameter("pacdir", Tools.SubString(Tools.ReplaceChars(servintePatient.saddress), 100))); //Dirección
            lParameters.Add(new OracleParameter("pactel", Tools.SubString(Tools.ReplaceChars(servintePatient.sphone), 15))); //Teléfono
            lParameters.Add(new OracleParameter("pacbar", servintePatient.sneighborhood)); //Barrio
            lParameters.Add(new OracleParameter("paczon", (!string.IsNullOrEmpty(servintePatient.surbanzone)) ? servintePatient.surbanzone : "U")); //Zona U = Urbana R = Rural
            lParameters.Add(new OracleParameter("paccel", Tools.SubString(Tools.ReplaceChars(servintePatient.scellphone), 20))); //Celular
            lParameters.Add(new OracleParameter("pacte2", DBNull.Value)); //Teléfono 2
            lParameters.Add(new OracleParameter("paccoe", servintePatient.smail)); //Correo electrónico
            lParameters.Add(new OracleParameter("pacact", "S")); //Activo S = SI N = No
            lParameters.Add(new OracleParameter("pacmun", servintePatient.scity)); //Ciudad de residencia
            lParameters.Add(new OracleParameter("pacnal", "COLOMBIANO(A)")); //Nacionalidad
            lParameters.Add(new OracleParameter("pacfex", DBNull.Value)); //Fecha expedición documento identificación
            lParameters.Add(new OracleParameter("paccdmn", servintePatient.scityname)); //Nombre ciudad de residencia
            lParameters.Add(new OracleParameter("paccnpa", servintePatient.snation)); //País de nacimiento
            lParameters.Add(new OracleParameter("barcod", servintePatient.sneighborhood)); //Localidad
            lParameters.Add(new OracleParameter("pacama", DBNull.Value)); //TODO Localidad
            lParameters.Add(new OracleParameter("pactim", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacidm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacdrm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pactma", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacmma", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacmdm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpma", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpno", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpap", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpti", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpid", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpdr", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpat", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpmu", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpdm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacppa", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacfac", "N")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacfnp", "S")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacidb", servintePatient.sdocument)); //Documento
            lParameters.Add(new OracleParameter("paca1b", Tools.ReplaceChars(servintePatient.sfirstname.ToUpper()))); //Primer nombre
            lParameters.Add(new OracleParameter("paca2b", Tools.SubString(Tools.ReplaceChars(servintePatient.ssecondname), 30))); //Segundo nombre
            lParameters.Add(new OracleParameter("pacnob", Tools.SubString(Tools.ReplaceChars(servintePatient.ssurname), 20))); //Primer apellido
            lParameters.Add(new OracleParameter("pacn2b", Tools.ReplaceChars(servintePatient.ssecondsurname))); //Segundo apellido
            lParameters.Add(new OracleParameter("pacnie", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("paccpo", "110131")); //Código postal??
            lParameters.Add(new OracleParameter("pacrpa", servintePatient.snation)); //País
            lParameters.Add(new OracleParameter("pachis", ipatient)); //Id del paciente
            lParameters.Add(new OracleParameter("pacpex", servintePatient.ssourcecountry)); //País de origen del documento
            lParameters.Add(new OracleParameter("paceex", servintePatient.sissuingentity)); //Entidad que explide el documento
            oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
            sQuery = null;
            lParameters = null;
            this.CreatePatientAud(ipatient, oDAC);
            return ipatient;
        }

        /// <summary>
        /// Método que crea el registro de auditoría del paciente en Servinte tabla ABPACAUD
        /// </summary>
        /// <param name="ipatient">Id del paciente</param>
        /// <param name="oracle">Objeto conexión a la base de datos</param>
        private void CreatePatientAud(int ipatient, Oracle oracle)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO ABPACAUD (PACAUDSEC, PACAUDUSU, PACAUDFAD, PACAUDEAD, PACAUDORI)");
            sQuery.Append(" VALUES (:PACAUDSEC, :PACAUDUSU, SYSDATE, :PACAUDEAD, :PACAUDORI)");
            lParameters.Add(new OracleParameter("PACAUDSEC", ipatient)); //Id del paciente
            lParameters.Add(new OracleParameter("PACAUDUSU", "admon")); //Usuario que crea el registro
            lParameters.Add(new OracleParameter("PACAUDEAD", this.parametroRip.scia)); //Estructura administrativa
            lParameters.Add(new OracleParameter("PACAUDORI", "caymov"));
            oracle.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para actualizar la tabla de pacientes ABPAC
        /// </summary>
        /// <param name="servintePatient">Objeto paciente</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void UpdatePatient(ServintePatient servintePatient, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder sQuery = new StringBuilder("UPDATE abpac SET pacide = :pacide, pactid = :pactid, pacap1 = :pacap1, pacap2 = :pacap2, pacnom = :pacnom, pacsex = :pacsex, pacnac = :pacnac, paclug = :paclug, pacest = :pacest, pacnma = :pacnma");
            sQuery.Append(", pacdir = :pacdir, pactel = :pactel, pacbar = :pacbar, paczon = :paczon, paccel = :paccel, pacte2 = :pacte2, paccoe = :paccoe, pacact = :pacact, pacmun = :pacmun, pacnal = :pacnal");
            sQuery.Append(", pacfex = :pacfex, pacdmn = :paccdmn, pacnpa = :pacnpa, pacama = :pacama, pactim = :pactim, pacidm = :pacidm, pacdrm = :pacdrm, pactma = :pactma, pacmma = :pacmma, pacmdm = :pacmdm, pacpma = :pacpma");
            sQuery.Append(", pacpno = :pacpno, pacpap = :pacpap, pacpti = :pacpti, pacpid = :pacpid, pacpdr = :pacpdr, pacpat = :pacpat, pacpmu = :pacpmu, pacpdm = :pacpdm, pacppa = :pacppa, pacfac = :pacfac");
            sQuery.Append(", pacfnp = :pacfnp, pacidb = :pacidb, paca1b = :paca1b, paca2b = :paca2b, pacnob = :pacnob, pacn2b = :pacn2b, pacnie = :pacnie, paccpo = :pacccpo");
            if (!string.IsNullOrEmpty(servintePatient.sissuingentity))
            {
                sQuery.Append(", paceex = :paceex");
                lParameters.Add(new OracleParameter("paceex", servintePatient.sissuingentity)); //Entidad que explide el documento
            }
            if (!string.IsNullOrEmpty(servintePatient.ssourcecountry))
            {
                sQuery.Append(", pacpex = :pacpex");
                lParameters.Add(new OracleParameter("pacpex", servintePatient.ssourcecountry)); //País de procedencia del documento
            }
            sQuery.Append(" WHERE pachis = :pachis");
            lParameters.Add(new OracleParameter("pacide", servintePatient.sdocument)); //Tipo de documento
            lParameters.Add(new OracleParameter("pactid", Tools.GetDocumentType(servintePatient.sdocumenttype, true))); //Documento
            lParameters.Add(new OracleParameter("pacap1", Tools.SubString(servintePatient.ssurname.ToUpper(), 15))); //Primer apellido
            if (!string.IsNullOrEmpty(servintePatient.ssecondsurname))
            {
                lParameters.Add(new OracleParameter("pacap2", Tools.SubString(servintePatient.ssecondsurname.ToUpper(), 15))); //Segundo apellido
            }
            else
            {
                lParameters.Add(new OracleParameter("pacap2", string.Empty)); //Segundo apellido
            }
            lParameters.Add(new OracleParameter("pacnom", Tools.SubString(servintePatient.sfirstname.ToUpper(), 20))); //Primer nombre
            lParameters.Add(new OracleParameter("pacsex", servintePatient.sgender.Trim())); //Género
            lParameters.Add(new OracleParameter("pacnac", servintePatient.dbirthdate)); //Fecha de nacimiento
            lParameters.Add(new OracleParameter("paclug", servintePatient.scity)); //Lugar de nacimiento
            lParameters.Add(new OracleParameter("pacest", servintePatient.smaritalstatus)); //Estado civil C = Casado D = Divorciado S = Soltero
            lParameters.Add(new OracleParameter("pacnma", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacdir", Tools.SubString(servintePatient.saddress, 100))); //Dirección
            lParameters.Add(new OracleParameter("pactel", Tools.SubString(servintePatient.sphone, 15))); //Teléfono
            lParameters.Add(new OracleParameter("pacbar", servintePatient.sneighborhood)); //Barrio
            lParameters.Add(new OracleParameter("paczon", servintePatient.surbanzone)); //Zona U = Urbana R = Rural
            lParameters.Add(new OracleParameter("paccel", Tools.SubString(servintePatient.scellphone, 20))); //Celular
            lParameters.Add(new OracleParameter("pacte2", DBNull.Value)); //Teléfono 2
            lParameters.Add(new OracleParameter("paccoe", servintePatient.smail)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacact", "S")); //Activo S = SI N = No
            lParameters.Add(new OracleParameter("pacmun", servintePatient.scity)); //Ciudad de residencia
            lParameters.Add(new OracleParameter("pacnal", "COLOMBIANO(A)")); //Nacionalidad
            lParameters.Add(new OracleParameter("pacfex", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("paccdmn", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacnpa", servintePatient.snation)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacama", DBNull.Value)); //TODO Localidad
            lParameters.Add(new OracleParameter("pactim", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacidm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacdrm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pactma", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacmma", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacmdm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpma", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpno", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpap", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpti", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpid", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpdr", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpat", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpmu", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacpdm", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacppa", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacfac", "N")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacfnp", "S")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacidb", servintePatient.sdocument)); //Documento
            lParameters.Add(new OracleParameter("paca1b", servintePatient.sfirstname)); //Primer nombre
            lParameters.Add(new OracleParameter("paca2b", servintePatient.ssecondname)); //Segundo nombre
            lParameters.Add(new OracleParameter("pacnob", Tools.SubString(servintePatient.ssurname, 20))); //Primer apellido
            lParameters.Add(new OracleParameter("pacn2b", servintePatient.ssecondsurname)); //Segundo apellido
            lParameters.Add(new OracleParameter("pacnie", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pacccpo", "110131")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("pachis", servintePatient.iid)); //Id del paciente
            oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para crear la información adicional del paciente en Servinte tabla ABPACOTR
        /// </summary>
        /// <param name="servintePatient">Objeto paciente Servinte</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreatePatientExtraInformation(ServintePatient servintePatient, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abpacotr (pacotrsec, pacotrocu, pacotrhab, pacotrcv1, pacotrcv2, pacotremp, pacotrtir, pacotridr");
            sQuery.Append(", pacotrnor, pacotrapr, pacotrpar, pacotrdrr, pacotrmur, pacotrter) VALUES (:pacotrsec, :pacotrocu, :pacotrhab , :pacotrcv1, :pacotrcv2");
            sQuery.Append(" , (SELECT EMPDETADM FROM INEMPDET WHERE EMPDETCOD = :pacotremp), :pacotrtir, :pacotridr, :pacotrnor, :pacotrapr, :pacotrpar, :pacotrdrr, :pacotrmur, :pacotrter)");
            lParameters.Add(new OracleParameter("pacotrsec", servintePatient.iid)); //Id del paciente
            //lParameters.Add(new OracleParameter("pacotrocu", Tools.SubString(servintePatient.sjob, 7))); //Código ocupación del paciente
            lParameters.Add(new OracleParameter("pacotrocu", Tools.SubString(servintePatient.sjob, 7))); //Código ocupación del paciente
            lParameters.Add(new OracleParameter("pacotrhab", 'S')); //Paciente acepta habeas data
            lParameters.Add(new OracleParameter("pacotrcv1", servintePatient.scovid1)); //TODO: Covid1
            lParameters.Add(new OracleParameter("pacotrcv2", servintePatient.scovid2)); //TODO: Covid2
            lParameters.Add(new OracleParameter("pacotremp", servintePatient.sagreementcode)); //Empresa del paciente
            lParameters.Add(new OracleParameter("pacotrtir", Tools.GetDocumentType(servintePatient.sdocumenttype, true))); //Tipo Documento responsable del paciente
            lParameters.Add(new OracleParameter("pacotridr", servintePatient.sdocument)); //Documento responsable del paciente
            lParameters.Add(new OracleParameter("pacotrnor", servintePatient.sfirstname)); //Primer nombre responsable del paciente
            lParameters.Add(new OracleParameter("pacotrapr", servintePatient.ssurname)); //Primer apellido responsable del paciente
            lParameters.Add(new OracleParameter("pacotrpar", "8")); //Tipo de Parentesco (Pusimos 8 por ahora)
            lParameters.Add(new OracleParameter("pacotrdrr", servintePatient.saddress)); //Dirección del responsable
            lParameters.Add(new OracleParameter("pacotrmur", servintePatient.scity)); //Ciudad del responsable
            lParameters.Add(new OracleParameter("pacotrter", Tools.SubString(servintePatient.sphone, 15))); //Teléfono del responsable
            oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para actualizar la información adicional del paciente  tabla ABPACOTR
        /// </summary>
        /// <param name="servintePatient">Objeto pacinte servinte</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void UpdatePatientExtraInformation(ServintePatient servintePatient, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder sQuery = new StringBuilder("UPDATE abpacotr SET pacotrhab = 'S'");
            if (!string.IsNullOrEmpty(servintePatient.sjob))
            {
                sQuery.Append(", pacotrocu = :pacotrocu");
                lParameters.Add(new OracleParameter("pacotrocu", Tools.SubString(servintePatient.sjob, 7))); //Código ocupación del paciente
            }
            if (!string.IsNullOrEmpty(servintePatient.scovid1))
            {
                sQuery.Append(", pacotrcv1 = :pacotrcv1");
                lParameters.Add(new OracleParameter("pacotrcv1", servintePatient.scovid1));
            }
            if (!string.IsNullOrEmpty(servintePatient.scovid2))
            {
                sQuery.Append(", pacotrcv2 = :pacotrcv2");
                lParameters.Add(new OracleParameter("pacotrcv2", servintePatient.scovid2));
            }
            sQuery.Append(" WHERE pacotrsec = :pacotrsec");
            lParameters.Add(new OracleParameter("pacotrsec", servintePatient.iid)); //Id del paciente
            oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para obtener el total del valor de los servicios
        /// </summary>
        /// <param name="lserviceRequests">Lista genérica de servicios</param>
        /// <returns>Entero con el valor de la suma de los servicios</returns>
        private decimal GetEntryValue(List<ServiceRequest> lserviceRequests)
        {
            return (lserviceRequests != null) ? lserviceRequests.Sum(x => (this.GetProductValue(x) * x.iqty)) : 0;
        }

        /// <summary>
        /// Método para crear el ingreso en la tabla AYMOV
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira Cita</param>
        /// <param name="servintePatient">Objeto Paciente Servinte</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>        
        private void CreateEntry(InspiraCita inspiraRequest, ServintePatient servintePatient, Oracle oDAC)
        {
            decimal dTotal = (!this.stype.Contains("Plantilla Programas")) ? this.GetEntryValue(inspiraRequest.lservices) : inspiraRequest.ientry;
            List<OracleParameter> lParameters = new List<OracleParameter>();
            OracleParameter oracleParameter = new OracleParameter();
            oracleParameter.ParameterName = "MOVEMP";
            Generic generic = Tools.GetCompanyFromRate(inspiraRequest.sagreement, inspiraRequest.sagreementname, servintePatient.safiliation, servintePatient.sfirstname + ' ' + servintePatient.ssurname, servintePatient.sdocument);
            oracleParameter.Value = generic.sfilter;
            string scompany = generic.scode;
            string scompanyname = generic.sname;
            string spolicy = (string.IsNullOrEmpty(servintePatient.spolicy)) ? servintePatient.sdocument : servintePatient.spolicy;
            oracleParameter.Direction = ParameterDirection.Input;
            string sTemplate = (!this.stype.Contains("Plantilla Programas")) ? string.Empty : inspiraRequest.stemplate;
            string srate = string.Empty;
            string squerycomplement = string.Empty;
            string sthird = string.IsNullOrEmpty(inspiraRequest.sthird) ? "73135051" : inspiraRequest.sthird;
            string sdoctor = this.GetDoctorCode(sthird);
            if (servintePatient.safiliation.EqualsAnyOf("P", "7", "9"))
            {
                srate = inspiraRequest.srate;
                squerycomplement = ":MOVTAR";
            }
            else
            {
                srate = inspiraRequest.sagreement;
                squerycomplement = "(SELECT EMPEADTAR FROM INEMPEAD WHERE EMPEADIND = 'S' AND EMPEADCOD = :MOVTAR AND EMPEADEAD = :MOVEAD)";
            }
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYMOV (MOVFUE, MOVDOC, MOVANO, MOVMES, MOVCCO, MOVFEC, MOVHOR, MOVREC, MOVTIP, MOVFUO, MOVDOO, MOVHIS, MOVNUM");
            stringBuilder.Append(", MOVMED, MOVESP, MOVPOL, MOVSIN, MOVDIA, MOVEMP, MOVDES, MOVCER, MOVRES, MOVTAR, MOVTSE, MOVPAQ, MOVVAL, MOVVRE, MOVVDE, MOVVRC, MOVVAB, MOVNRE, MOVREM");
            stringBuilder.Append(", MOVMUN, MOVANU, MOVPYP, MOVEMB, MOVFRE, MOVEAD, MOVEAO, MOVECN, MOVTR2, MOVCR2, MOVNR2, MOVUAD, MOVFAD, MOVUMO, MOVFMO, MOVEST, MOVMON, MOVJDV)");
            stringBuilder.Append(" VALUES (:MOVFUE, :MOVDOC, :MOVANO, :MOVMES, :MOVCCO, :MOVFEC, :MOVHOR, :MOVREC, :MOVTIP, :MOVFUO, :MOVDOO, :MOVHIS, :MOVNUM");
            stringBuilder.Append(", :MOVMED, :MOVESP, :MOVPOL, :MOVSIN, :MOVDIA, :MOVEMP, :MOVDES, :MOVCER, :MOVRES, " + squerycomplement);
            stringBuilder.Append(", :MOVTSE, :MOVPAQ, :MOVVAL, :MOVVRE, :MOVVDE, :MOVVRC, :MOVVAB, :MOVNRE, :MOVREM, :MOVMUN, :MOVANU, :MOVPYP, :MOVEMB, :MOVFRE, :MOVEAD, :MOVEAO, :MOVECN");
            stringBuilder.Append(", :MOVTR2, :MOVCR2, :MOVNR2, :MOVUAD, :MOVFAD, :MOVUMO, :MOVFMO, :MOVEST, :MOVMON, 'N')");
            lParameters.Add(new OracleParameter("MOVFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("MOVDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("MOVANO", inspiraRequest.ddate.Year)); //Año
            lParameters.Add(new OracleParameter("MOVMES", inspiraRequest.ddate.ToString("MM"))); //Mes
            //lParameters.Add(new OracleParameter("MOVCCO", inspiraRequest.scostcenter)); //Centro de costos
            lParameters.Add(new OracleParameter("MOVCCO", "9190")); //Centro de costos
            lParameters.Add(new OracleParameter("MOVFEC", inspiraRequest.ddate)); //Fecha
            lParameters.Add(new OracleParameter("MOVHOR", Tools.Time2Number(DateTime.Now))); //Hora
            lParameters.Add(new OracleParameter("MOVREC", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVTIP", oracleParameter.Value.ToString())); //Tipo de responsable: E = Empresa, P = Particular
            lParameters.Add(new OracleParameter("MOVFUO", this.parametroRip.ssource)); //Fuente origen
            lParameters.Add(new OracleParameter("MOVDOO", (this.ientry != 0) ? this.ientry : this.parametroRip.ientry)); //Ingreso Origen
            lParameters.Add(new OracleParameter("MOVHIS", servintePatient.iid)); //Id del paciente
            lParameters.Add(new OracleParameter("MOVNUM", "1")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVMED", sdoctor)); //Código del médico
            lParameters.Add(new OracleParameter("MOVESP", "100")); //Código especialidad del médico
            lParameters.Add(new OracleParameter("MOVPOL", spolicy)); //Número de poliza
            lParameters.Add(new OracleParameter("MOVSIN", inspiraRequest.sservicetype)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVDIA", (!string.IsNullOrEmpty(inspiraRequest.scie10) ? inspiraRequest.scie10 : "Z000"))); //Diagnóstico
            lParameters.Add(oracleParameter); //TODO Esto qué es (creo que el cargo si es empresa E o paciente P)
            lParameters.Add(new OracleParameter("MOVDES", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVCER", scompany)); //Código del convenio
            lParameters.Add(new OracleParameter("MOVRES", scompanyname)); //Nombre del convenio
            lParameters.Add(new OracleParameter("MOVTAR", srate)); //Código de la tarifa
            lParameters.Add(new OracleParameter("MOVTSE", inspiraRequest.sattentiontype)); //Tipo de servicio
            lParameters.Add(new OracleParameter("MOVPAQ", sTemplate)); //Código del paquete
            lParameters.Add(new OracleParameter("MOVVAL", dTotal)); //Valor del ingreso
            lParameters.Add(new OracleParameter("MOVVRE", "0")); //TODO Valor recargo
            lParameters.Add(new OracleParameter("MOVVDE", inspiraRequest.lservices.Sum(x => x.idiscount))); //Valor descuento
            lParameters.Add(new OracleParameter("MOVVRC", dTotal)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVVAB", "0")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVNRE", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVREM", this.parametroRip.snit)); //Nit de la FNC?
            lParameters.Add(new OracleParameter("MOVMUN", servintePatient.scity)); //Código de la ciudad
            lParameters.Add(new OracleParameter("MOVANU", "0")); //Ingreso anulado
            lParameters.Add(new OracleParameter("MOVPYP", "N")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVEMB", "N")); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVFRE", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVEAD", this.parametroRip.scia)); //Sede
            lParameters.Add(new OracleParameter("MOVEAO", this.parametroRip.scia)); //Sede
            lParameters.Add(new OracleParameter("MOVECN", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVTR2", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVCR2", DBNull.Value)); //TODO Esto qué es
            lParameters.Add(new OracleParameter("MOVNR2", DBNull.Value)); //Nombre del segundo responsable
            lParameters.Add(new OracleParameter("MOVUAD", Tools.GetUser(inspiraRequest.suser))); //Usuario que crea el ingreso
            lParameters.Add(new OracleParameter("MOVFAD", DateTime.Now)); //Fecha de creación
            lParameters.Add(new OracleParameter("MOVUMO", Tools.GetUser(inspiraRequest.suser))); //Usuario que crea el ingreso
            lParameters.Add(new OracleParameter("MOVFMO", DateTime.Now)); //Usuario que crea el ingreso
            lParameters.Add(new OracleParameter("MOVEST", "A")); //Estado del ingreso
            lParameters.Add(new OracleParameter("MOVMON", "MF")); //Moneda del movimiento
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
            generic = null;
        }

        /// <summary>
        /// Método para crear valores en la tabla adicional del ingreso AYMOVOTR
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira Cita</param>
        /// <param name="servintePatient">Objeto Paciente Servinte</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateEntryAdditional(InspiraCita inspiraRequest, ServintePatient servintePatient, Oracle oDAC)
        {
            string splan = (servintePatient.safiliation.EqualsAnyOf("P", "7", "9")) ? string.Empty : inspiraRequest.splan;
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYMOVOTR (MOVOTRFUE, MOVOTRDOC, MOVOTRTUS, MOVOTRNIV, MOVOTREST, MOVOTRATE, MOVOTRCIN, MOVOTRSEC, MOVOTRMUN");
            stringBuilder.Append(", MOVOTREAD, MOVOTRUFU, MOVOTRTIA, MOVOTRCEA, MOVOTRN1A, MOVOTRN2A, MOVOTRA1A, MOVOTRTRA, MOVOTRPAA, MOVOTRINE, MOVOTRN1C, MOVOTRA1C");
            stringBuilder.Append(", MOVOTRTEC, MOVOTRTAT, MOVOTRVAR, MOVOTRACO, MOVOTRPLA, MOVOTRCTR, MOVOTRVIA) VALUES");
            stringBuilder.Append(" (:MOVOTRFUE, :MOVOTRDOC, :MOVOTRTUS, :MOVOTRNIV, :MOVOTREST, :MOVOTRATE, :MOVOTRCIN, :MOVOTRSEC, :MOVOTRMUN");
            stringBuilder.Append(", :MOVOTREAD, :MOVOTRUFU, :MOVOTRTIA, :MOVOTRCEA, :MOVOTRN1A, :MOVOTRN2A, :MOVOTRA1A, :MOVOTRTRA, :MOVOTRPAA, :MOVOTRINE, :MOVOTRN1C, :MOVOTRA1C");
            stringBuilder.Append(", :MOVOTRTEC, :MOVOTRTAT, :MOVOTRVAR, :MOVOTRACO, :MOVOTRPLA, :MOVOTRCTR, :MOVOTRVIA)");
            lParameters.Add(new OracleParameter("MOVOTRFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("MOVOTRDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("MOVOTRTUS", servintePatient.safiliation)); //TODO: Tipo de afiliación 1 para afiliado
            lParameters.Add(new OracleParameter("MOVOTRNIV", servintePatient.slevel)); //Nivel socio económico
            lParameters.Add(new OracleParameter("MOVOTREST", "3")); //Estrato social
            lParameters.Add(new OracleParameter("MOVOTRATE", inspiraRequest.sattendingtype)); //Tipo de atención
            lParameters.Add(new OracleParameter("MOVOTRCIN", "13")); //Causa del ingreso, 13 para enfermedad general
            lParameters.Add(new OracleParameter("MOVOTRSEC", this.parametroRip.isequence)); //TODO: Tabla Emspar campo parsec
            lParameters.Add(new OracleParameter("MOVOTRMUN", servintePatient.scity)); //TODO: Ciudad (PARA FNC Bogotá para todos)
            lParameters.Add(new OracleParameter("MOVOTREAD", this.parametroRip.scia)); //Estructura administrativa
            lParameters.Add(new OracleParameter("MOVOTRUFU", inspiraRequest.sunit)); //Unidad funcional
            lParameters.Add(new OracleParameter("MOVOTRTIA", DBNull.Value)); //TODO: Tipo identifación del acudiente
            lParameters.Add(new OracleParameter("MOVOTRCEA", DBNull.Value)); //TODO: Documento del acudiente
            lParameters.Add(new OracleParameter("MOVOTRN1A", DBNull.Value)); //TODO: Primer nombre del acudiente
            lParameters.Add(new OracleParameter("MOVOTRN2A", DBNull.Value)); //TODO: Segundo nombre del acudiente
            lParameters.Add(new OracleParameter("MOVOTRA1A", DBNull.Value)); //TODO: Apellido del acudiente
            lParameters.Add(new OracleParameter("MOVOTRTRA", DBNull.Value)); //TODO: Teléfono del acudiente
            lParameters.Add(new OracleParameter("MOVOTRPAA", DBNull.Value)); //TODO: Parentesco del acudiente
            lParameters.Add(new OracleParameter("MOVOTRINE", "N")); //TODO: Identificador de embarazo
            lParameters.Add(new OracleParameter("MOVOTRN1C", "SIN ACOMPAÑANTE")); //TODO: Primer nombre acompañante
            lParameters.Add(new OracleParameter("MOVOTRA1C", "SIN ACOMPAÑANTE")); //TODO: Primer apellido acompañante
            lParameters.Add(new OracleParameter("MOVOTRTEC", DBNull.Value)); //TODO: Teléfono acompañante
            lParameters.Add(new OracleParameter("MOVOTRTAT", "I")); //TODO: Tipo médico que atiende
            lParameters.Add(new OracleParameter("MOVOTRVAR", this.GetEntryValue(inspiraRequest.lservices))); //TODO: Valor recaudado
            lParameters.Add(new OracleParameter("MOVOTRACO", "N")); //TODO: Tipo médico que atiende
            lParameters.Add(new OracleParameter("MOVOTRPLA", splan)); //Código del plan
            lParameters.Add(new OracleParameter("MOVOTRCTR", inspiraRequest.scontract)); //Código del contrato
            lParameters.Add(new OracleParameter("MOVOTRVIA", "02")); // Codigo Via Ingreso
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para crear los datos adicionales del ingreso tabla AYMOVOTR
        /// </summary>
        /// <param name="inspiraRequest"></param>
        /// <param name="scity"></param>
        /// <param name="oDAC"></param>
        private void CreateEntryAdditional(InspiraCita inspiraRequest, string scity, string susertype, Oracle oDAC)
        {
            string splan = string.Empty;
            if (inspiraRequest.sratename.Contains("PARTICULAR") && inspiraRequest.sagreement == "30")
            {
                splan = string.Empty;
            }
            else if (inspiraRequest.sratename.Contains("CORTESIA") || inspiraRequest.sratename.Contains("SUBSIDI"))
            {
                splan = string.Empty;
            }
            else
            {
                splan = inspiraRequest.splan;
            }            
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder sQueryTus = new StringBuilder();
            StringBuilder sQueryNiv = new StringBuilder();
            if (inspiraRequest.sagreement == "29")
            {
                sQueryTus.Append("'8'");
                sQueryNiv.Append("'6'");
            }
            else if (inspiraRequest.sratename.Contains("CORTESIA"))
            {
                sQueryTus.Append("'7'");
                sQueryNiv.Append("'7'");
            }
            else
            {
                sQueryTus.Append("(SELECT PAGTUS FROM INPAG WHERE PAGEMP = '");
                sQueryTus.Append(inspiraRequest.sagreement);
                sQueryTus.Append("' AND PAGPLA = '");
                sQueryTus.Append(inspiraRequest.splan);
                sQueryTus.Append("' FETCH NEXT 1 ROWS ONLY)");
                sQueryNiv.Append("(SELECT PAGNIV FROM INPAG WHERE PAGEMP = '");
                sQueryNiv.Append(inspiraRequest.sagreement);
                sQueryNiv.Append("' AND PAGPLA = '");
                sQueryNiv.Append(inspiraRequest.splan);
                sQueryNiv.Append("' FETCH NEXT 1 ROWS ONLY)");
            }
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYMOVOTR (MOVOTRFUE, MOVOTRDOC, MOVOTRTUS, MOVOTRNIV, MOVOTREST, MOVOTRATE, MOVOTRCIN, MOVOTRSEC, MOVOTRMUN");
            stringBuilder.Append(", MOVOTREAD, MOVOTRUFU, MOVOTRTIA, MOVOTRCEA, MOVOTRN1A, MOVOTRN2A, MOVOTRA1A, MOVOTRTRA, MOVOTRPAA, MOVOTRINE, MOVOTRN1C, MOVOTRA1C");
            stringBuilder.Append(", MOVOTRTEC, MOVOTRTAT, MOVOTRVAR, MOVOTRACO, MOVOTRPLA) VALUES");
            stringBuilder.Append(" (:MOVOTRFUE, :MOVOTRDOC,");
            stringBuilder.Append(sQueryTus.ToString());
            stringBuilder.Append(",");
            stringBuilder.Append(sQueryNiv.ToString());
            stringBuilder.Append(", :MOVOTREST, :MOVOTRATE, :MOVOTRCIN, :MOVOTRSEC, :MOVOTRMUN, :MOVOTREAD, :MOVOTRUFU, :MOVOTRTIA, :MOVOTRCEA, :MOVOTRN1A, :MOVOTRN2A, :MOVOTRA1A");
            stringBuilder.Append(", :MOVOTRTRA, :MOVOTRPAA, :MOVOTRINE, :MOVOTRN1C, :MOVOTRA1C, :MOVOTRTEC, :MOVOTRTAT, :MOVOTRVAR, :MOVOTRACO, :MOVOTRPLA)");
            lParameters.Add(new OracleParameter("MOVOTRFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("MOVOTRDOC", this.parametroRip.ientry)); //Número de ingreso
            //lParameters.Add(new OracleParameter("MOVOTRTUS", susertype)); //TODO: Tipo de afiliación 1 para afiliado            
            //lParameters.Add(new OracleParameter("MOVEMP", inspiraRequest.sagreement)); //TODO: Tipo de afiliación 1 para afiliado                        
            lParameters.Add(new OracleParameter("MOVOTREST", "3")); //Estrato social
            lParameters.Add(new OracleParameter("MOVOTRATE", Tools.GetAttentionType(this.stype, inspiraRequest.sservicegroup))); //Tipo de atención
            lParameters.Add(new OracleParameter("MOVOTRCIN", "13")); //Causa del ingreso, 13 para enfermedad general
            lParameters.Add(new OracleParameter("MOVOTRSEC", this.parametroRip.isequence)); //TODO: Tabla Emspar campo parsec
            lParameters.Add(new OracleParameter("MOVOTRMUN", scity)); //TODO: Ciudad (PARA FNC Bogotá para todos)
            lParameters.Add(new OracleParameter("MOVOTREAD", this.parametroRip.scia)); //Estructura administrativa
            lParameters.Add(new OracleParameter("MOVOTRUFU", inspiraRequest.sunit)); //Unidad funcional
            lParameters.Add(new OracleParameter("MOVOTRTIA", DBNull.Value)); //TODO: Tipo identifación del acudiente
            lParameters.Add(new OracleParameter("MOVOTRCEA", DBNull.Value)); //TODO: Documento del acudiente
            lParameters.Add(new OracleParameter("MOVOTRN1A", DBNull.Value)); //TODO: Primer nombre del acudiente
            lParameters.Add(new OracleParameter("MOVOTRN2A", DBNull.Value)); //TODO: Segundo nombre del acudiente
            lParameters.Add(new OracleParameter("MOVOTRA1A", DBNull.Value)); //TODO: Apellido del acudiente
            lParameters.Add(new OracleParameter("MOVOTRTRA", DBNull.Value)); //TODO: Teléfono del acudiente
            lParameters.Add(new OracleParameter("MOVOTRPAA", DBNull.Value)); //TODO: Parentesco del acudiente
            lParameters.Add(new OracleParameter("MOVOTRINE", DBNull.Value)); //TODO: Identificador de embarazo
            lParameters.Add(new OracleParameter("MOVOTRN1C", "SIN ACOMPAÑANTE")); //TODO: Primer nombre acompañante
            lParameters.Add(new OracleParameter("MOVOTRA1C", "SIN ACOMPAÑANTE")); //TODO: Primer apellido acompañante
            lParameters.Add(new OracleParameter("MOVOTRTEC", DBNull.Value)); //TODO: Teléfono acompañante
            lParameters.Add(new OracleParameter("MOVOTRTAT", DBNull.Value)); //TODO: Tipo médico que atiende
            lParameters.Add(new OracleParameter("MOVOTRVAR", this.GetEntryValue(inspiraRequest.lservices))); //TODO: Valor recaudado
            lParameters.Add(new OracleParameter("MOVOTRACO", "N")); //TODO: Tipo médico que atiende
            lParameters.Add(new OracleParameter("MOVOTRPLA", splan)); //Código del plan
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para crear el registro en el log de Servinte AYLOG
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateEntryLog(InspiraCita inspiraRequest, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYLOG (logusu, logter, logpro, logope, logde1, logva1, logde2, logva2, logde3, logva3");
            stringBuilder.Append(", logde4, logva4, logde5, logva5, logde6, logva6, logtip, logtab, logfec, logead) VALUES (:logusu, :logter, :logpro, :logope");
            stringBuilder.Append(", :logde1, :logva1, :logde2, :logva2, :logde3, :logva3, :logde4, :logva4, :logde5, :logva5, :logde6, :logva6, :logtip, :logtab, :logfec, :logead)");
            lParameters.Add(new OracleParameter("logusu", Tools.GetUser(inspiraRequest.suser))); //Usuario que crea el registro
            lParameters.Add(new OracleParameter("logter", "FA")); //Terminal
            lParameters.Add(new OracleParameter("logpro", "caymov 11.0.59")); //Programa
            lParameters.Add(new OracleParameter("logope", "Grabar")); //Operación
            lParameters.Add(new OracleParameter("logde1", "FUENTE")); //Campo actualizado 1
            lParameters.Add(new OracleParameter("logva1", this.parametroRip.ssource)); //Valor campo actualizado 1
            lParameters.Add(new OracleParameter("logde2", "DOCUMENTO")); //Campo actualizado 2
            lParameters.Add(new OracleParameter("logva2", this.parametroRip.ientry)); //Valor campo actualizado 2
            lParameters.Add(new OracleParameter("logde3", string.Empty)); //Campo actualizado 3
            lParameters.Add(new OracleParameter("logva3", string.Empty)); //Valor campo actualizado 3
            lParameters.Add(new OracleParameter("logde4", string.Empty)); //Campo actualizado 4
            lParameters.Add(new OracleParameter("logva4", string.Empty)); //Valor campo actualizado 4
            lParameters.Add(new OracleParameter("logde5", string.Empty)); //Campo actualizado 5
            lParameters.Add(new OracleParameter("logva5", string.Empty)); //Valor campo actualizado 5
            lParameters.Add(new OracleParameter("logde6", string.Empty)); //Campo actualizado 6
            lParameters.Add(new OracleParameter("logva6", string.Empty)); //Valor campo actualizado 6
            lParameters.Add(new OracleParameter("logtip", "I")); //TODO: Tipo de institución
            lParameters.Add(new OracleParameter("logtab", "aymov")); //Tabla que se modifica
            lParameters.Add(new OracleParameter("logfec", DateTime.Now)); //Fecha de edición
            lParameters.Add(new OracleParameter("logead", this.parametroRip.scia)); //Fecha de edición
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para generar egreso en Servinte tabla AYMOVEGR
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateEgress(InspiraCita inspiraRequest, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYMOVEGR (MOVEGRFUE, MOVEGRDOC, MOVEGRFEG, MOVEGRHEG, MOVEGRREG, MOVEGRANU, MOVEGRRIN, MOVEGRFSO, MOVEGRHSO, MOVEGRUAD");
            stringBuilder.Append(", MOVEGRFAD, MOVEGRTIR, MOVEGREAD) VALUES (:MOVEGRFUE, :MOVEGRDOC, :MOVEGRFEG, :MOVEGRHEG, :MOVEGRREG, :MOVEGRANU");
            stringBuilder.Append(", :MOVEGRRIN, :MOVEGRFSO, :MOVEGRHSO, :MOVEGRUAD, :MOVEGRFAD, :MOVEGRTIR, :MOVEGREAD)");
            lParameters.Add(new OracleParameter("MOVEGRFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("MOVEGRDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("MOVEGRFEG", inspiraRequest.ddate)); //Fecha del egreso
            lParameters.Add(new OracleParameter("MOVEGRHEG", inspiraRequest.ddate)); //Hora del egreso
            lParameters.Add(new OracleParameter("MOVEGRREG", "N")); //TODO: Remitido
            lParameters.Add(new OracleParameter("MOVEGRANU", "0")); //Egreso anulado
            lParameters.Add(new OracleParameter("MOVEGRRIN", "N")); //Remitido de otra institución
            lParameters.Add(new OracleParameter("MOVEGRFSO", inspiraRequest.ddate)); //Fecha de solicitud del servicio
            lParameters.Add(new OracleParameter("MOVEGRHSO", inspiraRequest.ddate)); //Hora de solicitud del servicio
            lParameters.Add(new OracleParameter("MOVEGRUAD", Tools.GetUser(inspiraRequest.suser))); //Usuario que hace la transacción
            lParameters.Add(new OracleParameter("MOVEGRFAD", DateTime.Now)); //Fecha y hora de grabación
            lParameters.Add(new OracleParameter("MOVEGRTIR", "N")); //Orden de servicio
            lParameters.Add(new OracleParameter("MOVEGREAD", this.parametroRip.scia)); //Estructura administrativa
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }


        /// <summary>
        /// Método para crear variables adicionales del ingreso en Servinte tabla AYMOVARS
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateEntryVars(InspiraCita inspiraRequest, ServintePatient servintePatient, Oracle oDAC)
        {
            StringBuilder squeryadditional1 = new StringBuilder();
            StringBuilder squeryadditional2 = new StringBuilder();
            if (string.IsNullOrEmpty(servintePatient.safiliation))
            {
                squeryadditional1.Append("(SELECT PAGTUS FROM INPAG, INTUS WHERE PAGEMP = '");
                squeryadditional1.Append(inspiraRequest.sagreement);
                squeryadditional1.Append("' AND PAGPLA = '");
                squeryadditional1.Append(inspiraRequest.splan);
                squeryadditional1.Append("' AND PAGTUS = TUSCOD FETCH NEXT 1 ROWS ONLY)");
                squeryadditional2.Append("(SELECT TUSREG FROM INPAG, INTUS WHERE PAGEMP = '");
                squeryadditional2.Append(inspiraRequest.sagreement);
                squeryadditional2.Append("' AND PAGPLA = '");
                squeryadditional2.Append(inspiraRequest.splan);
                squeryadditional2.Append("' AND PAGTUS = TUSCOD FETCH NEXT 1 ROWS ONLY)");
            }
            else
            {
                squeryadditional1.Append("(SELECT TUSREG FROM INTUS WHERE TUSCOD = '");
                squeryadditional1.Append(servintePatient.safiliation);
                squeryadditional1.Append("')");
                squeryadditional2.Append("(SELECT TUSADM FROM INTUS WHERE TUSCOD = '");
                squeryadditional2.Append(servintePatient.safiliation);
                squeryadditional2.Append("')");
            }
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYMOVARS (MOVARSFUE, MOVARSDOC, MOVARSAFI, MOVARSREG, MOVARSADM, MOVARSARS, MOVARSGRU, MOVARSANU, MOVARSEAD)");
            stringBuilder.Append(" VALUES (:MOVARSFUE, :MOVARSDOC, :MOVARSAFI,");
            stringBuilder.Append(squeryadditional1.ToString());
            stringBuilder.Append(", ");
            stringBuilder.Append(squeryadditional2.ToString());
            stringBuilder.Append(", :MOVARSARS, :MOVARSGRU, :MOVARSANU, :MOVARSEAD)");
            lParameters.Add(new OracleParameter("MOVARSFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("MOVARSDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("MOVARSAFI", Tools.GetMembershipLevel(servintePatient.slevel))); //Nivel de afiliado                        
            lParameters.Add(new OracleParameter("MOVARSARS", inspiraRequest.sagreement)); //Código ARS
            lParameters.Add(new OracleParameter("MOVARSGRU", DBNull.Value)); //Código ARS
            lParameters.Add(new OracleParameter("MOVARSANU", "0")); //Ingreso anulado
            lParameters.Add(new OracleParameter("MOVARSEAD", this.parametroRip.scia)); //Estructura administrativa
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
            squeryadditional2 = squeryadditional1 = null;
        }

        /// <summary>
        /// Método para crear el registro de la autorización en Servinte tabla AYMOVORD
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        /// <param name="bIsMultiple">Boolean indica si la transacción es sencilla o múltiple</param>
        private void CreateAuthorization(InspiraCita inspiraRequest, Oracle oDAC, bool bIsMultiple)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYMOVORD (MOVORDFUE, MOVORDDOC, MOVORDORD, MOVORDFUO, MOVORDDOO, MOVORDEST, MOVORDFEC, MOVORDVEN");
            stringBuilder.Append(", MOVORDHIN, MOVORDCAC, MOVORDSER, MOVORDTUT, MOVORDANT, MOVORDCIA, MOVORDSED, MOVORDEAD) VALUES (");
            stringBuilder.Append(":MOVORDFUE, :MOVORDDOC, :MOVORDORD, :MOVORDFUO, :MOVORDDOO, :MOVORDEST, :MOVORDFEC, :MOVORDVEN, :MOVORDHIN, :MOVORDCAC");
            stringBuilder.Append(", :MOVORDSER, :MOVORDTUT, :MOVORDANT, :MOVORDCIA, :MOVORDSED, :MOVORDEAD)");
            lParameters.Add(new OracleParameter("MOVORDFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("MOVORDDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("MOVORDORD", Tools.ReplaceChars(inspiraRequest.sauthorization).Replace(" ", "-"))); //Autorización
            if (bIsMultiple)
            {
                lParameters.Add(new OracleParameter("MOVORDFUO", this.parametroRip.ssource)); //Fuente de origen
                lParameters.Add(new OracleParameter("MOVORDDOO", this.ientry)); //Ingreso de origen
            }
            else
            {
                lParameters.Add(new OracleParameter("MOVORDFUO", DBNull.Value)); //Fuente de origen
                lParameters.Add(new OracleParameter("MOVORDDOO", DBNull.Value)); //Ingreso de origen
            }
            lParameters.Add(new OracleParameter("MOVORDEST", "A")); //Estado? A = Activo
            lParameters.Add(new OracleParameter("MOVORDFEC", inspiraRequest.ddate)); //Fecha de la autorización
            lParameters.Add(new OracleParameter("MOVORDVEN", inspiraRequest.ddate.AddMonths(1))); //Fecha de vencimiento de la autorización
            lParameters.Add(new OracleParameter("MOVORDHIN", "N")); //Habitación N para nuestro caso
            lParameters.Add(new OracleParameter("MOVORDCAC", "N")); //Cama acompañante N para nuestro caso
            lParameters.Add(new OracleParameter("MOVORDSER", "N")); //Servicio Enfermeria N para nuestro caso
            lParameters.Add(new OracleParameter("MOVORDTUT", "N")); //Tutela N para nuestro caso
            lParameters.Add(new OracleParameter("MOVORDANT", "N")); //Anticipo N para nuestro caso
            lParameters.Add(new OracleParameter("MOVORDCIA", "N")); //Cierre de la autorización N para nuestro caso
            lParameters.Add(new OracleParameter("MOVORDSED", this.parametroRip.scia)); //Sede
            lParameters.Add(new OracleParameter("MOVORDEAD", this.parametroRip.scia)); //Estructura administrativa
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para crear el detalle de la autorización en Servinte tabla AYORDDET
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateAuthorizationDetail(InspiraCita inspiraRequest, Oracle oDAC)
        {
            int iValue = this.GetSequenceNextValue("SQ_AYORDDET_SEC", oDAC);
            //int iValue = 1;
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYORDDET (ORDDETFUE, ORDDETDOC, ORDDETORD, ORDDETIND, ORDDETTIP, ORDDETCLA, ORDDETCER, ORDDETFEC");
            stringBuilder.Append(", ORDDETFVE, ORDDETEST, ORDDETUSU, ORDDETSEC, ORDDETEAD, ORDDETDE2, ORDDETDE1, ORDDETHOR) VALUES (:ORDDETFUE, :ORDDETDOC, :ORDDETORD, :ORDDETIND, :ORDDETTIP, :ORDDETCLA, :ORDDETCER, :ORDDETFEC");
            stringBuilder.Append(", :ORDDETFVE, :ORDDETEST, :ORDDETUSU, :ORDDETSEC, :ORDDETEAD, :ORDDETDE2, :ORDDETDE1, :ORDDETHOR)");
            lParameters.Add(new OracleParameter("ORDDETFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("ORDDETDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("ORDDETORD", Tools.ReplaceChars(inspiraRequest.sauthorization).Replace(" ", "-"))); //Autorización
            lParameters.Add(new OracleParameter("ORDDETIND", "P")); //Indicador de principal o secundaria P Para nuestro caso
            lParameters.Add(new OracleParameter("ORDDETTIP", "O")); //TODO: Tipo de orden O por defecto
            lParameters.Add(new OracleParameter("ORDDETCLA", "E")); //Clase de la orden: E por defecto
            lParameters.Add(new OracleParameter("ORDDETCER", inspiraRequest.sagreement)); //Responsable de la autorización (Empresa)
            lParameters.Add(new OracleParameter("ORDDETFEC", inspiraRequest.ddate)); //Fecha de la orden
            lParameters.Add(new OracleParameter("ORDDETFVE", inspiraRequest.ddate.AddMonths(1))); //Fecha de vencimiento de la orden
            lParameters.Add(new OracleParameter("ORDDETEST", "A")); //Estado de la autorización P:Pendiente R:Recibida A: Aprobada
            lParameters.Add(new OracleParameter("ORDDETUSU", Tools.GetUser(inspiraRequest.suser))); //Estado de la autorización P:Pendiente R:Recibida A: Aprobada
            lParameters.Add(new OracleParameter("ORDDETSEC", iValue)); //Secuencia de la orden
            lParameters.Add(new OracleParameter("ORDDETEAD", this.parametroRip.scia)); //Estructura administrativa
            lParameters.Add(new OracleParameter("ORDDETDE2", inspiraRequest.sname)); //Descripción 2 de la autorización
            lParameters.Add(new OracleParameter("ORDDETDE1", inspiraRequest.ientry.ToString())); //Ingreso asociado
            lParameters.Add(new OracleParameter("ORDDETHOR", inspiraRequest.ddate)); //Hora de la cita (se envía la fecha de la cita)
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inspiraRequest"></param>
        /// <param name="oDAC"></param>
        private void CreateEntryAuditLog(InspiraCita inspiraRequest, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYMOVOBS (MOVOBSPRO, MOVOBSFUE, MOVOBSDOC, MOVOBSLIN, MOVOBSOBS, MOVOBSOPE, MOVOBSUSU, MOVOBSFEC, MOVOBSEAD)");
            stringBuilder.Append(" VALUES (:MOVOBSPRO, :MOVOBSFUE, :MOVOBSDOC, :MOVOBSLIN, :MOVOBSOBS, :MOVOBSOPE, :MOVOBSUSU, :MOVOBSFEC, :MOVOBSEAD)");
            lParameters.Add(new OracleParameter("MOVOBSPRO", "cinaut")); //Programa
            lParameters.Add(new OracleParameter("MOVOBSFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("MOVOBSDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("MOVOBSLIN", 1)); //Línea
            lParameters.Add(new OracleParameter("MOVOBSOBS", "AUTORIZACIÓN GRABADA DESDE INSPIRA")); //Observaciones
            lParameters.Add(new OracleParameter("MOVOBSOPE", "G")); //Operación G: Grabar
            lParameters.Add(new OracleParameter("MOVOBSUSU", Tools.GetUser(inspiraRequest.suser))); //Usuario
            lParameters.Add(new OracleParameter("MOVOBSFEC", DateTime.Now)); //Fecha
            lParameters.Add(new OracleParameter("MOVOBSEAD", this.parametroRip.scia)); //Unidad Administrativa
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para crear el cargo en la tabla AYCAR
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="oDAC">Objeto conexión a base de datos</param>
        /// <param name="bismultiple">Boolean indica si la transacción es para un cargo múltiple</param>
        private void CreateCharge(InspiraCita inspiraRequest, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYCAR (CARFUE, CARDOC, CARANO, CARMES, CARFEC, CARFUO, CARDOO, CARANU, CAREAD, CAREAO)");
            stringBuilder.Append(" VALUES (:CARFUE, :CARDOC, :CARANO, :CARMES, :CARFEC, :CARFUO, :CARDOO, :CARANU, :CAREAD, :CAREAO)");
            lParameters.Add(new OracleParameter("CARFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("CARDOC", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("CARANO", inspiraRequest.ddate.Year)); //Año
            lParameters.Add(new OracleParameter("CARMES", inspiraRequest.ddate.ToString("MM"))); //Mes
            lParameters.Add(new OracleParameter("CARFEC", inspiraRequest.ddate)); //Fecha
            lParameters.Add(new OracleParameter("CARFUO", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("CARDOO", (this.ientry != 0) ? this.ientry : this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("CARANU", "0")); //Anulación
            lParameters.Add(new OracleParameter("CAREAD", this.parametroRip.scia)); //Estructura administrativa
            lParameters.Add(new OracleParameter("CAREAO", this.parametroRip.scia)); //Estructira administrativa origen
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para crear el cargo adicional de pérdidas y ganancias, tabla AYCARDET, solo funciona para los cargos paquetizados plantilla programas
        /// </summary>
        /// <param name="inspiraRequest">Objeto InspiraCita</param>
        /// <param name="i">Entero número de línea</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateChargeForLAW(InspiraCita inspiraRequest, int i, Oracle oDAC, decimal itotalappointment, decimal itotalpackage, ServintePatient servintePatient)
        {
            decimal totalservices = (itotalappointment == 0) ? this.GetEntryValue(inspiraRequest.lservices) : itotalappointment;
            decimal dValue = itotalpackage - totalservices;
            int iCharge = this.GetSequenceNextValue("SQ_AYCARDET_REG", oDAC);
            char cBilleable = 'S';
            string sConcept = string.Empty;
            string sthird = string.IsNullOrEmpty(inspiraRequest.sthird) ? "73135051" : inspiraRequest.sthird;
            if (inspiraRequest.stemplate.StartsWith("PAIR"))
            {
                sConcept = "PGAR";
            }
            else if (inspiraRequest.stemplate.StartsWith("PAS") && inspiraRequest.sunit == "1100")
            {
                sConcept = "PGAA";
            }
            else if (inspiraRequest.stemplate.StartsWith("PAS") && inspiraRequest.sunit == "1200")
            {
                sConcept = "PGAI";
            }            
            else if (inspiraRequest.stemplate.StartsWith("PVAS")  && inspiraRequest.sunit == "1100")
            {
                sConcept = "PGMV";
            }
            else if (inspiraRequest.stemplate.StartsWith("PVAS") && inspiraRequest.sunit == "1200")
            {
                sConcept = "PGHP";
            }
            else if (inspiraRequest.stemplate.StartsWith("PVM") && inspiraRequest.sunit == "1100")
            {
                sConcept = "PGVM";
            }
            else if (inspiraRequest.stemplate.StartsWith("PVM") && inspiraRequest.sunit == "1200")
            {
                sConcept = "PVMP";
            }
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("INSERT INTO AYCARDET (CARDETFUE, CARDETDOC, CARDETITE, CARDETLIN, CARDETANO, CARDETMES, CARDETFEC, CARDETFUO, CARDETDOO, CARDETNUM");
            stringBuilder.Append(", CARDETTAR, CARDETTSE, CARDETCON, CARDETCOD");
            stringBuilder.Append(", CARDETCCO, CARDETNIT, CARDETCAN, CARDETVUN, CARDETFES, CARDETTOT, CARDETREC, CARDETVRE, CARDETVEX, CARDETVFA, CARDETDFA, CARDETDES");
            stringBuilder.Append(", CARDETTIP, CARDETFAC, CARDETORI, CARDETANU, CARDETOCE, CARDETUAD, CARDETFAD, CARDETEAD, CARDETEAO, CARDETUFU, CARDETURG, CARDETTRM, CARDETMON, CARDETPIV, CARDETREG)");
            stringBuilder.Append(" VALUES (:CARDETFUE, :CARDETDOC, :CARDETITE, :CARDETLIN, :CARDETANO, :CARDETMES, :CARDETFEC, :CARDETFUO, :CARDETDOO, :CARDETNUM");
            stringBuilder.Append(", (SELECT EMPEADTAR FROM INEMPEAD WHERE EMPEADIND = 'S' AND EMPEADCOD = :CARDETEMP AND EMPEADEAD = :CARDETEAD FETCH FIRST 1 ROW ONLY), :CARDETTSE, :CARDETCON, :CARDETCOD");
            stringBuilder.Append(", (SELECT PROTARCCO FROM INPROTAR WHERE PROTARPRO = :CARDETCOD AND PROTARTAR = :CARDETTAR FETCH FIRST 1 ROW ONLY)");
            stringBuilder.Append(", :CARDETNIT, :CARDETCAN, :CARDETVUN, :CARDETFES, :CARDETTOT, :CARDETREC, :CARDETVRE, :CARDETVEX, :CARDETVFA, :CARDETDFA");
            stringBuilder.Append(", :CARDETDES, :CARDETTIP, :CARDETFAC, :CARDETORI, :CARDETANU, (SELECT PROTARCCO FROM INPROTAR WHERE PROTARPRO = :CARDETCOD AND PROTARTAR = :CARDETTAR FETCH FIRST 1 ROW ONLY)");
            stringBuilder.Append(", :CARDETUAD, :CARDETFAD, :CARDETEAD, :CARDETEAO, :CARDETUFU, :CARDETURG, :CARDETTRM, :CARDETMON, :CARDETPIV, :CARDETREG)");
            lParameters.Add(new OracleParameter("CARDETFUE", OracleDbType.Varchar2, this.parametroRip.ssource, ParameterDirection.Input)); //Fuente
            lParameters.Add(new OracleParameter("CARDETDOC", OracleDbType.Int32, this.parametroRip.ientry, ParameterDirection.Input)); //Número de ingreso
            lParameters.Add(new OracleParameter("CARDETITE", OracleDbType.Int32, 0, ParameterDirection.Input)); //Número de item
            lParameters.Add(new OracleParameter("CARDETLIN", OracleDbType.Int32, (i + 1), ParameterDirection.Input)); //Número de item
            lParameters.Add(new OracleParameter("CARDETANO", inspiraRequest.ddate.Year)); //Año
            lParameters.Add(new OracleParameter("CARDETMES", inspiraRequest.ddate.ToString("MM"))); //Mes
            lParameters.Add(new OracleParameter("CARDETFEC", inspiraRequest.ddate)); //Fecha del cargo            
            lParameters.Add(new OracleParameter("CARDETFUO", this.parametroRip.ssource)); //Fuente origen del cargo
            lParameters.Add(new OracleParameter("CARDETDOO", (this.ientry != 0) ? this.ientry : this.parametroRip.ientry)); //Ingreso origen del cargocardetnum
            lParameters.Add(new OracleParameter("CARDETNUM", "0")); //Número de ingreso del paciente
            lParameters.Add(new OracleParameter("CARDETEMP", inspiraRequest.sagreement)); //Código de la tarifa  
            lParameters.Add(new OracleParameter("CARDETTAR", inspiraRequest.srate)); //Código de la tarifa
            //lParameters.Add(new OracleParameter("CONCOD", inspiraRequest.lservices[0].sconcept)); //Código del concepto       
            lParameters.Add(new OracleParameter("CARDETTSE", inspiraRequest.sattentiontype)); //Tipo de servicio
            lParameters.Add(new OracleParameter("CARDETDFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
            lParameters.Add(new OracleParameter("CARDETDES", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor descuento
            lParameters.Add(new OracleParameter("CARDETCON", sConcept)); //Código del concepto
            lParameters.Add(new OracleParameter("CARDETCOD", inspiraRequest.stemplate)); //Código del servicio
            //lParameters.Add(new OracleParameter("CARDETCCO", serviceRequest.scostcenter)); //Código del centro de costos
            lParameters.Add(new OracleParameter("CARDETNIT", sthird)); //Código del tercero
            lParameters.Add(new OracleParameter("CARDETCAN", OracleDbType.Decimal, 1, ParameterDirection.Input)); //Cantidad
            lParameters.Add(new OracleParameter("CARDETVUN", OracleDbType.Decimal, dValue, ParameterDirection.Input)); //Valor unitario
            lParameters.Add(new OracleParameter("CARDETFES", "N")); // Realizo en Nocturnos (N) y/o festivos(F)
            lParameters.Add(new OracleParameter("CARDETTOT", OracleDbType.Decimal, dValue, ParameterDirection.Input)); //Valor total            
            lParameters.Add(new OracleParameter("CARDETREC", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor recargo            
            lParameters.Add(new OracleParameter("CARDETVRE", OracleDbType.Decimal, dValue, ParameterDirection.Input)); //Valor Reconocido
            lParameters.Add(new OracleParameter("CARDETVEX", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor excedente
            lParameters.Add(new OracleParameter("CARDETVFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor facturado                        
            lParameters.Add(new OracleParameter("CARDETTIP", "R")); //Tipo de resp.(R=Reconicido,E=Excedente)
            lParameters.Add(new OracleParameter("CARDETFAC", cBilleable)); //Servicio facturable S o N
            lParameters.Add(new OracleParameter("CARDETORI", "AN")); //Origen del cargo
            lParameters.Add(new OracleParameter("CARDETANU", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Estado del cargo
            //lParameters.Add(new OracleParameter("CARDETOCE", serviceRequest.scostcenter)); //Centro de costos origen
            lParameters.Add(new OracleParameter("CARDETUAD", Tools.GetUser(inspiraRequest.suser))); //Usuario auditoría
            lParameters.Add(new OracleParameter("CARDETFAD", DateTime.Now)); //Fecha auditoría            
            lParameters.Add(new OracleParameter("CARDETEAD", this.parametroRip.scia)); //Unidad administrativa
            lParameters.Add(new OracleParameter("CARDETEAO", this.parametroRip.scia)); //Unidad administrativa origen
            lParameters.Add(new OracleParameter("CARDETUFU", inspiraRequest.sunit)); //Unidad funcional
            lParameters.Add(new OracleParameter("CARDETURG", "N")); //Ingreso de urgencias
            lParameters.Add(new OracleParameter("CARDETTRM", OracleDbType.Decimal, 0, ParameterDirection.Input)); //TRM del movimiento
            lParameters.Add(new OracleParameter("CARDETMON", "MF")); //Moneda del movimiento (MF para nuestro caso)
            lParameters.Add(new OracleParameter("CARDETREG", iCharge)); //Número del cargo
            lParameters.Add(new OracleParameter("CARDETPIV", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            this.CreatePackageDetail(inspiraRequest, i, iCharge, oDAC);
            EntryResponse entryResponse = new EntryResponse()
            {
                ientry = this.parametroRip.ientry,
                ientrysource = (this.ientry == 0) ? this.ientry : this.parametroRip.ientry,
                ddate = inspiraRequest.ddate.ToString("yyyy-MM-dd"),
                sservice = inspiraRequest.stemplate,
                sdocument = servintePatient.sdocument,
                sdocumenttype = servintePatient.sdocumenttype,
                splan = inspiraRequest.splan,
                srate = inspiraRequest.srate,
                iqty = 1,
                icharge = iCharge,
                sconcept = sConcept,
                stype = this.stype,
                dvalue = Convert.ToInt32(itotalpackage),
                iline = (i + 1),
                sunit = inspiraRequest.sunit,
                sagreement = inspiraRequest.sagreement,
                stemplate = inspiraRequest.stemplate,
                sappointment = inspiraRequest.sappointment,
                sservicegroup = inspiraRequest.sservicegroup,
                sthird = inspiraRequest.sthird,
                sauthorization = inspiraRequest.sauthorization,               
                sservicename = "PYG",
            };
            this.CreateSingleProcedureRip(inspiraRequest, entryResponse, oDAC);           
        }

        private string GetDoctorCode(string snit)
        {
            Generic generic = this.ldoctors.FirstOrDefault(x => x.sname == snit);
            return generic != null ? generic.scode : "ARAM";
        }

        private void GetAllDoctors()
        {
            string query = "SELECT MEDCOD, MEDCED FROM INMED WHERE MEDACT = 'S'";
            DataTable dataTable = new DataTable();
            Generic generic = null;
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                dataTable = oracle.GetDataTable(query, null);
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    generic = new Generic()
                    {
                        scode = dataRow["MEDCOD"].ToString(),
                        sname = dataRow["MEDCED"].ToString(),
                    };
                    ldoctors.Add(generic);
                }
            }
            generic = null;
            dataTable.Dispose();
            dataTable = null;
        }

        /// <summary>
        /// Método para crear una línea de detalle de cargo en la tabla AYCARDET
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira Request</param>
        /// <param name="serviceRequest">Objeto Service Request</param>
        /// <param name="servintePatient">Objeto Paciente Inspira</param>
        /// <param name="sId">String id del formulario</param>        
        /// <param name="i">Entero número de línea</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateSingleChargeDetail(InspiraCita inspiraRequest, ServiceRequest serviceRequest, ServintePatient servintePatient, string sId, int i, Oracle oDAC, bool bisservice = true)
        {
            if (serviceRequest.bisprocedure && serviceRequest.sservicename.Contains("JUNTA"))
            {
                serviceRequest.bisprocedure = false;
            }
            int iCharge = this.GetSequenceNextValue("SQ_AYCARDET_REG", oDAC);
            decimal dValue = this.GetProductValue(serviceRequest);
            if (dValue == 0) throw new ApplicationException("El valor del servicio es 0 por lo cual no es posible crear el cargo. Favor revisar " +
                "el centro de costos " + serviceRequest.scostcenter + ", el concepto " + serviceRequest.sconcept + ", el producto " + serviceRequest.sservice + " y la tarifa " + serviceRequest.srate + " para el paciente: " 
                + servintePatient.sdocument + " con el plan: " + inspiraRequest.splan);
            char cBilleable = (serviceRequest.bbilleable) ? 'S' : 'N';
            EntryResponse entryResponse = null;
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder();
            string stable = string.Empty;
            string srate = string.Empty;
            string squerycomplement = string.Empty;
            if ((inspiraRequest.sratename.Contains("PARTICULAR") || inspiraRequest.sratename.Contains("REPROCESOS")
                || inspiraRequest.sratename.Contains("CORTESIA") || inspiraRequest.sratename.Contains("SUBSIDIADO")) && inspiraRequest.sagreement == "30")
            {
                srate = inspiraRequest.srate;
                squerycomplement = ":CARDETTAR";
            }
            else
            {
                srate = inspiraRequest.sagreement;
                squerycomplement = "(SELECT EMPEADTAR FROM INEMPEAD WHERE EMPEADIND = 'S' AND EMPEADCOD = :CARDETTAR AND EMPEADEAD = :CARDETEAD)";
            }
            stringBuilder.Append("INSERT INTO AYCARDET (CARDETFUE, CARDETDOC, CARDETITE, CARDETLIN, CARDETANO, CARDETMES, CARDETFEC, CARDETFUO, CARDETDOO, CARDETNUM");
            stringBuilder.Append(", CARDETTAR, CARDETTSE, CARDETCON, CARDETCOD");
            stringBuilder.Append(", CARDETCCO, CARDETNIT, CARDETCAN, CARDETVUN, CARDETFES, CARDETTOT, CARDETREC, CARDETVRE, CARDETVEX, CARDETVFA, CARDETDFA, CARDETDES");
            stringBuilder.Append(", CARDETTIP, CARDETFAC, CARDETORI, CARDETANU, CARDETOCE, CARDETUAD, CARDETFAD, CARDETEAD, CARDETEAO, CARDETUFU, CARDETURG, CARDETTRM, CARDETMON, CARDETPIV, CARDETREG)");
            stringBuilder.Append(" VALUES (:CARDETFUE, :CARDETDOC, :CARDETITE, :CARDETLIN, :CARDETANO, :CARDETMES, :CARDETFEC, :CARDETFUO, :CARDETDOO, :CARDETNUM");
            stringBuilder.Append(", " + squerycomplement + ", :CARDETTSE, :CARDETCON, :CARDETCOD, :CARDETCCO");
            stringBuilder.Append(", :CARDETNIT, :CARDETCAN, :CARDETVUN, :CARDETFES, :CARDETTOT, :CARDETREC, :CARDETVRE, :CARDETVEX, :CARDETVFA, :CARDETDFA");
            stringBuilder.Append(", :CARDETDES, :CARDETTIP, :CARDETFAC, :CARDETORI, :CARDETANU, :CARDETOCE, :CARDETUAD, :CARDETFAD, :CARDETEAD, :CARDETEAO, :CARDETUFU, :CARDETURG, :CARDETTRM, :CARDETMON, :CARDETPIV, :CARDETREG)");
            lParameters.Add(new OracleParameter("CARDETFUE", OracleDbType.Varchar2, this.parametroRip.ssource, ParameterDirection.Input)); //Fuente
            lParameters.Add(new OracleParameter("CARDETDOC", OracleDbType.Int32, this.parametroRip.ientry, ParameterDirection.Input)); //Número de ingreso
            lParameters.Add(new OracleParameter("CARDETITE", OracleDbType.Int32, 0, ParameterDirection.Input)); //Número de item
            lParameters.Add(new OracleParameter("CARDETLIN", OracleDbType.Int32, (i + 1), ParameterDirection.Input)); //Número de item
            lParameters.Add(new OracleParameter("CARDETANO", inspiraRequest.ddate.Year)); //Año
            lParameters.Add(new OracleParameter("CARDETMES", inspiraRequest.ddate.ToString("MM"))); //Mes
            lParameters.Add(new OracleParameter("CARDETFEC", inspiraRequest.ddate)); //Fecha del cargo            
            lParameters.Add(new OracleParameter("CARDETFUO", this.parametroRip.ssource)); //Fuente origen del cargo
            lParameters.Add(new OracleParameter("CARDETDOO", (this.ientry != 0) ? this.ientry : this.parametroRip.ientry)); //Ingreso origen del cargocardetnum
            lParameters.Add(new OracleParameter("CARDETNUM", "0")); //Número de ingreso del paciente
            lParameters.Add(new OracleParameter("CARDETTAR", srate)); //Código de la tarifa                    
            lParameters.Add(new OracleParameter("CARDETTSE", inspiraRequest.sattentiontype)); //Tipo de servicio
            lParameters.Add(new OracleParameter("CARDETDFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
            lParameters.Add(new OracleParameter("CARDETDES", OracleDbType.Decimal, serviceRequest.idiscount, ParameterDirection.Input)); //Valor descuento
            lParameters.Add(new OracleParameter("CARDETCON", serviceRequest.sconcept)); //Código del concepto
            lParameters.Add(new OracleParameter("CARDETCOD", serviceRequest.sservice)); //Código del servicio
            lParameters.Add(new OracleParameter("CARDETCCO", serviceRequest.scostcenter)); //Código del centro de costos
            lParameters.Add(new OracleParameter("CARDETNIT", this.GetThird(serviceRequest.sconcept, inspiraRequest.sthird))); //Código del tercero
            lParameters.Add(new OracleParameter("CARDETCAN", OracleDbType.Decimal, serviceRequest.iqty, ParameterDirection.Input)); //Cantidad
            lParameters.Add(new OracleParameter("CARDETVUN", OracleDbType.Decimal, (dValue - serviceRequest.idiscount), ParameterDirection.Input)); //Valor unitario
            lParameters.Add(new OracleParameter("CARDETFES", "N")); // Realizo en Nocturnos (N) y/o festivos(F)
            lParameters.Add(new OracleParameter("CARDETTOT", OracleDbType.Decimal, (serviceRequest.iqty * dValue), ParameterDirection.Input)); //Valor total            
            lParameters.Add(new OracleParameter("CARDETREC", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor recargo            
            lParameters.Add(new OracleParameter("CARDETVRE", OracleDbType.Decimal, (serviceRequest.iqty * dValue), ParameterDirection.Input)); //Valor Reconocido
            lParameters.Add(new OracleParameter("CARDETVEX", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor excedente
            lParameters.Add(new OracleParameter("CARDETVFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor facturado                        
            lParameters.Add(new OracleParameter("CARDETTIP", "R")); //Tipo de resp.(R=Reconicido,E=Excedente)
            lParameters.Add(new OracleParameter("CARDETFAC", cBilleable)); //Servicio facturable S o N
            lParameters.Add(new OracleParameter("CARDETORI", "AY")); //Origen del cargo
            lParameters.Add(new OracleParameter("CARDETANU", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Estado del cargo
            lParameters.Add(new OracleParameter("CARDETOCE", serviceRequest.scostcenter)); //Centro de costos origen
            lParameters.Add(new OracleParameter("CARDETUAD", Tools.GetUser(inspiraRequest.suser))); //Usuario auditoría
            lParameters.Add(new OracleParameter("CARDETFAD", DateTime.Now)); //Fecha auditoría            
            lParameters.Add(new OracleParameter("CARDETEAD", this.parametroRip.scia)); //Unidad administrativa
            lParameters.Add(new OracleParameter("CARDETEAO", this.parametroRip.scia)); //Unidad administrativa origen
            lParameters.Add(new OracleParameter("CARDETUFU", inspiraRequest.sunit)); //Unidad funcional
            lParameters.Add(new OracleParameter("CARDETURG", "N")); //Ingreso de urgencias
            lParameters.Add(new OracleParameter("CARDETTRM", OracleDbType.Decimal, 0, ParameterDirection.Input)); //TRM del movimiento
            lParameters.Add(new OracleParameter("CARDETMON", "MF")); //Moneda del movimiento (MF para nuestro caso)
            lParameters.Add(new OracleParameter("CARDETREG", iCharge)); //Moneda del movimiento (MF para nuestro caso)*/
            lParameters.Add(new OracleParameter("CARDETPIV", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            entryResponse = new EntryResponse()
            {
                idPaciente = servintePatient.idPaciente,
                idCargo = serviceRequest.idCargo,
                ientry = this.parametroRip.ientry,
                ientrysource = (this.ientry == 0) ? inspiraRequest.ientry : this.ientry,
                sid = sId,
                sdocument = servintePatient.sdocument,
                sdocumenttype = servintePatient.sdocumenttype,
                ddate = inspiraRequest.ddate.ToString("yyyy-MM-dd"),
                scostcenter = serviceRequest.scostcenter,
                sservice = serviceRequest.sservice,
                splan = inspiraRequest.splan,
                srate = inspiraRequest.srate,
                ipatient = servintePatient.iid,
                iqty = serviceRequest.iqty,
                icharge = iCharge,
                sconcept = serviceRequest.sconcept,
                sauthorization = serviceRequest.sauthorization,
                stype = this.stype,
                sservicename = serviceRequest.sservicename,
                dvalue = Convert.ToInt32(serviceRequest.iqty * serviceRequest.ivalue),
                iline = (i + 1),
                sunit = inspiraRequest.sunit,
                sagreement = inspiraRequest.sagreement,
                stemplate = inspiraRequest.stemplate,
                sappointment = inspiraRequest.sappointment,
                sservicegroup = inspiraRequest.sservicegroup,
                sthird = inspiraRequest.sthird,                
            };
            if (serviceRequest.idiscount != 0)
            {
                entryResponse.iupload = Convert.ToInt32(serviceRequest.idiscount);
            }
            if (!string.IsNullOrEmpty(serviceRequest.idCargo))
            {
                if (Tools.IsNumeric(serviceRequest.idCargo))
                {
                    entryResponse.iid = Convert.ToInt32(serviceRequest.idCargo);
                }                
            }
            if (this.stype == "Servicio" || this.stype == "Investigacion")
            {
                entryResponse.sappointment = sId;
            }
            else if (this.stype == "Educacion")
            {
                entryResponse.sevent = sId;
            }
            if (bisservice)
            {
                if (!serviceRequest.bisprocedure && serviceRequest.bbilleable && inspiraRequest.sattentiontype != "9")
                {
                    this.CreateSingleAssesmentRip(inspiraRequest, entryResponse, oDAC);
                    stable = "mscne";
                }
                else if (serviceRequest.bisprocedure && serviceRequest.bbilleable && inspiraRequest.sattentiontype != "9")
                {
                    this.CreateSingleProcedureRip(inspiraRequest, entryResponse, oDAC);
                    stable = "mspro";
                }
                this.CreateRipLog(inspiraRequest, entryResponse, oDAC, stable);
            }
            if (this.stype.Contains("Plantilla Programas"))
            {
                this.CreatePackageDetail(inspiraRequest, i, iCharge, oDAC);
            }
            this.lresponse.Add(entryResponse);
            lParameters = null;
            stringBuilder = null;
        }

        /// <summary>
        /// Método para crear el detalle de los cargos en la tabla de Servinte AYCARDET por fuera de la integración de Inspira
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inpsira Cita</param>
        /// <param name="serviceRequest">Objeto Servicio</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        /// <param name="i">Entero número de línea</param>
        /// <param name="servintePatient">Objeto paciente Servinte</param>
        /// <param name="bcreaterips">Boolean que indica si se deben crear los RIPS</param>
        private void CreateChargeDetail(InspiraCita inspiraRequest, ServiceRequest serviceRequest, Oracle oDAC, int i, ServintePatient servintePatient, bool bcreaterips = true)
        {
            int iCharge = this.GetSequenceNextValue("SQ_AYCARDET_REG", oDAC);
            char cBilleable = (serviceRequest.bbilleable) ? 'S' : 'N';
            EntryResponse entryResponse = null;
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder();
            string stable = string.Empty;
            string srate = string.Empty;
            string squerycomplement = string.Empty;
            if ((inspiraRequest.sratename.Contains("PARTICULAR") || inspiraRequest.sratename.Contains("REPROCESOS")
                || inspiraRequest.sratename.Contains("CORTESIA") || inspiraRequest.sratename.Contains("SUBSIDIADO")) && inspiraRequest.sagreement == "30")
            {
                srate = inspiraRequest.srate;
                squerycomplement = ":CARDETTAR";
            }
            else
            {
                srate = inspiraRequest.sagreement;
                squerycomplement = "(SELECT EMPEADTAR FROM INEMPEAD WHERE EMPEADIND = 'S' AND EMPEADCOD = :CARDETTAR AND EMPEADEAD = :CARDETEAD)";
            }
            stringBuilder.Append("INSERT INTO AYCARDET (CARDETFUE, CARDETDOC, CARDETITE, CARDETLIN, CARDETANO, CARDETMES, CARDETFEC, CARDETFUO, CARDETDOO, CARDETNUM");
            stringBuilder.Append(", CARDETTAR, CARDETTSE, CARDETCON, CARDETCOD");
            stringBuilder.Append(", CARDETCCO, CARDETNIT, CARDETCAN, CARDETVUN, CARDETFES, CARDETTOT, CARDETREC, CARDETVRE, CARDETVEX, CARDETVFA, CARDETDFA, CARDETDES");
            stringBuilder.Append(", CARDETTIP, CARDETFAC, CARDETORI, CARDETANU, CARDETOCE, CARDETUAD, CARDETFAD, CARDETEAD, CARDETEAO, CARDETUFU, CARDETURG, CARDETTRM, CARDETMON, CARDETPIV, CARDETREG)");
            stringBuilder.Append(" VALUES (:CARDETFUE, :CARDETDOC, :CARDETITE, :CARDETLIN, :CARDETANO, :CARDETMES, :CARDETFEC, :CARDETFUO, :CARDETDOO, :CARDETNUM");
            stringBuilder.Append(", " + squerycomplement + ", :CARDETTSE, :CARDETCON, :CARDETCOD, :CARDETCCO");
            stringBuilder.Append(", :CARDETNIT, :CARDETCAN, :CARDETVUN, :CARDETFES, :CARDETTOT, :CARDETREC, :CARDETVRE, :CARDETVEX, :CARDETVFA, :CARDETDFA");
            stringBuilder.Append(", :CARDETDES, :CARDETTIP, :CARDETFAC, :CARDETORI, :CARDETANU, :CARDETOCE, :CARDETUAD, :CARDETFAD, :CARDETEAD, :CARDETEAO, :CARDETUFU, :CARDETURG, :CARDETTRM, :CARDETMON, :CARDETPIV, :CARDETREG)");
            lParameters.Add(new OracleParameter("CARDETFUE", OracleDbType.Varchar2, this.parametroRip.ssource, ParameterDirection.Input)); //Fuente
            lParameters.Add(new OracleParameter("CARDETDOC", OracleDbType.Int32, this.parametroRip.ientry, ParameterDirection.Input)); //Número de ingreso
            lParameters.Add(new OracleParameter("CARDETITE", OracleDbType.Int32, 0, ParameterDirection.Input)); //Número de item
            lParameters.Add(new OracleParameter("CARDETLIN", OracleDbType.Int32, (i + 1), ParameterDirection.Input)); //Número de item
            lParameters.Add(new OracleParameter("CARDETANO", inspiraRequest.ddate.Year)); //Año
            lParameters.Add(new OracleParameter("CARDETMES", inspiraRequest.ddate.ToString("MM"))); //Mes
            lParameters.Add(new OracleParameter("CARDETFEC", inspiraRequest.ddate)); //Fecha del cargo            
            lParameters.Add(new OracleParameter("CARDETFUO", this.parametroRip.ssource)); //Fuente origen del cargo
            lParameters.Add(new OracleParameter("CARDETDOO", (this.ientry != 0) ? this.ientry : this.parametroRip.ientry)); //Ingreso origen del cargocardetnum
            lParameters.Add(new OracleParameter("CARDETNUM", "0")); //Número de ingreso del paciente
            lParameters.Add(new OracleParameter("CARDETTAR", srate)); //Código de la tarifa                    
            lParameters.Add(new OracleParameter("CARDETTSE", inspiraRequest.sattentiontype)); //Tipo de servicio
            lParameters.Add(new OracleParameter("CARDETDFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
            lParameters.Add(new OracleParameter("CARDETDES", OracleDbType.Decimal, serviceRequest.idiscount, ParameterDirection.Input)); //Valor descuento
            lParameters.Add(new OracleParameter("CARDETCON", serviceRequest.sconcept)); //Código del concepto
            lParameters.Add(new OracleParameter("CARDETCOD", serviceRequest.sservice)); //Código del servicio
            lParameters.Add(new OracleParameter("CARDETCCO", serviceRequest.scostcenter)); //Código del centro de costos
            lParameters.Add(new OracleParameter("CARDETNIT", this.GetThird(serviceRequest.sconcept, inspiraRequest.sthird))); //Código del tercero
            lParameters.Add(new OracleParameter("CARDETCAN", OracleDbType.Decimal, serviceRequest.iqty, ParameterDirection.Input)); //Cantidad
            lParameters.Add(new OracleParameter("CARDETVUN", OracleDbType.Decimal, serviceRequest.ivalue, ParameterDirection.Input)); //Valor unitario
            lParameters.Add(new OracleParameter("CARDETFES", "N")); // Realizo en Nocturnos (N) y/o festivos(F)
            lParameters.Add(new OracleParameter("CARDETTOT", OracleDbType.Decimal, (serviceRequest.iqty * serviceRequest.ivalue), ParameterDirection.Input)); //Valor total            
            lParameters.Add(new OracleParameter("CARDETREC", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor recargo            
            lParameters.Add(new OracleParameter("CARDETVRE", OracleDbType.Decimal, (serviceRequest.iqty * serviceRequest.ivalue), ParameterDirection.Input)); //Valor Reconocido
            lParameters.Add(new OracleParameter("CARDETVEX", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor excedente
            lParameters.Add(new OracleParameter("CARDETVFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor facturado                        
            lParameters.Add(new OracleParameter("CARDETTIP", "R")); //Tipo de resp.(R=Reconicido,E=Excedente)
            lParameters.Add(new OracleParameter("CARDETFAC", cBilleable)); //Servicio facturable S o N
            lParameters.Add(new OracleParameter("CARDETORI", "AY")); //Origen del cargo
            lParameters.Add(new OracleParameter("CARDETANU", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Estado del cargo
            lParameters.Add(new OracleParameter("CARDETOCE", serviceRequest.scostcenter)); //Centro de costos origen
            lParameters.Add(new OracleParameter("CARDETUAD", Tools.GetUser(inspiraRequest.suser))); //Usuario auditoría
            lParameters.Add(new OracleParameter("CARDETFAD", DateTime.Now)); //Fecha auditoría            
            lParameters.Add(new OracleParameter("CARDETEAD", this.parametroRip.scia)); //Unidad administrativa
            lParameters.Add(new OracleParameter("CARDETEAO", this.parametroRip.scia)); //Unidad administrativa origen
            lParameters.Add(new OracleParameter("CARDETUFU", inspiraRequest.sunit)); //Unidad funcional
            lParameters.Add(new OracleParameter("CARDETURG", "N")); //Ingreso de urgencias
            lParameters.Add(new OracleParameter("CARDETTRM", OracleDbType.Decimal, 0, ParameterDirection.Input)); //TRM del movimiento
            lParameters.Add(new OracleParameter("CARDETMON", "MF")); //Moneda del movimiento (MF para nuestro caso)
            lParameters.Add(new OracleParameter("CARDETREG", iCharge)); //Moneda del movimiento (MF para nuestro caso)*/
            lParameters.Add(new OracleParameter("CARDETPIV", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            entryResponse = new EntryResponse()
            {
                idCargo = serviceRequest.idCargo,
                ientry = this.parametroRip.ientry,
                ientrysource = (this.ientry == 0) ? inspiraRequest.ientry : this.ientry,
                ddate = inspiraRequest.ddate.ToString("yyyy-MM-dd"),
                scostcenter = serviceRequest.scostcenter,
                sservice = serviceRequest.sservice,
                splan = inspiraRequest.splan,
                srate = inspiraRequest.srate,
                iqty = serviceRequest.iqty,
                icharge = iCharge,
                sconcept = serviceRequest.sconcept,
                sauthorization = serviceRequest.sauthorization,
                sservicename = serviceRequest.sservicename,
                dvalue = Convert.ToInt32(serviceRequest.iqty * serviceRequest.ivalue),
                iline = (i + 1),
                sagreement = inspiraRequest.sagreement,
                sdocument = servintePatient.sdocument,
                sdocumenttype = servintePatient.sdocument,
                ipatient = servintePatient.iid,
                stype = this.stype,
                sunit = inspiraRequest.sunit,
                sappointment = inspiraRequest.sappointment,
                sservicegroup = inspiraRequest.sservicegroup,
                sthird = inspiraRequest.sthird,                                
            };
            if (serviceRequest.idiscount != 0)
            {
                entryResponse.iupload = Convert.ToInt32(serviceRequest.idiscount);
            }
            if (this.lresponse != null)
            {
                this.lresponse.Add(entryResponse);
            }
            if (bcreaterips)
            {
                if (!serviceRequest.bisprocedure && serviceRequest.bbilleable && inspiraRequest.sattentiontype != "9")
                {
                    this.CreateSingleAssesmentRip(inspiraRequest, entryResponse, oDAC);
                    stable = "mscne";
                }
                else if (serviceRequest.bisprocedure && serviceRequest.bbilleable && inspiraRequest.sattentiontype != "9")
                {
                    this.CreateSingleProcedureRip(inspiraRequest, entryResponse, oDAC);
                    stable = "mspro";
                }
                this.CreateRipLog(inspiraRequest, entryResponse, oDAC, stable);
            }
        }

        /// <summary>
        /// Método para crear el detalle del cargo en la tabla de Servinte AYCARDET
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira</param>
        /// <param name="inspiraRequest">String Id de la transacción en Inspira</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateChargeDetail(InspiraCita inspiraRequest, ServintePatient servintePatient, string sId, Oracle oDAC)
        {
            if (inspiraRequest.lservices != null)
            {
                int iCharge = 0;
                char cBilleable = 'N';
                EntryResponse entryResponse = null;
                List<OracleParameter> lParameters = new List<OracleParameter>();
                StringBuilder stringBuilder = new StringBuilder();
                string stable = string.Empty;
                int i = 0;
                foreach (ServiceRequest item in inspiraRequest.lservices)
                {
                    iCharge = this.GetSequenceNextValue("SQ_AYCARDET_REG", oDAC);
                    cBilleable = (item.bbilleable) ? 'S' : 'N';
                    stringBuilder.Append("INSERT INTO AYCARDET (CARDETFUE, CARDETDOC, CARDETITE, CARDETLIN, CARDETANO, CARDETMES, CARDETFEC, CARDETFUO, CARDETDOO, CARDETNUM");
                    stringBuilder.Append(", CARDETTAR, CARDETTSE, CARDETCON, CARDETCOD");
                    stringBuilder.Append(", CARDETCCO, CARDETNIT, CARDETCAN, CARDETVUN, CARDETFES, CARDETTOT, CARDETREC, CARDETVRE, CARDETVEX, CARDETVFA, CARDETDFA, CARDETDES");
                    stringBuilder.Append(", CARDETTIP, CARDETFAC, CARDETORI, CARDETANU, CARDETOCE, CARDETUAD, CARDETFAD, CARDETEAD, CARDETEAO, CARDETUFU, CARDETURG, CARDETTRM, CARDETMON, CARDETPIV, CARDETREG)");
                    stringBuilder.Append(" VALUES (:CARDETFUE, :CARDETDOC, :CARDETITE, :CARDETLIN, :CARDETANO, :CARDETMES, :CARDETFEC, :CARDETFUO, :CARDETDOO, :CARDETNUM");
                    stringBuilder.Append(", (SELECT EMPEADTAR FROM INEMPEAD WHERE EMPEADIND = 'S' AND EMPEADCOD = :CARDETTAR AND EMPEADEAD = :CARDETEAD), :CARDETTSE, :CARDETCON, :CARDETCOD, :CARDETCCO");
                    stringBuilder.Append(", :CARDETNIT, :CARDETCAN, :CARDETVUN, :CARDETFES, :CARDETTOT, :CARDETREC, :CARDETVRE, :CARDETVEX, :CARDETVFA, :CARDETDFA");
                    stringBuilder.Append(", :CARDETDES, :CARDETTIP, :CARDETFAC, :CARDETORI, :CARDETANU, :CARDETOCE, :CARDETUAD, :CARDETFAD, :CARDETEAD, :CARDETEAO, :CARDETUFU, :CARDETURG, :CARDETTRM, :CARDETMON, :CARDETPIV, :CARDETREG)");
                    lParameters.Add(new OracleParameter("CARDETFUE", OracleDbType.Varchar2, this.parametroRip.ssource, ParameterDirection.Input)); //Fuente
                    lParameters.Add(new OracleParameter("CARDETDOC", OracleDbType.Int32, this.parametroRip.ientry, ParameterDirection.Input)); //Número de ingreso
                    lParameters.Add(new OracleParameter("CARDETITE", OracleDbType.Int32, 0, ParameterDirection.Input)); //Número de item
                    lParameters.Add(new OracleParameter("CARDETLIN", OracleDbType.Int32, (i + 1), ParameterDirection.Input)); //Número de item
                    lParameters.Add(new OracleParameter("CARDETANO", inspiraRequest.ddate.Year)); //Año
                    lParameters.Add(new OracleParameter("CARDETMES", inspiraRequest.ddate.ToString("MM"))); //Mes
                    lParameters.Add(new OracleParameter("CARDETFEC", inspiraRequest.ddate)); //Fecha del cargo            
                    lParameters.Add(new OracleParameter("CARDETFUO", this.parametroRip.ssource)); //Fuente origen del cargo
                    lParameters.Add(new OracleParameter("CARDETDOO", (this.ientry != 0) ? this.ientry : this.parametroRip.ientry)); //Ingreso origen del cargocardetnum
                    lParameters.Add(new OracleParameter("CARDETNUM", "0")); //Número de ingreso del paciente
                    lParameters.Add(new OracleParameter("CARDETTAR", inspiraRequest.sagreement)); //Código de la tarifa                    
                    lParameters.Add(new OracleParameter("CARDETTSE", inspiraRequest.sattentiontype)); //Tipo de servicio
                    lParameters.Add(new OracleParameter("CARDETDFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
                    lParameters.Add(new OracleParameter("CARDETDES", OracleDbType.Decimal, item.idiscount, ParameterDirection.Input)); //Valor descuento
                    lParameters.Add(new OracleParameter("CARDETCON", item.sconcept)); //Código del concepto
                    lParameters.Add(new OracleParameter("CARDETCOD", item.sservice)); //Código del servicio
                    lParameters.Add(new OracleParameter("CARDETCCO", item.scostcenter)); //Código del centro de costos
                    lParameters.Add(new OracleParameter("CARDETNIT", inspiraRequest.sthird)); //Código del tercero
                    lParameters.Add(new OracleParameter("CARDETCAN", OracleDbType.Decimal, item.iqty, ParameterDirection.Input)); //Cantidad
                    lParameters.Add(new OracleParameter("CARDETVUN", OracleDbType.Decimal, item.ivalue, ParameterDirection.Input)); //Valor unitario
                    lParameters.Add(new OracleParameter("CARDETFES", "N")); // Realizo en Nocturnos (N) y/o festivos(F)
                    lParameters.Add(new OracleParameter("CARDETTOT", OracleDbType.Decimal, (item.iqty * item.ivalue), ParameterDirection.Input)); //Valor total            
                    lParameters.Add(new OracleParameter("CARDETREC", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor recargo            
                    lParameters.Add(new OracleParameter("CARDETVRE", OracleDbType.Decimal, (item.iqty * item.ivalue), ParameterDirection.Input)); //Valor Reconocido
                    lParameters.Add(new OracleParameter("CARDETVEX", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor excedente
                    lParameters.Add(new OracleParameter("CARDETVFA", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Valor facturado                        
                    lParameters.Add(new OracleParameter("CARDETTIP", "R")); //Tipo de resp.(R=Reconicido,E=Excedente)
                    lParameters.Add(new OracleParameter("CARDETFAC", cBilleable)); //Servicio facturable S o N
                    lParameters.Add(new OracleParameter("CARDETORI", "AY")); //Origen del cargo
                    lParameters.Add(new OracleParameter("CARDETANU", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Estado del cargo
                    lParameters.Add(new OracleParameter("CARDETOCE", item.scostcenter)); //Centro de costos origen
                    lParameters.Add(new OracleParameter("CARDETUAD", Tools.GetUser(inspiraRequest.suser))); //Usuario auditoría
                    lParameters.Add(new OracleParameter("CARDETFAD", DateTime.Now)); //Fecha auditoría            
                    lParameters.Add(new OracleParameter("CARDETEAD", this.parametroRip.scia)); //Unidad administrativa
                    lParameters.Add(new OracleParameter("CARDETEAO", this.parametroRip.scia)); //Unidad administrativa origen
                    lParameters.Add(new OracleParameter("CARDETUFU", inspiraRequest.sunit)); //Unidad funcional
                    lParameters.Add(new OracleParameter("CARDETURG", "N")); //Ingreso de urgencias
                    lParameters.Add(new OracleParameter("CARDETTRM", OracleDbType.Decimal, 0, ParameterDirection.Input)); //TRM del movimiento
                    lParameters.Add(new OracleParameter("CARDETMON", "MF")); //Moneda del movimiento (MF para nuestro caso)
                    lParameters.Add(new OracleParameter("CARDETREG", iCharge)); //Moneda del movimiento (MF para nuestro caso)*/
                    lParameters.Add(new OracleParameter("CARDETPIV", OracleDbType.Decimal, 0, ParameterDirection.Input)); //Número de días facturados
                    oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
                    entryResponse = new EntryResponse()
                    {
                        idPaciente = servintePatient.idPaciente,
                        idCargo = item.idCargo,
                        ientry = this.parametroRip.ientry,
                        ientrysource = (this.ientry == 0) ? inspiraRequest.ientry : this.ientry,
                        sid = sId,
                        sdocument = servintePatient.sdocument,
                        sdocumenttype = servintePatient.sdocumenttype,
                        ddate = inspiraRequest.ddate.ToString("yyyy-MM-dd"),
                        scostcenter = item.scostcenter,
                        sservice = item.sservice,
                        splan = inspiraRequest.splan,
                        srate = inspiraRequest.srate,
                        ipatient = servintePatient.iid,
                        iqty = item.iqty,
                        icharge = iCharge,
                        sconcept = item.sconcept,
                        sauthorization = item.sauthorization,
                        stype = this.stype,
                        sservicename = item.sservicename,
                        dvalue = Convert.ToInt32(item.iqty * item.ivalue),
                        iline = (i + 1),
                        sunit = inspiraRequest.sunit,
                        sagreement = inspiraRequest.sagreement,
                        sappointment = inspiraRequest.sappointment,
                        sservicegroup = inspiraRequest.sservicegroup,
                    };
                    if (this.stype == "Servicio" || this.stype == "Investigacion")
                    {
                        entryResponse.sappointment = sId;
                    }
                    else
                    {
                        entryResponse.sevent = sId;
                    }
                    this.lresponse.Add(entryResponse);
                    if (!inspiraRequest.lservices[i].bisprocedure && inspiraRequest.lservices[i].bbilleable && inspiraRequest.sattentiontype != "9")
                    {
                        this.CreateSingleAssesmentRip(inspiraRequest, entryResponse, oDAC);
                        stable = "mscne";
                    }
                    else if (inspiraRequest.lservices[i].bisprocedure && inspiraRequest.lservices[i].bbilleable && inspiraRequest.sattentiontype != "9")
                    {
                        this.CreateSingleProcedureRip(inspiraRequest, entryResponse, oDAC);
                        stable = "mspro";
                    }
                    //this.CreateRipLog(inspiraRequest, entryResponse, oDAC, stable);
                    lParameters.RemoveRange(0, lParameters.Count);
                    stringBuilder.Clear();
                    i++;
                }
                entryResponse = null;
                lParameters = null;
                stringBuilder = null;
            }
        }

        /// <summary>
        /// Método para crear la atención del paciente tabla MSATE
        /// </summary>
        /// <param name="inspiraRequest">Objeto Inspira Cita</param>
        /// <param name="servintePatient">Objeto Paciente Servinte</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateAttentionRip(InspiraCita inspiraRequest, ServintePatient servintePatient, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO MSATE (ATEIPS, ATEDOC, ATETIF, ATEHIS, ATEFUE, ATEDTO, ATEFSO, ATEHSO, ATEEST, ATEFHE");
            stringBuilder.Append(", ATEUSE, ATEDIL, ATEFHG, ATEUSU, ATEEAD, ATEEOC, ATEEMP) VALUES (:ATEIPS, :ATEDOC, :ATETIF, :ATEHIS, :ATEFUE, :ATEDTO, :ATEFSO, :ATEHSO, :ATEEST, :ATEFHE");
            stringBuilder.Append(", :ATEUSE, :ATEDIL, :ATEFHG, :ATEUSU, :ATEEAD, :ATEEOC, (SELECT EMPDETADM FROM INEMPDET WHERE EMPDETCOD = :ATEEMP FETCH FIRST 1 ROWS ONLY))");
            lParameters.Add(new OracleParameter("ATEIPS", this.parametroRip.snit)); //NiT Ips
            lParameters.Add(new OracleParameter("ATEDOC", this.parametroRip.isequence)); //Consecutivo de atención
            lParameters.Add(new OracleParameter("ATETIF", "A")); // Indicador de donde provienen los datos. H:Hospitalizacion, A: Ayudas, L: Laboratorio,n: Ninguno
            lParameters.Add(new OracleParameter("ATEHIS", servintePatient.iid)); // Indicador del paciente
            lParameters.Add(new OracleParameter("ATEFUE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("ATEDTO", this.parametroRip.ientry)); //Id del ingreso
            lParameters.Add(new OracleParameter("ATEFSO", inspiraRequest.ddate)); //Fecha
            lParameters.Add(new OracleParameter("ATEHSO", inspiraRequest.ddate)); //Hora
            lParameters.Add(new OracleParameter("ATEEST", "A")); //Estado de la información (Pendiente/Aprobado)   Aprobado para nuestro caso
            lParameters.Add(new OracleParameter("ATEFHE", DateTime.Now)); //Fecha y hora de grabación del estado
            lParameters.Add(new OracleParameter("ATEUSE", Tools.GetUser(inspiraRequest.suser))); //Usuario que realiza la operación
            lParameters.Add(new OracleParameter("ATEDIL", Tools.GetUser(inspiraRequest.suser))); //Usuario que diligencia la información
            lParameters.Add(new OracleParameter("ATEFHG", DateTime.Now)); //Fecha y hora de grabación
            lParameters.Add(new OracleParameter("ATEUSU", Tools.GetUser(inspiraRequest.suser))); //Usuario que aprueba la transacción
            lParameters.Add(new OracleParameter("ATEEAD", this.parametroRip.scia)); //Estructura administrativa ingreso
            lParameters.Add(new OracleParameter("ATEEOC", this.parametroRip.scia)); //Estructura administrativa cargo
            lParameters.Add(new OracleParameter("ATEEMP", inspiraRequest.sagreement)); //Estructura administrativa cargo
            
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            stringBuilder = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para crear registro en la tabla de Servinte MSATEIDE, es requerido para el cargo pero no sé para qué sirve
        /// </summary>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        private void CreateAttentionIde(Oracle oDAC, string usertype, string newusertype = "01")
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            string squery = "INSERT INTO MSATEIDE (ATEIDEIPS, ATEIDEDOC, ATEIDETUS, ATEIDEEAD, ATEIDETUR, ATEIDEMAR) VALUES (:ATEIDEIPS, :ATEIDEDOC, (SELECT TUSREG FROM INTUS WHERE TUSCOD = :ATEIDETUS FETCH FIRST 1 ROWS ONLY), :ATEIDEEAD,  (SELECT TUSFEM FROM INTUS WHERE TUSCOD = :ATEIDETUS FETCH FIRST 1 ROWS ONLY), :ATEIDEMAR)";
            lParameters.Add(new OracleParameter("ATEIDEIPS", this.parametroRip.snit)); //NiT Ips
            lParameters.Add(new OracleParameter("ATEIDEDOC", this.parametroRip.isequence)); //Consecutivo de atención
            lParameters.Add(new OracleParameter("ATEIDETUS", usertype)); //Consecutivo de atención
            lParameters.Add(new OracleParameter("ATEIDEEAD", this.parametroRip.scia)); //Empresa
            //lParameters.Add(new OracleParameter("ATEIDETUR", newusertype)); //Tipo de usuario
            lParameters.Add(new OracleParameter("ATEIDEMAR", "01")); //Modalidad de atención
            oDAC.ExecuteNonQuery(squery, lParameters, false, true);
            lParameters = null;
        }

        private void CreateMsaRip(InspiraCita inspiraCita, EntryResponse entryResponse, Oracle oDAC)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO msacrips (acripsips, acripsdoc, acripsead, acripsite, acripsreg, acripstab, acripseoc, acripsfue, acripsdto");
            stringBuilder.Append(", acripshis, acripsnum, acripstif, acripstrp, acripscfu, acripscdo, acripsfac, acripsfre, acripsest, acripsinc, acripsin1, acripsin2, acripsin3)");
            stringBuilder.Append("(:acripsips, :acripsdoc, :acripsead, :acripsite, :acripsreg, :acripstab, :acripseoc, :acripsfue, :acripsdto, :acripshis, :acripsnum, :acripstif, :acripstrp, :acripscfu, :acripscdo");
            stringBuilder.Append(", :acripsfac, :acripsfre, :acripsest, :acripsinc, :acripsin1, :acripsin2, :acripsin3)");
        }

        /// <summary>
        /// Método para crear el RIP de consulta para un servicio en la tabla MSCNE
        /// </summary>
        /// <param name="inspiraCita">Objeto Inspira Cita</param>
        /// <param name="entryResponse">Objeto Respuesta de Integración</param>
        /// <param name="oDAC">Objeto conexión a base de datos</param>
        private void CreateSingleAssesmentRip(InspiraCita inspiraCita, EntryResponse entryResponse, Oracle oDAC)
        {
            string sattendingtype = (inspiraCita.sattendingtype == "T") ? "06" : "01";
            string sservicegroup = (entryResponse.sservicename.Contains("CONSULTA")) ? "01" : "02";
            string sservicetype = (Tools.GetDocumentType(entryResponse.sdocumenttype, true) == "TI" || Tools.GetDocumentType(entryResponse.sdocumenttype, true) == "RC") ? "386" : "331";
            string scie10 = string.IsNullOrEmpty(inspiraCita.scie10) ? "Z000" : inspiraCita.scie10;
            string sthird = string.IsNullOrEmpty(inspiraCita.sthird) ? "73135051" : inspiraCita.sthird;
            string sdoctor = this.GetDoctorCode(sthird);
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO MSCNE (CNEIPS, CNEDOC, CNESEC, CNEAUT, CNEFAT, CNETIP, CNEESP, CNECLA, CNECEX, CNEINC");
            stringBuilder.Append(", CNEDIA, CNETDX, CNEFCN, CNEMED, CNESER, CNECCO, CNERSN, CNEPEN, CNEFHG, CNEUSU, CNEREG, CNETAB, CNEFTE, CNEDTO, CNEEAD, CNEEOC, CNECMR, CNEGSR, CNECSR, CNECER, CNETDR, CNEIDR) VALUES(");
            stringBuilder.Append(":CNEIPS, :CNEDOC, :CNESEC, (SELECT MOVORDORD FROM AYMOVORD WHERE MOVORDDOC = :CNEAUT AND MOVORDFUE = :CNEFUENTE), :CNEFAT, (SELECT PROANE FROM INPRO WHERE PROCOD = :CNETIP AND PROACT = 'S' FETCH FIRST 1 ROWS ONLY), (SELECT PROESP FROM INPRO WHERE PROCOD = '" + entryResponse.sservice + " ' AND PROACT = 'S' FETCH FIRST 1 ROWS ONLY), :CNECLA");
            stringBuilder.Append(", :CNECEX, :CNEINC, :CNEDIA, :CNETDX, :CNEFCN, :CNEMED");
            stringBuilder.Append(", :CNESER, :CNECCO, :CNERSN, :CNEPEN, :CNEFHG, :CNEUSU, :CNEREG, :CNETAB, :CNEFTE, :CNEDTO, :CNEEAD, :CNEEOC, :CNECMR, :CNEGSR, :CNECSR, :CNECER, :CNETDR, :CNEIDR)");
            lParameters.Add(new OracleParameter("CNEIPS", this.parametroRip.snit)); //Nit de la IPS
            lParameters.Add(new OracleParameter("CNEDOC", this.parametroRip.isequence)); //Número de ingreso   
            lParameters.Add(new OracleParameter("CNESEC", entryResponse.iline)); //Número de secuencia (línea)
            //lParameters.Add(new OracleParameter("CNEAUT", entryResponse.sauthorization)); //Número de autorización
            lParameters.Add(new OracleParameter("CNEAUT", this.parametroRip.ientry)); //Número de ingreso
            lParameters.Add(new OracleParameter("CNEFUENTE", this.parametroRip.ssource)); //Fuente del ingreso
            lParameters.Add(new OracleParameter("CNEFAT", Convert.ToDateTime(entryResponse.ddate))); //Fecha de la atención
            lParameters.Add(new OracleParameter("CNETIP", entryResponse.sservice)); //Código de la consulta
            //lParameters.Add(new OracleParameter("CNEESP", "(SELECT PROESP FROM INPRO WHERE PROCOD = '" + entryResponse.sservice + " ')")); //Código de la especialidad
            lParameters.Add(new OracleParameter("CNECLA", "1")); //Clase de consulta, 1 para nosotros?
            lParameters.Add(new OracleParameter("CNECEX", "13")); //Código causa externa, 13 es enfermedad general
            lParameters.Add(new OracleParameter("CNEINC", "0")); //Días de incapacidad
            lParameters.Add(new OracleParameter("CNEDIA", scie10)); //Diagnóstico principal
            lParameters.Add(new OracleParameter("CNETDX", 1)); //Tipo de diagnostico principal: 1 Impresión diagnóstica
            lParameters.Add(new OracleParameter("CNEFCN", "10")); //Finalidad de la consulta
            lParameters.Add(new OracleParameter("CNEMED", sdoctor)); //Código del médico
            lParameters.Add(new OracleParameter("CNESER", "28")); //Tabla de servicios INSER, 28 es empresa FNC
            lParameters.Add(new OracleParameter("CNECCO", entryResponse.scostcenter)); //Código del centro de costos
            lParameters.Add(new OracleParameter("CNERSN", "S")); //Remitido S = Si N = No
            lParameters.Add(new OracleParameter("CNEPEN", "P")); //Estado del RIPS P = Pendiente
            lParameters.Add(new OracleParameter("CNEFHG", DateTime.Now)); //Fecha y hora de la grabación
            lParameters.Add(new OracleParameter("CNEUSU", Tools.GetUser(inspiraCita.suser))); //usuario que crea el RIPS
            lParameters.Add(new OracleParameter("CNEREG", entryResponse.icharge)); //Id del detalle del cargo
            lParameters.Add(new OracleParameter("CNETAB", "AYCARDET")); //Programa
            lParameters.Add(new OracleParameter("CNEFTE", this.parametroRip.ssource)); //Fuente
            lParameters.Add(new OracleParameter("CNEDTO", this.parametroRip.ientry)); //Ingreso
            lParameters.Add(new OracleParameter("CNEEAD", this.parametroRip.scia)); //Unidad administrativa
            lParameters.Add(new OracleParameter("CNEEOC", this.parametroRip.scia)); //Unidad administrativa
            lParameters.Add(new OracleParameter("CNECMR", sattendingtype)); //Modalidad de atención: 01 Intramural, 07: Telemedicina no interactiva
            lParameters.Add(new OracleParameter("CNEGSR", sservicegroup)); //Grupo de servicios: 01 Consulta Externa, 02: Apoyo diagnóstico
            lParameters.Add(new OracleParameter("CNECSR", sservicetype)); //Código del servicio: 331 Neumología, 386: Neumología pediátrica
            lParameters.Add(new OracleParameter("CNECER", "38")); //Causa externa de la atención: 38 Efermedad general
            lParameters.Add(new OracleParameter("CNETDR", "CC")); //Tipo de documento
            lParameters.Add(new OracleParameter("CNEIDR", sthird)); //Número de documento
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            lParameters = null;
            stringBuilder = null;
        }

        private void CreateSingleProcedureRip(InspiraCita inspiraCita, EntryResponse entryResponse, Oracle oDAC)
        {
            string sattendingtype = (inspiraCita.sattendingtype == "T") ? "06" : "01";
            string sservicegroup = (entryResponse.sservicename.Contains("CONSULTA")) ? "01" : "02";
            string sservicetype = (Tools.GetDocumentType(entryResponse.sdocumenttype, true) == "TI" || Tools.GetDocumentType(entryResponse.sdocumenttype, true) == "RC") ? "386" : "331";
            string scie10 = string.IsNullOrEmpty(inspiraCita.scie10) ? "Z000" : inspiraCita.scie10;
            string sthird = string.IsNullOrEmpty(inspiraCita.sthird) ? "73135051" : inspiraCita.sthird;
            string sdoctor = this.GetDoctorCode(sthird); //SELECT MEDCOD, MEDCED FROM INMED WHERE MEDACT = 'S'
            List <OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO MSPRO (PROIPS, PRODOC, PROSEC, PROAUT, PROFPR, PROCPR, PROPRO, PROTOT, PROCAN, PROQOD");
            stringBuilder.Append(", PROAGR, PROMED, PROSER, PROPEN, PROFHG, PROUSU, PROREG, PROTAB, PROFTE, PRODTO, PROEAD, PROEOC, procmr, progsr, procsr, provir, protdr, proidr, proher, PRODIA, PROTIP, PROCCO) VALUES (");
            stringBuilder.Append(":PROIPS, :PRODOC, :PROSEC, :PROAUT, :PROFPR, :PROCPR, (SELECT PROANE FROM INPRO WHERE PROCOD = :PROPRO AND PROACT = 'S' FETCH FIRST 1 ROWS ONLY), :PROTOT, :PROCAN");
            stringBuilder.Append(", (SELECT CASE WHEN PROTIP = 'Q' THEN 1 ELSE 0 END FROM INPRO WHERE PROCOD = '" + entryResponse.sservice + "' AND PROACT = 'S' FETCH FIRST 1 ROWS ONLY)");
            stringBuilder.Append(", (SELECT CONAGR FROM FACON WHERE CONCOD = :PROAGR), :PROMED");
            stringBuilder.Append(", :PROSER, :PROPEN, :PROFHG, :PROUSU, :PROREG, :PROTAB, :PROFTE, :PRODTO, :PROEAD, :PROEOC, :procmr, :progsr, :procsr, :provir, :protdr, :proidr, :proher, :PRODIA, :PROTIP, :PROCCO)");
            lParameters.Add(new OracleParameter("PROIPS", this.parametroRip.snit)); //Nit de la IPS
            lParameters.Add(new OracleParameter("PRODOC", this.parametroRip.isequence));//Número de la secuencia PARSEC de MSPAR   
            lParameters.Add(new OracleParameter("PROSEC", entryResponse.iline)); //Número de secuencia (número de línea en aycardet CARDETLIN)
            lParameters.Add(new OracleParameter("PROAUT", entryResponse.sauthorization)); //Número de autorización
            lParameters.Add(new OracleParameter("PROFPR", Convert.ToDateTime(entryResponse.ddate))); //Fecha de la atención
            lParameters.Add(new OracleParameter("PROCPR", 1)); //Ámbito del procedimiento
            lParameters.Add(new OracleParameter("PROPRO", entryResponse.sservice)); //Código del procedimiento
            lParameters.Add(new OracleParameter("PROTOT", entryResponse.dvalue)); //Valor del procedimiento
            lParameters.Add(new OracleParameter("PROCAN", entryResponse.iqty)); //Cantidad del procedimiento
            lParameters.Add(new OracleParameter("PROAGR", entryResponse.sconcept)); //Concepto de agrupación se llama del maestro de conceptos
            lParameters.Add(new OracleParameter("PROMED", sdoctor)); //Código del médico
            lParameters.Add(new OracleParameter("PROSER", "28"));  //Tabla de servicios INSER, 28 es empresa FNC
            lParameters.Add(new OracleParameter("PROPEN", "P"));  //Estado de rips P por pediente
            lParameters.Add(new OracleParameter("PROFHG", DateTime.Now)); //Fecha de generación
            lParameters.Add(new OracleParameter("PROUSU", Tools.GetUser(inspiraCita.suser))); //Usuario que genera rl RIP
            lParameters.Add(new OracleParameter("PROREG", entryResponse.icharge)); //Id del cargo
            lParameters.Add(new OracleParameter("PROTAB", "AYCARDET")); //Tabla de la cual proviene el cargo
            lParameters.Add(new OracleParameter("PROFTE", this.parametroRip.ssource)); //Fuente del ingreso
            lParameters.Add(new OracleParameter("PRODTO", this.parametroRip.ientry)); //Documento origen del cargo (id del ingreso)
            lParameters.Add(new OracleParameter("PROEAD", this.parametroRip.scia)); //Unidad administrativa 
            lParameters.Add(new OracleParameter("PROEOC", this.parametroRip.scia)); //Unidad administrativa 
            lParameters.Add(new OracleParameter("procmr", sattendingtype)); //Modalidad de atención: 01 Intramural, 07: Telemedicina no interactiva
            lParameters.Add(new OracleParameter("progsr", sservicegroup)); //Grupo de servicios: 01 Consulta Externa, 02: Apoyo diagnóstico
            lParameters.Add(new OracleParameter("procsr", sservicetype)); //Código del servicio: 331 Neumología, 386: Neumología pediátrica
            lParameters.Add(new OracleParameter("provir", "02")); //Vía de ingreso: 02 Derivado consulta externa
            lParameters.Add(new OracleParameter("protdr", "CC")); //Tipo de documento del médico
            lParameters.Add(new OracleParameter("proidr", sthird)); //Número de documento del médico
            lParameters.Add(new OracleParameter("proher", Convert.ToDateTime(entryResponse.ddate))); //Hora de atención
            lParameters.Add(new OracleParameter("PRODIA", scie10)); //Código del Diagnóstico
            lParameters.Add(new OracleParameter("PROTIP", "1")); //Finalidad del procedimiento
            lParameters.Add(new OracleParameter("PROCCO", entryResponse.scostcenter)); //Centro de costos
            try
            {
                oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Facturacion", "DAC", ex);
            }            
            lParameters = null;
            stringBuilder = null;
        }
        

        /// <summary>
        /// Método para crear el log del registro de rip en la tabla MSLOG
        /// </summary>
        /// <param name="inspiraCita">Objeto Inspira Cita</param>
        /// <param name="entryResponse">Objeto Respuesta Integración</param>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        /// <param name="stable">String nombre de la tabla que se ha modificado</param>
        private void CreateRipLog(InspiraCita inspiraCita, EntryResponse entryResponse, Oracle oDAC, string stable)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO MSLOG (logusu, logter, logpro, logope, logde1, logva1, logde2, logva2, logde3, logva3");
            stringBuilder.Append(", logde4, logva4, logde5, logva5, logde6, logva6, logtip, logtab, logfec, logead) VALUES (:logusu, :logter, :logpro, :logope, :logde1, :logva1");
            stringBuilder.Append(", :logde2, :logva2, :logde3, :logva3, :logde4, :logva4, :logde5, :logva5, :logde6, :logva6, :logtip, :logtab, :logfec, :logead)");
            lParameters.Add(new OracleParameter("logusu", Tools.GetUser(inspiraCita.suser))); //Usuario que realiza la transacción
            lParameters.Add(new OracleParameter("logter", "FA")); //TODO: Esto qué es?
            lParameters.Add(new OracleParameter("logpro", "caymov 13.0.37")); //Aplicativo
            lParameters.Add(new OracleParameter("logope", "Grabar")); //Operación
            lParameters.Add(new OracleParameter("logde1", "IPS")); //Descripción 1
            lParameters.Add(new OracleParameter("logva1", this.parametroRip.snit)); //Valor 1
            lParameters.Add(new OracleParameter("logde2", "CONSECUTIVO")); //Descripción 2
            lParameters.Add(new OracleParameter("logva2", entryResponse.icharge)); //Valor 2
            lParameters.Add(new OracleParameter("logde3", "SECUENCIA")); //Descripción 3
            lParameters.Add(new OracleParameter("logva3", entryResponse.iline)); //Valor 3
            lParameters.Add(new OracleParameter("logde4", string.Empty)); //Descripción 4
            lParameters.Add(new OracleParameter("logva4", string.Empty)); //Valor 4
            lParameters.Add(new OracleParameter("logde5", string.Empty)); //Descripción 5
            lParameters.Add(new OracleParameter("logva5", string.Empty)); //Valor 5
            lParameters.Add(new OracleParameter("logde6", string.Empty)); //Descripción 6
            lParameters.Add(new OracleParameter("logva6", string.Empty)); //Valor 6
            lParameters.Add(new OracleParameter("logtip", "G")); //Tipo de transacción G es Grabar
            lParameters.Add(new OracleParameter("logtab", stable)); //Tabla modificada
            lParameters.Add(new OracleParameter("logfec", DateTime.Now)); //Fecha de la transacción
            lParameters.Add(new OracleParameter("logead", this.parametroRip.scia)); //Estructura administrativa
            oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            lParameters = null;
            stringBuilder = null;
        }

        /// <summary>
        /// Método para crear el log de la transacción para el usuario tabla siusuact
        /// </summary>
        /// <param name="oracle">Objeto conexión a la basde de datos</param>
        private void CreateUserLog(Oracle oracle)
        {
            string squery = "UPDATE siusuact SET usuactfec = SYSDATE WHERE usuactapl = :usuactapl AND usuactusu = :usuactusu AND usuactpro = :usuactpro AND usuactead = :usuactead";
            List<OracleParameter> lParameters = new List<OracleParameter>();
            lParameters.Add(new OracleParameter("usuactapl", "FACCAR")); //Aplicación
            lParameters.Add(new OracleParameter("usuactusu", "admon")); //Usuario
            lParameters.Add(new OracleParameter("usuactpro", "1974")); //Programa
            lParameters.Add(new OracleParameter("usuactead", this.parametroRip.scia)); //Estructura administrativa
            oracle.ExecuteNonQuery(squery, lParameters, false, true);
            lParameters = null;
        }

        /// <summary>
        /// Método para paquetizar los cargos de un paquete, tabla AYCARPAQ
        /// </summary>
        /// <param name="inspiraCita">Objeto Inspira Cita</param>
        /// <param name="iline">Entero número de item</param>
        /// <param name="icharge">Entero número del cargo</param>
        /// <param name="oracle">Objeto conexión a la base de datos</param>
        private void CreatePackageDetail(InspiraCita inspiraCita, int iline, int icharge, Oracle oracle)
        {
            StringBuilder stringBuilder = new StringBuilder("INSERT INTO AYCARPAQ (CARPAQFUE, CARPAQDOC, CARPAQITE, CARPAQLIN, CARPAQREG, CARPAQPAQ, CARPAQEAD)");
            stringBuilder.Append(" VALUES (:CARPAQFUE, :CARPAQDOC, :CARPAQITE, :CARPAQLIN, :CARPAQREG, :CARPAQPAQ, :CARPAQEAD)");
            List<OracleParameter> lParameters = new List<OracleParameter>();
            lParameters.Add(new OracleParameter("CARPAQFUE", this.parametroRip.ssource)); //Aplicación
            lParameters.Add(new OracleParameter("CARPAQDOC", this.parametroRip.ientry)); //Usuario
            lParameters.Add(new OracleParameter("CARPAQITE", "1")); //Programa
            lParameters.Add(new OracleParameter("CARPAQLIN", (iline + 1))); //Estructura administrativa
            lParameters.Add(new OracleParameter("CARPAQREG", icharge)); //Programa
            lParameters.Add(new OracleParameter("CARPAQPAQ", inspiraCita.stemplate)); //Programa
            lParameters.Add(new OracleParameter("CARPAQEAD", this.parametroRip.scia)); //Programa
            oracle.ExecuteNonQuery(stringBuilder.ToString(), lParameters, false, true);
            lParameters = null;
            stringBuilder = null;
        }

        /// <summary>
        /// Método para incrementar el número de secuencia de atención en Servinte tabla MSPAR
        /// </summary>
        /// <param name="oDAC">Objeto conexión a la base de datos</param>
        /// <param name="scompany">String id de la compañía a incrementar la secuencia</param>
        private void IncreaseAttentionSequence(Oracle oDAC, string scompany)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            string squery = "UPDATE MSPAR SET PARSEC = PARSEC + 1 WHERE PARCIA = :PARCIA";
            lParameters.Add(new OracleParameter(":PARCIA", scompany));
            oDAC.ExecuteNonQuery(squery, lParameters, false, true);
            lParameters = null;
        }

        /// <summary>
        /// Método para obtener los parámetros base para crear los rips, tabla MSPAR
        /// </summary>
        /// <returns>Objeto ParametroRip</returns>
        private void GetRipsParameters()
        {
            DataTable dataTable = new DataTable();
            string query = "SELECT * FROM MSPAR FETCH FIRST 1 ROWS ONLY";
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                dataTable = oracle.GetDataTable(query, null);
                if (dataTable.Rows.Count > 0)
                {
                    this.parametroRip.isequence = Convert.ToInt32(dataTable.Rows[0]["PARSEC"]);
                    this.parametroRip.scia = dataTable.Rows[0]["PARCIA"].ToString();
                    this.parametroRip.snit = dataTable.Rows[0]["PARIPS"].ToString();
                }
            }
            dataTable.Dispose();
            dataTable = null;
        }

        /// <summary>
        /// Método que obtiene el listado de terceros con sus conceptos, tabla FACONNOIT
        /// </summary>
        private void GetConceptThirds()
        {
            string query = "SELECT CONNITCON, CONNITNIT FROM FACONNIT WHERE CONNITIND = 'S'";
            DataTable dataTable = new DataTable();
            Generic generic = null;
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                dataTable = oracle.GetDataTable(query, null);
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    generic = new Generic()
                    {
                        scode = dataRow["CONNITCON"].ToString(),
                        sname = dataRow["CONNITNIT"].ToString(),
                    };
                    lconnit.Add(generic);
                }
            }
            generic = null;
            dataTable.Dispose();
            dataTable = null;
        }

        /// <summary>
        /// Método para obtener el tercero para un concepto y un nit de la lista de conceptos y nit
        /// </summary>
        /// <param name="sconcept">String código del concepto</param>
        /// <param name="sthird">String nit del tercero</param>
        /// <returns>String nit del tercero encontrado</returns>
        private string GetThird(string sconcept, string sthird)
        {
            Generic generic = this.lconnit.FirstOrDefault(x => x.scode == sconcept && x.sname == sthird);
            if (generic != null)
            {
                return sthird;
            }
            else
            {
                generic = this.lconnit.FirstOrDefault(x => x.scode == sconcept);
                return (generic != null) ? generic.sname : string.Empty;
            }
        }

        private List<int> GetEntryList(Oracle oracle, int i)
        {
            return null;
        }

        /// <summary>
        /// Método para obtener los parámetros base para crear los rips en una transacción, tabla MSPAR
        /// </summary>
        /// <returns>Objeto ParametroRip</returns>
        private void GetRipsParameters(Oracle oracle)
        {
            DataTable dataTable = new DataTable();
            string query = "SELECT * FROM MSPAR FETCH FIRST 1 ROWS ONLY";
            dataTable = oracle.GetDataTable(query, null, false, true);
            if (dataTable.Rows.Count > 0)
            {
                this.parametroRip.isequence = Convert.ToInt32(dataTable.Rows[0]["PARSEC"]);
                this.parametroRip.scia = dataTable.Rows[0]["PARCIA"].ToString();
                this.parametroRip.snit = dataTable.Rows[0]["PARIPS"].ToString();
            }
            dataTable.Dispose();
            dataTable = null;
        }

        /// <summary>
        /// Método para obtener el número de secuencia para el ingreso tabla INFUE
        /// </summary>
        /// <param name="oracle">Objeto de conexión a la base de datos</param>
        private void GetEntrySequence(Oracle oracle, string scompany, string ssource = "CARGOS")
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            DataTable dataTable = new DataTable();
            string query = "SELECT FUECOD, FUESEC FROM INFUE WHERE FUENOM = :FUENOM AND FUEEAD = :FUEEAD";
            lParameters.Add(new OracleParameter(":FUENOM", ssource));
            lParameters.Add(new OracleParameter(":FUEEAD", scompany));
            dataTable = oracle.GetDataTable(query, lParameters, false, true);
            if (dataTable.Rows.Count > 0)
            {
                this.parametroRip.ientry = Convert.ToInt32(dataTable.Rows[0]["FUESEC"]);
                this.parametroRip.ssource = dataTable.Rows[0]["FUECOD"].ToString();
            }
            dataTable.Dispose();
            dataTable = null;
            lParameters = null;
        }

        /// <summary>
        /// Método para actualizar la secuencia del ingreso en la tabla de Servinte INFUE
        /// </summary>
        /// <param name="oracle">Objeto conexión de la base de datos</param>
        private void UpdateEntrySequence(Oracle oracle)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            string squery = "UPDATE INFUE SET FUESEC = FUESEC + 1 WHERE FUECOD = :FUECOD AND FUEEAD = :FUEEAD";
            lParameters.Add(new OracleParameter(":FUECOD", this.parametroRip.ssource));
            lParameters.Add(new OracleParameter(":FUEEAD", this.parametroRip.scia));
            oracle.ExecuteNonQuery(squery, lParameters, false, true);
            lParameters = null;
        }

        /// <summary>
        /// Método para obtener el id del detalle del cargo creado
        /// </summary>
        /// <param name="iline">Entero línea del cargo</param>
        /// <param name="ientry">Entero id del ingreso</param>
        /// <returns>Entero con el id del detalle cargo</returns>
        private int GetChargeFromResult(int iline, int ientry)
        {
            EntryResponse entryResponse = this.lresponse.FirstOrDefault(x => x.ientry == ientry && x.iline == iline);
            return (entryResponse != null) ? entryResponse.icharge : 0;
        }

        private decimal GetProductValue(ServiceRequest inspiraServicio)
        {
            if (this.lTarifaProducto != null)
            {
                TarifaProducto tarifaProducto = this.lTarifaProducto.FirstOrDefault(x => x.starifa == inspiraServicio.srate && x.sconcepto == inspiraServicio.sconcept
                                                                                    && x.scentro == inspiraServicio.scostcenter && x.sproducto == inspiraServicio.sservice);
                return (tarifaProducto != null) ? Convert.ToDecimal(tarifaProducto.ivalor) : inspiraServicio.ivalue;
            }
            return inspiraServicio.ivalue;
        }

        private void GetRatesByConceptByProduct(Oracle oracle)
        {
            StringBuilder stringBuilder = new StringBuilder("SELECT PROTARTAR, PROTARCON, PROTARCCO, PROTARPRO, PROTARVAL, CASE WHEN PROTARFMO IS NULL THEN PROTARFAD ELSE PROTARFMO END PROTARFMO, CCONOM");
            stringBuilder.Append(" FROM SERVINTE.INPROTAR INNER JOIN SERVINTE.COCCO ON CCOCOD = PROTARCCO UNION ALL SELECT EXATARTAR, EXATARCON, EXATARCCO, EXATAREXA, EXATARVAL, CASE WHEN EXATARFMO IS NULL ");
            stringBuilder.Append(" THEN EXATARFAD ELSE EXATARFMO END, CCONOM FROM SERVINTE.INEXATAR INNER JOIN SERVINTE.COCCO ON CCOCOD = EXATARCCO");
            this.lTarifaProducto = new List<TarifaProducto>();
            TarifaProducto tarifaProducto = null;
            DataTable dt = new DataTable();
            try
            {
                dt = oracle.GetDataTable(stringBuilder.ToString(), null, false, true);
                foreach (DataRow item in dt.Rows)
                {
                    tarifaProducto = new TarifaProducto()
                    {
                        scentro = item["PROTARCCO"].ToString(),
                        starifa = item["PROTARTAR"].ToString(),
                        sconcepto = item["PROTARCON"].ToString(),
                        sproducto = item["PROTARPRO"].ToString(),
                        ivalor = Convert.ToInt32(item["PROTARVAL"]),
                    };
                    this.lTarifaProducto.Add(tarifaProducto);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
            }
        }


        #endregion

        #endregion

        #region Integración Inspira Servinte en Batch

        #region Métodos públicos

        /// <summary>
        /// Método para obtener el listado de pacientes creados actualmente en Servinte
        /// </summary>
        /// <param name="aPatients">Array string con documentos de los pacientes</param>
        /// <returns></returns>
        public List<ServintePatient> GetPatients(string[] aPatients)
        {
            string sPatients = string.Join("','", aPatients);
            ServintePatient oPatient = null;
            List<ServintePatient> lPatients = new List<ServintePatient>();
            DataTable dt = new DataTable();
            StringBuilder sQuery = new StringBuilder("SELECT PACIDE, PACTID, PACAUDFAD FROM VPACIENTES WHERE PACIDE IN ('");
            sQuery.Append(sPatients);
            sQuery.Append("') ORDER BY PACAUDFAD DESC");
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                dt = oDAC.GetDataTable(sQuery.ToString(), null);
                foreach (DataRow dr in dt.Rows)
                {
                    oPatient = new ServintePatient()
                    {
                        sdocument = dr["PACIDE"].ToString(),
                        sdocumenttype = dr["PACTID"].ToString(),
                    };
                    lPatients.Add(oPatient);
                }
            }
            dt.Dispose();
            dt = null;
            sQuery = null;
            oPatient = null;
            return lPatients;
        }

        /// <summary>
        /// Método para crear los cargos de programa especiales desde el aplicativo que corre diariamiente
        /// </summary>
        /// <param name="lservintePatients">Lista genérica de pacientes</param>
        /// <returns>Lista genérica con la información de los registros para crear en la estadística</returns>
        public List<EntryResponse> CreateChargesForPrograms(List<ServintePatient> lservintePatients)
        {
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            int j = 0;
            int i = 0;
            this.stype = "Programas";
            EntryResponse entryResponse = null;
            using (Oracle oDAC = new Oracle())
            {
                try
                {
                    oDAC.sConnection = this.sconnection;
                    oDAC.Connect();
                    oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                    foreach (ServintePatient servintePatient in lservintePatients)
                    {
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                        {
                            foreach (ServiceRequest service in inspiraCita.lservices)
                            {
                                entryResponse = new EntryResponse()
                                {
                                    sappointment = inspiraCita.sappointment,
                                    sauthorization = inspiraCita.sauthorization,
                                    sservice = service.sservice,
                                    sconcept = service.sconcept,
                                    scostcenter = service.scostcenter,
                                    sdocument = servintePatient.sdocument,
                                    sdocumenttype = servintePatient.sdocumenttype,
                                    iline = (j + 1),
                                    sservicename = service.sservicename,
                                    stype = (inspiraCita.scostcenter.Contains("PROTOCOLO") || inspiraCita.sagreementname.Contains("COLCIENCIAS")) ? "Investigación solo estadistica" : "Programas solo estadistica",
                                    ddate = inspiraCita.ddate.ToString("yyyy-MM-dd"),
                                    splan = inspiraCita.splan,
                                    srate = inspiraCita.srate,
                                    iqty = service.iqty,
                                    dvalue = Convert.ToInt32(service.iqty * service.ivalue),
                                    //idCargo = "Solo estadistica",
                                    sagreement = inspiraCita.sagreement,
                                    sunit = inspiraCita.sunit,
                                    ipatient = servintePatient.iid,
                                    idPaciente = servintePatient.idPaciente,
                                    sservicegroup = inspiraCita.sservicegroup,
                                };
                                this.lresponse.Add(entryResponse);
                                j++;
                            }
                            /*if (inspiraCita.sservicegroup == "1")
                            {
                                this.GetEntrySequence(oDAC, this.parametroRip.scia);
                                this.UpdateEntrySequence(oDAC);
                                this.CreateEntry(inspiraCita, servintePatient, oDAC);
                                this.CreateEntryAdditional(inspiraCita, servintePatient, oDAC);
                                this.CreateEntryLog(inspiraCita, oDAC);
                                this.CreateEgress(inspiraCita, oDAC);
                                this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                                if (!string.IsNullOrEmpty(inspiraCita.sauthorization))
                                {
                                    this.CreateAuthorization(inspiraCita, oDAC, false);
                                    this.CreateAuthorizationDetail(inspiraCita, oDAC);
                                }                                
                                this.CreateEntryAuditLog(inspiraCita, oDAC);
                                this.CreateAttentionRip(inspiraCita, servintePatient, oDAC);
                                this.CreateAttentionIde(oDAC);
                                this.CreateCharge(inspiraCita, oDAC);
                                foreach (ServiceRequest service in inspiraCita.lservices)
                                {
                                    this.CreateSingleChargeDetail(inspiraCita, service, servintePatient, string.Empty, j, oDAC);
                                    j++;
                                }
                                //if ((inspiraCita.ientry - this.GetEntryValue(inspiraCita.lservices) != 0))
                                //{
                                    //this.CreateChargeForLAW(inspiraCita, j, oDAC);
                                //}
                                this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                                this.GetRipsParameters(oDAC);
                            }
                            else
                            {
                                foreach (ServiceRequest service in inspiraCita.lservices)
                                {
                                    entryResponse = new EntryResponse()
                                    {
                                        sappointment = inspiraCita.sappointment,
                                        sauthorization = inspiraCita.sauthorization,
                                        sservice = service.sservice,
                                        sconcept = service.sconcept,
                                        scostcenter = service.scostcenter,
                                        sdocument = servintePatient.sdocument,
                                        sdocumenttype = servintePatient.sdocumenttype,
                                        iline = (j + 1),
                                        sservicename = service.sservicename,
                                        stype = "Programas solo estadistica",
                                        ddate = inspiraCita.ddate,
                                        splan = inspiraCita.splan,
                                        srate = inspiraCita.srate,
                                        iqty = service.iqty,
                                        dvalue = (service.iqty * service.ivalue),
                                        //idCargo = "Solo estadistica",
                                        sagreement = inspiraCita.sagreement,
                                        sunit = inspiraCita.sunit,
                                        ipatient = servintePatient.iid,       
                                        idPaciente = servintePatient.idPaciente,
                                        sservicegroup = inspiraCita.sservicegroup,
                                    };
                                    this.lresponse.Add(entryResponse);
                                    j++;
                                }                                
                            }*/
                            j = 0;
                        }
                        i++;
                    }
                    oDAC.Commit();
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
            }
            entryResponse = null;
            return this.lresponse;
        }

        /// <summary>
        /// Verifica de forma específica y eficiente si una autorización ya fue utilizada para un paciente, plan y servicio.
        /// </summary>
        /// <param name="documentType">Tipo de documento del paciente</param>
        /// <param name="document">Documento del paciente</param>
        /// <param name="plan">Plan del paciente</param>
        /// <param name="authorization">Número de autorización a verificar</param>
        /// <param name="serviceCode">Código del servicio</param>
        /// <param name="oracle">Objeto de conexión a la base de datos</param>
        /// <returns>True si la autorización ya existe para esos criterios.</returns>
        public bool SpecificAuthorizationExists(string documentType, string document, string plan, string authorization, string serviceCode, Oracle oracle)
        {
            oracle.Connect();
            StringBuilder squery = new StringBuilder("SELECT COUNT(1) FROM SERVINTE.AYORDDET ");
            squery.Append(" INNER JOIN SERVINTE.AYMOV ON MOVDOC = ORDDETDOC AND MOVFUE = ORDDETFUE");
            squery.Append(" INNER JOIN SERVINTE.ABPAC ON PACHIS = MOVHIS");
            squery.Append(" INNER JOIN SERVINTE.AYMOVOTR ON MOVOTRDOC = MOVDOC AND MOVOTRFUE = MOVFUE");
            squery.Append(" WHERE ORDDETFUE = '03' AND MOVANU = 0");
            squery.Append(" AND PACTID = :PACTID AND PACIDE = :PACIDE"); // Paciente
            squery.Append(" AND MOVOTRPLA = :MOVOTRPLA"); // Plan
            squery.Append(" AND ORDDETORD = :ORDDETDOC"); // Autorización
            squery.Append(" AND EXISTS (SELECT 1 FROM SERVINTE.AYCARDET WHERE CARDETDOC = MOVDOC AND CARDETFUE = MOVFUE AND CARDETCOD = :CARDETCOD)"); // Servicio
            List<OracleParameter> oracleParameters = new List<OracleParameter>
            {
                new OracleParameter("PACTID", documentType),
                new OracleParameter("PACIDE", document),
                new OracleParameter("MOVOTRPLA", plan),
                new OracleParameter("ORDDETDOC", authorization),
                new OracleParameter("CARDETCOD", serviceCode)
            };
            object result = oracle.GetScalar(squery.ToString(), oracleParameters, false);
            oracle.Dispose();
            return (result != null && Convert.ToInt32(result) > 0);
        }

        /// <summary>
        /// Verifica de forma específica y eficiente si un cargo ya existe para una combinación de parámetros.
        /// Reemplaza la necesidad de GetTodayCharges para esta validación.
        /// </summary>
        /// <param name="servintePatient">Objeto del paciente</param>
        /// <param name="inspiraCita">Objeto de la cita</param>
        /// <param name="serviceRequest">Objeto del servicio</param>
        /// <param name="oracle">Objeto de conexión a la base de datos</param>
        /// <returns>True si el cargo ya existe.</returns>
        public bool SpecificEntryExists(ServintePatient servintePatient, InspiraCita inspiraCita, ServiceRequest serviceRequest, Oracle oracle)
        {
            oracle.Connect();
            // Algunos servicios específicos no se validan, mantenemos esta lógica.
            if (serviceRequest.sservice.EqualsAnyOf("991201", "991202", "939403", "860203", "860201"))
            {
                return false;
            }
            StringBuilder squery = new StringBuilder("SELECT COUNT(1) FROM SERVINTE.AYMOV ");
            squery.Append("INNER JOIN SERVINTE.ABPAC ON PACHIS = MOVHIS ");
            squery.Append("INNER JOIN SERVINTE.AYCARDET ON CARDETFUE = MOVFUE AND CARDETDOC = MOVDOC ");
            squery.Append("INNER JOIN SERVINTE.AYORDDET ON ORDDETDOC = MOVDOC AND ORDDETFUE = MOVFUE ");
            squery.Append("WHERE MOVANU = 0 AND CARDETANU = 0 AND TRUNC(MOVFEC) = TRUNC(SYSDATE) AND MOVFUE = '03' ");
            squery.Append("AND PACIDE = :PACIDE ");
            squery.Append("AND PACTID = :PACTID ");
            squery.Append("AND CARDETCOD = :CARDETCOD "); // Código del servicio
            squery.Append("AND CARDETCCO = :CARDETCCO "); // Centro de costos
            squery.Append("AND CARDETUFU = :CARDETUFU "); // Unidad funcional
            squery.Append("AND ORDDETORD = :ORDDETORD "); // Autorización
            squery.Append("AND CARDETCON = :CARDETCON "); // Concepto
            squery.Append("AND MOVCER = :MOVCER ");       // Convenio
            squery.Append("AND MOVUAD = :MOVUAD");         // Contrato (en el original usaba suser, lo mantengo)
            List<OracleParameter> oracleParameters = new List<OracleParameter>
            {
                new OracleParameter("PACIDE", servintePatient.sdocument),
                new OracleParameter("PACTID", Tools.GetDocumentType(servintePatient.sdocumenttype)),
                new OracleParameter("CARDETCOD", serviceRequest.sservice),
                new OracleParameter("CARDETCCO", serviceRequest.scostcenter),
                new OracleParameter("CARDETUFU", inspiraCita.sunit),
                new OracleParameter("ORDDETORD", inspiraCita.sauthorization),
                new OracleParameter("CARDETCON", serviceRequest.sconcept),
                new OracleParameter("MOVCER", inspiraCita.sagreement),
                new OracleParameter("MOVUAD", inspiraCita.suser)
            };
            object result = oracle.GetScalar(squery.ToString(), oracleParameters, false);
            oracle.Dispose();
            return (result != DBNull.Value && Convert.ToInt32(result) > 0);
        }

        // Nota: El método GetTodayCharges ya no es necesario para la validación, 
        // pero puede que se use en otro lugar. Si no, podrías eliminarlo de las tres capas.

        public DataTable GetTodayCharges()
        {           
            Oracle oracle = new Oracle();
            StringBuilder stringBuilder = new StringBuilder("SELECT PACIDE, PACTID, CARDETCOD, CARDETCON, CARDETCCO, ORDDETORD, MOVCER, CARDETUFU, MOVUAD");
            stringBuilder.Append(" FROM SERVINTE.AYMOV INNER JOIN SERVINTE.ABPAC ON PACHIS = MOVHIS");
            stringBuilder.Append(" INNER JOIN SERVINTE.AYCARDET ON CARDETFUE = MOVFUE AND CARDETDOC = MOVDOC");
            stringBuilder.Append(" INNER JOIN SERVINTE.AYORDDET ON ORDDETDOC = MOVDOC AND ORDDETFUE = MOVFUE");
            stringBuilder.Append(" WHERE MOVANU = 0 AND CARDETANU = 0 AND TRUNC(MOVFEC) = TRUNC(SYSDATE) AND CARDETFAC = 'S' AND CARDETCON NOT IN ('80', 'AJUS') AND MOVFUE = '03'");
            try
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(stringBuilder.ToString(), null);
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                oracle.Dispose();
                oracle = null;                
                stringBuilder = null;
            }
        }

        /// <summary>
        /// Método que obtiene una lista genérica de productos por concepto y tarifa
        /// </summary>
        /// <returns>Lista genérica con los conceptos por tarifa por producto por centro de costo</returns>
        public List<Generic> GetProductConceptsyRate()
        {
            string query = "SELECT * FROM VTARIFAPRODUCTO";
            List<Generic> lgenerics = new List<Generic>();
            Generic generic = null;
            DataTable dt = new DataTable();
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                dt = oracle.GetDataTable(query, null);
                foreach (DataRow dataRow in dt.Rows)
                {
                    generic = new Generic()
                    {
                        scode = dataRow["PROTARTAR"].ToString(),
                        sname = dataRow["PROTARPRO"].ToString(),
                        sfilter = dataRow["PROTARCCO"].ToString(),
                        sextra1 = dataRow["PROTARCON"].ToString(),
                        dextra2 = Convert.ToDouble(dataRow["PROTARVAL"]),
                    };
                    lgenerics.Add(generic);
                }
            }
            dt.Dispose();
            dt = null;
            generic = null;
            return lgenerics;
        }

        #endregion



        #endregion

        #region Migracion de Gestor a Servinte

        public void CreateChargesFromServinte(List<ServintePatient> lservintePatients)
        {
            int ipatient = 0;
            int j = 0;
            this.stype = "Migracion";
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                try
                {
                    foreach (ServintePatient servintePatient in lservintePatients)
                    {
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                        {
                            this.GetEntrySequence(oDAC, this.parametroRip.scia);
                            this.UpdateEntrySequence(oDAC);
                            this.CreateEntry(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAdditional(inspiraCita, servintePatient.scity, servintePatient.safiliation, oDAC);
                            this.CreateEntryLog(inspiraCita, oDAC);
                            this.CreateEgress(inspiraCita, oDAC);
                            this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                            this.CreateAuthorization(inspiraCita, oDAC, false);
                            this.CreateAuthorizationDetail(inspiraCita, oDAC);
                            this.CreateEntryAuditLog(inspiraCita, oDAC);
                            this.CreateAttentionRip(inspiraCita, servintePatient, oDAC);
                            this.CreateAttentionIde(oDAC, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                            this.CreateCharge(inspiraCita, oDAC);
                            foreach (ServiceRequest service in inspiraCita.lservices)
                            {
                                this.CreateChargeDetail(inspiraCita, service, oDAC, j, servintePatient);
                                j++;
                            }
                            this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                            this.GetRipsParameters(oDAC);
                        }
                    }
                    oDAC.Commit();
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
            }
        }

        #endregion

        #region Generación automática de soportes para la historia

        public List<Charge> GetChargesWithoutSupport(string scompany)
        {
            List<Charge> list = new List<Charge>(); 
            DataTable dt = new DataTable();
            Charge charge = null, tmpcharge = null;
            StringBuilder stringBuilder = new StringBuilder("SELECT MOVFUE, MOVDOC, MOVHIS, MOVHCEEPI, MOVFEC, ORDDETDE2, PLANOM, ESCDOCCAT, ESCDOCUSU FROM AYMOV");
            stringBuilder.Append(" INNER JOIN AYMOVHCE ON MOVHCEFUE = MOVFUE AND MOVDOC = MOVHCEDOC");
            stringBuilder.Append(" INNER JOIN AYORDDET ON ORDDETDOC = MOVDOC AND ORDDETFUE = MOVFUE");
            stringBuilder.Append(" INNER JOIN AYMOVOTR ON MOVOTRDOC = MOVDOC AND MOVOTRFUE = MOVFUE");
            stringBuilder.Append(" INNER JOIN INPLA ON PLACOD = MOVOTRPLA");
            stringBuilder.Append(" LEFT JOIN HIESCDOC ON ESCDOCEPI = MOVHCEEPI AND ESCDOCCAT = 'Soporte Clinico'");
            //stringBuilder.Append(" WHERE NOT EXISTS (SELECT ESCDOCNUM FROM HIESCDOC WHERE ESCDOCEPI = MOVHCEEPI AND ESCDOCCAT = 'Soporte Clinico') AND MOVFEC BETWEEN ADD_MONTHS(SYSDATE, -1) AND SYSDATE");
            stringBuilder.Append(" WHERE MOVFEC BETWEEN ADD_MONTHS(SYSDATE, -3) AND SYSDATE");
            //stringBuilder.Append(" WHERE MOVFEC BETWEEN ADD_MONTHS(SYSDATE, -2) AND ADD_MONTHS(SYSDATE, -1)");
            stringBuilder.Append(" AND MOVFUE = '03' AND MOVTIP = 'E' AND MOVOTRPLA NOT IN ('");
            stringBuilder.Append(scompany);
            stringBuilder.Append("') AND ESCDOCCAT IS NULL AND MOVANU = 0");
            //stringBuilder.Append(" FETCH FIRST 1000 ROWS ONLY");
            stringBuilder.Append(" UNION ALL");
            stringBuilder.Append(" SELECT MOVFUE, MOVDOC, MOVHIS, MOVHCEEPI, MOVFEC, ORDDETDE2, PLANOM, ESCDOCCAT, ESCDOCUSU FROM AYMOV");
            stringBuilder.Append(" INNER JOIN AYMOVHCE ON MOVHCEFUE = MOVFUE AND MOVDOC = MOVHCEDOC");
            stringBuilder.Append(" INNER JOIN AYORDDET ON ORDDETDOC = MOVDOC AND ORDDETFUE = MOVFUE");
            stringBuilder.Append(" INNER JOIN AYMOVOTR ON MOVOTRDOC = MOVDOC AND MOVOTRFUE = MOVFUE");
            stringBuilder.Append(" INNER JOIN INPLA ON PLACOD = MOVOTRPLA");
            stringBuilder.Append(" INNER JOIN HIESCDOC ON ESCDOCEPI = MOVHCEEPI");
            stringBuilder.Append(" WHERE MOVFEC BETWEEN ADD_MONTHS(SYSDATE, -3) AND SYSDATE");
            //stringBuilder.Append(" WHERE MOVFEC BETWEEN ADD_MONTHS(SYSDATE, -2) AND ADD_MONTHS(SYSDATE, -1)");
            stringBuilder.Append(" AND MOVFUE = '03' AND MOVTIP = 'E' AND MOVOTRPLA NOT IN ('");
            stringBuilder.Append(scompany);
            stringBuilder.Append("') AND ESCDOCCAT = 'Soporte Clinico' AND MOVANU = 0");
            //stringBuilder.Append(" FETCH FIRST 300 ROWS ONLY");*/
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                try
                {
                    dt = oracle.GetDataTable(stringBuilder.ToString(), null);
                    List<Charge> ltmp = this.GetChargesLinesDetail(scompany, oracle);
                    foreach (DataRow dr in dt.Rows)
                    {
                        charge = new Charge()
                        {
                            inumber = Convert.ToInt32(dr["MOVDOC"]),
                            ssource = dr["MOVFUE"].ToString(),
                            iusertype = Convert.ToInt32(dr["MOVHIS"]),
                            ilevel = Convert.ToInt32(dr["MOVHCEEPI"]),
                            dcreateddate = Convert.ToDateTime(dr["MOVFEC"]),
                            sappointment = dr["ORDDETDE2"].ToString(),
                            splanname = dr["PLANOM"].ToString(),
                            sprogram = (dr["ESCDOCCAT"] != DBNull.Value) ? dr["ESCDOCCAT"].ToString() : string.Empty,
                            sattentiontype = (dr["ESCDOCCAT"] != DBNull.Value) ? "false" : "true",
                            scode = dr["ESCDOCUSU"].ToString(),
                        };
                        tmpcharge = ltmp.FirstOrDefault(x => x.inumber == charge.inumber && x.ssource == charge.ssource);
                        if (tmpcharge != null)
                        {
                            charge.sagreementcode = tmpcharge.sagreementcode;
                            charge.sagreementname = tmpcharge.sagreementname;
                            if (tmpcharge.sconcept == "7000")
                            {
                                charge.scostcenter = "INSUMOS Y MEDICAMENTOS";
                                charge.sappointment = "NO APLICA";
                            }
                            else
                            {
                                charge.scostcenter = tmpcharge.scostcenter;
                            }                            
                        }
                        list.Add(charge);
                    }
                    return list;
                }
                catch (Exception ex)
                {
                    LogError.WriteError("CreaSoporteCargos", "DAC", ex);
                    throw;
                }
                finally
                {
                    dt.Dispose();
                    dt = null;
                    charge = null;
                }
                
            }
        }

        private List<Charge> GetChargesLinesDetail(string scompany, Oracle oracle)
        {
            DataTable dt = new DataTable();
            Charge charge = null;
            List<Charge> list = new List<Charge>();
            StringBuilder stringBuilder = new StringBuilder("SELECT CARDETFUE, CARDETDOC, CARDETCOD, PRONOM, CCONOM, CARDETCON FROM AYMOV");            
            stringBuilder.Append(" INNER JOIN AYCARDET ON CARDETFUE = MOVFUE AND CARDETDOC = MOVDOC");
            stringBuilder.Append(" INNER JOIN AYMOVOTR ON MOVOTRDOC = MOVDOC AND MOVOTRFUE = MOVFUE");
            stringBuilder.Append(" LEFT JOIN INPRO ON PROCOD = CARDETCOD");
            stringBuilder.Append(" INNER JOIN COCCO ON CARDETCCO = CCOCOD");
            stringBuilder.Append(" WHERE MOVFEC BETWEEN ADD_MONTHS(SYSDATE, -3) AND SYSDATE");
            //stringBuilder.Append(" WHERE MOVFEC BETWEEN ADD_MONTHS(SYSDATE, -2) AND ADD_MONTHS(SYSDATE, -1)");
            stringBuilder.Append(" AND MOVFUE = '03' AND MOVTIP = 'E' AND MOVOTRPLA NOT IN ('");
            stringBuilder.Append(scompany);
            stringBuilder.Append("') AND CARDETCCO <> '9190' AND MOVANU = 0");
            //stringBuilder.Append(" AND CARDETDOC = 1376278");            
            dt = oracle.GetDataTable(stringBuilder.ToString(), null);
            foreach (DataRow dr in dt.Rows)
            {
                charge = new Charge()
                {
                    inumber = Convert.ToInt32(dr["CARDETDOC"]),
                    ssource = dr["CARDETFUE"].ToString(),      
                    sagreementcode = dr["CARDETCOD"].ToString(),
                    sagreementname = dr["PRONOM"].ToString(),     
                    scostcenter = dr["CCONOM"].ToString(),
                    sconcept = dr["CARDETCON"].ToString()
                };
                list.Add(charge);
            }
            return list;
        }

        public int GetChargeChapter(int icharge, string ssource, Oracle oracle)
        {
            string squery = "SELECT MOVHCEEPI FROM AYMOVHCE WHERE MOVHCEFUE = :MOVHCEFUE AND MOVHCEDOC = :MOVHCEDOC";
            List<OracleParameter> parameters = new List<OracleParameter>();
            parameters.Add(new OracleParameter("MOVHCEFUE", ssource));
            parameters.Add(new OracleParameter("MOVHCEDOC", icharge));
            object ichapter = oracle.GetScalar(squery, parameters, true);
            return (ichapter != null ? (int)ichapter : 0);
        }

        public List<Generic> CreateChargeSupports(Charge charge, List<Generic> lfiles)
        {
            Oracle oracle = new Oracle();
            oracle.sConnection = this.sconnection;
            int isequence = 0;
            StringBuilder sb = new StringBuilder();
            StringBuilder squery = new StringBuilder();
            List<OracleParameter> parameters = new List<OracleParameter>();
            try
            {
                oracle.Connect();
                oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                int j = 0, ichapter = 0;
                //ichapter = this.GetChargeChapter(charge.inumber, charge.ssource, oracle);
                ichapter = charge.ilevel;
                if (ichapter > 0)
                {
                    squery.Append("INSERT INTO HIESCDOC (ESCDOCNUM, ESCDOCEPI, ESCDOCDCI, ESCDOCDES, ESCDOCCAT, ESCDOCFEC, ESCDOCUSU, ESCDOCPAG, ESCDOCHIS, ESCDOCUBI, ESCDOCNOM, ESCDOCITE)");
                    squery.Append(" VALUES (SQ_HIESCDOC_NUM.NEXTVAL, :ESCDOCEPI, :ESCDOCDCI, :ESCDOCDES, :ESCDOCCAT, :ESCDOCFEC, :ESCDOCUSU, :ESCDOCPAG, :ESCDOCHIS, :ESCDOCUBI, :ESCDOCNOM, :ESCDOCITE)");
                    //parameters.Add(new OracleParameter("ESCDOCNUM", "SELECT SQ_HIESCDOC_NUM.NEXTVAL FROM DUAL"));
                    parameters.Add(new OracleParameter("ESCDOCEPI", ichapter));
                    parameters.Add(new OracleParameter("ESCDOCDCI", isequence));
                    parameters.Add(new OracleParameter("ESCDOCDES", "003"));
                    parameters.Add(new OracleParameter("ESCDOCCAT", "Soporte Clinico"));
                    parameters.Add(new OracleParameter("ESCDOCFEC", DateTime.Now));
                    parameters.Add(new OracleParameter("ESCDOCUSU", "admon"));
                    parameters.Add(new OracleParameter("ESCDOCPAG", lfiles.Count));
                    parameters.Add(new OracleParameter("ESCDOCHIS", charge.iusertype));
                    parameters.Add(new OracleParameter("ESCDOCUBI", "99"));
                    parameters.Add(new OracleParameter("ESCDOCNOM", sb.ToString()));
                    parameters.Add(new OracleParameter("ESCDOCITE", "1"));
                    oracle.ExecuteNonQuery(squery.ToString(), parameters, false, true);
                    squery.Clear();
                    parameters.Clear();
                    isequence = this.GetSequenceCurrentValue("SQ_HIESCDOC_NUM", oracle);
                    sb.Append(charge.iusertype.ToString());
                    sb.Append("-");
                    sb.Append(ichapter.ToString());
                    sb.Append("-003-");
                    sb.Append(isequence.ToString());
                    sb.Append("-");
                    sb.Append(DateTime.Now.ToString("ddMMyyyy"));
                    squery.Append("UPDATE HIESCDOC SET ESCDOCDCI = :ESCDOCDCI, ESCDOCNOM = :ESCDOCNOM WHERE ESCDOCNUM = :ESCDOCNUM");
                    parameters.Add(new OracleParameter("ESCDOCDCI", isequence));
                    parameters.Add(new OracleParameter("ESCDOCNOM", sb.ToString()));
                    parameters.Add(new OracleParameter("ESCDOCNUM", isequence));
                    oracle.ExecuteNonQuery(squery.ToString(), parameters, false, true);
                    foreach (var item in lfiles)
                    {                        
                        lfiles[j].scode = sb.ToString();                                                            
                        j++;
                    }
                    oracle.Commit();
                }                
                return lfiles;
            }
            catch (Exception ex)
            {
                if (oracle.oracleTransaction != null)
                {
                    if (oracle.oracleConnection.State == ConnectionState.Open)
                    {
                        oracle.RollBack();
                    }
                }
                LogError.WriteError("CreaSoporteCargos", "DAC", ex);
                throw;
            }
            finally
            {
                oracle.Dispose();
                oracle = null;
            }
        }

        #endregion

        #region Carga Servinte Plantillas

        /// <summary>
        /// Método para obtener las listas genéricas de código y nombre de la vista VTABLASBASICAS
        /// </summary>
        /// <returns>DataTable con las listas genéricas</returns>
        public DataTable GetGenericTable()
        {
            //StringBuilder squery = new StringBuilder("SELECT CODIGO, NOMBRE, TABLA FROM VTABLASBASICAS");
            StringBuilder squery = new StringBuilder("SELECT CODIGO, NOMBRE, TABLA FROM VTABLASBASICAS");
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(squery.ToString(), null);
            }
        }

        /// <summary>
        /// Método para obtener el listado de paquetes vista VPAQUETES
        /// </summary>
        /// <returns>DataTable con los paquetes</returns>
        public DataTable GetPackages()
        {
            //string squery = "SELECT * FROM VPAQUETES";
            string squery = "SELECT * FROM VPAQUETES";
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(squery, null);
            }
        }

        /// <summary>
        /// Método para obtener las tarifas por producto vista VTARIFAPRODUCTO
        /// </summary>
        /// <returns>DataTable con las tarifas pro producto</returns>
        public DataTable GetProductRates()
        {
            string squery = "SELECT * FROM VTARIFAPRODUCTO";
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(squery, null);
            }
        }

        /// <summary>
        /// Método para crear los cargos para Urgencias que no van a Servinte, únicamente a la estadística
        /// </summary>
        /// <param name="lpatients">Lista genérica de pacientes</param>
        /// <returns>Lista genérica de resultado de la transacción</returns>
        public List<EntryResponse> CreateEmergenciesCharges(List<ServintePatient> lpatients)
        {
            this.stype = "Urgencias";
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            EntryResponse entryResponse = null;
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                try
                {
                    foreach (ServintePatient servintePatient in lpatients)
                    {
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                        {
                            foreach (ServiceRequest serviceRequest in inspiraCita.lservices)
                            {
                                entryResponse = new EntryResponse()
                                {
                                    ddate = inspiraCita.ddate.ToString("yyyy-MM-dd"),
                                    ipatient = servintePatient.iid,
                                    sagreement = inspiraCita.sagreement,
                                    sconcept = serviceRequest.sconcept,
                                    scostcenter = serviceRequest.scostcenter,
                                    sdocument = servintePatient.sdocument,
                                    sdocumenttype = servintePatient.sdocumenttype,
                                    splan = inspiraCita.splan,
                                    srate = inspiraCita.srate,
                                    iline = 1,
                                    sservice = serviceRequest.sservice,
                                    sservicename = serviceRequest.sservicename,
                                    dvalue = Convert.ToInt32(serviceRequest.ivalue),
                                    iqty = serviceRequest.iqty,
                                    stype = "Urgencias solo estadistica",
                                    sunit = inspiraCita.sunit,
                                    icharge = 0,
                                    ientry = 0,
                                    sservicegroup = inspiraCita.sservicegroup,
                                };
                                this.lresponse.Add(entryResponse);
                            }
                        }
                    }
                    oDAC.Commit();
                    return lresponse;
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Método para crear los cargos que vienen desde hospitalización
        /// </summary>
        /// <param name="lpatients">Lista genérica de pacientes Servinte</param>
        /// <returns>Lista genérica resultado de la creación de cargos</returns>
        public List<EntryResponse> CreateHospitalizationCharges(List<ServintePatient> lpatients)
        {
            this.stype = "Hospitalizacion";
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            int i = 0, j = 0;
            List<ServintePatient> patients = lpatients.GroupBy(x => new { x.sdocument, x.sdocumenttype }).Select(y => y.First()).ToList();
            List<ServintePatient> linspiraCitas = null;
            InspiraCita inspiraCita = null;
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                try
                {
                    foreach (ServintePatient servintePatient in patients)
                    {
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        linspiraCitas = lpatients.FindAll(x => x.sdocument == servintePatient.sdocument && x.sdocumenttype == servintePatient.sdocumenttype);
                        i = 0;
                        this.ientry = 0;
                        foreach (ServintePatient patient in linspiraCitas)
                        {
                            inspiraCita = patient.lappointments[0];
                            this.GetEntrySequence(oDAC, this.parametroRip.scia);
                            this.UpdateEntrySequence(oDAC);
                            this.CreateEntry(inspiraCita, servintePatient, oDAC);
                            //this.CreateEntryAdditional(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAdditional(inspiraCita, servintePatient.scity, servintePatient.safiliation, oDAC);
                            this.CreateEntryLog(inspiraCita, oDAC);
                            this.CreateEgress(inspiraCita, oDAC);
                            this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAuditLog(inspiraCita, oDAC);
                            this.CreateAttentionRip(inspiraCita, servintePatient, oDAC);
                            this.CreateAttentionIde(oDAC, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                            this.CreateCharge(inspiraCita, oDAC);
                            j = 0;
                            foreach (ServiceRequest service in inspiraCita.lservices)
                            {
                                this.CreateChargeDetail(inspiraCita, service, oDAC, j, servintePatient, false);
                                j++;
                            }
                            this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                            this.GetRipsParameters(oDAC);
                            if (i == 0)
                            {
                                this.ientry = this.parametroRip.ientry;
                            }
                            i++;
                        }
                    }
                    oDAC.Commit();
                    return lresponse;
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
                finally
                {
                    linspiraCitas = null;
                    patients = null;
                }
            }
        }
        /// <summary>
        /// Método para crear los cargos por plantilla de las valoraciones
        /// </summary>
        /// <param name="lpatients">Lista genérica de pacientes</param>
        /// <returns>Lista genérica de respuesta de la creación de los cargos</returns>
        public List<EntryResponse> CreateValuationServices(List<ServintePatient> lpatients)
        {
            this.stype = "Programas plantilla inicial";
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            int i = 0, j = 0;
            List<int> lentries = new List<int>();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                lentries = this.GetAllEntries(oDAC);
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                try
                {
                    foreach (ServintePatient servintePatient in lpatients)
                    {
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        i = 0;
                        this.ientry = 0;
                        foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                        {
                            if (inspiraCita.ientrysource != 0 && this.EntryIsActive(lentries, inspiraCita.ientrysource)) this.ientry = inspiraCita.ientrysource;
                            this.GetEntrySequence(oDAC, this.parametroRip.scia);
                            this.UpdateEntrySequence(oDAC);
                            this.CreateEntry(inspiraCita, servintePatient, oDAC);
                            //this.CreateEntryAdditional(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAdditional(inspiraCita, servintePatient.scity, servintePatient.safiliation, oDAC);
                            this.CreateEntryLog(inspiraCita, oDAC);
                            this.CreateEgress(inspiraCita, oDAC);
                            this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAuditLog(inspiraCita, oDAC);
                            this.CreateAttentionRip(inspiraCita, servintePatient, oDAC);
                            this.CreateAttentionIde(oDAC, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                            this.CreateCharge(inspiraCita, oDAC);
                            j = 0;
                            foreach (ServiceRequest service in inspiraCita.lservices)
                            {
                                this.CreateChargeDetail(inspiraCita, service, oDAC, j, servintePatient, false);
                                j++;
                            }
                            if (i == 0 && this.ientry == 0)
                            {
                                this.ientry = this.parametroRip.ientry;
                            }
                            this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                            this.GetRipsParameters(oDAC);
                            i++;
                        }
                    }
                    oDAC.Commit();
                    return lresponse;
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
                finally
                {
                    lpatients = null;
                }
            }
        }

        /// <summary>
        /// Método para crear los cargos que vienen desde hospitalización
        /// </summary>
        /// <param name="lpatients">Lista genérica de pacientes Servinte</param>
        /// <returns>Lista genérica resultado de la creación de cargos</returns>
        public List<EntryResponse> CreatServicesCharges(List<ServintePatient> lpatients)
        {
            this.stype = "Otros Servicios";
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            int i = 0, j = 0;            
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                try
                {
                    foreach (ServintePatient servintePatient in lpatients)
                    {
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }                        
                        i = 0;
                        this.ientry = 0;
                        foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                        {                                                                                    
                            this.GetEntrySequence(oDAC, this.parametroRip.scia);
                            this.UpdateEntrySequence(oDAC);
                            if (inspiraCita.ientrysource != 0)
                            {
                                this.ientry = inspiraCita.ientrysource;
                            }
                            this.CreateEntry(inspiraCita, servintePatient, oDAC);
                            //this.CreateEntryAdditional(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAdditional(inspiraCita, servintePatient.scity, servintePatient.safiliation, oDAC);
                            this.CreateEntryLog(inspiraCita, oDAC);
                            this.CreateEgress(inspiraCita, oDAC);
                            this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                            if (!string.IsNullOrEmpty(inspiraCita.sauthorization))
                            {
                                this.CreateAuthorization(inspiraCita, oDAC, false);
                                this.CreateAuthorizationDetail(inspiraCita, oDAC);
                            }
                            this.CreateEntryAuditLog(inspiraCita, oDAC);
                            this.CreateAttentionRip(inspiraCita, servintePatient, oDAC);
                            this.CreateAttentionIde(oDAC, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                            this.CreateCharge(inspiraCita, oDAC);
                            j = 0;
                            foreach (ServiceRequest service in inspiraCita.lservices)
                            {
                                this.CreateChargeDetail(inspiraCita, service, oDAC, j, servintePatient, false);
                                j++;
                            }
                            if (i == 0 && inspiraCita.ientrysource == 0)
                            {
                                this.ientry = this.parametroRip.ientry;
                            }
                            this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                            this.GetRipsParameters(oDAC);                           
                            i++;
                        }
                    }
                    oDAC.Commit();
                    return lresponse;
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
                finally
                {                    
                    lpatients = null;
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="lpatients"></param>
        /// <returns></returns>
        public List<EntryResponse> CreateFibrosisCharges(List<ServintePatient> lpatients)
        {
            this.stype = "Fibrosis";
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            int i = 0, j = 0;
            List<ServintePatient> patients = lpatients.GroupBy(x => new { x.sdocument, x.sdocumenttype }).Select(y => y.First()).ToList();
            List<ServintePatient> linspiraCitas = null;
            InspiraCita inspiraCita = null;
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                try
                {
                    foreach (ServintePatient servintePatient in patients)
                    {
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        linspiraCitas = lpatients.FindAll(x => x.sdocument == servintePatient.sdocument && x.sdocumenttype == servintePatient.sdocumenttype);
                        i = 0;
                        this.ientry = 0;
                        foreach (ServintePatient patient in linspiraCitas)
                        {
                            inspiraCita = patient.lappointments[0];
                            this.GetEntrySequence(oDAC, this.parametroRip.scia);
                            this.UpdateEntrySequence(oDAC);
                            this.CreateEntry(inspiraCita, servintePatient, oDAC);
                            //this.CreateEntryAdditional(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAdditional(inspiraCita, servintePatient.scity, servintePatient.safiliation, oDAC);
                            this.CreateEntryLog(inspiraCita, oDAC);
                            this.CreateEgress(inspiraCita, oDAC);
                            this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAuditLog(inspiraCita, oDAC);
                            this.CreateAttentionRip(inspiraCita, servintePatient, oDAC);
                            this.CreateAttentionIde(oDAC, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                            this.CreateCharge(inspiraCita, oDAC);
                            j = 0;
                            foreach (ServiceRequest service in inspiraCita.lservices)
                            {
                                this.CreateChargeDetail(inspiraCita, service, oDAC, j, servintePatient, false);
                                j++;
                            }
                            this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                            this.GetRipsParameters(oDAC);
                            if (i == 0)
                            {
                                this.ientry = this.parametroRip.ientry;
                            }
                            i++;
                        }
                    }
                    oDAC.Commit();
                    return lresponse;
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
                finally
                {
                    linspiraCitas = null;
                    patients = null;
                }
            }
        }

        /// <summary>
        /// Método para crear los cargos desde la plantilla de programas
        /// </summary>
        /// <param name="lservintePatients">Lista genérica de pacientes para crear los cargos</param>
        /// <returns>Lista genérica de respuesta para la estadística</returns>
        public List<EntryResponse> CreateEntryForPrograms(List<ServintePatient> lservintePatients)
        {
            this.stype = "Plantilla Programas";
            this.lresponse = new List<EntryResponse>();
            int ipatient = 0;
            int i = 0, j = 0;
            decimal itotalappointment = 0, itotalpackage = 0;
            List<int> lentries = new List<int>();
            using (Oracle oDAC = new Oracle())
            {
                try
                {
                    oDAC.sConnection = this.sconnection;
                    oDAC.Connect();
                    oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                    lentries = this.GetAllEntries(oDAC);
                    foreach (ServintePatient servintePatient in lservintePatients)
                    {
                        this.ientry = 0;
                        this.CreateUserLog(oDAC);
                        ipatient = this.PatientExists(servintePatient, oDAC);
                        if (ipatient == 0)
                        {
                            servintePatient.iid = this.CreatePatient(servintePatient, oDAC);
                            this.CreatePatientExtraInformation(servintePatient, oDAC);
                        }
                        else
                        {
                            servintePatient.iid = ipatient;
                            this.UpdatePatient(servintePatient, oDAC);
                            this.UpdatePatientExtraInformation(servintePatient, oDAC);
                        }
                        i = 0;
                        itotalappointment = 0;
                        itotalpackage = 0;
                        foreach (InspiraCita inspiraCita in servintePatient.lappointments)
                        {                            
                            j = 0;
                            if (inspiraCita.ientrysource != 0 && this.EntryIsActive(lentries, inspiraCita.ientrysource)) this.ientry = inspiraCita.ientrysource;
                            this.GetEntrySequence(oDAC, this.parametroRip.scia);
                            this.UpdateEntrySequence(oDAC);
                            this.CreateEntry(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryAdditional(inspiraCita, servintePatient, oDAC);
                            this.CreateEntryLog(inspiraCita, oDAC);
                            this.CreateEgress(inspiraCita, oDAC);
                            this.CreateEntryVars(inspiraCita, servintePatient, oDAC);
                            if (!string.IsNullOrEmpty(inspiraCita.sauthorization))
                            {
                                this.CreateAuthorization(inspiraCita, oDAC, false);
                                this.CreateAuthorizationDetail(inspiraCita, oDAC);
                            }
                            this.CreateEntryAuditLog(inspiraCita, oDAC);
                            this.CreateAttentionRip(inspiraCita, servintePatient, oDAC);
                            this.CreateAttentionIde(oDAC, servintePatient.safiliation, Tools.GetUserType1306(servintePatient.safiliation, Tools.GetAge(servintePatient.dbirthdate.Value)));
                            this.CreateCharge(inspiraCita, oDAC);
                            foreach (ServiceRequest service in inspiraCita.lservices)
                            {
                                this.CreateSingleChargeDetail(inspiraCita, service, servintePatient, string.Empty, j, oDAC, false);
                                j++;
                                itotalappointment += service.iqty * service.ivalue;
                            }                            
                            /*if ((inspiraCita.itotal - this.GetEntryValue(inspiraCita.lservices) != 0))
                            {
                                this.CreateChargeForLAW(inspiraCita, j, oDAC);
                            }*/
                            this.IncreaseAttentionSequence(oDAC, this.parametroRip.scia);
                            this.GetRipsParameters(oDAC);
                            if (i == 0 && this.ientry == 0)
                            {
                                itotalpackage = inspiraCita.itotal;
                                this.ientry = this.parametroRip.ientry;
                            }
                            i++;
                        }
                        this.CreateChargeForLAW(servintePatient.lappointments[0], j, oDAC, itotalappointment, itotalpackage, servintePatient);
                    }
                    oDAC.Commit();
                }
                catch (Exception ex)
                {
                    if (oDAC.oracleTransaction != null)
                    {
                        if (oDAC.oracleConnection.State == ConnectionState.Open)
                        {
                            oDAC.RollBack();
                        }
                    }
                    LogError.WriteError("FNCInspira", "DAC", ex);
                    throw;
                }
            }
            return this.lresponse;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oracle"></param>
        /// <returns></returns>
        private List<int> GetAllEntries(Oracle oracle)
        {
            List<int> lstentries = new List<int>();
            if (oracle != null)
            {
                string squery = "SELECT MOVDOC FROM AYMOV WHERE MOVANU = 0 AND MOVEST = 'A' AND MOVFUE = '03'";
                DataTable dt = oracle.GetDataTable(squery, null);
                if (dt != null)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        lstentries.Add(Convert.ToInt32(dr["MOVDOC"]));
                    }
                    dt.Dispose();
                    dt = null;
                }                
            }
            return lstentries;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lstentries"></param>
        /// <param name="ientry"></param>
        /// <returns></returns>
        private bool EntryIsActive(List<int> lstentries, int ientry)
        {
            return (lstentries.FirstOrDefault(x => x.Equals(ientry)) != 0);
        }

        #endregion

        #region Procedimiento Acostados

        public void CreatePendingChargesReport()
        {
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                oracle.ExecuteNonQuery("SP_FNCGENERAREPORTEACOSTADOS", null, true);
            }
        }

        #endregion
        /// <summary>
        /// Método para destruir el objeto y liberar la memoria
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
