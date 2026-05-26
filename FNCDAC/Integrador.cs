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
using Oracle.ManagedDataAccess.Types;
using System.Security.Policy;

namespace FNCDAC
{
    public class Integrador : IDisposable
    {

        /// <summary>
        /// Cadena de caracteres conexión a la base de datos
        /// </summary>
        public string sconnection { get; set; }

        /// <summary>
        /// Método para ingresar el listado de registros de ingresos y cargos en la tabla de sincronización INSPIRASERVINTE
        /// </summary>
        /// <param name="entryResponses"></param>
        /// <returns></returns>
        public List<EntryResponse> InsertRecord(List<EntryResponse> entryResponses)
        {
            List<OracleParameter> lParameters = null;
            OracleParameter oracleParameter = null;
            StringBuilder sQuery = new StringBuilder();
            Oracle oracle = new Oracle();
            oracle.sConnection = this.sconnection;            
            try
            {
                oracle.Connect();
                oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                for (int i = 0; i < entryResponses.Count; i++)
                {
                    if (entryResponses[i].iupload == 1)
                    {
                        lParameters = new List<OracleParameter>();
                        if (entryResponses[i].iid == 0)
                        {
                            oracleParameter = new OracleParameter("IS_ID", OracleDbType.Int32, ParameterDirection.Output);
                            sQuery.Append("INSERT INTO INSPIRASERVINTE (IS_AUTORIZACION, IS_CANTIDAD, IS_FECHA, IS_TIPODOCUMENTO, IS_DOCUMENTO, IS_TARIFA, IS_EMPRESA");
                            sQuery.Append(", IS_INGRESO, IS_CARGO, IS_CITA, IS_SERVICIO, IS_CENTROCOSTO, IS_EVENTO, IS_TIPO, IS_CONCEPTO, IS_NOMBRESERVICIO, IS_VALOR");
                            sQuery.Append(", IS_PACIENTE, IS_CARGOINSPIRA, IS_LINEA, IS_UNIDAD, IS_PLAN, IS_GRUPO) VALUES (:IS_AUTORIZACION, :IS_CANTIDAD, :IS_FECHA, :IS_TIPODOCUMENTO, :IS_DOCUMENTO, :IS_TARIFA, :IS_EMPRESA");
                            sQuery.Append(", :IS_INGRESO, :IS_CARGO, :IS_CITA, :IS_SERVICIO, :IS_CENTROCOSTO, :IS_EVENTO, :IS_TIPO, :IS_CONCEPTO, :IS_NOMBRESERVICIO");
                            sQuery.Append(", :IS_VALOR, :IS_PACIENTE, :IS_CARGOINSPIRA, :IS_LINEA, :IS_UNIDAD, :IS_PLAN, :IS_GRUPO) RETURNING IS_ID INTO :IS_ID");
                            lParameters.Add(new OracleParameter("IS_AUTORIZACION", entryResponses[i].sauthorization));
                            lParameters.Add(new OracleParameter("IS_CANTIDAD", entryResponses[i].iqty));
                            lParameters.Add(new OracleParameter("IS_FECHA", Convert.ToDateTime(entryResponses[i].ddate)));
                            lParameters.Add(new OracleParameter("IS_TIPODOCUMENTO", Tools.GetDocumentType(entryResponses[i].sdocumenttype, true)));
                            lParameters.Add(new OracleParameter("IS_DOCUMENTO", entryResponses[i].sdocument));
                            lParameters.Add(new OracleParameter("IS_TARIFA", entryResponses[i].srate));
                            lParameters.Add(new OracleParameter("IS_EMPRESA", entryResponses[i].sagreement));
                            lParameters.Add(new OracleParameter("IS_INGRESO", entryResponses[i].ientry));
                            lParameters.Add(new OracleParameter("IS_CARGO", entryResponses[i].icharge));
                            lParameters.Add(new OracleParameter("IS_CITA", entryResponses[i].sappointment));
                            lParameters.Add(new OracleParameter("IS_SERVICIO", entryResponses[i].sservice));
                            lParameters.Add(new OracleParameter("IS_CENTROCOSTO", entryResponses[i].scostcenter));
                            lParameters.Add(new OracleParameter("IS_EVENTO", entryResponses[i].sevent));
                            lParameters.Add(new OracleParameter("IS_TIPO", entryResponses[i].stype));
                            lParameters.Add(new OracleParameter("IS_CONCEPTO", entryResponses[i].sconcept));
                            lParameters.Add(new OracleParameter("IS_NOMBRESERVICIO", entryResponses[i].sservicename));
                            lParameters.Add(new OracleParameter("IS_VALOR", entryResponses[i].dvalue));
                            lParameters.Add(new OracleParameter("IS_PACIENTE", entryResponses[i].ipatient));
                            lParameters.Add(new OracleParameter("IS_CARGOINSPIRA", entryResponses[i].idCargo));
                            lParameters.Add(new OracleParameter("IS_LINEA", entryResponses[i].iline));
                            lParameters.Add(new OracleParameter("IS_UNIDAD", entryResponses[i].sunit));
                            lParameters.Add(new OracleParameter("IS_PLAN", entryResponses[i].splan));
                            lParameters.Add(new OracleParameter("IS_GRUPO", entryResponses[i].sservicegroup));
                            lParameters.Add(oracleParameter);
                            oracle.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
                            entryResponses[i].iid = Convert.ToInt32((decimal)(OracleDecimal)(oracleParameter.Value));
                            //entryResponses[i].dvalue = 0;
                        }
                        else
                        {
                            sQuery.Append("UPDATE INSPIRASERVINTE SET IS_AUTORIZACION = :IS_AUTORIZACION, IS_CANTIDAD = :IS_CANTIDAD, IS_FECHA = :IS_FECHA, IS_TIPODOCUMENTO = :IS_TIPODOCUMENTO");
                            sQuery.Append(", IS_DOCUMENTO = :IS_DOCUMENTO, IS_TARIFA = :IS_TARIFA, IS_EMPRESA = :IS_EMPRESA, IS_INGRESO = :IS_INGRESO, IS_CARGO = :IS_CARGO, IS_SERVICIO = :IS_SERVICIO");
                            sQuery.Append(", IS_CENTROCOSTO = :IS_CENTROCOSTO, IS_CONCEPTO = :IS_CONCEPTO, IS_VALOR = :IS_VALOR, IS_LINEA = :IS_LINEA, IS_UNIDAD = :IS_UNIDAD, IS_PLAN = :IS_PLAN");
                            sQuery.Append(", IS_PACIENTE = :IS_PACIENTE WHERE IS_ID = :IS_ID");
                            lParameters.Add(new OracleParameter("IS_AUTORIZACION", entryResponses[i].sauthorization));
                            lParameters.Add(new OracleParameter("IS_CANTIDAD", entryResponses[i].iqty));
                            lParameters.Add(new OracleParameter("IS_FECHA", Convert.ToDateTime(entryResponses[i].ddate)));
                            lParameters.Add(new OracleParameter("IS_TIPODOCUMENTO", Tools.GetDocumentType(entryResponses[i].sdocumenttype, true)));
                            lParameters.Add(new OracleParameter("IS_DOCUMENTO", entryResponses[i].sdocument));
                            lParameters.Add(new OracleParameter("IS_TARIFA", entryResponses[i].srate));
                            lParameters.Add(new OracleParameter("IS_EMPRESA", entryResponses[i].sagreement));
                            lParameters.Add(new OracleParameter("IS_INGRESO", entryResponses[i].ientry));
                            lParameters.Add(new OracleParameter("IS_CARGO", entryResponses[i].icharge));
                            lParameters.Add(new OracleParameter("IS_SERVICIO", entryResponses[i].sservice));
                            lParameters.Add(new OracleParameter("IS_CENTROCOSTO", entryResponses[i].scostcenter));
                            lParameters.Add(new OracleParameter("IS_CONCEPTO", entryResponses[i].sconcept));
                            lParameters.Add(new OracleParameter("IS_VALOR", entryResponses[i].dvalue));
                            lParameters.Add(new OracleParameter("IS_PACIENTE", entryResponses[i].ipatient));
                            lParameters.Add(new OracleParameter("IS_LINEA", entryResponses[i].iline));
                            lParameters.Add(new OracleParameter("IS_UNIDAD", entryResponses[i].sunit));
                            lParameters.Add(new OracleParameter("IS_PLAN", entryResponses[i].splan));
                            lParameters.Add(new OracleParameter("IS_ID", entryResponses[i].iid));
                            oracle.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
                            //entryResponses[i].dvalue = 0;
                        }
                        sQuery.Clear();
                    }
                                                                           
                }
                oracle.Commit();
                return entryResponses;
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
                lParameters = null;
                oracleParameter = null;
                sQuery = null;
            }
        }

