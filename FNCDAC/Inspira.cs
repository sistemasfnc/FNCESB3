using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEntity;
using System.Data;
using System.Data.SqlClient;
using FNCUtils;

namespace FNCDAC
{
    public class Inspira : IDisposable
    {
        public string sConnection { get; set; }

        
        public List<ServintePackage> GetPackages()
        {
            List<ServintePackage> lPackages = new List<ServintePackage>();
            DataTable dataTable = new DataTable();
            ServinteOracle servinteOracle = null;
            ServintePackage servintePackage = null;
            try
            {
                servinteOracle = new ServinteOracle();
                servinteOracle.sconnection = this.sConnection;
                dataTable = servinteOracle.GetPackages();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    servintePackage = new ServintePackage()
                    {
                        sconcept = dataRow["CONCEPTO"].ToString(),
                        sunit = dataRow["UNIDAD"].ToString(),
                        scostcenter = dataRow["CENTRO"].ToString(),
                        srate = dataRow["TARIFA"].ToString(),
                        spackage = dataRow["PAQUETE"].ToString(),
                        dvale = Convert.ToInt32(dataRow["VALORPAQUETE"]),
                        sservice = dataRow["SERVICIO"].ToString(),
                    };
                    lPackages.Add(servintePackage);
                }
                return lPackages;
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                servinteOracle.Dispose();

            }
        }
        /// <summary>
        /// Método para obtener las citas de los pacientes de programas 
        /// </summary>
        /// <returns>Lista genérica de pacientes</returns>
        public List<ServintePatient> GetPatients(List<ServintePackage> lPackages)
        {
            DataTable dt = new DataTable();
            List<ServintePatient> lPatient = new List<ServintePatient>();
            ServintePatient patient = null;
            InspiraCita inspiraCita = null;
            ServiceRequest serviceRequest = null;
            ServintePackage servintePackage = null;
            int ivalue = 0;
            try
            {
                dt = this.GetPatientsData();
                (from DataRow dataRow in dt.Rows
                 group dataRow by new { documento = dataRow.Field<string>("IDENTIFICACION"), tipodocumento = dataRow.Field<string>("TIPO_IDENTIFICACION") } into f
                 select new
                 {
                     key = f.Key,
                     Elements = f,
                 }).ToList().ForEach(f =>
                 {
                     patient = new ServintePatient()
                     {
                         ssurname = f.Elements.First()["APELLIDO_P"].ToString(),
                         ssecondsurname = f.Elements.First()["APELLIDO_S"].ToString(),
                         sfirstname = f.Elements.First()["NOMBRE_P"].ToString(),
                         ssecondname = f.Elements.First()["NOMBRE_S"].ToString(),
                         dbirthdate = Convert.ToDateTime(f.Elements.First()["FECHA_NACIMIENTO"]),
                         sdocument = f.Elements.First()["IDENTIFICACION"].ToString(),
                         sdocumenttype = Tools.GetDocumentType(f.Elements.First()["TIPO_IDENTIFICACION"].ToString()),
                         saddress = f.Elements.First().ToString(),
                         surbanzone = "U",
                         sphone = f.Elements.First()["TELEFONO"].ToString(),
                         sbornplace = "169",
                         sneighborhood = "43",
                         sgender = f.Elements.First()["SEXO"].ToString(),
                         lappointments = new List<InspiraCita>(),
                         scity = "11001",
                         scovid1 = "N",
                         scovid2 = "N",
                         scityname = "BOGOTA D.C.",   
                         snation = "169",                                      
                     };
                     (from DataRow datarow in f.Elements
                      where datarow["IDENTIFICACION"].ToString() == f.Elements.First()["IDENTIFICACION"].ToString() && datarow["TIPO_IDENTIFICACION"].ToString() == f.Elements.First()["TIPO_IDENTIFICACION"].ToString()
                      group datarow by datarow["IDCITA"] into a
                      select new
                      {
                          Key = a.Key,
                          Elements = a,
                      }).ToList().ForEach(a =>
                      {
                          
                          inspiraCita = new InspiraCita()
                          {
                              ddate = (a.Elements.First()["FECHA_CITA"] != DBNull.Value) ? Convert.ToDateTime(a.Elements.First()["FECHA_CITA"]) : DateTime.Now.AddDays(-1),
                              sagreementname = a.Elements.First()["NOM_EMPRESA"].ToString(),
                              sagreement = a.Elements.First()["COD_EMPRESA"].ToString(),
                              srate = a.Elements.First()["COD_TARIFA"].ToString(),
                              sappointment = a.Key.ToString(),
                              scostcenter = a.Elements.First()["COD_CENTROCOSTO"].ToString(),
                              scie10 = "Z000",
                              sservicegroup = a.Elements.First()["CREACARGO"].ToString(),
                              sunit = (Convert.ToInt32(a.Elements.First()["EDAD"]) >= 18) ? "1100" : "1200",
                              sthird = a.Elements.First()["NUM_PROFESIONAL"].ToString(),
                              sagreementtype = "E",
                              sattentiontype = "2",
                              sservicetype = "28",                              
                              lservices = new List<ServiceRequest>(),
                              stemplate = a.Elements.First()["PLANTILLA"].ToString(),
                              sratename = a.Elements.First()["NOM_TARIFA"].ToString(),     
                              splan = a.Elements.First()["COD_PLAN"].ToString(),
                              sauthorization = a.Elements.First()["NUM_AUTORIZACION"].ToString(),                              
                          };                          
                          (from DataRow datarow in a.Elements
                           where datarow["IDCITA"].ToString() == a.Key.ToString()
                           group datarow by datarow["NOM_GRUPO"] into s
                           select new
                           {
                               Key = s.Key,
                               Elements = s,
                           }).ToList().ForEach(s =>
                           {
                               serviceRequest = new ServiceRequest()
                               {
                                   scostcenter = s.Elements.First()["COD_CENTROCOSTO"].ToString(),
                                   sservice = s.Elements.First()["SERVICEID_CODE"].ToString(),
                                   iqty = 1,
                                   idiscount = 0,
                                   sconcept = s.Elements.First()["COD_CONCEPTO"].ToString(),
                                   srate = s.Elements.First()["CON_TARIFA"].ToString(),
                                   ivalue = Convert.ToInt32(s.Elements.First()["VALOR"]),
                                   sservicename = s.Elements.First()["SERVICEID_NOMBRE"].ToString(),
                                   bisprocedure = !s.Elements.First()["SERVICEID_NOMBRE"].ToString().Contains("CONSULTA"),
                               };
                               servintePackage = lPackages.FirstOrDefault(x => x.sconcept == serviceRequest.sconcept && x.sunit == inspiraCita.sunit && x.srate == serviceRequest.srate && x.spackage == inspiraCita.stemplate);
                               ivalue = (servintePackage != null) ? servintePackage.dvale : 0;
                               inspiraCita.ientry = ivalue;
                               inspiraCita.lservices.Add(serviceRequest);
                           });
                          patient.lappointments.Add(inspiraCita);
                      });
                     lPatient.Add(patient);
                 });                                  
                return lPatient;
            }
            catch (Exception)
            {                
                throw;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                patient = null;
                inspiraCita = null;
                serviceRequest = null;
            }
        }
               
