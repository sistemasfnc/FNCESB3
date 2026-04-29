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
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Globalization;

namespace FNCDAC
{
    public class Statistic : IDisposable
    {
        public string sconnection { get; set; }

        public Statistic()
        {

        }

        /// <summary>
        /// Método para obtejer los ingreos facturados del día
        /// </summary>
        /// <returns>DataTable con el ingreso y el número de la factura</returns>
        public DataTable GetInvoicedEntries()
        {
            string squery = "SELECT * FROM VCARGOSFACTURADOSHOY";
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                return oracle.GetDataTable(squery, null);
            }
        }

        public DataTable GetProgramsEntries(EntryExtended entryExtended)
        {
            StringBuilder stringBuilder = new StringBuilder("SELECT * FROM VCARGOSPROGRAMASSINFACTURA WHERE 1 = 1");
            List<OracleParameter> lparameters = new List<OracleParameter>();
            if (entryExtended.dinitial.HasValue)
            {
                stringBuilder.Append(" AND TO_DATE(TO_CHAR(IS_FECHA, 'YYYY-MM-DD'), 'YYYY-MM-DD') >= TO_DATE(:INICIO, 'YYYY-MM-DD')");
                lparameters.Add(new OracleParameter(":INICIO", entryExtended.dinitial.Value.ToString("yyyy-MM-dd")));

            }
            if (entryExtended.dfinal.HasValue)
            {
                stringBuilder.Append(" AND TO_DATE(TO_CHAR(IS_FECHA, 'YYYY-MM-DD'), 'YYYY-MM-DD') <= TO_DATE(:FIN, 'YYYY-MM-DD')");
                lparameters.Add(new OracleParameter(":FIN", entryExtended.dfinal.Value.ToString("yyyy-MM-dd")));

            }
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(stringBuilder.ToString(), lparameters);
            }
        }

        public void UpdateEntryInvoices(List<EntryExtended> lentryExtendeds)
        {
            List<OracleParameter> lParameters = null;
            StringBuilder sQuery = new StringBuilder("UPDATE INSPIRASERVINTE SET IS_FACTURA = :IS_FACTURA WHERE IS_INGRESO = :IS_INGRESO");
            Oracle oracle = new Oracle();
            try
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                oracle.oracleTransaction = oracle.oracleConnection.BeginTransaction();
                foreach (EntryExtended item in lentryExtendeds)
                {
                    lParameters.Add(new OracleParameter("IS_FACTURA", item.iinvoice));
                    lParameters.Add(new OracleParameter("IS_INGRESO", item.ientry));
                    oracle.ExecuteNonQuery(sQuery.ToString(), lParameters, false, true);
                    lParameters.RemoveRange(0, lParameters.Count);
                }
                oracle.Commit();
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
                sQuery = null;

            }
        }

        public DataTable GetDataForDate(int iyear, int imonth)
        {
            StringBuilder stringBuilder = new StringBuilder("SELECT * FROM INSPIRASERVINTE WHERE IS_TIPO LIKE '%solo estadistica'");
            stringBuilder.Append(" AND EXTRACT(YEAR FROM IS_FECHA) = :YEAR AND EXTRACT(MONTH FROM IS_FECHA) = :MONTH");
            List<OracleParameter> lparameters = new List<OracleParameter>();
            using (Oracle oracle = new Oracle())
            {

                lparameters.Add(new OracleParameter("YEAR", iyear));
                lparameters.Add(new OracleParameter("MONTH", imonth));
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                return oracle.GetDataTable(stringBuilder.ToString(), lparameters);
            }
        }

        public void BulkDataFromCSV(string stable, string sfile)
        {
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                StringBuilder stringBuilder = new StringBuilder("BULK INSERT ");
                stringBuilder.Append(stable);
                stringBuilder.Append(" FROM '");
                stringBuilder.Append(sfile);
                stringBuilder.Append("' WITH (FIELDTERMINATOR = ',', ROWTERMINATOR = '\n')");
                oracle.ExecuteNonQuery(stringBuilder.ToString(), null);
            }
        }

        public void CreateRows(string strtable)
        {
            using (Oracle oracle = new Oracle())
            {
                try
                {
                    oracle.sConnection = this.sconnection;
                    oracle.Connect();
                    List<OracleParameter> oracleParameters = new List<OracleParameter>();
                    oracleParameters.Add(new OracleParameter("VTABLE", strtable));
                    oracle.ExecuteNonQuery("SPINGRESAREGISTROSTABLAEXTERNA", oracleParameters, true, false);
                }
                catch (Exception ex)
                {
                    LogError.WriteError("ServicioDescarga", "ServicioDescarga", ex);
                    throw;
                }                
            }
        }


        public void PurgeTable(string stable, bool btruncate)
        {
            using (Oracle oracle = new Oracle())
            {
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                StringBuilder stringBuilder = new StringBuilder();
                if (btruncate)
                {
                    stringBuilder.Append("TRUNCATE TABLE");
                    stringBuilder.Append(stable);
                }
                else
                {
                    stringBuilder.Append("DELETE FROM ");
                    stringBuilder.Append(stable);
                    stringBuilder.Append(" WHERE TRUNC(FECHAADICION) = TRUNC(SYSDATE)");
                }               
                oracle.ExecuteNonQuery(stringBuilder.ToString(), null);
            }
        }

        public DataTable GetProgramsData(DateTime dtFechaInicial, DateTime dtFechaFinal, string splan, bool besvaloracion, string sagreement)
        {
            using (Oracle oracle = new Oracle())
            {
                string sview = string.Empty;
                StringBuilder sfields = new StringBuilder();
                sfields.Append("TIPODOCUMENTO, DOCUMENTO, PRIMERNOMBRE, SEGUNDONOMBRE, PRIMERAPELLIDO, SEGUNDOAPELLIDO, GENERO, FECHANACIMIENTO, CIUDAD, ESTADOCIVIL, TELEFONO, BARRIO, ZONA");
                sfields.Append(", OCUPACION, DIRECCION, CELULAR, CORREO, TIPOAFILIACION, NIVEL, CELULAR2, PAISORIGEN, CIUDADORIGEN, NOMBREMUNICIPIO, TIENECOVID, HATENIDOCOVID, TIPOATENCION, TIPOSERVICIO, TIPOCONVENIO, CONVENIO");
                sfields.Append(", NOMBRECONVENIO, TARIFA, NOMBRETARIFA, UNIDADFUNCIONAL, TERCERO, FECHA, AUTORIZACION, PLAN, CONCEPTO, CENTRO, SERVICIO, NOMBRESERVICIO, CANTIDAD, TIPOORIGEN, PAISEXPEDICIONDOCUMENTO");
                sfields.Append(", CONTRATO, PAQUETE, DIAGNOSTICO, IDESTADISTICA, TIENECITA, NOMBRECITA");
                if (!besvaloracion)
                {
                    sview = "VDATOSPROGRAMAS";                    
                }
                else
                {
                    sview = "VDATOSVALORACIONPROGRAMAS";
                    sfields.Append(", INGRESOBASE");
                }
                oracle.sConnection = this.sconnection;
                oracle.Connect();
                StringBuilder stringBuilder = new StringBuilder("SELECT " + sfields.ToString() +  " FROM " + sview + " WHERE FECHA BETWEEN TO_DATE(:INITIALDATE, 'YYYY-MM-DD') AND TO_DATE(:FINALDATE, 'YYYY-MM-DD') AND CONVENIO = :CONVENIO AND PLANANTERIOR LIKE '%' || :PLAN || '%'");
                List<OracleParameter> lparameters = new List<OracleParameter>();
                lparameters.Add(new OracleParameter("INITIALDATE", dtFechaInicial.ToString("yyyy-MM-dd")));
                lparameters.Add(new OracleParameter("FINALDATE", dtFechaFinal.ToString("yyyy-MM-dd")));
                lparameters.Add(new OracleParameter("CONVENIO", sagreement));
                lparameters.Add(new OracleParameter("PLAN", splan));
                return oracle.GetDataTable(stringBuilder.ToString(), lparameters);
            }
        }
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
