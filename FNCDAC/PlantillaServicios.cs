using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Data.SqlClient;

namespace FNCDAC
{
    public class PlantillaServicios
    {        
        /// <summary>
        /// String cadena de conexión a la base de datos
        /// </summary>
        public string sconnection {  get; set; }
        
        /// <summary>
        /// Método que obtiene la información de la BD de FNCStats con los servicios generados en la FNC hacia Sanitas
        /// </summary>
        /// <param name="sinitialdate">String fecha inicial de la consulta</param>
        /// <param name="sfinaldate">String fecha final de la consulta</param>
        /// <returns>DataTable con la información generada por la consulta</returns>
        public DataTable GetSanitasServiceTemplate(string sinitialdate, string sfinaldate)
        {
            //Se crea la consulta a la información. VServiciosSanitas es una vista de base de datos. Para más información acerca de la consulta de la vista dirigirse a FNCStats en HEIMDALL
            string squery = "SELECT * FROM [VServiciosSanitas] WHERE FechaCreacion BETWEEN @initialdate AND @finaldate";
            //Se realiza la conexión a la base de datos
            using (SQLServer sQLServer = new SQLServer(this.sconnection))
            {
                //Se asignan los parámetros a enviar a la consulta
                List<SqlParameter> parameters = new List<SqlParameter>();
                parameters.Add(new SqlParameter("@initialdate", sinitialdate));
                parameters.Add(new SqlParameter("@finaldate", sfinaldate));
                //Se retorna el DataTable con la información
                return sQLServer.GetDataTable(squery, parameters);
            }
        }
    }
}
