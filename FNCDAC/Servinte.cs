using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEntity;
using System.Data.SqlClient;
using System.Data;
using FNCUtils;

namespace FNCDAC
{
    public class Servinte : IDisposable
    {
        /// <summary>
        /// Cadena de conexión a la base de datos
        /// </summary>
        public string sConnection { get; set; }

        /// <summary>
        /// Objeto base manejador SQL Server
        /// </summary>
        private SQLServer oSQL { get; set; }

        /// <summary>
        /// Lista genérica de pacientes
        /// </summary>
        public List<Patient> lPatients { get; set; }


        /// <summary>
        /// Lista genérica de documentos de pacientes
        /// </summary>
        private List<Generic> lDocuments { get; set; }

        /// <summary>
        /// Constructor de la clase
        /// </summary>
        /// <param name="ConnectionString">Cadena de conexión a la base de datos</param>
        public Servinte(string ConnectionString = "", bool isMultiple = true)
        {
            if (!string.IsNullOrEmpty(ConnectionString))
            {
                this.sConnection = ConnectionString;
            }
            if (isMultiple)
            {
                this.lPatients = this.GetPatients();
                this.lDocuments = this.GetDocuments();
            }            
        }

        /// <summary>
        /// Método que obtiene el listado de documentos de Servinte (tabla abpacfau)
        /// </summary>
        /// <returns>Lista genérica de documentos</returns>
        private List<Generic> GetDocuments()
        {
            string squery = "SELECT pacfauide, pacfautid FROM abpacfau WITH (NOLOCK)";
            Generic oGeneric = null;
            List<Generic> lGeneric = new List<Generic>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                DataTable dt = oDAC.GetDataTable(squery, null);
                foreach (DataRow dr in dt.Rows)
                {
                    oGeneric = new Generic()
                    {
                        scode = dr["pacfautid"].ToString(),
                        sname = dr["pacfauide"].ToString(),                        
                    };
                    lGeneric.Add(oGeneric);
                }
                return lGeneric;
            }
        }