        /// <summary>
        /// Método que obtiene las información de las tablas intermedias
        /// </summary>
        /// <returns></returns>
        public List<InspiraTemporal> GetInspiraTables()
        {
            StringBuilder stringBuilder = new StringBuilder("SELECT Agreement__c.Id Id, Agreement__c.Code__c Codigo, Agreement__c.Name Nombre, 'Convenio' Tabla FROM Agreement__c");
            stringBuilder.Append(" UNION ALL SELECT HealthCarePlan__c.Id, HealthCarePlan__c.Code__c, HealthCarePlan__c.Name, 'Plan' FROM HealthCarePlan__c");
            stringBuilder.Append(" UNION ALL SELECT Rate__c.Id, Rate__c.Code__c, Rate__c.Name, 'Tarifa' FROM Rate__c");
            stringBuilder.Append(" UNION ALL SELECT Concept__c.Id, Concept__c.Code__c, Concept__c.Name, 'Concepto' FROM Concept__c");
            stringBuilder.Append(" UNION ALL SELECT CostCenter__c.Id, CostCenter__c.Code__c, CostCenter__c.Name, 'CentroCosto' FROM CostCenter__c");
            stringBuilder.Append(" UNION ALL SELECT Product__c.Id, Product__c.Name, Product__c.Name__c, 'Producto' FROM Product__c");
            DataTable dataTable = new DataTable();
            SQLServer sQLServer = new SQLServer(this.sconnection);
            List<InspiraTemporal> linspiraTemporals = new List<InspiraTemporal>();
            InspiraTemporal inspiraTemporal = null;
            try
            {
                dataTable = sQLServer.GetDataTable(stringBuilder.ToString(), null);
                foreach (DataRow item in dataTable.Rows)
                {
                    inspiraTemporal = new InspiraTemporal()
                    {
                        scod = item["Codigo"].ToString(),
                        santerior = item["Id"].ToString(),
                        snombre = item["Nombre"].ToString(),
                        stabla = item["Tabla"].ToString(),
                    };
                    linspiraTemporals.Add(inspiraTemporal);
                }
                return linspiraTemporals;
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                sQLServer.Dispose();
                sQLServer = null;
            }
        }

