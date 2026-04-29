using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.Data;
using EventLog;

namespace FNCDAC
{
    public class EnvioHistorias : IDisposable
    {
        public string sConnection { get; set; }

        public void InsertSuccess(DataTable dt)
        {
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                oDAC.BulkData("archivoenviado", dt);
            }
        }

        public void InsertErrors(DataTable dt)
        {
            using (SQLServer oDAC = new SQLServer(this.sConnection))
            {
                oDAC.BulkData("errorenvio", dt);
            }
        }

        public List<string> GetSuccessData()
        {
            string squery = "SELECT archivo FROM archivoenviado WHERE fecha = CAST(GETDATE() AS DATE)";
            List<string> lData = new List<string>();
            SQLServer oDAC = new SQLServer(this.sConnection);
            DataTable dt = new DataTable();
            try
            {
                dt = oDAC.GetDataTable(squery, null);
                foreach (DataRow dr in dt.Rows)
                {
                    lData.Add(dr["archivo"].ToString());
                }
                return lData;
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "DAC", ex);
                throw;
            }
            finally
            {
                oDAC.Dispose();
                oDAC = null;
                dt.Dispose();
                dt = null;
            }
            
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
