using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;
using EventLog;

namespace FNCDAC
{
    public class SQLServer : IDisposable
    {
        private bool IsConnected { get; set; }

        private string ConnectionString { get; set; }

        public SqlConnection oConnection { get; set; }

        public SqlTransaction dbTrans { get; set; }

        public SQLServer(string sConnection = "")
        {
            this.ConnectionString =  sConnection;
        }

        public bool Connect()
        {            
            try
            {
                Console.WriteLine("Abriendo conexión...");
                this.oConnection = new SqlConnection(this.ConnectionString);
                this.oConnection.Open();
                return true;
            }
            catch (SqlException ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                return false;
            }            
        }

        public int ExecuteNonQuery(string CommandText, List<SqlParameter> parameters, bool IsStoreProcedure = false)
        {
            int Response = 0;
            SqlCommand sc = null;
            try
            {
                if (this.Connect())
                {
                    sc = new SqlCommand(CommandText, this.oConnection);
                    if (IsStoreProcedure) sc.CommandType = CommandType.StoredProcedure;
                    if (parameters != null)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            sc.Parameters.Add(parameters[i]);
                        }
                    }
                    Response = sc.ExecuteNonQuery();
                }
            }
            catch (SqlException ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                sc.Parameters.Clear();
                sc.Dispose();
                sc = null;
            }
            return Response;
        }

        public void ExecuteNonQuery(string CommandText, List<SqlParameter> parameters, bool HasTransaction, bool IsStoreProcedure = false)
        {
            SqlCommand sc = null;
            try
            {
                sc = new SqlCommand(CommandText, this.oConnection);
                sc.Transaction = this.dbTrans;
                if (IsStoreProcedure) sc.CommandType = CommandType.StoredProcedure;
                if (parameters != null)
                {
                    foreach (SqlParameter parameter in parameters)
                    {
                        sc.Parameters.Add(parameter);
                    }
                }
                sc.ExecuteNonQuery();
            }
            catch (SqlException ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                if (sc != null)
                {
                    sc.Parameters.Clear();
                    sc.Dispose();
                    sc = null;
                }                
            }
        }

        public DataTable GetDataTable(string CommandText, List<SqlParameter> parameters, bool type = false, bool isTransaction = false)
        {
            DataSet ds = new DataSet();
            SqlDataAdapter da = null;
            try
            {
                if (!isTransaction)
                {
                    if (this.Connect())
                    {
                        Console.WriteLine("Obteniendo datos");
                        da = new SqlDataAdapter(CommandText, this.oConnection);
                        da.SelectCommand.CommandTimeout = 600;
                        if (type) da.SelectCommand.CommandType = CommandType.StoredProcedure;
                        if (parameters != null)
                        {
                            for (int i = 0; i < parameters.Count; i++)
                            {
                                da.SelectCommand.Parameters.Add(parameters[i]);
                            }
                        }

                    }
                }
                else
                {
                    da = new SqlDataAdapter(CommandText, this.oConnection);
                    da.SelectCommand.Transaction = this.dbTrans;
                    da.SelectCommand.CommandTimeout = 600;
                    if (type) da.SelectCommand.CommandType = CommandType.StoredProcedure;
                    if (parameters != null)
                    {
                        for (int i = 0; i < parameters.Count; i++)
                        {
                            da.SelectCommand.Parameters.Add(parameters[i]);
                        }
                    }
                }                
                da.Fill(ds);
            }
            catch (SqlException ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                if (da != null)
                {
                    da.SelectCommand.Parameters.Clear();
                    da.Dispose();
                    da = null;
                }                
            }
            return ds.Tables[0];
        }

        public object GetScalar(string CommandText, List<SqlParameter> parameters)
        {
            object Scalar = null;
            SqlCommand sc = null;
            try
            {
                if (this.Connect())
                {
                    sc = new SqlCommand(CommandText, this.oConnection);
                    for (int i = 0; i < parameters.Count; i++)
                    {
                        sc.Parameters.Add(parameters[i]);
                    }
                }
                Scalar = sc.ExecuteScalar();
                return Scalar;
            }
            catch (SqlException ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                if (sc != null)
                {
                    sc.Parameters.Clear();
                    sc.Dispose();
                    sc = null;
                }                
            }
        }

        public object GetScalar(string CommandText, List<SqlParameter> parameters, bool trans)
        {
            object Scalar = null;
            SqlCommand sc = null;
            try
            {
                sc = new SqlCommand(CommandText, this.oConnection);
                sc.Transaction = dbTrans;
                for (int i = 0; i < parameters.Count; i++)
                {
                    sc.Parameters.Add(parameters[i]);
                }
                Scalar = sc.ExecuteScalar();
                return Scalar;
            }
            catch (SqlException ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                if (sc != null)
                {
                    sc.Parameters.Clear();
                    sc.Dispose();
                    sc = null;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Commit()
        {
            this.dbTrans.Commit();
        }

        /// <summary>
        /// 
        /// </summary>
        public void RollBack()
        {
            this.dbTrans.Rollback();
        }

        private void CloseConnection()
        {
            if (this.oConnection.State == ConnectionState.Open)
            {
                this.oConnection.Close();
                this.IsConnected = false;
            }
        }

        public void BulkData(string sTable, DataTable dt)
        {
            SqlBulkCopy oBulk = new SqlBulkCopy(this.ConnectionString);
            try
            {
                if (this.Connect())
                {
                    oBulk.DestinationTableName = sTable;
                    oBulk.WriteToServer(dt);
                }
            }
            catch (SqlException ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                oBulk.Close();
                oBulk = null;
            }
        }

        public void Dispose()
        {
            this.CloseConnection();
            this.oConnection.Dispose();
            this.oConnection = null;
            this.IsConnected = false;
            GC.SuppressFinalize(this);
            GC.Collect();
        }

    }
}
