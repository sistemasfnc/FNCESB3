using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using Oracle.ManagedDataAccess.Client;
using EventLog;
using FNCEntity;
using System.Diagnostics.Contracts;
using System.Security.Policy;
using System.Data.SqlClient;
using System.Runtime.Remoting.Messaging;

namespace FNCDAC
{
    public class ServinteInspira : IDisposable
    {
        public string sconnection { get; set; }
           
        public DataTable _ObtenerInspiraTemporal()
        {
            string squery = "SELECT * FROM INSPIRATEMPORAL WHERE IT_SINCRONIZADO = 0 AND IT_EDICION = 0";            
            using (Oracle oDAC = new Oracle()) 
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                return oDAC.GetDataTable(squery, null);
            }
        }

        public void ActualizaEstadoInspiraTemporal(string stabla)
        {
            StringBuilder stringBuilder = new StringBuilder("SELECT ST_PARAMETRO1 FROM SINCRONIZATEMPORAL WHERE ST_TABLA = :ST_TABLA");
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            oracleParameters.Add(new OracleParameter("ST_TABLA", stabla));
            DataTable dataTable = new DataTable();
            Oracle oDAC = new Oracle();
            try
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                dataTable = oDAC.GetDataTable(stringBuilder.ToString(), oracleParameters, false, true);
                oracleParameters.Clear();
                stringBuilder.Clear();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    stringBuilder.Append("UPDATE INSPIRATEMPORAL SET IT_SINCRONIZADO = 1 WHERE IT_TABLA = :IT_TABLA AND IT_COD = :IT_COD");
                    oracleParameters.Add(new OracleParameter("IT_COD", dataRow["ST_PARAMETRO1"]));
                    if (stabla == "Produto")
                    {
                        oracleParameters.Add(new OracleParameter("IT_TABLA", "SERVICIO"));
                    }
                    else if (stabla == "Concepto")
                    {
                        oracleParameters.Add(new OracleParameter("IT_TABLA", "CONCEPTO"));
                    }
                    else if (stabla == "Plan")
                    {
                        oracleParameters.Add(new OracleParameter("IT_TABLA", "PLAN"));
                    }
                    else if (stabla == "Tarifas")
                    {
                        oracleParameters.Add(new OracleParameter("IT_TABLA", "TARIFA"));
                    }
                    else if (stabla == "Convenios")
                    {
                        oracleParameters.Add(new OracleParameter("IT_TABLA", "CONVENIO"));
                    }
                    else if (stabla == "Unidad Funcional")
                    {
                        oracleParameters.Add(new OracleParameter("IT_TABLA", "UNIDAD"));
                    }
                    else if (stabla == "Centros de Costo")
                    {
                        oracleParameters.Add(new OracleParameter("IT_TABLA", "CENTROCOSTO"));
                    }
                    oDAC.ExecuteNonQuery(stringBuilder.ToString(), oracleParameters, false, true);
                    stringBuilder.Clear();
                    oracleParameters.Clear();
                }
                oDAC.Commit();
            }
            catch (Exception)
            {
                if (oDAC.oracleConnection.State == ConnectionState.Open)
                {
                    oDAC.RollBack();
                }
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                oDAC.Dispose();
                oDAC = null;
                oracleParameters = null;
                stringBuilder = null;
            }            
        }

        public void ActualizaRelacionesTemporal(string stabla)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string squery = "SELECT DISTINCT * FROM SINCRONIZATEMPORAL WHERE ST_TABLA = :ST_TABLA";
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            oracleParameters.Add(new OracleParameter("ST_TABLA", stabla));
            DataTable dt = new DataTable();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                dt = oDAC.GetDataTable(squery, oracleParameters);
            }
            oracleParameters.Clear();
            Oracle oracle = new Oracle();
            try
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                foreach (DataRow dataRow in dt.Rows)
                {
                    if (stabla == "Centro de costo por unidad funcional")
                    {
                        stringBuilder.Append("UPDATE CENTROPORUNIDAD SET CU_SINCRONIZADO = 1 WHERE CU_UNIDAD = :ST_PARAMETRO2 AND CU_CENTRO = :ST_PARAMETRO1");
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO2", dataRow["ST_PARAMETRO2"]));
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO1", dataRow["ST_PARAMETRO1"]));
                    }
                    else if (stabla == "Tarifas por empresa")
                    {
                        stringBuilder.Append("UPDATE TARIFAPOREMPRESA SET TE_SINCRONIZADO = 1 WHERE TE_TARIFA = :ST_PARAMETRO1 AND TE_EMPRESA = :ST_PARAMETRO2 AND TE_PLAN = :ST_PARAMETRO3");
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO2", dataRow["ST_PARAMETRO2"]));
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO1", dataRow["ST_PARAMETRO1"]));
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO3", dataRow["ST_PARAMETRO3"]));
                    }
                    else if (stabla == "Productos por tarifa")
                    {
                        stringBuilder.Append("UPDATE TARIFAPORPRODUCTO SET TP_SINCRONIZADO = 1 WHERE TP_TARIFA = :ST_PARAMETRO1 AND TP_CONCEPTO = :ST_PARAMETRO2");
                        stringBuilder.Append(" AND TP_CENTROCOSTOS = :ST_PARAMETRO3 AND TP_PRODUCTO = :ST_PARAMETRO4");
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO2", dataRow["ST_PARAMETRO2"]));
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO1", dataRow["ST_PARAMETRO1"]));
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO3", dataRow["ST_PARAMETRO3"]));
                        oracleParameters.Add(new OracleParameter("ST_PARAMETRO4", dataRow["ST_PARAMETRO4"]));
                    }
                    oracle.ExecuteNonQuery(stringBuilder.ToString(), oracleParameters, false, true);
                    oracleParameters.Clear();
                    stringBuilder.Clear();
                }
                oracle.Commit();
            }
            catch (Exception ex)
            {
                if (oracle.oracleConnection.State == ConnectionState.Open)
                {
                    oracle.RollBack();
                }
                throw ex;
            }
            finally
            {
                oracle.Dispose();
                oracle = null;
                oracleParameters = null;
                stringBuilder = null;
                dt.Dispose();
                dt = null;
            }                                                
        }

        public void ActualizaEstadoInspiraTemporal(string sid, int iestado)
        {
            if (!string.IsNullOrEmpty(sid))
            {
                StringBuilder stringBuilder = new StringBuilder("UPDATE INSPIRATEMPORAL SET IT_SINCRONIZADO = :SINCRONIZADO WHERE IT_CONSECUTIVO IN (");
                stringBuilder.Append(sid);
                stringBuilder.Append(")");
                List<OracleParameter> lParameters = new List<OracleParameter>();
                using (Oracle oDAC = new Oracle())
                {
                    oDAC.sConnection = this.sconnection;
                    oDAC.Connect();
                    //oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                    lParameters.Add(new OracleParameter("SINCRONIZADO", iestado));
                    oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters);
                    //oDAC.Commit();                
                    lParameters = null;
                    stringBuilder = null;
                }
            }            
        }

        /// <summary>
        /// Método para actualizar la tabla el estado de sincronización intermedia de integración de conceptos, tarifas y productos
        /// </summary>
        /// <param name="squery">String query con los id a actualizar</param>
        /// <param name="iestado">Entero estado de la sincronización</param>
        public void ActualizaEstadoTarifaProducto(string squery, int iestado)
        {
            if (!string.IsNullOrEmpty(squery))
            {
                StringBuilder stringBuilder = new StringBuilder("UPDATE TARIFAPORPRODUCTO SET TP_SINCRONIZADO = :SINCRONIZADO WHERE TP_ID IN (");
                stringBuilder.Append(squery);
                stringBuilder.Append(")");
                List<OracleParameter> lParameters = new List<OracleParameter>();
                using (Oracle oDAC = new Oracle())
                {
                    oDAC.sConnection = this.sconnection;
                    oDAC.Connect();
                    //oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                    lParameters.Add(new OracleParameter("SINCRONIZADO", iestado));
                    oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters);
                    //oDAC.Commit();                
                    lParameters = null;
                    stringBuilder = null;
                }
            }            
        }

        /// <summary>
        /// Método para actualizar el estado de sincronización en la tabla tarifas por empresa
        /// </summary>
        /// <param name="iestado">Entero estado de sincronización</param>
        public void ActualizaEstadoTarifaConvenio(int iestado)
        {
            string squery = "UPDATE TARIFAPOREMPRESA SET TE_SINCRONIZADO = :SINCRONIZADO WHERE TE_SINCRONIZADO = 0";
            List<OracleParameter> lParameters = new List<OracleParameter>();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                lParameters.Add(new OracleParameter("SINCRONIZADO", iestado));
                oDAC.ExecuteNonQuery(squery, lParameters);
                lParameters = null;
            }
        }

        /// <summary>
        /// Método para actualizar la tabla el estado de sincronización intermedia de integración de centros de costo por unidad funcional
        /// </summary>
        /// <param name="squery">String query con los id a actualizar</param>
        /// <param name="iestado">Entero estado de la sincronización</param>
        public void ActualizaCentroUnidad(string squery, int iestado)
        {
            if (!string.IsNullOrEmpty(squery))
            {
                StringBuilder stringBuilder = new StringBuilder("UPDATE CENTROPORUNIDAD SET CU_SINCRONIZADO = :SINCRONIZADO WHERE CU_ID IN (");
                stringBuilder.Append(squery);
                stringBuilder.Append(")");
                List<OracleParameter> lParameters = new List<OracleParameter>();
                using (Oracle oDAC = new Oracle())
                {
                    oDAC.sConnection = this.sconnection;
                    oDAC.Connect();
                    //oDAC.oracleTransaction = oDAC.oracleConnection.BeginTransaction();
                    lParameters.Add(new OracleParameter("SINCRONIZADO", iestado));
                    oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters);
                    //oDAC.Commit();                
                    lParameters = null;
                    stringBuilder = null;
                }
            }            
        }

        public void ActualizaDescuentoTarifa(string squery, int iestado)
        {
            if (!string.IsNullOrEmpty(squery))
            {
                StringBuilder stringBuilder = new StringBuilder("UPDATE DESCUENTOTARIFA SET DT_SINCRONIZADO = :SINCRONIZADO WHERE DT_ID IN (");
                stringBuilder.Append(squery);
                stringBuilder.Append(")");
                List<OracleParameter> lParameters = new List<OracleParameter>();
                using (Oracle oDAC = new Oracle())
                {
                    oDAC.sConnection = this.sconnection;
                    oDAC.Connect();
                    lParameters.Add(new OracleParameter("SINCRONIZADO", iestado));
                    oDAC.ExecuteNonQuery(stringBuilder.ToString(), lParameters);
                    lParameters = null;
                    stringBuilder = null;
                }
            }
        }


        public DataTable ObtenerTarifasServicios()
        {
            string squery = "SELECT * FROM TARIFAPORPRODUCTO WHERE TP_SINCRONIZADO = 0";
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                return oDAC.GetDataTable(squery, null);
            }
        }

        public DataTable ObtenerUnidadFuncionalCentro()
        {
            string squery = "SELECT * FROM CENTROPORUNIDAD WHERE CU_SINCRONIZADO = 0";
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                return oDAC.GetDataTable(squery, null);
            }
        }

        public DataTable ObtenerTarifaEmpresa()
        {
            string squery = "SELECT * FROM TARIFAPOREMPRESA WHERE TE_SINCRONIZADO = 0";
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                return oDAC.GetDataTable(squery, null);
            }
        }

        public DataTable ObtenerDescuentoTarifa()
        {
            string squery = "SELECT * FROM DESCUENTOTARIFA WHERE DT_SINCRONIZADO = 0";
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = this.sconnection;
                oDAC.Connect();
                return oDAC.GetDataTable(squery, null);
            }
        }

        /// <summary>
        /// Método para crear los registros en SQL Server base de datos Inspira Alejus que lleva la integración
        /// </summary>
        /// <param name="stable"></param>
        /// <param name="inspiraTemporal"></param>
        public void CreaRegistrosMaestros(string stable, InspiraTemporal inspiraTemporal)
        {
            StringBuilder stringBuilder = new StringBuilder();
            List<SqlParameter> paramList = new List<SqlParameter>();    
            string snombre = string.Empty;
            using (SQLServer sqlServer = new SQLServer(this.sconnection))
            {
                snombre = (string.IsNullOrEmpty(inspiraTemporal.snombre)) ? string.Empty : inspiraTemporal.snombre;
                stringBuilder.Append("INSERT INTO ");
                stringBuilder.Append(stable);
                if (stable == "Product__c")
                {
                    stringBuilder.Append(" (Id, Name, Name__c) VALUES (@Id, @Name, @Name__c)");
                    paramList.Add(new SqlParameter("@Id", inspiraTemporal.sid));
                    paramList.Add(new SqlParameter("@Name", inspiraTemporal.scod));
                    paramList.Add(new SqlParameter("@Name__c", snombre));
                }
                else
                {
                    stringBuilder.Append(" (Id, Code__c, Name)");
                    stringBuilder.Append(" VALUES (@Id, @Code, @Name)");
                    paramList.Add(new SqlParameter("@Id", inspiraTemporal.sid));
                    paramList.Add(new SqlParameter("@Code", inspiraTemporal.scod));
                    paramList.Add(new SqlParameter("@Name", snombre));
                }                
                sqlServer.ExecuteNonQuery(stringBuilder.ToString(), paramList);
            }
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
