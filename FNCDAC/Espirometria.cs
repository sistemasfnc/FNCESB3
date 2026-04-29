using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventLog;
using FNCEntity;
using FNCUtils;

namespace FNCDAC
{
    public class Espirometria : IDisposable
    {
        public string sconnection { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }

        /// <summary>
        /// Método que genera la lista con las espirometrías para Sanitas
        /// </summary>      
        /// <param name="sfechainicial">String fecha inicial de las pruebas</param>
        /// <param name="sfechafinal">String fecha final de las pruebas</param>
        /// <returns>Lista genérica del objeto SanitasPrfp con la información de las pruebas para generar el archivo</returns>
        public List<SanitasPrfp> GetSanitasEspirometrias(string sfechainicial, string sfechafinal) 
        {
            DataTable dt = new DataTable();
            List<SanitasPrfp> list = new List<SanitasPrfp>();
            SanitasPrfp espirometria = null;
            try
            {
                dt = this.GetDataFromNDD("Sanitas", sfechainicial, sfechafinal);
                foreach (DataRow dr in dt.Rows)                 
                {
                    espirometria = new SanitasPrfp()
                    {
                        dtFecha = Convert.ToDateTime(dr["Fecha"]),
                        sNomIPS = dr["NombreIPS"].ToString(),
                        sCodIPS = dr["CodigoIPS"].ToString(),
                        sTipoDocumento = Tools.GetDocumentType(dr["TipoDocumento"].ToString(), false),
                        sNombres = dr["FirstName"].ToString(),
                        sApellidos = dr["LastName"].ToString(),
                        sDocumento = dr["Documento"].ToString(),
                        iEdad = Convert.ToInt32(dr["Edad"]),
                        sFactorRiesgo = dr["Factor"].ToString(),
                        sTabaquismo = dr["Tabaquismo"].ToString(),
                        dTalla = Convert.ToDecimal(dr["Talla"]),
                        dIMC = Convert.ToDecimal(dr["IMC"]),
                        dPeso = Convert.ToDecimal(dr["Peso"]),
                        sGenero = dr["Genero"].ToString(),
                        sResultado = dr["Resultado"].ToString(),
                        sObservaciones = dr["Observaciones"].ToString(),
                        sDiagnostico = dr["Diagnostico"].ToString(),    
                        sCIE10 = dr["CIE10"].ToString(),
                        sProcedimiento = ((dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] == DBNull.Value) || (dr["FEV1_Pre"] == DBNull.Value && dr["FEV1_Post"] != DBNull.Value)) ? "ESPIROMETRIA (893703)" : dr["Procedimiento"].ToString(),
                        sProviene = "NDD",
                    };                  
                    if (dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] != DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Pre"]);
                        espirometria.dVEF1Post = Convert.ToDecimal(dr["FEV1_Post"]);
                        espirometria.dCVFPos = Convert.ToDecimal(dr["FVC_Post"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Pre"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                        espirometria.dTasaPos = (espirometria.dVEF1Post != 0) ? espirometria.dCVFPos / espirometria.dVEF1Post : 0;
                    }
                    else if (dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] == DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Pre"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Pre"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    else if (dr["FEV1_Pre"] == DBNull.Value && dr["FEV1_Post"] != DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Post"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Post"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    list.Add(espirometria);
                }
                dt = this.GetDataFromSentrySuite("Sanitas", sfechainicial, sfechafinal);
                foreach (DataRow dr in dt.Rows)
                {
                    espirometria = new SanitasPrfp()
                    {
                        dtFecha = Convert.ToDateTime(dr["Fecha"]),
                        sNomIPS = dr["NombreIPS"].ToString(),
                        sCodIPS = dr["CodigoIPS"].ToString(),
                        sTipoDocumento = Tools.GetDocumentType(dr["TipoDocumento"].ToString(), false),
                        sNombres = dr["FirstName"].ToString(),
                        sApellidos = dr["LastName"].ToString(),
                        sDocumento = dr["Documento"].ToString(),
                        iEdad = Convert.ToInt32(dr["Edad"]),
                        sFactorRiesgo = dr["Factor"].ToString(),
                        sTabaquismo = dr["Tabaquismo"].ToString(),
                        dTalla = Convert.ToDecimal(dr["Talla"]),
                        dIMC = Convert.ToDecimal(dr["IMC"]),
                        dPeso = Convert.ToDecimal(dr["Peso"]),
                        sGenero = dr["Genero"].ToString(),
                        //sResultado = dr["Resultado"].ToString(),
                        //sObservaciones = dr["Observaciones"].ToString(),
                        sDiagnostico = dr["Diagnostico"].ToString(),
                        sCIE10 = dr["CIE10"].ToString(),
                        sProcedimiento = (dr["FEV1_Post"] == DBNull.Value) ? "ESPIROMETRIA (893703)" : "ESPIROMETRIA O CURVA DE FLUJO VOLUMEN PRE Y POST BRONCODILATADOR (893805)",
                        sProviene = "SentrySuite",
                    };
                    if (espirometria.sProcedimiento == "ESPIROMETRIA O CURVA DE FLUJO VOLUMEN PRE Y POST BRONCODILATADOR (893805)")
                    {
                        espirometria.dVEF1Pre = Tools.GetDValue(dr["FEV1_Pre"].ToString());
                        espirometria.dVEF1Post = Tools.GetDValue(dr["FEV1_Post"].ToString());
                        espirometria.dCVFPos = Tools.GetDValue(dr["CVF_Post"].ToString());
                        espirometria.dCVFPre = Tools.GetDValue(dr["CVF_Pre"].ToString());
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                        espirometria.dTasaPos = (espirometria.dVEF1Post != 0) ? espirometria.dCVFPos / espirometria.dVEF1Post : 0;
                    }
                    else
                    {
                        espirometria.dVEF1Pre = Tools.GetDValue(dr["FEV1_Pre"].ToString());
                        espirometria.dCVFPre = Tools.GetDValue(dr["CVF_Pre"].ToString());
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    list.Add(espirometria);
                }
                return list;
            }
            catch (Exception ex)
            {
                LogError.WriteError("EnviaEspirometrias", "EnviaEspirometrias", ex);
                throw;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                espirometria = null;
            }
        }


        /// <summary>
        /// Método que genera la lista con las espirometrías para Sanitas
        /// </summary>      
        /// <param name="sfechainicial">String fecha inicial de las pruebas</param>
        /// <param name="sfechafinal">String fecha final de las pruebas</param>
        /// <returns>Lista genérica del objeto SanitasPrfp con la información de las pruebas para generar el archivo</returns>
        public List<SanitasPrfp> GetSuraEspirometrias(string sfechainicial, string sfechafinal)
        {
            DataTable dt = new DataTable();
            List<SanitasPrfp> list = new List<SanitasPrfp>();
            SanitasPrfp espirometria = null;
            try
            {
                dt = this.GetDataFromNDD("Sura", sfechainicial, sfechafinal);
                foreach (DataRow dr in dt.Rows)
                {
                    espirometria = new SanitasPrfp()
                    {
                        dtFecha = Convert.ToDateTime(dr["Fecha"]),
                        sNomIPS = dr["NombreIPS"].ToString(),
                        sCodIPS = dr["CodigoIPS"].ToString(),
                        sTipoDocumento = Tools.GetDocumentType(dr["TipoDocumento"].ToString(), false),
                        sNombres = dr["FirstName"].ToString(),
                        sApellidos = dr["LastName"].ToString(),
                        sDocumento = dr["Documento"].ToString(),
                        iEdad = Convert.ToInt32(dr["Edad"]),
                        sFactorRiesgo = dr["Factor"].ToString(),
                        sTabaquismo = dr["Tabaquismo"].ToString(),
                        dTalla = Convert.ToDecimal(dr["Talla"]),
                        dIMC = Convert.ToDecimal(dr["IMC"]),
                        dPeso = Convert.ToDecimal(dr["Peso"]),
                        sGenero = dr["Genero"].ToString(),
                        sResultado = dr["Resultado"].ToString(),
                        sObservaciones = dr["Observaciones"].ToString(),
                        sDiagnostico = dr["Diagnostico"].ToString(),
                        sCIE10 = dr["CIE10"].ToString(),
                        sProcedimiento = ((dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] == DBNull.Value) || (dr["FEV1_Pre"] == DBNull.Value && dr["FEV1_Post"] != DBNull.Value)) ? "ESPIROMETRIA (893703)" : dr["Procedimiento"].ToString(),
                        sProviene = "NDD",
                    };
                    if (dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] != DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Pre"]);
                        espirometria.dVEF1Post = Convert.ToDecimal(dr["FEV1_Post"]);
                        espirometria.dCVFPos = Convert.ToDecimal(dr["FVC_Post"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Pre"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                        espirometria.dTasaPos = (espirometria.dVEF1Post != 0) ? espirometria.dCVFPos / espirometria.dVEF1Post : 0;
                    }
                    else if (dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] == DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Pre"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Pre"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    else if (dr["FEV1_Pre"] == DBNull.Value && dr["FEV1_Post"] != DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Post"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Post"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    list.Add(espirometria);
                }
                dt = this.GetDataFromSentrySuite("Sura", sfechainicial, sfechafinal);
                foreach (DataRow dr in dt.Rows)
                {
                    espirometria = new SanitasPrfp()
                    {
                        dtFecha = Convert.ToDateTime(dr["Fecha"]),
                        sNomIPS = dr["NombreIPS"].ToString(),
                        sCodIPS = dr["CodigoIPS"].ToString(),
                        sTipoDocumento = Tools.GetDocumentType(dr["TipoDocumento"].ToString(), false),
                        sNombres = dr["FirstName"].ToString(),
                        sApellidos = dr["LastName"].ToString(),
                        sDocumento = dr["Documento"].ToString(),
                        iEdad = Convert.ToInt32(dr["Edad"]),
                        sFactorRiesgo = dr["Factor"].ToString(),
                        sTabaquismo = dr["Tabaquismo"].ToString(),
                        dTalla = Convert.ToDecimal(dr["Talla"]),
                        dIMC = Convert.ToDecimal(dr["IMC"]),
                        dPeso = Convert.ToDecimal(dr["Peso"]),
                        sGenero = dr["Genero"].ToString(),
                        //sResultado = dr["Resultado"].ToString(),
                        //sObservaciones = dr["Observaciones"].ToString(),
                        sDiagnostico = dr["Diagnostico"].ToString(),
                        sCIE10 = dr["CIE10"].ToString(),
                        sProcedimiento = (dr["FEV1_Post"] == DBNull.Value) ? "ESPIROMETRIA (893703)" : "ESPIROMETRIA O CURVA DE FLUJO VOLUMEN PRE Y POST BRONCODILATADOR (893805)",
                        sProviene = "SentrySuite",
                    };
                    if (espirometria.sProcedimiento == "ESPIROMETRIA O CURVA DE FLUJO VOLUMEN PRE Y POST BRONCODILATADOR (893805)")
                    {
                        espirometria.dVEF1Pre = Tools.GetDValue(dr["FEV1_Pre"].ToString());
                        espirometria.dVEF1Post = Tools.GetDValue(dr["FEV1_Post"].ToString());
                        espirometria.dCVFPos = Tools.GetDValue(dr["CVF_Post"].ToString());
                        espirometria.dCVFPre = Tools.GetDValue(dr["CVF_Pre"].ToString());
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                        espirometria.dTasaPos = (espirometria.dVEF1Post != 0) ? espirometria.dCVFPos / espirometria.dVEF1Post : 0;
                    }
                    else
                    {
                        espirometria.dVEF1Pre = Tools.GetDValue(dr["FEV1_Pre"].ToString());
                        espirometria.dCVFPre = Tools.GetDValue(dr["CVF_Pre"].ToString());
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    list.Add(espirometria);
                }
                return list;
            }
            catch (Exception ex)
            {
                LogError.WriteError("EnviaEspirometrias", "EnviaEspirometrias", ex);
                throw;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                espirometria = null;
            }
        }

        /// <summary>
        /// Método que genera la lista con las espirometrías para Sanitas
        /// </summary>      
        /// <param name="sfechainicial">String fecha inicial de las pruebas</param>
        /// <param name="sfechafinal">String fecha final de las pruebas</param>
        /// <returns>Lista genérica del objeto SanitasPrfp con la información de las pruebas para generar el archivo</returns>
        public List<SanitasPrfp> GetEcopetrolEspirometrias(string sfechainicial, string sfechafinal)
        {
            DataTable dt = new DataTable();
            List<SanitasPrfp> list = new List<SanitasPrfp>();
            SanitasPrfp espirometria = null;
            try
            {
                dt = this.GetDataFromNDD("Ecopetrol", sfechainicial, sfechafinal);
                foreach (DataRow dr in dt.Rows)
                {
                    espirometria = new SanitasPrfp()
                    {
                        dtFecha = Convert.ToDateTime(dr["Fecha"]),
                        sNomIPS = dr["NombreIPS"].ToString(),
                        sCodIPS = dr["CodigoIPS"].ToString(),
                        sTipoDocumento = Tools.GetDocumentType(dr["TipoDocumento"].ToString(), false),
                        sNombres = dr["FirstName"].ToString(),
                        sApellidos = dr["LastName"].ToString(),
                        sDocumento = dr["Documento"].ToString(),
                        iEdad = Convert.ToInt32(dr["Edad"]),
                        sFactorRiesgo = dr["Factor"].ToString(),
                        sTabaquismo = dr["Tabaquismo"].ToString(),
                        dTalla = Convert.ToDecimal(dr["Talla"]),
                        dIMC = Convert.ToDecimal(dr["IMC"]),
                        dPeso = Convert.ToDecimal(dr["Peso"]),
                        sGenero = dr["Genero"].ToString(),
                        sResultado = dr["Resultado"].ToString(),
                        sObservaciones = dr["Observaciones"].ToString(),
                        sDiagnostico = dr["Diagnostico"].ToString(),
                        sCIE10 = dr["CIE10"].ToString(),
                        sProcedimiento = ((dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] == DBNull.Value) || (dr["FEV1_Pre"] == DBNull.Value && dr["FEV1_Post"] != DBNull.Value)) ? "ESPIROMETRIA (893703)" : dr["Procedimiento"].ToString(),
                        sProviene = "NDD",
                    };
                    if (dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] != DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Pre"]);
                        espirometria.dVEF1Post = Convert.ToDecimal(dr["FEV1_Post"]);
                        espirometria.dCVFPos = Convert.ToDecimal(dr["FVC_Post"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Pre"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                        espirometria.dTasaPos = (espirometria.dVEF1Post != 0) ? espirometria.dCVFPos / espirometria.dVEF1Post : 0;
                    }
                    else if (dr["FEV1_Pre"] != DBNull.Value && dr["FEV1_Post"] == DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Pre"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Pre"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    else if (dr["FEV1_Pre"] == DBNull.Value && dr["FEV1_Post"] != DBNull.Value)
                    {
                        espirometria.dVEF1Pre = Convert.ToDecimal(dr["FEV1_Post"]);
                        espirometria.dCVFPre = Convert.ToDecimal(dr["FVC_Post"]);
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    list.Add(espirometria);
                }
                dt = this.GetDataFromSentrySuite("Ecopetrol", sfechainicial, sfechafinal);
                foreach (DataRow dr in dt.Rows)
                {
                    espirometria = new SanitasPrfp()
                    {
                        dtFecha = Convert.ToDateTime(dr["Fecha"]),
                        sNomIPS = dr["NombreIPS"].ToString(),
                        sCodIPS = dr["CodigoIPS"].ToString(),
                        sTipoDocumento = Tools.GetDocumentType(dr["TipoDocumento"].ToString(), false),
                        sNombres = dr["FirstName"].ToString(),
                        sApellidos = dr["LastName"].ToString(),
                        sDocumento = dr["Documento"].ToString(),
                        iEdad = Convert.ToInt32(dr["Edad"]),
                        sFactorRiesgo = dr["Factor"].ToString(),
                        sTabaquismo = dr["Tabaquismo"].ToString(),
                        dTalla = Convert.ToDecimal(dr["Talla"]),
                        dIMC = Convert.ToDecimal(dr["IMC"]),
                        dPeso = Convert.ToDecimal(dr["Peso"]),
                        sGenero = dr["Genero"].ToString(),
                        //sResultado = dr["Resultado"].ToString(),
                        //sObservaciones = dr["Observaciones"].ToString(),
                        sDiagnostico = dr["Diagnostico"].ToString(),
                        sCIE10 = dr["CIE10"].ToString(),
                        sProcedimiento = (dr["FEV1_Post"] == DBNull.Value) ? "ESPIROMETRIA (893703)" : "ESPIROMETRIA O CURVA DE FLUJO VOLUMEN PRE Y POST BRONCODILATADOR (893805)",
                        sProviene = "SentrySuite",
                    };
                    if (espirometria.sProcedimiento == "ESPIROMETRIA O CURVA DE FLUJO VOLUMEN PRE Y POST BRONCODILATADOR (893805)")
                    {
                        espirometria.dVEF1Pre = Tools.GetDValue(dr["FEV1_Pre"].ToString());
                        espirometria.dVEF1Post = Tools.GetDValue(dr["FEV1_Post"].ToString());
                        espirometria.dCVFPos = Tools.GetDValue(dr["CVF_Post"].ToString());
                        espirometria.dCVFPre = Tools.GetDValue(dr["CVF_Pre"].ToString());
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                        espirometria.dTasaPos = (espirometria.dVEF1Post != 0) ? espirometria.dCVFPos / espirometria.dVEF1Post : 0;
                    }
                    else
                    {
                        espirometria.dVEF1Pre = Tools.GetDValue(dr["FEV1_Pre"].ToString());
                        espirometria.dCVFPre = Tools.GetDValue(dr["CVF_Pre"].ToString());
                        espirometria.dTasaPre = (espirometria.dVEF1Pre != 0) ? espirometria.dCVFPre / espirometria.dVEF1Pre : 0;
                    }
                    list.Add(espirometria);
                }
                return list;
            }
            catch (Exception ex)
            {
                LogError.WriteError("EnviaEspirometrias", "EnviaEspirometrias", ex);
                throw;
            }
            finally
            {
                dt.Dispose();
                dt = null;
                espirometria = null;
            }
        }

        private DataTable GetDataFromNDD(string scompany, string sfechainicial, string sfechafinal)
        {
            string sview = this.GetSQLView(scompany, "NDD");
            string squery = "SELECT * FROM " + sview;
            squery += " WHERE Fecha BETWEEN @FechaInicial AND @FechaFinal";
            List<SqlParameter> sqlParameters = new List<SqlParameter>();
            using (SQLServer sQLServer = new SQLServer(this.sconnection))
            {
                sqlParameters.Add(new SqlParameter("@FechaInicial", sfechainicial));
                sqlParameters.Add(new SqlParameter("@FechaFinal", sfechafinal));
                return sQLServer.GetDataTable(squery, sqlParameters);
            }            
        }

        private DataTable GetDataFromSentrySuite(string scompany, string sfechainicial, string sfechafinal)
        {
            string sview = this.GetSQLView(scompany, "SentrySuite");
            string squery = "SELECT * FROM " + sview;
            squery += " WHERE Fecha BETWEEN @FechaInicial AND @FechaFinal";
            List<SqlParameter> sqlParameters = new List<SqlParameter>();
            using (SQLServer sQLServer = new SQLServer(this.sconnection))
            {
                sqlParameters.Add(new SqlParameter("@FechaInicial", sfechainicial));
                sqlParameters.Add(new SqlParameter("@FechaFinal", sfechafinal));
                return sQLServer.GetDataTable(squery, sqlParameters);
            }
        }

        private string GetSQLView(string scompany, string ssystem)
        {            
            switch (scompany) 
            {
                case "Sura": return (ssystem == "NDD") ? "VEspirometriasSuraNDD" : "VEspirometriasSuraSentrySuite";
                case "Ecopetrol": return (ssystem == "NDD") ? "VEspirometriasEcopetrolNDD" : "VEspirometriasEcopetrolSentrySuite";
                default: return (ssystem == "NDD") ? "VEspirometriasSanitasNDD" : "VEspirometriasSanitasSentrySuite";
            }
        }
    }
}
