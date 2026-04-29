using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEntity;
using System.Data.SqlClient;
using System.Data;
using FNCUtils;
using Oracle.ManagedDataAccess.Client;
using EventLog;

namespace FNCDAC
{
    public class ConsInformado : IDisposable
    {
        /// <summary>
        /// Cadena de conexión a la base de datos
        /// </summary>
        public string sConnection { get; set; }

        public string sOracleConnection { get; set; }

        /// <summary>
        /// Constructor del objeto
        /// </summary>
        public ConsInformado()
        {

        }

        /// <summary>
        /// Método para crear el registro de la cita en la base de datos de consentimientos informados
        /// </summary>
        /// <param name="oEntity">Objeto consentimiento informado</param>
        public void CreateAppointmentRecord(Consentimiento oEntity)
        {
            try
            {
                this.SQLTransactions(oEntity);
            }
            catch (Exception ex)
            {
                LogError.WriteError("WSInspira", "Application", ex);
            }
            try
            {
                this.OracleTransactions(oEntity);

            }
            catch (Exception ex)
            {
                LogError.WriteError("WSInspira", "Application", ex);

            }
        }

        private void SQLTransactions(Consentimiento oEntity)
        {
            if (!this.AppointmentExists(oEntity))
            {
                this.InsertAppointment(oEntity);
            }
            else
            {
                this.DeleteAppointment(oEntity);
                this.InsertAppointment(oEntity);
            }
        }

        private void OracleTransactions(Consentimiento oEntity)
        {
            if (!this.AppointmentExistsOracle(oEntity))
            {
                this.InsertOracleAppointment(oEntity);
            }
            else
            {
                this.DeleteAppointmentOracle(oEntity);
                this.InsertOracleAppointment(oEntity);
            }
        }