        /// <summary>
        /// Método que obtiene el listado de pacientes de Servinte (tabla abpac)
        /// </summary>
        /// <param name="sDocument">Documento de paciente para filtro de la información</param>
        /// <returns>Lista genérica de pacientes</returns>
        public List<Patient> GetPatients(string sDocument = "")
        {
            StringBuilder sQuery = new StringBuilder("SELECT pacide, pactid FROM abpac WITH (NOLOCK) WHERE 1 = 1");
            Patient oPatient = null;
            List<Patient> lPaciente = new List<Patient>();
            List<SqlParameter> lParameters = new List<SqlParameter>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                if (!string.IsNullOrEmpty(sDocument))
                {
                    lParameters.Add(new SqlParameter("@pacid", sDocument));
                    sQuery.Append(" AND pacide = @pacid");
                }
                sQuery.Append(" ORDER BY pacfch DESC");
                DataTable dt = oDAC.GetDataTable(sQuery.ToString(), lParameters);
                foreach (DataRow dr in dt.Rows)
                {
                    oPatient = new Patient()
                    {
                        sdocument = dr["pacide"].ToString(),
                        sdocumenttype = dr["pactid"].ToString(),
                    };
                    lPaciente.Add(oPatient);
                }
                sQuery = null;
                lParameters = null;
                return lPaciente;
            }
        }

        /// <summary>
        /// Método que valida si un paciente existe en la BD de Servinte por tipo y número de documento
        /// </summary>
        /// <param name="oPatient">Objeto paciente</param>
        /// <returns>Verdadero si el paciente existe, falso en caso contrario</returns>
        private bool DocumentExists(Patient oPatient)
        {
            return this.lDocuments.Exists(x => x.scode == oPatient.sdocumenttype.Trim() && x.sname == oPatient.sdocument.Trim());
        }

        /// <summary>
        /// Método que valida si un paciente existe en la BD de Servinte por tipo y número de documento en el listado de pacientes
        /// </summary>
        /// <param name="oPatient">Objeto paciente</param>
        /// <returns>Verdadero si el paciente existe, falso en caso contrario</returns>
        private bool PatientExists(Patient oPatient)
        {
            return (this.lPatients.Count(x => x.sdocument.Trim() == oPatient.sdocument.Trim() && x.sdocumenttype.Trim().Replace("?", string.Empty) == oPatient.sdocumenttype.Trim()) > 0);
        }

        /// <summary>
        /// Método que obtiene el tipo de documento del paciente en Gestor
        /// </summary>
        /// <param name="sDocument">Documento del paciente</param>
        /// <returns>Tipo de documento del pacient</returns>
        private string GetDocumentType(string sDocument)
        {
            Patient oPatient = (this.lPatients.FirstOrDefault(x => x.sdocument == sDocument));
            return (oPatient != null) ? oPatient.sdocumenttype : string.Empty;
        }

        /// <summary>
        /// Método para crear un paciente en la BD Servinte tabla abpac
        /// </summary>
        /// <param name="oPatient">Objeto paciente</param>
        private void CreatePatient(Patient oPatient)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abpac");
            sQuery.Append(" (pacnom, pacap1, pacap2, pacsex, pacnac, paclug, pacest, pactel, pacbar, pacide, pactid, pacusu, pacfch, pachor, paczon, pacocu, pactic, pacidc, pacdir, pacsed) ");
            sQuery.Append(" VALUES (@pacnom, @pacap1, @pacap2, @pacsex, @pacnac, @paclug, @pacest, @pactel, @pacbar, @pacide, @pactid, @pacusu, @pacfch, @pachor, @paczon, @pacocu, @pactic, @pacidc, @pacdir, @pacsed)");
            lParameters.Add(new SqlParameter("@pacnom", Tools.SubString(oPatient.sname.ToUpper(), 20)));
            lParameters.Add(new SqlParameter("@pacap1", Tools.SubString(oPatient.ssurname.ToUpper(), 15)));
            lParameters.Add(new SqlParameter("@pacap2", Tools.SubString(oPatient.ssecondsurname.ToUpper(), 15)));
            lParameters.Add(new SqlParameter("@pacsex", oPatient.sgender));
            lParameters.Add(new SqlParameter("@pacnac", oPatient.dbirthdate));
            lParameters.Add(new SqlParameter("@paclug", oPatient.sbornplace));
            //lParameters.Add(new SqlParameter("@pacest", oPatient.smaritalstatus));
            lParameters.Add(new SqlParameter("@pacest", DBNull.Value));
            lParameters.Add(new SqlParameter("@pactel", Tools.SubString(oPatient.sphone, 15)));
            lParameters.Add(new SqlParameter("@pacbar", oPatient.sneighborhood));
            lParameters.Add(new SqlParameter("@pacide", oPatient.sdocument));
            lParameters.Add(new SqlParameter("@pactid", oPatient.sdocumenttype));
            lParameters.Add(new SqlParameter("@pacusu", oPatient.screatedby));
            lParameters.Add(new SqlParameter("@pacfch", oPatient.dcreateddate));
            lParameters.Add(new SqlParameter("@pachor", oPatient.dcreatedtime));
            lParameters.Add(new SqlParameter("@paczon", oPatient.surbanzone));
            lParameters.Add(new SqlParameter("@pacocu", oPatient.sjob));
            lParameters.Add(new SqlParameter("@pactic", oPatient.sdocumenttype));
            lParameters.Add(new SqlParameter("@pacidc", oPatient.sdocument));
            lParameters.Add(new SqlParameter("@pacdir", Tools.SubString(oPatient.saddress, 100)));
            lParameters.Add(new SqlParameter("@pacsed", oPatient.sbranch));            
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            this.lPatients.Add(new Patient() { sdocument = oPatient.sdocument, sdocumenttype = oPatient.sdocumenttype });
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método que inserta la información del tipo y documento del paciente en la BD de Servinte (tabla abpacfau)
        /// </summary>
        /// <param name="oPatient">Objeto paciente</param>
        private void CreatePatientDocument(Patient oPatient)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abpacfau (pacfautid , pacfauide , pacfauiir , pacfauiis , pacfauusu , pacfaufch)");
            sQuery.Append(" VALUES (@pacfautid, @pacfauide, @pacfauiir, @pacfauiis, @pacfauusu, GETDATE())");
            lParameters.Add(new SqlParameter("@pacfautid", oPatient.sdocumenttype));
            lParameters.Add(new SqlParameter("@pacfauide", oPatient.sdocument));
            lParameters.Add(new SqlParameter("@pacfauiir", "N"));
            lParameters.Add(new SqlParameter("@pacfauiis", "N"));
            lParameters.Add(new SqlParameter("@pacfauusu", oPatient.screatedby));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
        }

        /// <summary>
        /// Método que actualiza la información del tipo y documento del paciente en la BD de Servinte (tabla abpacfau)
        /// </summary>
        /// <param name="oPatient">Objeto paciente</param>
        /// <param name="icharge">Número del ingreso</param>
        private void UpdatePatientDocument(Patient oPatient, int icharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("UPDATE abpacfau");            
            sQuery.Append(" SET pacfauiir = @pacfauiir, pacfauiis = @pacfauiis, pacfaufir = GETDATE(), pacfaufur = @pacfaufur, pacfaudor = @pacfaudor");            
            sQuery.Append(", pacfaufis = GETDATE(), pacfaufus = @pacfaufus, pacfaudos = @pacfaudos, pacfauses = @pacfauses, pacfauusu = @pacfauusu, pacfaufch = GETDATE() WHERE pacfautid = @pacfautid AND pacfauide = @pacfauide");
            lParameters.Add(new SqlParameter("@pacfauiir", "S"));
            lParameters.Add(new SqlParameter("@pacfauiis", "S"));
            lParameters.Add(new SqlParameter("@pacfaufur", oPatient.oCharge.scode));
            lParameters.Add(new SqlParameter("@pacfaudor", icharge));
            lParameters.Add(new SqlParameter("@pacfaufus", oPatient.oCharge.scode));
            lParameters.Add(new SqlParameter("@pacfaudos", icharge));
            lParameters.Add(new SqlParameter("@pacfauses", oPatient.sbranch));
            lParameters.Add(new SqlParameter("@pacfauusu", oPatient.screatedby));
            lParameters.Add(new SqlParameter("@pacfautid", oPatient.sdocumenttype));
            lParameters.Add(new SqlParameter("@pacfauide", oPatient.sdocument));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
        }

        /// <summary>
        /// Método que actualiza la información del paciente en la BD de Servinte (tabla abpac)
        /// </summary>
        /// <param name="oPatient">Objeto paciente</param>
        private void UpdatePatient(Patient oPatient)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("UPDATE abpac SET pacnom = @pacnom, pacap1 = @pacap1, pacap2 = @pacap2");
            sQuery.Append(", pacusu = @pacusu, pacfch = @pacfch, pachor = @pachor");                        
            sQuery.Append(", pactic = @pactic, pacidc = @pacidc, pacsed = @pacsed");
            lParameters.Add(new SqlParameter("@pacnom", Tools.SubString(oPatient.sname, 20)));
            lParameters.Add(new SqlParameter("@pacap1", Tools.SubString(oPatient.ssurname, 15)));
            lParameters.Add(new SqlParameter("@pacap2", Tools.SubString(oPatient.ssecondsurname, 15)));
            lParameters.Add(new SqlParameter("@pacusu", oPatient.screatedby));
            lParameters.Add(new SqlParameter("@pacfch", oPatient.dcreateddate));
            lParameters.Add(new SqlParameter("@pachor", oPatient.dcreatedtime));
            if (!string.IsNullOrEmpty(oPatient.sgender))
            {
                sQuery.Append(", pacsex = @pacsex");
                lParameters.Add(new SqlParameter("@pacsex", oPatient.sgender));                
            }
            if (oPatient.dbirthdate.HasValue)
            {
                sQuery.Append(", pacnac = @pacnac");
                lParameters.Add(new SqlParameter("@pacnac", oPatient.dbirthdate));
            }
            if (!string.IsNullOrEmpty(oPatient.sbornplace))
            {
                sQuery.Append(", paclug = @paclug");
                lParameters.Add(new SqlParameter("@paclug", oPatient.sbornplace));
            }
            if (!string.IsNullOrEmpty(oPatient.smaritalstatus))
            {
                sQuery.Append(", pacest = @pacest");
                lParameters.Add(new SqlParameter("@pacest", oPatient.smaritalstatus));
            }
            if (!string.IsNullOrEmpty(oPatient.sphone))
            {
                sQuery.Append(", pactel = @pactel");
                lParameters.Add(new SqlParameter("@pactel", Tools.SubString(oPatient.sphone, 15)));
            }
            if (!string.IsNullOrEmpty(oPatient.sneighborhood))
            {
                sQuery.Append(", pacbar = @pacbar");
                lParameters.Add(new SqlParameter("@pacbar", oPatient.sneighborhood));
            }
            
            if (!string.IsNullOrEmpty(oPatient.surbanzone))
            {
                sQuery.Append(", paczon = @paczon");
                lParameters.Add(new SqlParameter("@paczon", oPatient.surbanzone));
            }
            if (!string.IsNullOrEmpty(oPatient.sjob))
            {
                sQuery.Append(", pacocu = @pacocu");
                lParameters.Add(new SqlParameter("@pacocu", oPatient.sjob));
            }            
            lParameters.Add(new SqlParameter("@pactic", oPatient.sdocumenttype));
            lParameters.Add(new SqlParameter("@pacidc", oPatient.sdocument));
            if (!string.IsNullOrEmpty(oPatient.saddress))
            {
                sQuery.Append(", pacdir = @pacdir");
                lParameters.Add(new SqlParameter("@pacdir", Tools.SubString(oPatient.saddress, 100)));
            }
            sQuery.Append(" WHERE pacide = @pacide AND pactid = @pactid");
            lParameters.Add(new SqlParameter("@pacsed", oPatient.sbranch));
            lParameters.Add(new SqlParameter("@pacide", oPatient.sdocument));
            lParameters.Add(new SqlParameter("@pactid", oPatient.sdocumenttype));                      
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método que crea el ingreso en la BD de Servinte (tabla abing)
        /// </summary>
        /// <param name="oCharge">Objeto ingreso</param>
        private void CreateCharge(Charge oCharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abing WITH (ROWLOCK)");
            sQuery.Append(" (ingfue, ingdoc, ingano, ingmes, ingpli, ingfec, ingide, ingtid, ingtus, ingniv, ingtir, ingidr, ingres, ingpla, ingtar, ingser, ingfac, inganu, ingusu, ingfch, inghor, ingtia, ingafi, ingvia, ingaut, ingcla, ingest, ingsed, ingprg, ingclo, ingufu)");
            sQuery.Append(" VALUES (@ingfue, @ingdoc, @ingano, @ingmes, @ingpli, @ingfec, @ingide, @ingtid, @ingtus, @ingniv, @ingtir, @ingidr, @ingres, @ingpla, @ingtar, @ingser, @ingfac, @inganu, @ingusu, @ingfch, @inghor, @ingtia, @ingafi, @ingvia, @ingaut, @ingcla, @ingest, @ingsed, @ingprg, @ingclo, @ingufu)");
            lParameters.Add(new SqlParameter("@ingfue", oCharge.scode));
            lParameters.Add(new SqlParameter("@ingdoc", oCharge.inumber));
            lParameters.Add(new SqlParameter("@ingano", oCharge.iyear));
            lParameters.Add(new SqlParameter("@ingmes", oCharge.smonth));
            lParameters.Add(new SqlParameter("@ingpli", oCharge.screatedby));
            lParameters.Add(new SqlParameter("@ingfec", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingide", oCharge.sdocument));
            lParameters.Add(new SqlParameter("@ingtid", oCharge.sdocumenttype));
            lParameters.Add(new SqlParameter("@ingtus", oCharge.iusertype));
            lParameters.Add(new SqlParameter("@ingniv", oCharge.ilevel));
            lParameters.Add(new SqlParameter("@ingtir", oCharge.sagreementtype)); 
            lParameters.Add(new SqlParameter("@ingidr", oCharge.sagreementcode));
            lParameters.Add(new SqlParameter("@ingres", Tools.SubString(oCharge.sagreementname, 30)));
            lParameters.Add(new SqlParameter("@ingpla", oCharge.splan));
            lParameters.Add(new SqlParameter("@ingtar", oCharge.srate));
            lParameters.Add(new SqlParameter("@ingser", oCharge.sadmissiontype));
            lParameters.Add(new SqlParameter("@ingfac", "N"));
            lParameters.Add(new SqlParameter("@inganu", "0"));
            lParameters.Add(new SqlParameter("@ingusu", oCharge.sassignedto));
            lParameters.Add(new SqlParameter("@ingfch", oCharge.dassigneddate));
            lParameters.Add(new SqlParameter("@inghor", oCharge.dassignedtime));
            lParameters.Add(new SqlParameter("@ingtia", (string.IsNullOrEmpty(oCharge.sattentiontype)) ? "A" : oCharge.sattentiontype));
            lParameters.Add(new SqlParameter("@ingafi", "N"));
            lParameters.Add(new SqlParameter("@ingvia", "6"));
            lParameters.Add(new SqlParameter("@ingaut", (string.IsNullOrEmpty(oCharge.sauthorization) ? DBNull.Value : (object)oCharge.sauthorization)));
            lParameters.Add(new SqlParameter("@ingcla", "P"));
            lParameters.Add(new SqlParameter("@ingest", "A"));
            lParameters.Add(new SqlParameter("@ingsed", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingprg", oCharge.sprogram));
            lParameters.Add(new SqlParameter("@ingclo", "P"));
            lParameters.Add(new SqlParameter("@ingufu", "00000"));            
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método que crea el registro de creación del ingreso en el log de transacciones de Servinte (tabla ablog)
        /// </summary>
        /// <param name="oCharge">Objeto ingreso</param>
        /// <param name="ssource">Código de la fuente de origsen</param>
        private void CreateChargeLog(Charge oCharge, string ssource)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO ablog (logusu, logter, logpro,  logope,  logde1, logva1, logde2, logva2, logde3, logva3, logtip, logtab, logfec, logead)");
            sQuery.Append(" VALUES (@logusu, @logter, @logpro, @logope, @logde1, @logva1, @logde2, @logva2, @logde3, @logva3, @logtip, @logtab, GETDATE(), @logead)");
            lParameters.Add(new SqlParameter("@logusu", oCharge.screatedby));
            lParameters.Add(new SqlParameter("@logter", "*"));
            lParameters.Add(new SqlParameter("@logpro", "CAFING 7.0.6"));
            lParameters.Add(new SqlParameter("@logope", "Grabar"));
            lParameters.Add(new SqlParameter("@logde1", "F.Ing"));
            lParameters.Add(new SqlParameter("@logva1", oCharge.scode));
            lParameters.Add(new SqlParameter("@logde2", "N.Ing"));
            lParameters.Add(new SqlParameter("@logva2", oCharge.inumber));
            lParameters.Add(new SqlParameter("@logde3", "Sede"));
            lParameters.Add(new SqlParameter("@logva3", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@logtip", "I"));
            lParameters.Add(new SqlParameter("@logtab", ssource));
            lParameters.Add(new SqlParameter("@logead", oCharge.sbranch));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método que crea el registro del ingreso en la tabla de cargos por empresa (tabla abingres)
        /// </summary>
        /// <param name="oCharge">Objeto ingreso</param>
        private void CreateCompanyCharge(Charge oCharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abingres WITH (ROWLOCK) (ingresfue, ingresdoc, ingressed, ingreslin, ingrestir, ingresidr, ingresres, ingrespla, ingresind)");
            sQuery.Append(" VALUES (@ingresfue, @ingresdoc, @ingressed, @ingreslin, @ingrestir, @ingresidr, @ingresres, @ingrespla, @ingresind)");
            lParameters.Add(new SqlParameter("@ingresfue", oCharge.scode));
            lParameters.Add(new SqlParameter("@ingresdoc", oCharge.inumber));
            lParameters.Add(new SqlParameter("@ingressed", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingreslin", 1));
            lParameters.Add(new SqlParameter("@ingrestir", oCharge.sagreementtype));
            if (oCharge.sagreementtype == "E")
            {
                lParameters.Add(new SqlParameter("@ingresidr", oCharge.sagreementcode));
                lParameters.Add(new SqlParameter("@ingresres", Tools.SubString(oCharge.sagreementname, 30)));
            }
            else
            {
                lParameters.Add(new SqlParameter("@ingresidr", oCharge.sdocument));
                lParameters.Add(new SqlParameter("@ingresres", Tools.SubString(oCharge.spatientname, 30)));
            }
            lParameters.Add(new SqlParameter("@ingrespla", oCharge.splan));
            lParameters.Add(new SqlParameter("@ingresind", "P"));            
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        private void CreateChargeACL(Charge oCharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abriactl WITH (ROWLOCK) (riactlfue, riactldoc, riactlano, riactlmes, riactlurg, riactlfur, riactlhur, riactlhos, riactlfho, riactlhho, riactlcon, riactlpro, riactlnac, riactltra, riactlfch, riactlusu, riactlmed, riactlotr)");
            sQuery.Append(" VALUES (@riactlfue, @riactldoc, @riactlano, @riactlmes, @riactlurg, @riactlfur, @riactlhur, @riactlhos, @riactlfho, @riactlhho, @riactlcon, @riactlpro, @riactlnac, @riactltra, @riactlfch, @riactlusu, @riactlmed, @riactlotr)");
            lParameters.Add(new SqlParameter("@riactlfue", oCharge.scode));
            lParameters.Add(new SqlParameter("@riactldoc", oCharge.inumber));
            lParameters.Add(new SqlParameter("@riactlano", oCharge.iyear));
            lParameters.Add(new SqlParameter("@riactlmes", oCharge.smonth));
            lParameters.Add(new SqlParameter("@riactlurg", "N"));
            lParameters.Add(new SqlParameter("@riactlfur", DBNull.Value));
            lParameters.Add(new SqlParameter("@riactlhur", DBNull.Value));
            lParameters.Add(new SqlParameter("@riactlhos", "N"));
            lParameters.Add(new SqlParameter("@riactlfho", DBNull.Value));
            lParameters.Add(new SqlParameter("@riactlhho", DBNull.Value));
            lParameters.Add(new SqlParameter("@riactlcon", "I"));
            lParameters.Add(new SqlParameter("@riactlpro", "N"));//TODO: De dónde sale el campo?
            lParameters.Add(new SqlParameter("@riactlnac", "N"));
            lParameters.Add(new SqlParameter("@riactltra", "N"));
            lParameters.Add(new SqlParameter("@riactlfch", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@riactlusu", oCharge.screatedby));
            lParameters.Add(new SqlParameter("@riactlmed", "N"));
            lParameters.Add(new SqlParameter("@riactlotr", "N"));            
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        private void InsertOtr(Charge oCharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abingotr WITH (ROWLOCK) (ingotrfue, ingotrdoc, ingotrpro, ingotrtip, ingotrmed, ingotrotm, ingotrprf, ingotrind, ingotrnme, ingotrafi, ingotrfic, ingotrffc, ingotrpor, ingotrtic, ingotridc, ingotrpac, ingotruag, ingotrfag, ingotrreq, ingotresp, ingotrrem, ingotrenr, ingotrfss, ingotrfir, ingotrfis, ingotrsed, ingotrpre, ingotrtel, ingotrpof)");
            sQuery.Append(" VALUES (@ingotrfue, @ingotrdoc, @ingotrpro, @ingotrtip, @ingotrmed, @ingotrotm, @ingotrprf, @ingotrind, @ingotrnme, @ingotrafi, @ingotrfic, @ingotrffc, @ingotrpor, @ingotrtic, @ingotridc, @ingotrpac, @ingotruag, @ingotrfag, @ingotrreq, @ingotresp, @ingotrrem, @ingotrenr, @ingotrfss, @ingotrfir, @ingotrfis, @ingotrsed, @ingotrpre, @ingotrtel, @ingotrpof)");            
            lParameters.Add(new SqlParameter("@ingotrfue", oCharge.scode));//
            lParameters.Add(new SqlParameter("@ingotrdoc", oCharge.inumber));//
            lParameters.Add(new SqlParameter("@ingotrpro", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrtip", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrmed", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrotm", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrprf", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrind", "N"));//
            lParameters.Add(new SqlParameter("@ingotrnme", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrafi", "N"));//
            lParameters.Add(new SqlParameter("@ingotrfic", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrffc", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrpor", DBNull.Value));//
            lParameters.Add(new SqlParameter("@ingotrtic", ""));//
            lParameters.Add(new SqlParameter("@ingotridc", ""));//
            lParameters.Add(new SqlParameter("@ingotrpac", ""));//
            lParameters.Add(new SqlParameter("@ingotruag", ""));
            lParameters.Add(new SqlParameter("@ingotrfag", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingotrreq", "N"));
            lParameters.Add(new SqlParameter("@ingotresp", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrrem", "N"));
            lParameters.Add(new SqlParameter("@ingotrenr", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrfss", oCharge.dcreateddate));            
            lParameters.Add(new SqlParameter("@ingotrfir", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingotrfis", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingotrsed", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingotrpre", "N"));
            lParameters.Add(new SqlParameter("@ingotrtel", DBNull.Value));            
            lParameters.Add(new SqlParameter("@ingotrpof", DBNull.Value));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        private void UpdateOtr(Charge oCharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("UPDATE abingotr SET ingotrprf = @ingotrprf, ingotrind = @ingotrind, ingotrnme = @ingotrnme, ingotrtel = @ingotrtel, ingotrfic = @ingotrfic");
            sQuery.Append(", ingotrffc = @ingotrffc, ingotrpor = @ingotrpor, ingotruag = @ingotruag, ingotrfag = @ingotrfag, ingotrreq = @ingotrreq, ingotresp = @ingotresp, ingotrrem = @ingotrrem, ingotrfss = @ingotrfss");
            sQuery.Append(", ingotrfir = @ingotrfir, ingotrfis = @ingotrfis, ingotrsed = @ingotrsed, ingotrpof = @ingotrpof WHERE ingotrfue = @ingotrfue AND ingotrdoc = @ingotrdoc AND ingotrsed = @ingotrsed");                                      
            lParameters.Add(new SqlParameter("@ingotrprf", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrind", "N"));
            lParameters.Add(new SqlParameter("@ingotrnme", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrtel", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrfic", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrffc", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrpor", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotruag", "")); 
            lParameters.Add(new SqlParameter("@ingotrfag", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingotrreq", "N"));
            lParameters.Add(new SqlParameter("@ingotresp", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrrem", "N"));
            lParameters.Add(new SqlParameter("@ingotrenr", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrfss", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingotrfir", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingotrfis", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingotrsed", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingotrpof", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingotrfue", oCharge.scode));
            lParameters.Add(new SqlParameter("@ingotrdoc", oCharge.inumber));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        /// <param name="ipos"></param>
        private void CreateAuthorization(Charge oCharge, int ipos)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abingaut WITH (ROWLOCK) (ingautfue, ingautdoc, ingautsed, ingautlin, ingautaut, ingautfec, ingauttpr, ingautact, ingautctr, ingautcac)");            
            sQuery.Append(" VALUES (@ingautfue, @ingautdoc, @ingautsed, @ingautlin, @ingautaut, @ingautfec, @ingauttpr, @ingautact, @ingautctr, @ingautcac)");
            lParameters.Add(new SqlParameter("@ingautfue", oCharge.scode));
            lParameters.Add(new SqlParameter("@ingautdoc", oCharge.inumber));
            lParameters.Add(new SqlParameter("@ingautsed", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingautlin", (ipos + 1)));
            lParameters.Add(new SqlParameter("@ingautaut", (string.IsNullOrEmpty(oCharge.sauthorization) ? DBNull.Value : (object)oCharge.sauthorization)));           
            lParameters.Add(new SqlParameter("@ingauttpr", "P"));
            lParameters.Add(new SqlParameter("@ingautact", oCharge.ldetail[ipos].sservice));
            lParameters.Add(new SqlParameter("@ingautctr", "S"));
            lParameters.Add(new SqlParameter("@ingautcac", 1));
            lParameters.Add(new SqlParameter("@ingautfec", oCharge.dcreateddate));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        private void CreateChargeTransaction(Charge oCharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abingtra WITH (ROWLOCK) (ingtrafue, ingtradoc, ingtracla, ingtrased, ingtrafuo, ingtradoo, ingtraseo, ingtrafut, ingtradot, ingtraset, ingtratra, ingtrafec, ingtratfa)");
            sQuery.Append(" VALUES (@ingtrafue, @ingtradoc, @ingtracla, @ingtrased, @ingtrafuo, @ingtradoo, @ingtraseo, @ingtrafut, @ingtradot, @ingtraset, @ingtratra, @ingtrafec, @ingtratfa)");            
            lParameters.Add(new SqlParameter("@ingtrafue", oCharge.scode));
            lParameters.Add(new SqlParameter("@ingtradoc", oCharge.inumber));
            lParameters.Add(new SqlParameter("@ingtracla", "P"));
            lParameters.Add(new SqlParameter("@ingtrased", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingtrafuo", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingtradoo", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingtraseo", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingtrafut", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingtradot", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingtraset", DBNull.Value));
            lParameters.Add(new SqlParameter("@ingtratra", "P"));
            lParameters.Add(new SqlParameter("@ingtrafec", oCharge.dcreateddate));
            lParameters.Add(new SqlParameter("@ingtratfa", DBNull.Value));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        /// <param name="ipos"></param>
        private void CreateChargeDetail(Charge oCharge, int ipos)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO abingdet WITH (ROWLOCK)");
            sQuery.Append(" (ingdetfue, ingdetdoc, ingdetlin, ingdetano, ingdetmes, ingdetpli, ingdetfec, ingdetfuo, ingdetdoo, ingdetcon, ingdetcod, ingdettpr, ingdetcco, ingdettot, ingdetnit, ingdetcan, ingdetfes, ingdetrec, ingdetvre, ingdetvex, ingdetvot, ingdetvfa, ingdetdfa, ingdetdes, ingdettip, ingdetfac, ingdettfa, ingdettco, ingdetanu, ingdetusu, ingdetfch, ingdethor, ingdetori, ingdetvun, ingdetsed, ingdetufu )");
            sQuery.Append(" VALUES (@ingdetfue, @ingdetdoc, @ingdetlin, @ingdetano, @ingdetmes, @ingdetpli, @ingdetfec, @ingdetfuo, @ingdetdoo, @ingdetcon, @ingdetcod, @ingdettpr, @ingdetcco, @ingdettot, @ingdetnit, @ingdetcan, @ingdetfes, @ingdetrec, @ingdetvre, @ingdetvex, @ingdetvot, @ingdetvfa, @ingdetdfa, @ingdetdes, @ingdettip, @ingdetfac, @ingdettfa, @ingdettco, @ingdetanu, @ingdetusu, @ingdetfch, @ingdethor, @ingdetori, @ingdetvun, @ingdetsed, @ingdetufu)");
            lParameters.Add(new SqlParameter("@ingdetfue", "CA"));//Modificar para cargo desde Inspira
            lParameters.Add(new SqlParameter("@ingdetdoc", oCharge.ldetail[ipos].inumber));
            lParameters.Add(new SqlParameter("@ingdetlin", (ipos + 1)));
            lParameters.Add(new SqlParameter("@ingdetano", oCharge.iyear));
            lParameters.Add(new SqlParameter("@ingdetmes", oCharge.smonth));
            lParameters.Add(new SqlParameter("@ingdetpli", oCharge.screatedby));
            lParameters.Add(new SqlParameter("@ingdetfec", oCharge.dcreateddate));
            //lParameters.Add(new SqlParameter("@ingdetfuo", oCharge.ssource));
            lParameters.Add(new SqlParameter("@ingdetfuo", oCharge.scode));
            lParameters.Add(new SqlParameter("@ingdetdoo", oCharge.inumber));
            lParameters.Add(new SqlParameter("@ingdetcon", oCharge.ldetail[ipos].sconcept));
            lParameters.Add(new SqlParameter("@ingdetcod", oCharge.ldetail[ipos].sservice));
            lParameters.Add(new SqlParameter("@ingdettpr", "P"));
            lParameters.Add(new SqlParameter("@ingdetcco", oCharge.ldetail[ipos].scostcenter));
            lParameters.Add(new SqlParameter("@ingdettot", oCharge.ldetail[ipos].dtotal * oCharge.ldetail[ipos].iqty));
            lParameters.Add(new SqlParameter("@ingdetnit", oCharge.ldetail[ipos].snit));
            lParameters.Add(new SqlParameter("@ingdetcan", oCharge.ldetail[ipos].iqty));            
            lParameters.Add(new SqlParameter("@ingdetfes", "N"));
            lParameters.Add(new SqlParameter("@ingdetrec", "0"));
            lParameters.Add(new SqlParameter("@ingdetvre", oCharge.ldetail[ipos].dtotal * oCharge.ldetail[ipos].iqty));
            lParameters.Add(new SqlParameter("@ingdetvex", "0"));
            lParameters.Add(new SqlParameter("@ingdetvot", "0"));
            lParameters.Add(new SqlParameter("@ingdetvfa", "0"));
            lParameters.Add(new SqlParameter("@ingdetdfa", "0"));
            lParameters.Add(new SqlParameter("@ingdetdes", "0"));
            lParameters.Add(new SqlParameter("@ingdettip", "R"));
            lParameters.Add(new SqlParameter("@ingdetfac", "S"));
            lParameters.Add(new SqlParameter("@ingdettfa", "N"));
            lParameters.Add(new SqlParameter("@ingdettco", "C"));
            lParameters.Add(new SqlParameter("@ingdetanu", "0"));
            lParameters.Add(new SqlParameter("@ingdetusu", oCharge.sassignedto));
            lParameters.Add(new SqlParameter("@ingdetfch", oCharge.dassigneddate));
            lParameters.Add(new SqlParameter("@ingdethor", oCharge.dassigneddate));
            lParameters.Add(new SqlParameter("@ingdetori", "CAR"));
            lParameters.Add(new SqlParameter("@ingdetvun", oCharge.ldetail[ipos].dtotal));
            lParameters.Add(new SqlParameter("@ingdetsed", oCharge.sbranch));
            lParameters.Add(new SqlParameter("@ingdetufu", "00000"));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        /// <param name="ipos"></param>
        private void CreateDetailLog(Charge oCharge, int ipos)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO aflog (logusu, logter, logpro,  logope,  logde1, logva1,  logde2, logva2,  logde3, logva3,  logtip, logtab,  logfec, logead)");
            sQuery.Append(" VALUES (@logusu, @logter, @logpro,  @logope,  @logde1, @logva1, @logde2, @logva2, @logde3, @logva3, @logtip, @logtab, GETDATE(), @logead)");
            lParameters.Add(new SqlParameter("@logusu", oCharge.screatedby));
            lParameters.Add(new SqlParameter("@logter", "*"));
            lParameters.Add(new SqlParameter("@logpro", "cafcar 7.0.11"));
            lParameters.Add(new SqlParameter("@logope", "Grabar"));
            lParameters.Add(new SqlParameter("@logde1", "F.Cargo"));
            lParameters.Add(new SqlParameter("@logva1", "CA")); //Modificar para cargo desde inspira
            lParameters.Add(new SqlParameter("@logde2", "N.Cargo"));
            lParameters.Add(new SqlParameter("@logva2", oCharge.ldetail[ipos].inumber));
            lParameters.Add(new SqlParameter("@logde3", ""));
            lParameters.Add(new SqlParameter("@logva3", ""));
            lParameters.Add(new SqlParameter("@logtip", "I"));
            lParameters.Add(new SqlParameter("@logtab", "abingdet"));
            lParameters.Add(new SqlParameter("@logead", "01"));            
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        /// <param name="iline"></param>
        /// <param name="isprocedure"></param>
        /// <returns></returns>
        private bool DetailHasRip(Charge oCharge, int iline, bool isprocedure)
        {
            object value = null;
            StringBuilder sQuery = new StringBuilder();
            List<SqlParameter> lParameters = new List<SqlParameter>();
            if (isprocedure)
            {
                sQuery.Append("SELECT riaprodoc FROM abriapro WHERE riaprofue = @riaprofue AND riaprodoc = @riaprodoc AND riaprosed = @riaprosed AND riaprolin = @riaprolin");
                lParameters.Add(new SqlParameter("@riaprofue", oCharge.scode));
                lParameters.Add(new SqlParameter("@riaprodoc", oCharge.ldetail[iline].inumber));
                lParameters.Add(new SqlParameter("@riaprolin", (iline + 1)));
                lParameters.Add(new SqlParameter("@riaprosed", oCharge.sbranch));                
            }
            else
            {
                sQuery.Append("SELECT riacondoc FROM abriacon WHERE riaconfue = @riaconfue AND riacondoc = @riacondoc AND riaconsed = @riaconsed AND riaconlin = @riaconlin");
                lParameters.Add(new SqlParameter("@riaconfue", oCharge.scode));
                lParameters.Add(new SqlParameter("@riacondoc", oCharge.inumber));
                lParameters.Add(new SqlParameter("@riaconsed", oCharge.sbranch));
                lParameters.Add(new SqlParameter("@riaconlin", (iline + 1)));
            }
            value = this.oSQL.GetScalar(sQuery.ToString(), lParameters, true);
            lParameters = null;
            sQuery = null;
            return (value != null);
        }

        /// <summary>
        /// Método que crea los rips en la tabla (abriapro o abriacon)
        /// </summary>
        /// <param name="oCharge">Objeto cargo</param>
        /// <param name="ipos">Número de Línea</param>
        /// <param name="isProcedure">Indica si se va a insertar el rip para procedimiento o consulta</param>
        private void CreateRipLog(Charge oCharge, int ipos, bool isPro = true)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder();
            if (isPro)
            {
                sQuery.Append("INSERT INTO abriapro WITH (ROWLOCK) (riaprofue, riaprodoc, riaprolin, riaprofec, riaprocod, riaprocan, riaprocla, riaprotip, riaproind, riaprofca, riaprodca, riaprolca, riaprofch, riaprousu, riaproest, riaproaut, riaproval, riaprotpr, riaproagr, riaprosed)");
                sQuery.Append(" VALUES (@riaprofue, @riaprodoc, @riaprolin, @riaprofec, @riaprocod, @riaprocan, @riaprocla, @riaprotip, @riaproind, @riaprofca, @riaprodca, @riaprolca, @riaprofch, @riaprousu, @riaproest, @riaproaut, @riaproval, @riaprotpr, @riaproagr, @riaprosed)");
                lParameters.Add(new SqlParameter("@riaprofue", oCharge.scode));
                lParameters.Add(new SqlParameter("@riaprodoc", oCharge.ldetail[ipos].inumber));
                lParameters.Add(new SqlParameter("@riaprolin", (ipos + 1)));
                lParameters.Add(new SqlParameter("@riaprofec", oCharge.dcreateddate));
                lParameters.Add(new SqlParameter("@riaprocod", oCharge.ldetail[ipos].sservice));
                lParameters.Add(new SqlParameter("@riaprocan", oCharge.ldetail[ipos].iqty));
                lParameters.Add(new SqlParameter("@riaprocla", "1"));
                lParameters.Add(new SqlParameter("@riaprotip", "1"));//TODO: Se envía 1 por ser insumo en teoría
                lParameters.Add(new SqlParameter("@riaproind", "A"));
                lParameters.Add(new SqlParameter("@riaprofca", "CA"));//Modificar para cargo desde inspira
                lParameters.Add(new SqlParameter("@riaprodca", oCharge.ldetail[ipos].inumber));
                lParameters.Add(new SqlParameter("@riaprolca", "1"));
                lParameters.Add(new SqlParameter("@riaprofch", oCharge.dcreateddate));
                lParameters.Add(new SqlParameter("@riaprousu", oCharge.screatedby));
                lParameters.Add(new SqlParameter("@riaproest", "A"));
                lParameters.Add(new SqlParameter("@riaproaut", ""));
                lParameters.Add(new SqlParameter("@riaproval", oCharge.ldetail[ipos].dtotal));
                lParameters.Add(new SqlParameter("@riaprotpr", oCharge.ldetail[ipos].stype));
                lParameters.Add(new SqlParameter("@riaproagr", ""));//TODO: Este campo qué es?
                lParameters.Add(new SqlParameter("@riaprosed", oCharge.sbranch));
            }
            else
            {
                sQuery.Append("INSERT INTO abriacon WITH (ROWLOCK) (riaconfue, riacondoc, riaconsed, riaconlin, riaconfec, riaconcod, riaconesp, riacondis, riacongra, riacondin, riaconcon1, riaconcon2, riaconcon3, riaconcon4, riaconcon5, riaconcon6, riaconcon7, riaconcau, riacondia, riacontdi, riaconind, riaconfca, riacondca, riaconlca, riaconfch, riaconusu, riaconest, riacondil, riaconaut, riacondia1, riacondia2, riacondia3, riacontot, riaconvmo, riaconval)");
                sQuery.Append(" VALUES (@riaconfue, @riacondoc, @riaconsed, @riaconlin, @riaconfec, @riaconcod, @riaconesp, @riacondis, @riacongra, @riacondin, @riaconcon1, @riaconcon2, @riaconcon3, @riaconcon4, @riaconcon5, @riaconcon6, @riaconcon7, @riaconcau, @riacondia, @riacontdi, @riaconind, @riaconfca, @riacondca, @riaconlca, @riaconfch, @riaconusu, @riaconest, @riacondil, @riaconaut, @riacondia1, @riacondia2, @riacondia3, @riacontot, @riaconvmo, @riaconval)");
                lParameters.Add(new SqlParameter("@riaconfue", oCharge.scode));
                lParameters.Add(new SqlParameter("@riacondoc", oCharge.inumber));
                lParameters.Add(new SqlParameter("@riaconsed", oCharge.sbranch));
                lParameters.Add(new SqlParameter("@riaconlin", (ipos + 1)));
                lParameters.Add(new SqlParameter("@riaconfec", oCharge.dcreateddate));
                lParameters.Add(new SqlParameter("@riaconcod", oCharge.ldetail[ipos].sservice));
                lParameters.Add(new SqlParameter("@riaconesp", Tools.GetSpeciality(oCharge.ldetail[ipos].sservice)));
                lParameters.Add(new SqlParameter("@riacondis", DBNull.Value));
                lParameters.Add(new SqlParameter("@riacongra", DBNull.Value));
                lParameters.Add(new SqlParameter("@riacondin", "0"));
                lParameters.Add(new SqlParameter("@riaconcon1", DBNull.Value));
                lParameters.Add(new SqlParameter("@riaconcon2", DBNull.Value));
                lParameters.Add(new SqlParameter("@riaconcon3", DBNull.Value));
                lParameters.Add(new SqlParameter("@riaconcon4", DBNull.Value));
                lParameters.Add(new SqlParameter("@riaconcon5", DBNull.Value));
                lParameters.Add(new SqlParameter("@riaconcon6", DBNull.Value));
                lParameters.Add(new SqlParameter("@riaconcon7", DBNull.Value));
                lParameters.Add(new SqlParameter("@riaconcau", "13"));
                lParameters.Add(new SqlParameter("@riacondia", Tools.GetDiagnosis(oCharge.ssource)));                
                lParameters.Add(new SqlParameter("@riacontdi", "3"));
                lParameters.Add(new SqlParameter("@riaconind", "A"));
                lParameters.Add(new SqlParameter("@riaconfca", "CA"));//Modificar para cargo desde Inspira
                lParameters.Add(new SqlParameter("@riacondca", oCharge.ldetail[ipos].inumber));
                lParameters.Add(new SqlParameter("@riaconlca", "1"));
                lParameters.Add(new SqlParameter("@riaconfch", oCharge.dcreateddate));
                lParameters.Add(new SqlParameter("@riaconusu", oCharge.screatedby));
                lParameters.Add(new SqlParameter("@riaconest", "A"));
                lParameters.Add(new SqlParameter("@riacondil", oCharge.screatedby));
                lParameters.Add(new SqlParameter("@riaconaut", (string.IsNullOrEmpty(oCharge.sauthorization) ? DBNull.Value : (object)oCharge.sauthorization)));
                //lParameters.Add(new SqlParameter("@riaconaut", DBNull.Value));
                lParameters.Add(new SqlParameter("@riacondia1", DBNull.Value));
                lParameters.Add(new SqlParameter("@riacondia2", DBNull.Value));
                lParameters.Add(new SqlParameter("@riacondia3", DBNull.Value));
                lParameters.Add(new SqlParameter("@riacontot", oCharge.ldetail[ipos].dtotal));
                lParameters.Add(new SqlParameter("@riaconvmo", "0"));
                lParameters.Add(new SqlParameter("@riaconval", oCharge.ldetail[ipos].dtotal));                
            }
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);            
            sQuery = null;
            lParameters = null;
        }  
        
        /// <summary>
        /// Método que obtiene el listado de ingresos que no han sido facturados en Servinte
        /// </summary>
        /// <returns>Lista genérica con el ingreso, documento y fuente</returns>
        private List<Generic> GetNotInvoicedCharges()
        {
            DataTable dt = new DataTable();
            List<Generic> lGeneric = new List<Generic>();
            Generic oGeneric = null;
            StringBuilder sQuery = new StringBuilder("SELECT ingdoc, ingide, ingfue, ingfec FROM abing WITH (NOLOCK) INNER JOIN abingdet WITH (NOLOCK) ON");
            sQuery.Append(" ingdoc = ingdetdoo AND ingfue = ingdetfuo AND ingsed = ingdetsed");
            sQuery.Append(" LEFT JOIN abingfac WITH (NOLOCK) ON ingfacdca = ingdetdoc AND ingdetfue = ingfacfca AND ingdetlin = ingfaclin");
            sQuery.Append(" AND ingdetsed = ingfacsed WHERE inganu = 0 AND ingfacdoc IS NULL");
            try
            {
                dt = this.oSQL.GetDataTable(sQuery.ToString(), null, false, true);
                foreach (DataRow dr in dt.Rows)
                {
                    oGeneric = new Generic()
                    {
                        scode = dr["ingide"].ToString(),
                        iid = Convert.ToInt32(dr["ingdoc"]),
                        sname = dr["ingfue"].ToString(),
                        dtDate = Convert.ToDateTime(dr["ingfec"]),
                    };
                    lGeneric.Add(oGeneric);
                }
                return lGeneric;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                oGeneric = null;
                sQuery = null;
            }
        }

        /// <summary>
        /// Método que crea un ingreso y su cargo en Servinte
        /// </summary>
        /// <param name="oPatient">Objeto paciente</param>
        /// <param name="oCharge">Objeto cargo</param>
        /// <returns>Entero con el número de ingreso creado</returns>
        public int ChargeTransaction(Patient oPatient, Charge oCharge)
        {
            this.oSQL = new SQLServer(this.sConnection);
            try
            {
                if (this.oSQL.Connect())
                {
                    this.oSQL.dbTrans = this.oSQL.oConnection.BeginTransaction();
                    oCharge.inumber = this.GetSecuenceNumber(oCharge.scode, oCharge.sbranchname, true);
                    oPatient.sbranch = oCharge.sbranch;
                    this.UpdateSIIDE(oCharge.screatedby);
                    if (!this.UserHasLog(oCharge.screatedby))
                        this.CreateUserLog(oCharge.screatedby, oCharge.sbranch);
                    if (!this.PatientExists(oPatient))
                    {
                        this.CreatePatient(oPatient);
                        this.CreatePatientDocument(oPatient);
                    }
                    else
                    {                        
                        this.UpdatePatient(oPatient);
                    }                    
                    this.CreateCharge(oCharge);
                    this.CreateChargeLog(oCharge, "abing");
                    this.CreateCompanyCharge(oCharge);
                    this.CreateChargeACL(oCharge);
                    this.InsertOtr(oCharge);
                    this.UpdateOtr(oCharge);
                    this.CreateChargeTransaction(oCharge);
                    this.CreateChargeLog(oCharge, "abingtra");
                    this.UpdatePatientDocument(oPatient, oCharge.inumber);
                    for (int i = 0; i < oCharge.ldetail.Count; i++)
                    {
                        if (oCharge.sagreementtype != "P")
                        {
                            this.CreateAuthorization(oCharge, i);
                        }
                        /*this.CreateChargeDetail(oCharge, i);
                        this.CreateDetailLog(oCharge, i);
                        this.CreateRipLog(oCharge, i);*/
                    }
                    this.UpdateSecuenceNumber(oCharge.scode, oCharge.sbranchname);
                    this.oSQL.Commit();                    
                }
                return oCharge.inumber;
            }
            catch (Exception ex)
            {
                if (this.oSQL.oConnection.State == ConnectionState.Open) this.oSQL.RollBack();
                throw ex;
            }
            finally
            {
                
            }
        }

        /// <summary>
        /// Método que crea los cargos para el servicio de terapia y hospitalizacion
        /// </summary>
        /// <param name="lPatient">Lista genérica que contiene los pacientes con sus ingresos</param>
        /// <param name="lGeneric">Lista genérica con los documentos de los responsables de la atención de los servicios en la FNC</param>
        /// <returns>Lista genérica con el resultado de los pacientes e ingresos creados</returns>
        public List<Patient> CreateTherapyCharges(List<Patient> lPatient, List<Generic> lGeneric)
        {
            this.oSQL = new SQLServer(this.sConnection);
            DataTable dt = new DataTable();
            //List<Generic> lCharges = null;
            Generic oGeneric = null;
            Generic oEntity = null;
            string sResponsible = string.Empty;
            List<Patient> lTmp = null;
            List<Patient> lTmp1 = null;
            Patient oItem = null;
            int iCharge = 0;
            int j = 0;
            bool bflag = true;
            try
            {                
                if (this.oSQL.Connect())
                {
                    this.oSQL.dbTrans = this.oSQL.oConnection.BeginTransaction();
                    //lCharges = this.GetNotInvoicedCharges();
                    dt = this.GetThird();
                    if (!this.UserHasLog(lPatient[0].oCharge.screatedby))
                    {
                        this.CreateUserLog(lPatient[0].oCharge.screatedby, lPatient[0].oCharge.sbranch);
                    }
                    lTmp = lPatient.GroupBy(x => new { x.sdocument, x.bhaserror }).Select(y => y.First()).ToList();
                    foreach (Patient item in lTmp)
                    {
                        if (item.bhaserror)
                        {
                            //Acá se valida si un paciente tiene ingreso sin facturar entonces se trae ese número de ingreso
                            //oGeneric = lCharges.FirstOrDefault(x => x.scode == item.sdocument && x.dtDate.Year == item.dcreateddate.Year && x.dtDate.Month == item.dcreateddate.Month);                        
                            iCharge = (oGeneric != null) ? oGeneric.iid : this.GetSecuenceNumber("IN", "ADMI", true);
                            //iCharge = this.GetSecuenceNumber("IN", "ADMI", true);
                            bflag = true;
                            lTmp1 = lPatient.FindAll(x => x.sdocument == item.sdocument);
                            for (int k = 0; k < lTmp1.Count && bflag; k++)
                            {
                                oItem = lTmp1[k];
                                oItem.oCharge.inumber = iCharge;
                                if (j == 0)
                                {
                                    this.UpdateSIIDE(oItem.oCharge.screatedby);
                                    if (!this.PatientExists(oItem))
                                    {
                                        if (oItem.bhaserror)
                                        {
                                            this.CreatePatient(oItem);
                                            if (!this.DocumentExists(oItem))
                                            {
                                                this.CreatePatientDocument(oItem);
                                            }
                                        }
                                        else
                                        {
                                            bflag = false;
                                        }
                                    }
                                    else
                                    {
                                        this.UpdatePatient(oItem);
                                    }
                                    if (oGeneric == null)
                                    {
                                        oEntity = this.GetPaymentType(oItem.oCharge.splan, oItem.oCharge.sagreementcode);
                                        if (oEntity != null)
                                        {
                                            oItem.oCharge.iusertype = Convert.ToInt32(oEntity.scode);
                                            oItem.oCharge.ilevel = Convert.ToInt32(oEntity.sname);
                                        }
                                        this.CreateCharge(oItem.oCharge);
                                        this.CreateChargeLog(oItem.oCharge, "abing");
                                        this.CreateCompanyCharge(oItem.oCharge);
                                        this.CreateChargeACL(oItem.oCharge);
                                        this.InsertOtr(oItem.oCharge);
                                        this.UpdateOtr(oItem.oCharge);
                                        this.CreateChargeTransaction(oItem.oCharge);
                                        this.CreateChargeLog(oItem.oCharge, "abingtra");
                                    }
                                    this.UpdatePatientDocument(oItem, oItem.oCharge.inumber);
                                }
                                oItem.oCharge.ldetail[0].inumber = this.GetSecuenceNumber("CA", "ADMI", true);
                                for (int i = 0; i < oItem.oCharge.ldetail.Count; i++)
                                {
                                    sResponsible = this.GetResponsible(oItem.oCharge.ldetail[i].sservice, lGeneric, dt, oItem.oCharge.scostcenter, oItem.oCharge.snit);
                                    oItem.oCharge.ldetail[i].snit = (!string.IsNullOrEmpty(sResponsible)) ? sResponsible : oItem.oCharge.snit;
                                    this.CreateChargeDetail(oItem.oCharge, i);
                                    this.CreateDetailLog(oItem.oCharge, i);
                                }
                                this.UpdateSecuenceNumber("CA", "ADMI");
                                j++;
                            }
                            if (oGeneric == null) this.UpdateSecuenceNumber("IN", "ADMI");
                            j = 0;
                        }                        
                    }
                    this.oSQL.Commit();
                }
                return lPatient;
            }
            catch (Exception ex)
            {
                if (this.oSQL.oConnection.State == ConnectionState.Open) this.oSQL.RollBack();
                throw ex;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                oItem = null;
                lTmp1 = null;
                lTmp = null;
                oGeneric = null;
                oEntity = null;
                //lCharges = null;
            }
        }

        /// <summary>
        /// Método que crea los cargos
        /// </summary>
        /// <param name="lPatient">Lista genérica que contiene los pacientes con sus ingresos</param>
        /// <param name="lGeneric">Lista genérica con los documentos de los responsables de la atención de los servicios en la FNC</param>
        /// <returns>Lista genérica con el resultado de los pacientes e ingresos creados</returns>
        public List<Patient> CreateCharges(List<Patient> lPatient, List<Generic> lGeneric)
        {
            this.oSQL = new SQLServer(this.sConnection);
            DataTable dt = new DataTable();
            string sResponsible = string.Empty;
            string sType = string.Empty;
            int k = 1;
            try
            {
                if (this.oSQL.Connect())
                {
                    this.oSQL.dbTrans = this.oSQL.oConnection.BeginTransaction();
                    dt = this.GetThird();
                    if (!this.UserHasLog(lPatient[0].oCharge.screatedby))
                    {
                        this.CreateUserLog(lPatient[0].oCharge.screatedby, lPatient[0].oCharge.sbranch);
                    }                    
                    foreach (Patient oPatient in lPatient)
                    {
                        if (oPatient.bhaserror)
                        {
                            oPatient.oCharge.inumber = this.GetSecuenceNumber("IN", "ADMI", true);
                            sType = this.GetDocumentType(oPatient.sdocument);
                            if (!string.IsNullOrEmpty(sType)) oPatient.sdocumenttype = sType;
                            this.UpdateSIIDE(oPatient.oCharge.screatedby);
                            if (!this.PatientExists(oPatient))
                            {
                                this.CreatePatient(oPatient);
                                if (!this.DocumentExists(oPatient))
                                {
                                    this.CreatePatientDocument(oPatient);
                                }                                
                            }
                            else
                            {
                                this.UpdatePatient(oPatient);
                            }
                            oPatient.oCharge.sdocumenttype = oPatient.sdocumenttype;
                            oPatient.oCharge.sdocument = oPatient.sdocument;
                            this.CreateCharge(oPatient.oCharge);
                            this.CreateChargeLog(oPatient.oCharge, "abing");
                            this.CreateCompanyCharge(oPatient.oCharge);
                            this.CreateChargeACL(oPatient.oCharge);
                            this.InsertOtr(oPatient.oCharge);
                            this.UpdateOtr(oPatient.oCharge);
                            this.CreateChargeTransaction(oPatient.oCharge);
                            this.CreateChargeLog(oPatient.oCharge, "abingtra");
                            this.UpdatePatientDocument(oPatient, oPatient.oCharge.inumber);
                            if (oPatient.oCharge.ssource.Contains("EDUCACI") || oPatient.oCharge.ssource.Contains("LLAMADA"))
                            {
                                oPatient.oCharge.ldetail = this.GetEducationDetail(oPatient.oCharge, oPatient.oCharge.scostcenter);
                            }
                            else if (oPatient.oCharge.ssource.Contains("HOSPITALIZA"))
                            {
                                oPatient.oCharge.ldetail[0].inumber = this.GetSecuenceNumber("CA", "ADMI", true);                                                                   
                            }
                            else
                            {
                                oPatient.oCharge.ldetail = this.FillDetailByTemplate(oPatient.oCharge);
                            }
                            for (int i = 0; i < oPatient.oCharge.ldetail.Count; i++)
                            {
                                sResponsible = this.GetResponsible(oPatient.oCharge.ldetail[i].sservice, lGeneric, dt, oPatient.oCharge.scostcenter, oPatient.oCharge.snit);
                                oPatient.oCharge.ldetail[i].snit = (!string.IsNullOrEmpty(sResponsible)) ? sResponsible : oPatient.oCharge.snit;
                                this.CreateAuthorization(oPatient.oCharge, i);
                                this.CreateChargeDetail(oPatient.oCharge, i);
                                this.CreateDetailLog(oPatient.oCharge, i);
                                if (!this.DetailHasRip(oPatient.oCharge, i, (oPatient.oCharge.ldetail[i].itype == 0)))
                                {
                                    this.CreateRipLog(oPatient.oCharge, i, (oPatient.oCharge.ldetail[i].itype == 0));
                                }
                            }
                            this.UpdateSecuenceNumber("IN", "ADMI");
                            this.UpdateSecuenceNumber("CA", "ADMI");
                            k++;  
                        }                                               
                    }
                    this.oSQL.Commit();
                }
                return lPatient;
            }
            catch (Exception ex)
            {
                if (this.oSQL.oConnection.State == ConnectionState.Open) this.oSQL.RollBack();
                EventLog.LogError.WriteMessage("Trazabilidad", "DAC", ex.Message + " " + k.ToString());
                throw ex;
            }
            finally
            {
                dt.Dispose();
                dt = null;
            }            
        }

        /// <summary>
        /// Método que actualiza la tabla siide en Servinte
        /// </summary>
        /// <param name="sUser">Código del usuario en Servinte</param>
        private void UpdateSIIDE(string sUser)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("UPDATE siide WITH (ROWLOCK) SET ideufi = GETDATE() WHERE idecod = @idecod");
            lParameters.Add(new SqlParameter("@idecod", sUser));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// Método que verifica si un usuario ya tiene log de transacciones creado para la aplicación Gestor en Servinte
        /// </summary>
        /// <param name="sUser">Código del usuario</param>
        /// <returns>Verdadero si el usuario tiene log falso en caso contrario</returns>
        private bool UserHasLog(string sUser)
        {
            object value = null;
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("SELECT usuactapl FROM siusuact WHERE usuactusu = @usuactusu AND usuactapl = @usuactapl AND usuactpro = @usuactpro AND usuactead = @usuactead");
            lParameters.Add(new SqlParameter("@usuactapl", "GESTOR"));
            lParameters.Add(new SqlParameter("@usuactusu", sUser));
            lParameters.Add(new SqlParameter("@usuactpro", 1977));
            lParameters.Add(new SqlParameter("@usuactead", "01"));
            value = this.oSQL.GetScalar(sQuery.ToString(), lParameters, true);
            return (value != null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sUser"></param>
        private void CreateUserLog(string sUser, string sBranch)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQuery = new StringBuilder("INSERT INTO siusuact (usuactapl, usuactusu, usuactfec, usuactpro, usuactead)");
            sQuery.Append(" VALUES (@usuactapl, @usuactusu, GETDATE(), @usuactpro, @usuactead)");
            lParameters.Add(new SqlParameter("@usuactapl", "GESTOR"));
            lParameters.Add(new SqlParameter("@usuactusu", sUser));
            lParameters.Add(new SqlParameter("@usuactpro", 1977));
            lParameters.Add(new SqlParameter("@usuactead", sBranch));
            this.oSQL.ExecuteNonQuery(sQuery.ToString(), lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scode"></param>
        /// <param name="ssource"></param>
        private void UpdateSecuenceNumber(string scode, string ssource)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            string sQuery = "UPDATE infue SET fuesec = fuesec + 1 WHERE fuecod = @fuecod AND fuepli = @fuepli";
            lParameters.Add(new SqlParameter("@fuecod", scode));
            lParameters.Add(new SqlParameter("@fuepli", ssource));
            this.oSQL.ExecuteNonQuery(sQuery, lParameters, true, false);
            sQuery = null;
            lParameters = null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scode"></param>
        /// <param name="ssource"></param>
        /// <param name="bistransacction"></param>
        /// <returns></returns>
        public int GetSecuenceNumber(string scode, string ssource, bool bistransacction = false)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            StringBuilder sQyery = new StringBuilder("SELECT fuesec FROM infue WHERE fuecod = @fuecod AND fuepli = @fuepli");
            lParameters.Add(new SqlParameter("@fuecod", scode));
            lParameters.Add(new SqlParameter("@fuepli", ssource));
            object isecuence = null;
            try
            {
                if (!bistransacction)
                {
                    using (SQLServer oSQL = new SQLServer(this.sConnection))
                    {
                        isecuence = oSQL.GetScalar(sQyery.ToString(), lParameters);
                    }
                }
                else
                {
                    isecuence = this.oSQL.GetScalar(sQyery.ToString(), lParameters, true);
                }                
                return (isecuence != null) ? Convert.ToInt32(isecuence) : 0;
            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="scode"></param>
        /// <returns></returns>
        public string GetBranchName(string scode)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            string squery = "SELECT plicod FROM inpli WHERE plised = @plised";
            lParameters.Add(new SqlParameter("@plised", scode));
            object sname = null;
            try
            {
                using (SQLServer oSQL = new SQLServer(this.sConnection))
                {
                    sname = oSQL.GetScalar(squery, lParameters);
                }
                return (sname != null) ? sname.ToString() : string.Empty;
            }
            catch (Exception)
            {

                throw;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        /// <returns></returns>
        private List<ChargeDetail> FillDetailByTemplate(Charge oCharge)
        {
            DataTable dt = new DataTable();
            List<ChargeDetail> lChargeDetail = new List<ChargeDetail>();
            ChargeDetail oChargeDetail = null;
            int icharge = 0;
            try
            {
                dt = this.GetDetailTemplateData(oCharge);
                icharge = this.GetSecuenceNumber("CA", "ADMI", true);
                foreach (DataRow dr in dt.Rows)
                {
                    oChargeDetail = new ChargeDetail()
                    {
                        inumber = icharge,
                        iqty = 1,
                        sconcept = dr["pladetcon"].ToString(),
                        scostcenter = dr["pladetcco"].ToString(),
                        sservice = Tools.SubString(dr["pladetcod"].ToString(), 6),
                        dtotal = Convert.ToDecimal(dr["protarval"]),     
                        //snit = dr["proemp"].ToString(),    
                        stype = dr["protip"].ToString(),
                        itype = Convert.ToInt32(dr["tipo"]),
                    };
                    lChargeDetail.Add(oChargeDetail);
                }
                return lChargeDetail;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                oChargeDetail = null;
            }

        }        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        /// <returns></returns>
        private List<ChargeDetail> GetEducationDetail(Charge oCharge, string sCostCenter)
        {
            List<ChargeDetail> lChargeDetail = new List<ChargeDetail>();
            ChargeDetail oChargeDetail = null;
            DataTable dt = new DataTable();
            int icharge = 0;            
            try
            {
                dt = this.GetEducationDetailData(oCharge);
                icharge = this.GetSecuenceNumber("CA", "ADMI", true);
                if (dt.Rows.Count > 0)
                {
                    oChargeDetail = new ChargeDetail()
                    {
                        sservice = Tools.SubString(dt.Rows[0]["protarpro"].ToString(), 6),
                        sconcept = dt.Rows[0]["protarcon"].ToString(),
                        scostcenter = dt.Rows[0]["protarcco"].ToString(),
                        inumber = icharge,
                        iqty = 1,
                        dtotal = Convert.ToDecimal(dt.Rows[0]["protarval"]),
                        //snit = dt.Rows[0]["protarnit"].ToString(),
                        stype = dt.Rows[0]["protip"].ToString(),
                        itype = Convert.ToInt32(dt.Rows[0]["tipo"]),
                    };
                    lChargeDetail.Add(oChargeDetail);
                }
                return lChargeDetail;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                oChargeDetail = null;
                dt.Dispose();
                dt = null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="oCharge"></param>
        /// <returns></returns>
        private DataTable GetEducationDetailData(Charge oCharge)
        {
            string sCost = (oCharge.splanname.Contains("AIREPOC")) ? "AIRE" : (oCharge.scostcenter == "COPE") ? "ASIN" : "ASAD";
            List <SqlParameter> lParameters = new List<SqlParameter>();            
            string sQuery = "SELECT protarpro, protarval, protarcon, protarcco, (SELECT empnit FROM inemp WHERE empcod = @agreement) protarnit, protip, CASE WHEN UPPER(pronom) LIKE '%CONSULTA%' THEN 1 ELSE 0 END tipo FROM inprotar, inpro WHERE protarpro = @service AND protartar = @rate AND protarpro = procod AND protarcco = @costcenter";
            lParameters.Add(new SqlParameter("@service", oCharge.ldetail[0].sservice));
            lParameters.Add(new SqlParameter("@rate", oCharge.srate));
            lParameters.Add(new SqlParameter("@agreement", oCharge.sagreementcode));
            lParameters.Add(new SqlParameter("@costcenter", sCost));
            return this.oSQL.GetDataTable(sQuery, lParameters, false, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sservice"></param>
        /// <param name="lGeneric"></param>
        /// <returns></returns>
        private string GetResponsible(string sservice, List<Generic> lGeneric, DataTable dt, string scostcenter, string snit)
        {
            Generic oGeneric = null;
            if (lGeneric != null)
            {
                oGeneric = lGeneric.Find(x => x.scode == sservice && x .sfilter == scostcenter);
            }            
            if (oGeneric != null)
            {
                return oGeneric.sname;
            }
            else
            {
                DataRow dr = dt.AsEnumerable().Where(x => x["nitnit"].ToString() == snit).FirstOrDefault();
                if (dr != null)
                {
                    return snit;
                }
                else
                {
                    if (scostcenter == "COPE")
                    {
                        return "46679720";
                    }
                    else if (scostcenter == "COAD")
                    {
                        return "73135051";
                    }
                }
            }
            return string.Empty;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private DataTable GetThird()
        {
            string squery = "SELECT nitnom, nitnit FROM conit WHERE nitact = 'S'";
            return this.oSQL.GetDataTable(squery, null, false, true);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sPlan"></param>
        /// <param name="sCompany"></param>
        /// <returns></returns>
        public Generic GetPaymentType(string sPlan, string sCompany)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            string sQuery = "SELECT TOP 1 pagtus, pagniv FROM inpag WHERE pagemp = @company AND pagpla = @plan";
            DataTable dt = new DataTable();
            Generic oGeneric = new Generic() { scode = "1", sname = "1" };
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@company", sCompany));
                lParameters.Add(new SqlParameter("@plan", sPlan));
                dt = oDAC.GetDataTable(sQuery, lParameters);
                if (dt.Rows.Count > 0)
                {
                    oGeneric.scode = dt.Rows[0]["pagtus"].ToString();
                    oGeneric.sname = dt.Rows[0]["pagniv"].ToString();
                }
                dt.Dispose();
                dt = null;
                lParameters = null;
                return oGeneric;
            }                            
        }

        /// <summary>
        /// Método que obtiene los códigos de las fuentes para ingresos y cargos de pacientes de una sede (tabla sifue)
        /// </summary>
        /// <param name="sCompany">Código de la sede</param>
        /// <returns>Lista genérica con los códigos encontrados</returns>
        public List<string> GetSourcesCodes(string sCompany)
        {
            List<string> lSources = new List<string>();
            string sQuery = "SELECT fuecod FROM sifue WHERE fuenom IN ('INGRESOS DE PACIENTES', 'CARGOS DE PACIENTES') AND fuesed = @company ORDER BY fuenom DESC";
            List<SqlParameter> lParameters = new List<SqlParameter>();
            DataTable dt = new DataTable();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@company", sCompany));
                dt = oDAC.GetDataTable(sQuery, lParameters);
                foreach (DataRow dr in dt.Rows)
                {
                    lSources.Add(dr["fuecod"].ToString());
                }
            }
            if (lSources.Count == 0) lSources.Add("IN");
            dt.Dispose();
            dt = null;
            lParameters = null;
            return lSources;
        }

        /// <summary>
        /// Método que obtiene el listado de documentos, tipo de documento y fechas de un listado de pacientes en Servinte
        /// </summary>
        /// <param name="aPatients">Arreglo con las identificaciones de los pacientes</param>
        /// <returns>Lista genérica con la información de tipo de documento, documento y fecha de nacimiento del paciente</returns>
        public List<Patient> GetPatients(string[] aPatients)
        {
            string sPatients = string.Join("','", aPatients);
            Patient oPatient = null;
            List<Patient> lPatients = new List<Patient>();
            DataTable dt = new DataTable();
            StringBuilder sQuery = new StringBuilder("SELECT pacide, pactid, pacfch FROM abpac WITH (NOLOCK) WHERE pacide IN ('");
            sQuery.Append(sPatients);
            sQuery.Append("')");
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                dt = oDAC.GetDataTable(sQuery.ToString(), null);
                foreach (DataRow dr in dt.Rows)
                {
                    oPatient = new Patient()
                    {
                        sdocument = dr["pacide"].ToString(),
                        sdocumenttype = dr["pactid"].ToString(),
                        dcreateddate = Convert.ToDateTime(dr["pacfch"]),
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
        /// Genera listado de las compañías activas en el ERP (código y nombre)
        /// </summary>
        /// <returns>Lista genérica con las compañías</returns>
        public List<Generic> GetCompanyList()
        {
            List<Generic> lGeneric = new List<Generic>();
            DataTable dt = new DataTable();
            Generic oEntity = null;
            string sQuery = "SELECT empcod, empnom FROM inemp WITH (NOLOCK) WHERE empact = 'S'";
            try
            {
                using (SQLServer oDAC = new SQLServer(this.sConnection))
                {
                    dt = oDAC.GetDataTable(sQuery, null);
                    foreach (DataRow dr in dt.Rows)
                    {
                        oEntity = new Generic()
                        {
                            scode = dr["empcod"].ToString(),
                            sname = dr["empnom"].ToString(),
                        };
                        lGeneric.Add(oEntity);
                    }
                }
                return lGeneric;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                dt.Dispose();
                sQuery = null;
                dt = null;
                oEntity = null;
            }
        }

        /// <summary>
        /// Método que obtiene las tarifas por servicio activas en Servinte
        /// </summary>
        /// <returns>Retorna lista genérica con las tarifas con sus servicios</returns>
        public List<Generic> GetServiceRates()
        {
            List<Generic> lGeneric = new List<Generic>();
            DataTable dt = new DataTable();
            Generic oEntity = null;
            string sQuery = "SELECT protarpro, protartar, protarval, protip, protarcon FROM inprotar WITH (NOLOCK) INNER JOIN inpro ON procod = protarpro WHERE proact = 'S'";
            try
            {
                using (SQLServer oDAC = new SQLServer(this.sConnection))
                {
                    dt = oDAC.GetDataTable(sQuery, null);
                    foreach (DataRow dr in dt.Rows)
                    {
                        oEntity = new Generic()
                        {
                            scode = dr["protarpro"].ToString(),
                            sname = dr["protartar"].ToString(),
                            iid = Convert.ToInt32(dr["protarval"]),
                            sfilter = dr["protip"].ToString(),  
                            sextra1 = dr["protarcon"].ToString(),
                        };
                        lGeneric.Add(oEntity);
                    }
                }
                return lGeneric;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                dt.Dispose();
                sQuery = null;
                dt = null;
                oEntity = null;
            }
        }

        /// <summary>
        /// Método que obtiene una tabla de datos con la información de una plantilla de Servinte
        /// </summary>
        /// <param name="oCharge">Objeto Cargo</param>
        /// <returns>DataTable con la información de la plantilla encontrada</returns>
        public DataTable GetDetailTemplateData(Charge oCharge)
        {
            List<SqlParameter> lParameters = new List<SqlParameter>();
            string sCost = string.Empty;
            if (!string.IsNullOrEmpty(oCharge.splanname))
            {
                if (oCharge.splanname.Contains("AIREPOC")) sCost = "AIRE";
                else
                {
                    sCost = (oCharge.scostcenter == "COPE") ? "ASIN" : "ASAD";
                }
            }
            else
            {
                sCost = (oCharge.scostcenter == "COPE") ? "ASIN" : "ASAD";
            }            
            StringBuilder sQuery = new StringBuilder("SELECT DISTINCT pladetpla, pladetlin, pladettip, pladetcon, pladettpr, pladetcod, pladetcan, (SELECT empnit FROM inemp WHERE empcod = @agreement) proemp");
            sQuery.Append(", connom nombre_con, conarc, pladetfac, pladetcco, protarval, protip, CASE WHEN UPPER(pronom) LIKE '%CONSULTA%' THEN 1 ELSE 0 END tipo FROM fapladet, facon, inprotar, inpro WHERE pladetcon = concod");
            sQuery.Append(" AND pladetpla = @template AND protarpro = pladetcod AND protartar = @rate AND procod = pladetcod AND pladetcco = @scost AND protarcon = pladetcon");
            lParameters.Add(new SqlParameter("@template", oCharge.stemplate));
            lParameters.Add(new SqlParameter("@rate", oCharge.srate));
            lParameters.Add(new SqlParameter("@agreement", oCharge.sagreementcode));
            lParameters.Add(new SqlParameter("@scost", sCost));
            return this.oSQL.GetDataTable(sQuery.ToString(), lParameters, false, true);
        }

        #region Métodos para la migración

        private ServintePackage FindConcept(List<ServintePackage> lservintePackages, string sservice, string srate, string sconcept)
        {
            return lservintePackages.FirstOrDefault(x => x.sservice == sservice && x.srate == srate && x.sconcept.StartsWith(sconcept));            
        }

        public List<ServintePackage> GetProductRates()
        {
            string squery = "SELECT * FROM VTARIFAPRODUCTO";
            DataTable dt = new DataTable();
            Oracle oracle = null;
            List<ServintePackage> lServintePackage = new List<ServintePackage>();
            ServintePackage servintePackage = null;
            try
            {
                oracle = new Oracle();
                oracle.sConnection = this.sConnection;
                oracle.Connect();
                dt = oracle.GetDataTable(squery, null);
                foreach (DataRow dr in dt.Rows)
                {
                    servintePackage = new ServintePackage()
                    {
                        sconcept = dr["PROTARCON"].ToString(),
                        sservice = dr["PROTARPRO"].ToString(),
                        scostcenter = dr["PROTARCCO"].ToString(),
                        srate = dr["PROTARTAR"].ToString(),
                    };
                    lServintePackage.Add(servintePackage);
                }
                return lServintePackage;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                oracle.Dispose();
                oracle = null;
                servintePackage = null;
                dt.Dispose();
                dt = null;
            }

        }

        public List<Generic> GetGroupsData()
        {
            string query = "SELECT * FROM TPlantillaCargo";
            DataTable dataTable = new DataTable();
            List<Generic> lGeneric = new List<Generic>();
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                dataTable = oDAC.GetDataTable(query, null);
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    var generic = new Generic()
                    {
                        scode = dataRow["pl_grupo"].ToString(),
                        iid = Convert.ToInt32(dataRow["pl_creacargo"]),
                    };
                    lGeneric.Add(generic);
                }
            }
            dataTable.Dispose();
            dataTable = null;
            return lGeneric;
        }

        /// <summary>
        /// Método para obtener el listado de cargos que se van a migrar de Gestor a Servinte
        /// </summary>
        /// <returns>Lista genérica de objeto Paciente Serviente para la integración</returns>
        public List<ServintePatient> GetMigrationCharges(List<ServintePackage> lservintePackages)
        {
            //string squery = "SELECT * FROM VCargosMigrar WHERE ingdoc = 2035101";
            string squery = "SELECT * FROM VCargosMigrar";
            DataTable dataTable = new DataTable();
            SQLServer sQLServer = null;
            List<ServintePatient> lservintePatients = new List<ServintePatient>();
            ServintePatient servintePatient = null;
            InspiraCita inspiraCita = null;
            ServiceRequest serviceRequest = null;
            string[] anames = new string[2];
            string sconcept = string.Empty;
            string scostcenter = string.Empty;
            string sufu = string.Empty;
            ServintePackage servintePackage = null;
            try
            {
                sQLServer = new SQLServer(this.sConnection);
                dataTable = sQLServer.GetDataTable(squery, null);
                (from DataRow datarow in dataTable.Rows group datarow by new { documento = datarow.Field<string>("pacide"), tipodocumento = datarow.Field<string>("pactid") } into f
                select new
                {
                    Key = f.Key,
                    Elements = f,
                }).ToList().ForEach(f =>
                {
                    anames = f.Elements.First()["pacnom"].ToString().Split(' ');
                    servintePatient = new ServintePatient()
                    {
                        sdocument = Tools.GetDocumentType(f.Elements.First()["pacide"].ToString()),
                        sdocumenttype = f.Elements.First()["pactid"].ToString(),
                        sfirstname = anames[0],
                        ssecondname = (anames.Length > 1) ? anames[1] : string.Empty,
                        ssurname = f.Elements.First()["pacap1"].ToString(),
                        ssecondsurname = f.Elements.First()["pacap2"].ToString(),
                        saddress = f.Elements.First()["pacdir"].ToString(),
                        sgender = f.Elements.First()["pacsex"].ToString(),
                        surbanzone = f.Elements.First()["paczon"].ToString(),
                        sbornplace = f.Elements.First()["paclug"].ToString(),
                        sneighborhood = f.Elements.First()["pacbar"].ToString(),
                        scovid1 = "N",
                        scovid2 = "N",
                        scellphone = f.Elements.First()["pactel"].ToString(),
                        smaritalstatus = f.Elements.First()["pacest"].ToString(),
                        snation = "169",
                        slevel = f.Elements.First()["pacniv"].ToString(),
                        safiliation = f.Elements.First()["ingtus"].ToString(),
                        scity = f.Elements.First()["paclug"].ToString(),
                        sphone = f.Elements.First()["pactel"].ToString(),
                        dbirthdate = Convert.ToDateTime(f.Elements.First()["pacnac"]),
                        lappointments = new List<InspiraCita>(),
                    };
                    (from DataRow datarow in dataTable.Rows
                    where datarow["pactid"].ToString() == f.Elements.First()["pactid"].ToString() && datarow["pacide"].ToString() == f.Elements.First()["pacide"].ToString()
                    group datarow by datarow["ingdetdoc"] into q
                    select new
                    {
                         Key = q.Key,
                         Elements = q,
                    }).ToList().ForEach(q =>
                    {
                        inspiraCita = new InspiraCita()
                        {
                            sagreementname = q.Elements.First()["n_nit"].ToString().Trim(),
                            sagreement = q.Elements.First()["emp_new"].ToString().Trim(),
                            srate = q.Elements.First()["tar_new"].ToString().Trim(),
                            ddate = Convert.ToDateTime(q.Elements.First()["ingdetfec"]),
                            sunit = q.Elements.First()["ingufu"].ToString().Trim(),
                            sagreementtype = q.Elements.First()["ingtir"].ToString().Trim(),
                            sservicetype = q.Elements.First()["ingser"].ToString().Trim(),
                            sattentiontype = (q.Elements.First()["plan_new"].ToString().Contains("FCI")) ? "1" : "2",
                            sauthorization = q.Elements.First()["ingaut"].ToString().Trim(),
                            scie10 = "Z000",
                            sappointment = q.Elements.First()["ingdoc"].ToString(),
                            splan = q.Elements.First()["plan_new"].ToString().Trim(),
                            sratename = q.Elements.First()["tar_nomb"].ToString().Trim(),
                            lservices = new List<ServiceRequest>(),
                            sthird = q.Elements.First()["ingdetnit"].ToString().Trim(),
                            sservicegroup = q.Elements.First()["ingtia"].ToString().Trim(),
                            ientry = Convert.ToInt32(q.Elements.First()["ingdoc"]),
                        };
                        (from DataRow datarow in dataTable.Rows
                         where datarow["ingdetdoc"].ToString() == q.Key.ToString()
                         group datarow by new { servicio = datarow["ingdetcod"], linea = datarow["ingdetlin"] }  into a
                         select new
                         {
                             Key = a.Key,
                             Elements = a,
                         }).ToList().ForEach(a =>
                         {
                             sufu = inspiraCita.sunit.Equals("1200") ? "3" : "1";
                             servintePackage = this.FindConcept(lservintePackages, a.Elements.First()["ingdetcod"].ToString(), a.Elements.First()["tar_new"].ToString(), sufu);
                             serviceRequest = new ServiceRequest()
                             {
                                 sauthorization = a.Elements.First()["ingaut"].ToString().Trim(),
                                 sconcept = (servintePackage != null) ? servintePackage.sconcept : a.Elements.First()["con_new"].ToString(),
                                 scostcenter = (servintePackage != null) ? servintePackage.scostcenter : a.Elements.First()["ccosto"].ToString(),
                                 srate = a.Elements.First()["tar_new"].ToString().Trim(),
                                 sservice = a.Elements.First()["cod_new"].ToString().Trim(),
                                 iqty =  Convert.ToInt32(a.Elements.First()["ingdetcan"]),
                                 ivalue = Convert.ToInt32(a.Elements.First()["ingdetvun"]),
                                 idiscount = Convert.ToDecimal(a.Elements.First()["ingdetdes"]),
                                 bisprocedure = !a.Elements.First()["pronom"].ToString().ToUpper().Contains("CONSULTA"),
                                 sservicename = a.Elements.First()["pronom"].ToString().Trim(),
                                 bbilleable = (a.Elements.First()["ingdetfac"].ToString() == "S"),                                 
                             };
                             inspiraCita.lservices.Add(serviceRequest);
                         });
                        servintePatient.lappointments.Add(inspiraCita);
                     });
                    lservintePatients.Add(servintePatient);
                });
                return lservintePatients;
            }
            catch (Exception ex)
            {
                EventLog.LogError.WriteError("Migracion", "DAC", ex);
                throw;
            }
            finally
            {
                sQLServer.Dispose();
                sQLServer = null;
                dataTable.Dispose();
                dataTable = null;
                servintePatient = null;
                inspiraCita = null;
                serviceRequest = null;
            }
        }

        #endregion

        /// <summary>
        /// Dispose de object
        /// </summary>
        public void Dispose()
        {
            if (this.oSQL != null)
            {
                this.oSQL.Dispose();
                this.oSQL = null;
            }
            if (this.lPatients != null)
            {
                this.lPatients = null;
            }            
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
