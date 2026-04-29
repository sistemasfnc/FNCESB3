using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using EventLog;
using System.Data;

namespace FNCDAC
{
    /// <summary>
    /// Clase para manejar la base de datos
    /// </summary>
    public class Oracle : IDisposable
    {
        /// <summary>
        /// Propidad conexión a la base de datos
        /// </summary>
        public OracleConnection oracleConnection { get; set; }

        /// <summary>
        /// Propiedad cadena de conexión a la base de datos
        /// </summary>
        public string sConnection { get; set; }

        public OracleTransaction oracleTransaction { get; set; }

        public Oracle()
        {

        }

        /// <summary>
        /// Método que abre la conexión con la base de datos
        /// </summary>
        public void Connect()
        {            
            try
            {
                this.oracleConnection = new OracleConnection(this.sConnection);
                this.oracleConnection.Open();
            }
            catch (OracleException ex)
            {
                LogError.WriteError("Integrador", "DAC", ex);
                throw;
            }
        }

        /// <summary>
        /// Método que ejecuta una consulta SQL
        /// </summary>
        /// <param name="sCommandText">String consulta</param>
        /// <param name="lparameters">Generic List parámetros de la consulta</param>
        /// <param name="bIsStoreProcedure">Boolean consulta es un procedimiento almacenado</param>
        /// <param name="bIsTransaction">Boolean consulta es una transacción</param>
        public void ExecuteNonQuery(string sCommandText, List<OracleParameter> lparameters, bool bIsStoreProcedure = false, bool bIsTransaction = false)
        {            
            OracleCommand oracleCommand = null;
            try
            {
                if (this.oracleConnection.State == ConnectionState.Open)
                {
                    oracleCommand = new OracleCommand(sCommandText, this.oracleConnection);
                    oracleCommand.BindByName = true;
                    if (bIsTransaction)
                    {
                        oracleCommand.Transaction = this.oracleTransaction;
                    }
                    if (bIsStoreProcedure)
                    {
                        oracleCommand.CommandType = CommandType.StoredProcedure;
                    }
                    if (lparameters != null)
                    {
                        foreach (OracleParameter parameter in lparameters)
                        {
                            oracleCommand.Parameters.Add(parameter);
                        }
                    }
                    oracleCommand.ExecuteNonQuery();
                }
            }
            catch (OracleException ex)
            {
                LogError.WriteError("Integrador", "DAC", ex);
                throw;
            }
            finally
            {
                if (oracleCommand != null)
                {
                    oracleCommand.Parameters.Clear();
                    oracleCommand.Dispose();
                    oracleCommand = null;
                }
            }
        }

        /// <summary>
        /// Método que ejecuta una consulta de selección en la base de datos
        /// </summary>
        /// <param name="sCommandText"></param>
        /// <param name="lparameters"></param>
        /// <param name="bIsStoreProcedure"></param>
        /// <param name="bIsTransaction"></param>
        /// <returns></returns>
        public DataTable GetDataTable(string sCommandText, List<OracleParameter> lparameters, bool bIsStoreProcedure = false, bool bIsTransaction = false)
        {
            DataSet dataSet = new DataSet();
            OracleDataAdapter oracleDataAdapter = null;
            try
            {
                if (this.oracleConnection.State == ConnectionState.Open)
                {
                    oracleDataAdapter = new OracleDataAdapter(sCommandText, this.oracleConnection);
                    if (bIsTransaction)
                    {
                        oracleDataAdapter.SelectCommand.Transaction = this.oracleTransaction;
                    }
                    if (bIsStoreProcedure)
                    {
                        oracleDataAdapter.SelectCommand.CommandType = CommandType.StoredProcedure;
                    }
                    if (lparameters != null)
                    {
                        for (int i = 0; i < lparameters.Count; i++)
                        {
                            oracleDataAdapter.SelectCommand.Parameters.Add(lparameters[i]);
                        }
                    }
                    oracleDataAdapter.Fill(dataSet);
                    return dataSet.Tables[0];
                }
                return new DataTable();
            }
            catch (OracleException ex)
            {
                LogError.WriteError("Integrador", "DAC", ex);
                throw;
            }
            finally
            {
                if (oracleDataAdapter != null)
                {
                    oracleDataAdapter.SelectCommand.Parameters.Clear();
                    oracleDataAdapter.Dispose();
                    oracleDataAdapter = null;
                }
            }
        }

        /// <summary>
        /// Método para obtener un valor escalar de una consulta
        /// </summary>
        /// <param name="sCommandText">String consulta</param>
        /// <param name="lparameters">Generic List parámetros de la consulta</param>
        /// <param name="bIsTransaction">Boolean es o no transacción</param>
        /// <returns>Generic object con el resultado de la consulta</returns>
        public object GetScalar(string sCommandText, List<OracleParameter> lparameters, bool bIsTransaction = false)
        {
            object oscalar = null;
            OracleCommand oracleCommand = null;
            try
            {
                if (this.oracleConnection.State == ConnectionState.Open)
                {
                    oracleCommand = new OracleCommand(sCommandText, this.oracleConnection);
                    if (bIsTransaction)
                    {
                        oracleCommand.Transaction = this.oracleTransaction;
                    }
                    if (lparameters != null)
                    {
                        for (int i = 0; i < lparameters.Count; i++)
                        {
                            oracleCommand.Parameters.Add(lparameters[i]);
                        }
                    }                    
                    oscalar = oracleCommand.ExecuteScalar();                    
                }
                return oscalar;
            }
            catch (OracleException ex)
            {
                LogError.WriteError("Integrador", "DAC", ex);
                throw;
            }
            finally
            {
                if (oracleCommand != null)
                {
                    oracleCommand.Parameters.Clear();
                    oracleCommand.Dispose();
                    oracleCommand = null;
                }
            }
        }

        
        public void Commit()
        {
            this.oracleTransaction.Commit();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RollBack()
        {
            this.oracleTransaction.Rollback();
        }


        public void Dispose()
        {
            if (this.oracleConnection != null)
            {
                if (this.oracleConnection.State == ConnectionState.Open)
                {
                    this.oracleConnection.Close();
                }
                this.oracleConnection.Dispose();
                this.oracleConnection = null;
            }            
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