        private void InsertAppointment(Consentimiento oEntity)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                StringBuilder sQuery = new StringBuilder("INSERT INTO CI_Cita (NombreCita, TipoIdent, NoIdent, PNombre, SNombre, PApellido, SApellido, Telefono, CodServicio, NomServicio, FechaCarga, Estado, Edad, idAccount, HData, DocProfesional)");
                sQuery.Append(" VALUES (@Cita, @TipoDocumento, @Documento, @PNombre, @SNombre, @PApellido, @SApellido, @Telefono, @CodigoServicio, @Servicio, @Fecha, 1, @Edad, @IdCuenta, @Habeas, @DocProfesional)");
                if (oEntity.iage < 18)
                {
                    oEntity.scups += "001";
                }
                lParameters.Add(new SqlParameter("@Cita", oEntity.sappointmemt));
                lParameters.Add(new SqlParameter("@TipoDocumento", oEntity.sdocumenttype));
                lParameters.Add(new SqlParameter("@Documento", oEntity.sdocument));
                lParameters.Add(new SqlParameter("@PNombre", oEntity.sfirstname));
                lParameters.Add(new SqlParameter("@SNombre", oEntity.ssecondname));
                lParameters.Add(new SqlParameter("@PApellido", oEntity.ssurname));
                lParameters.Add(new SqlParameter("@SApellido", oEntity.ssecondsurname));
                lParameters.Add(new SqlParameter("@Telefono", string.Empty));
                lParameters.Add(new SqlParameter("@CodigoServicio", oEntity.scups));
                lParameters.Add(new SqlParameter("@Servicio", oEntity.sservicename));
                lParameters.Add(new SqlParameter("@Fecha", oEntity.dappointmentdate));
                lParameters.Add(new SqlParameter("@Edad", oEntity.iage));
                lParameters.Add(new SqlParameter("@IdCuenta", oEntity.sid));
                lParameters.Add(new SqlParameter("@Habeas", oEntity.shabeasdata));
                lParameters.Add(new SqlParameter("@DocProfesional", string.Empty));
                oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters);
                sQuery = null;
                lParameters = null;
            }
        }

        private void InsertOracleAppointment(Consentimiento oEntity)
        {
            List<OracleParameter> lParameters = new List<OracleParameter>();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sOracleConnection;
                oDAC.Connect();
                StringBuilder sQuery = new StringBuilder("INSERT INTO TBL_CI_CITA (NombreCita, TipoIdent, NoIdent, PNombre, SNombre, PApellido, SApellido, Telefono, CodServicio, NomServicio, FechaCarga, Estado, Edad, idAccount, HData, DocProfesional)");
                sQuery.Append(" VALUES (:Cita, :TipoDocumento, :Documento, :PNombre, :SNombre, :PApellido, :SApellido, :Telefono, :CodigoServicio, :Servicio, :Fecha, 1, :Edad, :IdCuenta, :Habeas, :DocProfesional)");
                if (oEntity.iage < 18)
                {
                    oEntity.scups += "001";
                }
                lParameters.Add(new OracleParameter("Cita", oEntity.sappointmemt));
                lParameters.Add(new OracleParameter("TipoDocumento", oEntity.sdocumenttype));
                lParameters.Add(new OracleParameter("Documento", oEntity.sdocument));
                lParameters.Add(new OracleParameter("PNombre", oEntity.sfirstname));
                lParameters.Add(new OracleParameter("SNombre", oEntity.ssecondname));
                lParameters.Add(new OracleParameter("PApellido", oEntity.ssurname));
                lParameters.Add(new OracleParameter("SApellido", oEntity.ssecondsurname));
                lParameters.Add(new OracleParameter("Telefono", string.Empty));
                lParameters.Add(new OracleParameter("CodigoServicio", oEntity.scups));
                lParameters.Add(new OracleParameter("Servicio", oEntity.sservicename));
                lParameters.Add(new OracleParameter("Fecha", oEntity.dappointmentdate));
                lParameters.Add(new OracleParameter("Edad", oEntity.iage));
                lParameters.Add(new OracleParameter("IdCuenta", oEntity.sid));
                lParameters.Add(new OracleParameter("Habeas", oEntity.shabeasdata));
                lParameters.Add(new OracleParameter("DocProfesional", string.Empty));
                oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters);
                sQuery = null;
                lParameters = null;
            }
        }

        public void ResearchConsent(Consentimiento oEntity, bool bYoungest)
        {
            try
            {
                this.CreateResearchConsent(oEntity, bYoungest);
            }
            catch (Exception ex)
            {
                LogError.WriteError("WSInspira", "Application", ex);                
            }
            try
            {
                this.CreateResearchConsentOracle(oEntity, bYoungest);
            }
            catch (Exception ex)
            {
                LogError.WriteError("WSInspira", "Application", ex);
            }                        
        }

        public void CreateResearchConsent(Consentimiento oEntity, bool bYoungest = false)
        {
            if (!this.ValidateResearchConsent(oEntity))
            {
                List<SqlParameter> lParameters = new List<SqlParameter>();
                using (SQLServer oDAC = new SQLServer(this.sConnection))
                {
                    StringBuilder sQuery = new StringBuilder("INSERT INTO CI_Cita (NombreCita, TipoIdent, NoIdent, PNombre, SNombre, PApellido, SApellido, Telefono, CodServicio, NomServicio, FechaCarga, Estado, Edad, idAccount)");
                    sQuery.Append(" VALUES (@Cita, @TipoDocumento, @Documento, @PNombre, @SNombre, @PApellido, @SApellido, @Telefono, @CodigoServicio, @Servicio, @Fecha, 1, @Edad, @IdCuenta)");
                    if (oEntity.iage < 18)
                    {
                        oEntity.scups = (!bYoungest) ? "FNC1001" : "FNC1002";
                    }
                    else
                    {
                        oEntity.scups = "FNC1";
                    }
                    lParameters.Add(new SqlParameter("@Cita", oEntity.sappointmemt));
                    lParameters.Add(new SqlParameter("@TipoDocumento", oEntity.sdocumenttype));
                    lParameters.Add(new SqlParameter("@Documento", oEntity.sdocument));
                    lParameters.Add(new SqlParameter("@PNombre", oEntity.sfirstname));
                    lParameters.Add(new SqlParameter("@SNombre", oEntity.ssecondname));
                    lParameters.Add(new SqlParameter("@PApellido", oEntity.ssurname));
                    lParameters.Add(new SqlParameter("@SApellido", oEntity.ssecondsurname));
                    lParameters.Add(new SqlParameter("@Telefono", string.Empty));
                    lParameters.Add(new SqlParameter("@CodigoServicio", oEntity.scups));
                    lParameters.Add(new SqlParameter("@Servicio", "Autorizacion uso de datos investigacion"));
                    lParameters.Add(new SqlParameter("@Fecha", oEntity.dappointmentdate));
                    lParameters.Add(new SqlParameter("@Edad", oEntity.iage));
                    lParameters.Add(new SqlParameter("@IdCuenta", oEntity.sid));
                    oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters);
                    sQuery = null;
                    lParameters = null;
                }
            }
        }

        private void CreateResearchConsentOracle(Consentimiento oEntity, bool bYoungest = false)
        {
            if (!this.ValidateResearchConsentOracle(oEntity))
            {
                List<OracleParameter> lParameters = new List<OracleParameter>();
                using (Oracle oDAC = new Oracle())
                {
                    oDAC.sConnection = this.sOracleConnection;
                    oDAC.Connect();
                    StringBuilder sQuery = new StringBuilder("INSERT INTO TBL_CI_CITA (NombreCita, TipoIdent, NoIdent, PNombre, SNombre, PApellido, SApellido, Telefono, CodServicio, NomServicio, FechaCarga, Estado, Edad, idAccount)");
                    sQuery.Append(" VALUES (:Cita, :TipoDocumento, :Documento, :PNombre, :SNombre, :PApellido, :SApellido, :Telefono, :CodigoServicio, :Servicio, :Fecha, 1, :Edad, :IdCuenta)");
                    if (oEntity.iage < 18)
                    {
                        oEntity.scups = (!bYoungest) ? "FNC1001" : "FNC1002";
                    }
                    else
                    {
                        oEntity.scups = "FNC1";
                    }
                    lParameters.Add(new OracleParameter("Cita", oEntity.sappointmemt));
                    lParameters.Add(new OracleParameter("TipoDocumento", oEntity.sdocumenttype));
                    lParameters.Add(new OracleParameter("Documento", oEntity.sdocument));
                    lParameters.Add(new OracleParameter("PNombre", oEntity.sfirstname));
                    lParameters.Add(new OracleParameter("SNombre", oEntity.ssecondname));
                    lParameters.Add(new OracleParameter("PApellido", oEntity.ssurname));
                    lParameters.Add(new OracleParameter("SApellido", oEntity.ssecondsurname));
                    lParameters.Add(new OracleParameter("Telefono", string.Empty));
                    lParameters.Add(new OracleParameter("CodigoServicio", oEntity.scups));
                    lParameters.Add(new OracleParameter("Servicio", "Autorizacion uso de datos investigacion"));
                    lParameters.Add(new OracleParameter("Fecha", oEntity.dappointmentdate));
                    lParameters.Add(new OracleParameter("Edad", oEntity.iage));
                    lParameters.Add(new OracleParameter("IdCuenta", oEntity.sid));
                    oDAC.ExecuteNonQuery(sQuery.ToString(), lParameters);
                    sQuery = null;
                    lParameters = null;
                }
            }
        }



        private bool ValidateResearchConsent(Consentimiento oEntity)
        {
            if (oEntity != null)
            {
                //string sQuery = "SELECT idCita FROM CI_Cita WITH (NOLOCK) WHERE Estado = 2 AND TipoIdent = @TipoDocumento AND NoIdent = @Documento AND CodServicio IN ('FNC1', 'FNC1001', 'FNC1002')";
                string sQuery = "SELECT idCita FROM CI_Cita WITH (NOLOCK) WHERE NombreCita = @Cita AND CodServicio IN ('FNC1', 'FNC1001', 'FNC1002') AND NoIdent = @Documento AND TipoIdent = @TipoDocumento";
                List<SqlParameter> lParameters = new List<SqlParameter>();
                object oResult = null;
                using (SQLServer oDAC = new SQLServer(this.sConnection))
                {
                    lParameters.Add(new SqlParameter("@Cita", oEntity.sappointmemt));
                    lParameters.Add(new SqlParameter("@TipoDocumento", oEntity.sdocumenttype));
                    lParameters.Add(new SqlParameter("@Documento", oEntity.sdocument));
                    oResult = oDAC.GetScalar(sQuery, lParameters);
                    lParameters = null;
                    return (oResult != null);
                }
            }
            return false;            
        }

        private bool ValidateResearchConsentOracle(Consentimiento oEntity)
        {
            if (oEntity != null)
            {
                //string sQuery = "SELECT idCita FROM CI_Cita WITH (NOLOCK) WHERE Estado = 2 AND TipoIdent = @TipoDocumento AND NoIdent = @Documento AND CodServicio IN ('FNC1', 'FNC1001', 'FNC1002')";
                string sQuery = "SELECT idCita FROM TBL_CI_CITA WHERE NombreCita = :Cita AND CodServicio IN ('FNC1', 'FNC1001', 'FNC1002') AND NoIdent = :Documento AND TipoIdent = :TipoDocumento";
                List<OracleParameter> lParameters = new List<OracleParameter>();
                object oResult = null;
                using (Oracle oDAC = new Oracle())
                {
                    oDAC.sConnection = this.sOracleConnection;
                    oDAC.Connect();
                    lParameters.Add(new OracleParameter("Cita", oEntity.sappointmemt));
                    lParameters.Add(new OracleParameter("TipoDocumento", oEntity.sdocumenttype));
                    lParameters.Add(new OracleParameter("Documento", oEntity.sdocument));
                    oResult = oDAC.GetScalar(sQuery, lParameters);
                    lParameters = null;
                    return (oResult != null);
                }
            }
            return false;
        }

        public List<Patient> GetResearchData()
        {
            string squery = "SELECT TipoIdent, NoIdent, Estado FROM CI_Cita WITH (NOLOCK) WHERE CodServicio IN ('FNC1', 'FNC1001', 'FNC1002') AND FechaCarga BETWEEN '20240101' AND CAST(GETDATE() AS DATE)";
            DataTable dataTable = new DataTable();
            List<Patient> patients = new List<Patient>();
            Patient patient = null;
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                dataTable = oDAC.GetDataTable(squery, null);
                foreach (DataRow dr in dataTable.Rows)
                {
                    patient = new Patient()
                    {
                        sdocument = dr["NoIdent"].ToString(),
                        sdocumenttype = dr["TipoIdent"].ToString(),
                        istatus = Convert.ToInt32(dr["Estado"]),
                    };
                    patients.Add(patient);
                }                
            }
            dataTable.Dispose();
            dataTable = null;
            patient = null;
            return patients;
        }

        public List<Patient> GetResearchDataOracle()
        {
            string squery = "SELECT TipoIdent, NoIdent, Estado FROM TBL_CI_CITA WHERE CodServicio IN ('FNC1', 'FNC1001', 'FNC1002') AND FechaCarga >= CURRENT_DATE";
            DataTable dataTable = new DataTable();
            List<Patient> patients = new List<Patient>();
            Patient patient = null;
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sOracleConnection;
                oDAC.Connect();
                dataTable = oDAC.GetDataTable(squery, null);
                foreach (DataRow dr in dataTable.Rows)
                {
                    patient = new Patient()
                    {
                        sdocument = dr["NoIdent"].ToString(),
                        sdocumenttype = dr["TipoIdent"].ToString(),
                        istatus = Convert.ToInt32(dr["Estado"]),
                    };
                    patients.Add(patient);
                }
            }
            dataTable.Dispose();
            dataTable = null;
            patient = null;
            return patients;
        }

        public bool AppointmentExists(Consentimiento oEntity)
        {
            string sQuery = "SELECT idCita FROM CI_Cita WITH (NOLOCK) WHERE NombreCita = @Cita AND CodServicio NOT IN ('FNC1', 'FNC1001', 'FNC1002') AND CodServicio = @Servicio AND NoIdent = @Documento AND TipoIdent = @TipoDocumento";
            object oResult = null;
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@Cita", oEntity.sappointmemt));
                lParameters.Add(new SqlParameter("@Servicio", oEntity.scups));
                lParameters.Add(new SqlParameter("@Documento", oEntity.sdocument));
                lParameters.Add(new SqlParameter("@TipoDocumento", oEntity.sdocumenttype));
                oResult = oDAC.GetScalar(sQuery, lParameters);
                lParameters = null;
                return (oResult != null);
            }
        }

        public bool AppointmentExistsOracle(Consentimiento oEntity)
        {
            string sQuery = "SELECT idCita FROM TBL_CI_CITA WHERE NombreCita = :Cita AND CodServicio NOT IN ('FNC1', 'FNC1001', 'FNC1002') AND CodServicio = :Servicio AND NoIdent = :Documento AND TipoIdent = :TipoDocumento";
            object oResult = null;
            List<OracleParameter> lParameters = new List<OracleParameter>();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sOracleConnection;
                oDAC.Connect();
                lParameters.Add(new OracleParameter("Cita", oEntity.sappointmemt));
                lParameters.Add(new OracleParameter("Servicio", oEntity.scups));
                lParameters.Add(new OracleParameter("Documento", oEntity.sdocument));
                lParameters.Add(new OracleParameter("TipoDocumento", oEntity.sdocumenttype));
                oResult = oDAC.GetScalar(sQuery, lParameters);
                lParameters = null;
                return (oResult != null);
            }
        }

        public void DeleteAppointment(Consentimiento oEntity)
        {
            string sQuery = "DELETE FROM CI_Cita WHERE NombreCita = @Cita AND CodServicio NOT IN ('FNC1', 'FNC1001', 'FNC1002') AND NoIdent = @Documento AND TipoIdent = @TipoDocumento";
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@Cita", oEntity.sappointmemt));
                lParameters.Add(new SqlParameter("@Servicio", oEntity.scups));
                lParameters.Add(new SqlParameter("@Documento", oEntity.sdocument));
                lParameters.Add(new SqlParameter("@TipoDocumento", oEntity.sdocumenttype));
                oDAC.ExecuteNonQuery(sQuery, lParameters);                
            }
        }

        public void DeleteAppointmentOracle(Consentimiento oEntity)
        {
            string sQuery = "DELETE FROM TBL_CI_CITA WHERE NombreCita = :Cita AND CodServicio NOT IN ('FNC1', 'FNC1001', 'FNC1002') AND NoIdent = :Documento AND TipoIdent = :TipoDocumento";
            List<OracleParameter> lParameters = new List<OracleParameter>();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sOracleConnection;
                oDAC.Connect();
                lParameters.Add(new OracleParameter("Cita", oEntity.sappointmemt));
                lParameters.Add(new OracleParameter("Servicio", oEntity.scups));
                lParameters.Add(new OracleParameter("Documento", oEntity.sdocument));
                lParameters.Add(new OracleParameter("TipoDocumento", oEntity.sdocumenttype));
                oDAC.ExecuteNonQuery(sQuery, lParameters);
            }
        }

        public void UpdateConsentUser(string sAppointment, string sUser)
        {
            StringBuilder stringBuilder = new StringBuilder("UPDATE CI_Cita SET [DocProfesional] = @sUser WHERE NombreCita = @Cita AND CodServicio NOT IN ('FNC1', 'FNC1001', 'FNC1002')");
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@Cita", sAppointment));
                lParameters.Add(new SqlParameter("@sUser", sUser));
                oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters);
            }
        }

        public void UpdateConsentUserOracle(string sAppointment, string sUser)
        {
            StringBuilder stringBuilder = new StringBuilder("UPDATE TBL_CI_CITA SET DocProfesional = :sUser WHERE NombreCita = :Cita AND CodServicio NOT IN ('FNC1', 'FNC1001', 'FNC1002')");
            List<OracleParameter> lParameters = new List<OracleParameter>();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sOracleConnection;
                oDAC.Connect();
                lParameters.Add(new OracleParameter("Cita", sAppointment));
                lParameters.Add(new OracleParameter("sUser", sUser));
                oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters);
            }
        }

        public void Dispose()
        {
            GC.Collect();
            GC.SuppressFinalize(this);
        }
    }
}


