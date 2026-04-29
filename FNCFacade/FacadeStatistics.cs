using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using FNCEntity;
using FNCDAC;


namespace FNCFacade
{
    public class FacadeStatistics : IDisposable
    {
        public string sConnection { get; set; }

        public List<EntryResponse> GetDataForDate(int iyear, int imonth)
        {
            List<EntryResponse> lentryExtendeds = new List<EntryResponse>();
            Statistic statistic = new Statistic();
            DataTable dataTable = new DataTable();
            EntryResponse entryExtended = null;
            try
            {
                statistic.sconnection = this.sConnection;
                dataTable = statistic.GetDataForDate(iyear, imonth);
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    entryExtended = new EntryResponse()
                    {
                        iid = Convert.ToInt32(dataRow["IS_ID"]),
                        sappointment = dataRow["IS_CITA"].ToString(),                        
                    };
                }
                return lentryExtendeds;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                statistic.Dispose();
                statistic = null;
            }
        }

        public List<EntryExtended> GetEntriesForPrograms(EntryExtended entryExtended)
        {
            List<EntryExtended> entryExtendeds = new List<EntryExtended>();
            Statistic statistic = new Statistic();
            DataTable dataTable = new DataTable();
            try
            {
                statistic.sconnection = this.sConnection;
                dataTable = statistic.GetProgramsEntries(entryExtended);
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    entryExtended = new EntryExtended()
                    {
                        iqty = Convert.ToInt32(dataRow["IS_CANTIDAD"]),
                        iinvoice = Convert.ToInt32(dataRow["IS_FACTURA"]),
                        spatient = dataRow["PACIENTE"].ToString(),
                        sconcept = dataRow["IS_CONCEPTO"].ToString(),
                        scostcenter = dataRow["IS_CENTROCOSTO"].ToString(),
                        ientry = Convert.ToInt32(dataRow["IS_INGRESO"]),
                        sdocument = dataRow["IS_DOCUMENTO"].ToString(),
                        sdocumenttype = dataRow["IS_TIPODOCUMENTO"].ToString(),
                        sservice = dataRow["IS_SERVICIO"].ToString(),
                        sservicename = dataRow["IS_NOMBRESERVICIO"].ToString(),
                        ddate = Convert.ToDateTime(dataRow["IS_FECHA"]),
                        srate = dataRow["IS_TARIFA"].ToString(),
                        sagreement = dataRow["EMPRES"].ToString(),
                        splan = dataRow["PLANOM"].ToString(),
                        sconceptname = dataRow["CONNOM"].ToString(),
                        dvalue = Convert.ToDecimal(dataRow["IS_VALOR"]),
                    };
                    entryExtendeds.Add(entryExtended);
                }
                return entryExtendeds;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                statistic.Dispose();
                statistic = null;
            }
            
            
        }

        public void UpdateEntriesInvoice(List<EntryExtended> lentryExtendeds)
        {
            using (Statistic statistic = new Statistic())
            {
                statistic.sconnection = this.sConnection;
                statistic.UpdateEntryInvoices(lentryExtendeds);
            }
        }

        public void BulkDataFromCSV(string stable, string sfile)
        {
            using (Statistic statistic = new Statistic())
            {
                statistic.sconnection = this.sConnection;
                statistic.BulkDataFromCSV(stable, sfile);
            }
        }

        public void PurgeTable(string stable, bool btruncate)
        {
            using (Statistic statistic = new Statistic())
            {
                statistic.sconnection = this.sConnection;
                statistic.PurgeTable(stable, btruncate);
            }
        }

        public void CreateRows(string strtable)
        {
            using (Statistic statistic = new Statistic())
            {
                statistic.sconnection = this.sConnection;
                statistic.CreateRows(strtable);
            }
        }

        public DataTable GetProgramsData(DateTime dtFechaInicial, DateTime dtFechaFinal, string splan, string sagreement, bool besvaloracion = false)
        {
            using (Statistic statistic = new Statistic())
            {
                statistic.sconnection = this.sConnection;
                return statistic.GetProgramsData(dtFechaInicial, dtFechaFinal, splan, besvaloracion, sagreement);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
