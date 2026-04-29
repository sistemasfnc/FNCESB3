using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using FNCEntity;
using static System.Net.Mime.MediaTypeNames;

namespace FNCDAC
{
    public class Digiturno : IDisposable
    {
        private string sConnection { get; set; }

        public Digiturno(string Connection)
        {
            this.sConnection = Connection;
        }

        public string GetQueueCode(string sCode)
        {
            string Query = "SELECT COL_PKUNICODIGO FROM DG45_COLAS WHERE COL_SDSTRNOMBRE = @Code";
            List<SqlParameter> lParameters = new List<SqlParameter>();
            lParameters.Add(new SqlParameter("@Code", sCode));
            object sReturn = null;
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                sReturn = oDAC.GetScalar(Query, lParameters);
                return (sReturn != null) ? sReturn.ToString() : string.Empty;
            }
        }

        public string GetPriorityCode(string sPriority)
        {
            string Query = "SELECT [PRI_PKUNICODIGO] FROM [DG45_PRIORIDADES_SALA] WHERE [PRI_SDSTRNOMBRE] = @Code";
            List<SqlParameter> lParameters = new List<SqlParameter>();
            lParameters.Add(new SqlParameter("@Code", sPriority));
            object sReturn = null;
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                sReturn = oDAC.GetScalar(Query, lParameters);
                return (sReturn != null) ? sReturn.ToString() : string.Empty;
            }
        }

        public bool ValidateTurn(string doctype, string document, string date, string starttime, string endtime)
        {
            object sReturn = null;
            StringBuilder sQuery = new StringBuilder("SELECT patientdocument FROM digiturno WHERE patientdocument = @documento");
            sQuery.Append(" AND documenttype = @type AND appointmentdate = @date AND starthour = @starthour AND endhour = @endhour");
            List<SqlParameter> lParameters = new List<SqlParameter>();
            lParameters.Add(new SqlParameter("@documento", document));
            lParameters.Add(new SqlParameter("@type", doctype));
            lParameters.Add(new SqlParameter("@date", date));
            lParameters.Add(new SqlParameter("@starthour", starttime));
            lParameters.Add(new SqlParameter("@endhour", endtime));
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                sReturn = oDAC.GetScalar(sQuery.ToString(), lParameters);
                return (sReturn != null);
            }
        }

        public void Insert(string doctype, string document, string date, string starttime, string endtime)
        {
            StringBuilder sQuery = new StringBuilder("INSERT INTO digiturno VALUES(@type, @documento, @date, @starthour, @endhour)");
            List<SqlParameter> lParameters = new List<SqlParameter>();
            lParameters.Add(new SqlParameter("@documento", document));
            lParameters.Add(new SqlParameter("@type", doctype));
            lParameters.Add(new SqlParameter("@date", date));
            lParameters.Add(new SqlParameter("@starthour", starttime));
            lParameters.Add(new SqlParameter("@endhour", endtime));
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters);
            }
        }

        /// <summary>
        /// Método que obtiene el listado de turnos a mostrar en la pantalla de pediatría
        /// </summary>
        /// <returns>Lista genérica con los turnos encontrados</returns>
        public List<TurnoEntity> GetListTurn()
        {
            List<TurnoEntity> lTurno = new List<TurnoEntity>();
            string sQuery = "SELECT * FROM VTurnosPediatria ORDER BY TRA_SDDATHORASOLICITUD DESC";
            DataTable dt = new DataTable();
            TurnoEntity oTurno = null;
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                dt = oDAC.GetDataTable(sQuery, null);
                foreach (DataRow dr in dt.Rows)
                {
                    oTurno = new TurnoEntity()
                    {
                        roomcode = dr["TER_SDINTNID"].ToString(),
                        patientname = dr["TUR_SDSTRNOMBRECLIENTE"].ToString(),
                    };
                    lTurno.Add(oTurno);
                }
                dt.Dispose();
                dt = null;
                oTurno = null;
                return lTurno;
            }


        }

        /// <summary>
        /// Método que inserta el turno en la tabla TableroExternoFNC para poder mostrar el turno en el tablero
        /// </summary>
        /// <param name="sRoom">String número de sala</param>
        /// <param name="sPatient">String nombre del paciente</param>
        /// <param name="sTurn">String número de turno</param>
        /// <param name="sDocumentType">String tipo de documento del paciente</param>
        /// <param name="sDocument">String documento del paciente</param>
        /// <param name="bIsUpdate">Boleano que indica si se actualiza la tabla o se realiza el insert</param>
        /// <param name="sFloor">String piso de atención</param>
        public void PatientOnCall(string sRoom, string sPatient, string sTurn, string sDocumentType, string sDocument, bool bIsUpdate, string sFloor)
        {
            if (string.IsNullOrEmpty(sTurn)) sTurn = this.GetTurnByPatient(sDocument, sDocumentType);
            StringBuilder sQuery = new StringBuilder();
            List<SqlParameter> lParameters = new List<SqlParameter>();
            if (!bIsUpdate)
            {
                sRoom = sRoom.Replace("PED", string.Empty);
                sRoom = sRoom.Replace("CONS", string.Empty);
                sQuery.Append("INSERT INTO TableroExternoFNC ([Turno_Numero], [Turno_HoraInsercionEnTabla], [Turno_EnLlamado], [TipoId_DescripcionTipoId],");
                sQuery.Append("[Cliente_Identificacion], [Cliente_NombreCompleto], [Cola_Nombre], [Asesor_Nombre], [Asesor_Modulo], [Hora_Actualizacion], [Piso_Nombre])");
                sQuery.Append(" VALUES (@Turno, CONVERT(DATETIME, GETDATE(), 111), 1, @TipoDocumento, @Documento, @Nombre, '(G) Cita Adulto FCI - FNC'");
                sQuery.Append(", 'Agenda Inspira', @Sala,  CONVERT(DATETIME, GETDATE(), 111), @Piso)");
                lParameters.Add(new SqlParameter("@Turno", sTurn));
                lParameters.Add(new SqlParameter("@TipoDocumento", sDocumentType));
                lParameters.Add(new SqlParameter("@Documento", sDocument));
                lParameters.Add(new SqlParameter("@Nombre", sPatient));
                lParameters.Add(new SqlParameter("@Sala", sRoom));
                lParameters.Add(new SqlParameter("@Piso", sFloor));                
            }
            else
            {
                sQuery.Append("UPDATE TableroExternoFNC SET [Turno_EnLlamado] = 0, [Hora_Actualizacion] = CONVERT(DATETIME, GETDATE(), 111)");
                sQuery.Append(" WHERE [Cliente_Identificacion] = @Documento");
                lParameters.Add(new SqlParameter("@Documento", sDocument));
            }
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters);
            }
            lParameters = null;
            sQuery = null;
        }

        /// <summary>
        /// Método para obtener el id de cola de acuerdo al nombre
        /// </summary>
        /// <param name="sName">String nombre de la cola</param>
        /// <returns>Entero id de la cola buscada</returns>
        public int GetQueueIdByName(string sName)
        {
            string sQuery = "SELECT IdCola FROM Colas WHERE Nombre = @Nombre";
            List<SqlParameter> lParameters = new List<SqlParameter>();
            object oresult = null;
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@Nombre", sName));
                oresult = oDAC.GetScalar(sQuery.ToString(), lParameters);
                return (oresult != null) ? Convert.ToInt32(oresult) : 7;
            }
        }

        /// <summary>
        /// Método para obtener el ID de un turno por su número
        /// </summary>
        /// <param name="sTurn">String número de turno</param>
        /// <returns>Entero con el Id del turno encontrado</returns>
        private int GetTurnId(string sTurn)
        {
            object oTurn = null;
            string sQuery1 = "SELECT IdTurno FROM Turnos WHERE CAST(HoraCreacion AS DATE) = CAST(GETDATE() AS DATE) AND Numero = @Turno";
            string sQuery2 = "SELECT IdTurno FROM H_Turnos WHERE CAST(HoraCreacion AS DATE) = CAST(GETDATE() AS DATE) AND Numero = @Turno";
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@Turno", sTurn));
                oTurn = oDAC.GetScalar(sQuery2, lParameters);
                if (oTurn != null)
                {
                    return Convert.ToInt32(oTurn);
                }
                else
                {
                    oTurn = oDAC.GetScalar(sQuery1, lParameters);
                    return (oTurn != null) ? Convert.ToInt32(oTurn) : 0;
                }
            }
        }

        /// <summary>
        /// Método que valida si un turno ya existe en la tabla TableroExternoFNC para actualizarlo o hacer el llamado
        /// </summary>
        /// <param name="sTurn">String número de turno</param>
        /// <returns>Boleano que indica si el turno existe o no en la tabla</returns>
        private bool TurnExists(string sTurn)
        {
            string sQuery = "SELECT Turno_Id FROM TableroExternoFNC WHERE CAST(Hora_Actualizacion AS DATE) = CAST(GETDATE() AS DATE) AND Turno_Numero = @Turno";
            object oTurn = null;
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@Turno", sTurn));
                oTurn = oDAC.GetScalar(sQuery, lParameters);
                return (oTurn != null) ? (Convert.ToInt32(oTurn) != 0) : false;
            }
        }

        /// <summary>
        /// Método para obtener el número de turno dependiendo del documento y el tipo de documento del paciente
        /// </summary>
        /// <param name="sDocument">String número de documento del paciente</param>
        /// <param name="sDocumentType">String tipo de documento del paciente</param>
        /// <returns>String número de turno del paciente</returns>
        private string GetTurnByPatient(string sDocument, string sDocumentType)
        {
            StringBuilder sQuery = new StringBuilder("SELECT TOP 1 Numero FROM Turnos, TurnosXClientes, UsuariosClientes, TipoDocumento WHERE TurnosXClientes.IdUsuarioCliente = UsuariosClientes.IdCliente");
            sQuery.Append(" AND UsuariosClientes.IdTipoIdentificacion = TipoDocumento.IdTipoIdentificacion AND TurnosXClientes.IdTurno = Turnos.IdTurno AND TipoDocumento.IdTipoIdentificacion = UsuariosClientes.IdTipoIdentificacion");
            sQuery.Append(" AND TipoDocumento.NOMBRECORTO = @TipoDocumento AND Identificacion = @Documento ORDER BY HoraCreacion DESC");
            object oResult = null;
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@TipoDocumento", sDocumentType));
                lParameters.Add(new SqlParameter("@Documento", sDocument));
                oResult = oDAC.GetScalar(sQuery.ToString(), lParameters);
                return (oResult != null) ? oResult.ToString() : "A401";
            }
        }

        /// <summary>
        /// Método que obtiene un paciente de la base de datos del digiturno por el número de turno
        /// </summary>
        /// <param name="sTurn">String número de turno</param>
        /// <returns>Objeto genérico con el documento y el tipo de documento del paciente</returns>
        public Generic GetPatientFromTurn(string sTurn)
        {
            StringBuilder sQuery = new StringBuilder("SELECT NOMBRECORTO, Identificacion FROM TipoDocumento, UsuariosClientes, TurnosXClientes, Turnos WHERE TurnosXClientes.IdUsuarioCliente = UsuariosClientes.IdCliente");
            sQuery.Append(" AND UsuariosClientes.IdTipoIdentificacion = TipoDocumento.IdTipoIdentificacion AND TurnosXClientes.IdTurno = Turnos.IdTurno AND Turnos.IdTurno = @Turno");
            List<SqlParameter> lParameters = new List<SqlParameter>();
            Generic generic = new Generic();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                DataTable dt = new DataTable();
                lParameters.Add(new SqlParameter("@Turno", sTurn));
                dt = oDAC.GetDataTable(sQuery.ToString(), lParameters);
                if (dt.Rows.Count > 0)
                {
                    generic.scode = dt.Rows[0]["NOMBRECORTO"].ToString();
                    generic.sname = dt.Rows[0]["Identificacion"].ToString();
                }
                dt.Dispose();
                dt = null;
            }
            return generic;
        }

        /// <summary>
        /// Método para obtener el Id de un tipo de documento por su nombre
        /// </summary>
        /// <param name="sDocumentType">String nombre del tipo de documento</param>
        /// <returns>Id del tipo de documento</returns>
        public int GetDocumentTypeId(string sDocumentType)
        {
            string sQuery = "SELECT IdTipoIdentificacion FROM TipoDocumento WHERE NOMBRECORTO = @TipoDocumento";
            List<SqlParameter> lParameters = new List<SqlParameter>();
            object oResult = null;
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@TipoDocumento", sDocumentType));
                oResult = oDAC.GetScalar(sQuery, lParameters);
                return (oResult != null) ? Convert.ToInt32(oResult) : 1; 
            }
        }

        public int GetQueueByElements(Digiturno5 digiturno, int iage)
        {
            StringBuilder stringBuilder = new StringBuilder("SELECT c.IdCola FROM Nodos n1 INNER JOIN Nodos n2 ON n1.IdNodo = n2.IdNodoPadre");
            stringBuilder.Append(" INNER JOIN Nodos n3 ON n2.IdNodo = n3.IdNodoPadre INNER JOIN Nodos n4 ON n3.IdNodo = n4.IdNodoPadre");
            stringBuilder.Append(" INNER JOIN Elementos e ON n4.IdElemento = e.IdElemento INNER JOIN Colas c ON c.IdNodo = n4.IdNodo");
            stringBuilder.Append(" WHERE n1.IdElemento = @TipoEdad AND n2.IdNivel = 2 AND n3.IdNivel = 3");
            stringBuilder.Append(" AND n4.IdNivel = 4 AND n2.IdElemento = @TipoUrgencia AND n3.IdElemento = @TipoPlan");
            stringBuilder.Append(" AND n4.IdElemento = @TipoUnidad");
            List<SqlParameter> lParameters = new List<SqlParameter>();
            object oResult = null;
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@TipoEdad", iage));
                lParameters.Add(new SqlParameter("@TipoUrgencia", digiturno.oPatient.iattendance));
                lParameters.Add(new SqlParameter("@TipoPlan", digiturno.oPatient.iplan));
                lParameters.Add(new SqlParameter("@TipoUnidad", digiturno.oPatient.iunit));
                oResult = oDAC.GetScalar(stringBuilder.ToString(), lParameters);
                return (oResult != null) ? Convert.ToInt32(oResult) : 0;                
            }
        }

        public DataTable GetInfoTurn(List<int> lstturn)
        {
            string turnlist = string.Join(",", lstturn);
            StringBuilder stringBuilder = new StringBuilder("SELECT I.Inicio, S.IdTurno, I.IdEstadoServicio, T.Numero, E.Nombre, S.IdSala, S.IdCola, S.IdServicio, s.IdNodo FROM InfoEstadosServicio I INNER JOIN Servicios S ON I.IdServicio = S.IdServicio");
            stringBuilder.Append(" INNER JOIN Turnos T ON T.IdTurno = s.IdTurno INNER JOIN EstadosServicio E ON E.IdEstadoServicio = I.IdEstadoServicio WHERE S.IdTurno IN (");
            stringBuilder.Append(turnlist);
            stringBuilder.Append(") AND CAST(I.Inicio AS DATE) = CAST(GETDATE() AS DATE)");
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                return oDAC.GetDataTable(stringBuilder.ToString(), null);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
