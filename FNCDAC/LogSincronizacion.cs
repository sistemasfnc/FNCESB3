using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEntity;
using Oracle.ManagedDataAccess.Client;

namespace FNCDAC
{
    /// <summary>
    /// Objeto para almacenar el registro de transacciones de la sincronización
    /// </summary>
    public static class LogSincronizacion
    {
        /// <summary>
        /// Método que ingresa un registro en el log de sincronizaciones
        /// </summary>
        /// <param name="logSincroniza">Objeto log de sincronización</param>
        /// <param name="sconexion">String cadena de conexión a la base de datos</param>
        public static void CreateLog(LogSincroniza logSincroniza, string sconexion)
        {
            string squery = "INSERT INTO LOGSINCRONIZACION (LS_COD, LS_MENSAJE, LS_TABLA) VALUES (:codigo, :mensaje, :tabla)";
            List<OracleParameter> lParametros = new List<OracleParameter>();
            using (Oracle oDAC = new Oracle())
            {                
                oDAC.sConnection = sconexion;
                oDAC.Connect();
                lParametros.Add(new OracleParameter(":codigo", logSincroniza.scodigo));
                lParametros.Add(new OracleParameter(":mensaje", logSincroniza.smensaje));
                lParametros.Add(new OracleParameter(":tabla", logSincroniza.stabla));
                oDAC.ExecuteNonQuery(squery, lParametros);
            }
        }

        public static void CreateSyncTmp(List<SincronizaTemporal> lsincronizaTemporals, string sconnection)
        {
            StringBuilder squery = new StringBuilder("INSERT INTO SINCRONIZATEMPORAL (ST_TABLA, ST_PARAMETRO1, ST_PARAMETRO2, ST_PARAMETRO3, ST_PARAMETRO4)");
            squery.Append("  VALUES (:ST_TABLA, :ST_PARAMETRO1, :ST_PARAMETRO2, :ST_PARAMETRO3, :ST_PARAMETRO4)");
            List<OracleParameter> oracleParameters = new List<OracleParameter>();
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = sconnection;
                oDAC.Connect();
                foreach (var item in lsincronizaTemporals)
                {
                    oracleParameters.Add(new OracleParameter("ST_TABLA", item.stable));
                    oracleParameters.Add(new OracleParameter("ST_PARAMETRO1", item.sparameter1));
                    oracleParameters.Add(new OracleParameter("ST_PARAMETRO2", item.sparameter2));
                    oracleParameters.Add(new OracleParameter("ST_PARAMETRO3", item.sparameter3));
                    oracleParameters.Add(new OracleParameter("ST_PARAMETRO4", item.sparameter4));
                    oDAC.ExecuteNonQuery(squery.ToString(), oracleParameters);
                    oracleParameters.Clear();
                }
            }
        }

        public static void TruncateSyncTmp(string sconnection)
        {
            using (Oracle oDAC = new Oracle())
            {
                oDAC.sConnection = sconnection;
                oDAC.Connect();
                oDAC.ExecuteNonQuery("TRUNCATE TABLE SINCRONIZATEMPORAL", null);
            }
        }
    }
}
