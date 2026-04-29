using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCDAC;

namespace FNCFacade
{
    public class FacadeIntegraBus : IDisposable
    {
        public string sconnection { get; set; }

        public List<string> GetCompanies()
        {
            List<string> companies = new List<string>();
            using (Integrador oracle = new Integrador())
            {
                oracle.sconnection = sconnection;
                DataTable dt = new DataTable();
                dt = oracle.GetPlansForSupports();
                foreach (DataRow plan in dt.Rows)
                {
                    companies.Add(plan["PLACOD"].ToString());
                }
                dt.Dispose();
                dt = null;
            }
            return companies;
        }

        public DataTable ObtenerDiferencias()
        {
            using (Integrador oracle = new Integrador())
            {
                oracle.sconnection = sconnection;
                DataTable dt = new DataTable();
                dt = oracle.ObtenerDiferencias();
                return dt;
            }
        }

        /// <summary>
        /// Retorna los registros de Salesforce (tabla InspiraTarifasValores ya cargada)
        /// cuyo Value__c difiere con el valor actual del ERP.
        /// Columnas retornadas: Id (Salesforce), NuevoValor (int desde ERP).
        /// </summary>
        public DataTable ObtenerValoresCambiados()
        {
            using (Integrador integrador = new Integrador())
            {
                integrador.sconnection = sconnection;
                return integrador.ObtenerValoresCambiados();
            }
        }

        public void BulkData(string stable, DataTable dt)
        {
            using (Integrador oracle = new Integrador())
            {
                oracle.sconnection = sconnection;
                oracle.BulkData(stable, dt);
            }
        }

        public void TruncarTabla(string stable)
        {             
            using (Integrador oracle = new Integrador())
            {
                oracle.sconnection = sconnection;
                oracle.TruncarTabla(stable);
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