        /// <summary>
        /// Método para obtener la información de las citas de los pacientes de programas
        /// </summary>
        /// <returns>DataTable con la información de las citas de pacientes de programas del día anterior</returns>
        private DataTable GetPatientsData()
        {
            StringBuilder sQuery = new StringBuilder("SELECT * FROM [VNewCargosProgramas]");
            using (SQLServer oSQL = new SQLServer(this.sConnection))
            {
                return oSQL.GetDataTable(sQuery.ToString(), null);
            }
        }
        
        private DataTable GetServiceResponsibleData()
        {
            string squery = "SELECT * FROM ResponsableServicio";
            using (SQLServer oSQL = new SQLServer(this.sConnection))
            {
                return oSQL.GetDataTable(squery, null);
            }
        }


        public List<Generic> GetServiceResponsible()
        {
            List<Generic> lGeneric = new List<Generic>();
            DataTable dt = new DataTable();
            Generic oGeneric = null;
            try
            {
                dt = this.GetServiceResponsibleData();
                foreach (DataRow dr in dt.Rows)
                {
                    oGeneric = new Generic()
                    {
                        sname = dr["responsable"].ToString(),
                        scode = dr["servicio"].ToString(),
                        sfilter = dr["concepto"].ToString(),
                    };
                    lGeneric.Add(oGeneric);
                }
                return lGeneric;
            }
            catch (Exception)
            {

                throw;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                oGeneric = null;
            }
        }
       

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
        

        public bool NeedsInvoice(string sPlan, string sGroup)
        {
            //string sQuery = "SELECT COUNT(1) FROM InspiraFactura WHERE [Plan] LIKE '%' + @Plan + '%' AND Grupo LIKE '%' + @Grupo + '%'";
            string sQuery = "SELECT COUNT(1) FROM InspiraFactura WHERE [Plan] = @Plan";
            List<SqlParameter> lParameters = new List<SqlParameter>();            
            object oresult = null;
            using (SQLServer oSQL = new SQLServer(this.sConnection))
            {
                lParameters.Add(new SqlParameter("@Plan", sPlan));
                oresult = oSQL.GetScalar(sQuery, lParameters);
                return (oresult == null) ? false : (Convert.ToInt32(oresult) != 0);
            }
        }
    }
}