        public DataTable ObtenerDiferencias()
        {
            string query = @"SELECT r.Id   IdTarifa,c.Id   IdConcepto, cc.Id  IdCentro, pp.Id  IdProducto, CAST(P.PROTARVAL AS INTEGER) Valor
                                FROM (SELECT * FROM OPENQUERY(integrabus, 'SELECT * FROM VTARIFAPRODUCTO')) P LEFT JOIN [InspiraTarifasConceptos] ON  P.PROTARTAR = [RateId__r.Code__c]
                                AND P.PROTARCON = [ConceptId__r.Code__c] AND P.PROTARCCO = [CostCenterId__r.Code__c] AND P.PROTARPRO = [ProductId__r.Name] LEFT JOIN Rate__c r ON r.Code__c  = P.PROTARTAR
                                LEFT JOIN Concept__c c  ON c.Code__c  = P.PROTARCON LEFT JOIN CostCenter__c  cc ON cc.Code__c = P.PROTARCCO LEFT JOIN Product__c pp ON pp.Name = P.PROTARPRO
                                WHERE [RateId__r.Code__c] IS NULL AND PROTARPRO NOT LIKE 'PQ%' AND PROTARCCO NOT IN ('3101', '1101') AND PROTARPRO NOT LIKE 'ED%' AND PROTARTAR NOT IN ('IÑ')
                                AND PROTARPRO NOT IN ('IN0101','891702','401101','218405','252001', '277101','285104','295604','305102','332205','IN099','345202')";
            using (SQLServer db = new SQLServer(this.sconnection))
            {
                // GetDataTable de la clase SQLServer provista; sin parámetros adicionales
                return db.GetDataTable(query, null);
            }
        }

        /// <summary>
        /// Obtiene los registros cuyos valores cambiaron en el ERP respecto a lo que
        /// existe en Salesforce (tabla InspiraTarifasValores ya debe estar cargada).
        /// Retorna Id del registro en Salesforce y el nuevo valor del ERP.
        /// </summary>
        public DataTable ObtenerValoresCambiados()
        {
            string query = @"SELECT DISTINCT InspiraTarifasValores.Id, CAST(P.PROTARVAL AS INTEGER) NuevoValor
                             FROM (SELECT * FROM OPENQUERY(integrabus, 'SELECT * FROM VTARIFAPRODUCTO')) P
                             LEFT JOIN [InspiraTarifasValores]
                                ON  P.PROTARTAR = [RateId__r.Code__c]
                               AND P.PROTARCON = [ConceptId__r.Code__c]
                               AND P.PROTARCCO = [CostCenterId__r.Code__c]
                               AND P.PROTARPRO = [ProductId__r.Name]
                             INNER JOIN Rate__c       r  ON r.Code__c  = P.PROTARTAR
                             INNER JOIN Concept__c    c  ON c.Code__c  = P.PROTARCON
                             INNER JOIN CostCenter__c cc ON cc.Code__c = P.PROTARCCO
                             INNER JOIN Product__c    pp ON pp.Name    = P.PROTARPRO
                             WHERE PROTARPRO NOT LIKE 'PQ%'
                               AND InspiraTarifasValores.Value__c <> P.PROTARVAL";
            using (SQLServer db = new SQLServer(this.sconnection))
            {
                return db.GetDataTable(query, null);
            }
        }

        public void TruncarTabla(string stable)
        {
            string query = $"TRUNCATE TABLE {stable}";
            using (SQLServer db = new SQLServer(this.sconnection))
            {
                // ExecuteNonQuery de la clase SQLServer provista; sin parámetros adicionales
                db.ExecuteNonQuery(query, null);
            }
        }

        public void BulkData(string stable, DataTable dt)
        {
            using (SQLServer db = new SQLServer(this.sconnection))
            {
                db.BulkData(stable, dt);
            }
        }

        public DataTable GetPlansForSupports()
        {
            string squery = "SELECT PLACOD FROM SOPORTEPLAN";
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = sconnection;
                oracle.Connect();
                return oracle.GetDataTable(squery, null);
            }
        }


        /// <summary>
        /// Método para destruir y liberar el objeto
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }               
    }
}
