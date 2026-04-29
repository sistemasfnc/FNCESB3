using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FNCEntity;
using FNCDAC;
using System.Data;
using EventLog;
using FNCFacade.FNCESB;
using FNCUtils;
using System.Data.Odbc;
using System.Security.Policy;

namespace FNCFacade
{
    public class FacadeInspiraServinte : IDisposable
    {
        public string sConnection { get; set; }

        public string sConnection2 { get; set; }

        public FacadeInspiraServinte(string connection)
        {
            this.sConnection = connection;
        }

        public FacadeInspiraServinte()
        {

        }

        #region Métodos de base de datos

        /// <summary>
        /// Método para obtener el listado de tarifas por producto que se va a actualizar en Inspira
        /// </summary>
        /// <returns>Lista genérica de tarifas y productos</returns>
        public List<FNCEntity.TarifaProducto> ObtenerTarifasProductos()
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            DataTable dataTable = new DataTable();
            List<FNCEntity.TarifaProducto> lTarifaProducto = new List<FNCEntity.TarifaProducto>();
            FNCEntity.TarifaProducto tarifaProducto = null;
            try
            {
                servinteInspira.sconnection = this.sConnection;
                dataTable = servinteInspira.ObtenerTarifasServicios();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    tarifaProducto = new FNCEntity.TarifaProducto()
                    {
                        sproducto = dataRow["TP_PRODUCTO"].ToString(),
                        starifa = dataRow["TP_TARIFA"].ToString(),
                        sconcepto = dataRow["TP_CONCEPTO"].ToString(),
                        scentro = dataRow["TP_CENTROCOSTOS"].ToString(),
                        iedicion = Convert.ToInt32(dataRow["TP_EDICION"]),
                        isincronizado = Convert.ToInt32(dataRow["TP_SINCRONIZADO"]),
                        ivalor = Convert.ToInt32(dataRow["TP_VALOR"]),
                        dfecha = Convert.ToDateTime(dataRow["TP_FECHA"]),
                        iid = Convert.ToInt32(dataRow["TP_ID"]),
                    };
                    lTarifaProducto.Add(tarifaProducto);
                }
                return lTarifaProducto;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                tarifaProducto = null;
                servinteInspira.Dispose();
                servinteInspira = null;
            }
        }

        public List<FNCEntity.InspiraTemporal> GetAuthorizations()
        {
            DataTable dataTable = new DataTable();
            List<FNCEntity.InspiraTemporal> inspiraTemporals = new List<FNCEntity.InspiraTemporal>();
            ServinteOracle servinteOracle = new ServinteOracle();
            FNCEntity.InspiraTemporal inspiraTemporal = null;
            try
            {
                servinteOracle.sconnection = this.sConnection;
                dataTable = servinteOracle.GetAuthorizationsDetail();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    inspiraTemporal = new FNCEntity.InspiraTemporal()
                    {
                        scod = dataRow["PACTID"].ToString(),
                        snombre = dataRow["PACIDE"].ToString(),
                        sparametro1 = dataRow["CARDETCOD"].ToString(),
                        sparametro2 = dataRow["MOVOTRPLA"].ToString(),
                        stabla = dataRow["ORDDETDOC"].ToString(),
                    };
                    inspiraTemporals.Add(inspiraTemporal);
                }
                return inspiraTemporals;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                inspiraTemporal = null;
                dataTable.Dispose();
                dataTable = null;
                servinteOracle.Dispose();
                servinteOracle = null;
            }
        }

        public List<FNCEntity.InspiraTemporal> GetUserTypes()
        {
            DataTable dataTable = new DataTable();
            List<FNCEntity.InspiraTemporal> inspiraTemporals = new List<FNCEntity.InspiraTemporal>();
            ServinteOracle servinteOracle = new ServinteOracle();
            FNCEntity.InspiraTemporal inspiraTemporal = null;
            try
            {
                servinteOracle.sconnection = this.sConnection;
                dataTable = servinteOracle.GetUserTypes();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    inspiraTemporal = new FNCEntity.InspiraTemporal()
                    {
                        scod = dataRow["PAGEMP"].ToString(),
                        snombre = dataRow["PAGPLA"].ToString(),
                        sparametro1 = dataRow["PAGTUS"].ToString(),
                        sparametro2 = dataRow["PAGNIV"].ToString(),
                    };
                    inspiraTemporals.Add(inspiraTemporal);
                }
                return inspiraTemporals;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                inspiraTemporal = null;
                dataTable.Dispose();
                dataTable = null;
                servinteOracle.Dispose();
                servinteOracle = null;
            }
        }

        public List<FNCEntity.InspiraTemporal> ObtenerTarifaEmpresa()
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            DataTable dataTable = new DataTable();
            List<FNCEntity.InspiraTemporal> ltarifaempresa = new List<FNCEntity.InspiraTemporal>();
            FNCEntity.InspiraTemporal inspiraTemporal = null;
            try
            {
                servinteInspira.sconnection = this.sConnection;
                dataTable = servinteInspira.ObtenerTarifaEmpresa();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    inspiraTemporal = new FNCEntity.InspiraTemporal()
                    {
                        scod = dataRow["TE_EMPRESA"].ToString(),
                        sparametro1 = dataRow["TE_TARIFA"].ToString(),
                        sparametro2 = dataRow["TE_PLAN"].ToString(),
                        cactivo = Convert.ToChar(dataRow["TE_ACTIVO"]),   
                        santerior = dataRow["TE_EANTERIOR"].ToString(),
                        snombre = dataRow["TE_TANTERIOR"].ToString(),
                        stabla = dataRow["TE_PANTERIOR"].ToString(),
                        soperacion = dataRow["TE_OPERACION"].ToString(),
                        iid = Convert.ToInt32(dataRow["TE_ID"]),
                    };
                    ltarifaempresa.Add(inspiraTemporal);
                }
                return ltarifaempresa;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                inspiraTemporal = null;
                servinteInspira.Dispose();
                servinteInspira = null;
            }
        }

        /// <summary>
        /// Método para obtener los centros de costo por unidad funcional
        /// </summary>
        /// <returns>Lista genérica con los centros de costo por unidad funcional</returns>
        public List<FNCEntity.Generic> ObtenerUnidadFuncionalCentro()
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            DataTable dataTable = new DataTable();
            List<FNCEntity.Generic> lUnidadCentro = new List<FNCEntity.Generic>();
            FNCEntity.Generic generic = null;
            try
            {
                servinteInspira.sconnection = this.sConnection;
                dataTable = servinteInspira.ObtenerUnidadFuncionalCentro();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    generic = new FNCEntity.Generic()
                    {
                        scode = dataRow["CU_UNIDAD"].ToString(),
                        sname = dataRow["CU_CENTRO"].ToString(),
                        iid = Convert.ToInt32(dataRow["CU_ID"]),
                    };
                    lUnidadCentro.Add(generic);
                }
                return lUnidadCentro;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                generic = null;
                servinteInspira.Dispose();
                servinteInspira = null;
            }
        }

        /// <summary>
        /// Método para obtener los descuentos por tarifa
        /// </summary>
        /// <returns>Lista genérica con los descuentos por convenio, tarifa y concepto</returns>
        public List<FNCEntity.InspiraTemporal> ObtenerDescuentoTarifa()
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            DataTable dataTable = new DataTable();
            List<FNCEntity.InspiraTemporal> ltarifadescuento = new List<FNCEntity.InspiraTemporal>();
            FNCEntity.InspiraTemporal inspiraTemporal = null;
            try
            {
                servinteInspira.sconnection = this.sConnection;
                dataTable = servinteInspira.ObtenerDescuentoTarifa();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    inspiraTemporal = new FNCEntity.InspiraTemporal()
                    {
                        scod = dataRow["DT_CONVENIO"].ToString(),
                        sparametro1 = dataRow["DT_TARIFA"].ToString(),
                        sparametro2 = dataRow["DT_CONCEPTO"].ToString(),
                        cactivo = Convert.ToChar(dataRow["DT_ACTIVO"]),
                        iparametro3 = Convert.ToInt32(dataRow["DT_PORCENTAJE"]),
                        iid = Convert.ToInt32(dataRow["DT_ID"]),
                    };
                    ltarifadescuento.Add(inspiraTemporal);
                }
                return ltarifadescuento;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                inspiraTemporal = null;
                servinteInspira.Dispose();
                servinteInspira = null;
            }
        }

        /// <summary>
        /// Método para obtener la información que aún no se ha sincronizado con Inspira
        /// </summary>
        /// <returns></returns>
        public List<FNCEntity.InspiraTemporal> ObtenerInspiraTemporal()
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            DataTable dataTable = new DataTable();
            List<FNCEntity.InspiraTemporal> lServinteInspira = new List<FNCEntity.InspiraTemporal>();
            FNCEntity.InspiraTemporal inspiraTemporal = null;
            try
            {
                servinteInspira.sconnection = this.sConnection;
                dataTable = servinteInspira._ObtenerInspiraTemporal();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    inspiraTemporal = new FNCEntity.InspiraTemporal()
                    {
                        snombre = dataRow["IT_NOMBRE"].ToString(),
                        scod = dataRow["IT_COD"].ToString(),
                        sparametro1 = dataRow["IT_PARAMETRO1"].ToString(),
                        sparametro2 = dataRow["IT_PARAMETRO2"].ToString(),
                        iparametro3 = (dataRow["IT_PARAMETRO3"] != DBNull.Value) ? Convert.ToInt32(dataRow["IT_PARAMETRO3"]) : 0,
                        iid = (dataRow["IT_CONSECUTIVO"] != DBNull.Value) ? Convert.ToInt32(dataRow["IT_CONSECUTIVO"]) : 0,
                        cactivo = (dataRow["IT_ACTIVO"] != DBNull.Value) ? Convert.ToChar(dataRow["IT_ACTIVO"]) : 'S',
                        stabla = dataRow["IT_TABLA"].ToString(),
                        lconsecutivo = Convert.ToInt64(dataRow["IT_CONSECUTIVO"]),
                        iedicion = (dataRow["IT_ANTERIOR"] != DBNull.Value) ? 1 : 0,
                        santerior = dataRow["IT_ANTERIOR"].ToString(),
                    };
                    lServinteInspira.Add(inspiraTemporal);
                }
                return lServinteInspira;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                dataTable.Dispose();
                dataTable = null;
                inspiraTemporal = null;
                servinteInspira.Dispose();
                servinteInspira = null;
            }
        }

        public void IngresaValoresCreados(List<FNCEntity.InspiraTemporal> lFinal)
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            try
            {
                servinteInspira.sconnection = this.sConnection2;
                foreach (var item in lFinal)
                {
                    servinteInspira.CreaRegistrosMaestros(item.stabla, item);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                servinteInspira.Dispose();
                servinteInspira = null;
            }
        }

        public void ActualizaEstadoTablaTemporal(List<FNCEntity.InspiraTemporal> inspiraTemporals)
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            try
            {
                servinteInspira.sconnection = this.sConnection;
                if (inspiraTemporals.FindAll(x => x.stabla == "TARIFA").Count > 0)
                {
                    servinteInspira.ActualizaEstadoInspiraTemporal("Tarifas");
                }
                if (inspiraTemporals.FindAll(x => x.stabla == "CONVENIO").Count > 0)
                {
                    servinteInspira.ActualizaEstadoInspiraTemporal("Convenios");
                }
                if (inspiraTemporals.FindAll(x => x.stabla == "CENTROCOSTO").Count > 0)
                {
                    servinteInspira.ActualizaEstadoInspiraTemporal("Centros de Costo");
                }
                if (inspiraTemporals.FindAll(x => x.stabla == "PLAN").Count > 0)
                {
                    servinteInspira.ActualizaEstadoInspiraTemporal("Plan");
                }
                if (inspiraTemporals.FindAll(x => x.stabla == "PRODUCTO").Count > 0)
                {
                    servinteInspira.ActualizaEstadoInspiraTemporal("Produto");
                }
                if (inspiraTemporals.FindAll(x => x.stabla == "CONCEPTO").Count > 0)
                {
                    servinteInspira.ActualizaEstadoInspiraTemporal("Concepto");
                }
                if (inspiraTemporals.FindAll(x => x.stabla == "UNIDAD").Count > 0)
                {
                    servinteInspira.ActualizaEstadoInspiraTemporal("Unidad Funcional");
                }                                
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;                
            }
            finally
            {
                servinteInspira.Dispose();
                servinteInspira = null;
            }
        }

        /// <summary>
        /// Método que actualiza el estado de los registros que ya han sido sincronizados
        /// </summary>
        /// <param name="lInspiraTemporal">Lista genérica objeto temporal de integración</param>
        /// <param name="iestado">Entero estado</param>
        public void ActualizaEstadoTablaTemporal(List<FNCEntity.InspiraTemporal> lInspiraTemporal, int iestado)
        {
            ServinteInspira servinteInspira = new ServinteInspira();            
            StringBuilder stringBuilder = new StringBuilder();
            try
            {
                servinteInspira.sconnection = this.sConnection;
                for (int i = 0; i < lInspiraTemporal.Count; i++)
                {
                    stringBuilder.Append("SELECT ");
                    stringBuilder.Append(lInspiraTemporal[i].iid.ToString());
                    stringBuilder.Append(" FROM DUAL");
                    if (i < lInspiraTemporal.Count - 1)
                    {
                        stringBuilder.Append(" UNION ALL ");
                    }                    
                }
                servinteInspira.ActualizaEstadoInspiraTemporal(stringBuilder.ToString(), iestado);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                servinteInspira.Dispose();
                servinteInspira = null;                
                stringBuilder = null;
            }
        }

        public void TruncateTmpTable()
        {
            LogSincronizacion.TruncateSyncTmp(this.sConnection);
        }

        public void ActualizaTarifaEmpresa(int iestado)
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            try
            {
                servinteInspira.sconnection = this.sConnection;                
                servinteInspira.ActualizaEstadoTarifaConvenio(iestado);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                servinteInspira.Dispose();
                servinteInspira = null;                
            }
        }

        public void ActualizaRelacion(string stabla)
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            StringBuilder stringBuilder = new StringBuilder();
            try
            {
                servinteInspira.sconnection = this.sConnection;
                servinteInspira.ActualizaRelacionesTemporal(stabla);                
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                servinteInspira.Dispose();
                servinteInspira = null;
                stringBuilder = null;
            }
        }

        /// <summary>
        /// Método para actualizar el valor de las relaciones en la base de datos
        /// </summary>
        /// <param name="lobjetos">Lista genérica de objetos</param>
        /// <param name="iestado">Entero estado de la sincronización</param>
        /// <param name="sobjeto">String objeto a actualizar</param>
        public void ActualizaRelacion(object lobjetos, int iestado, string sobjeto)
        {
            ServinteInspira servinteInspira = new ServinteInspira();
            StringBuilder stringBuilder = new StringBuilder();
            try
            {
                servinteInspira.sconnection = this.sConnection;
                if (sobjeto == "TarifaProducto")
                {
                    List<FNCEntity.TarifaProducto> ltarifaProductos = lobjetos as List<FNCEntity.TarifaProducto>;
                    for (int i = 0; i < ltarifaProductos.Count; i++)
                    {
                        stringBuilder.Append("SELECT ");
                        stringBuilder.Append(ltarifaProductos[i].iid.ToString());
                        stringBuilder.Append(" FROM DUAL");
                        if (i < ltarifaProductos.Count - 1)
                        {
                            stringBuilder.Append(" UNION ALL ");
                        }                                                
                    }
                    servinteInspira.ActualizaEstadoTarifaProducto(stringBuilder.ToString(), iestado);
                }                
                if (sobjeto == "UnidadCentro")
                {
                    List<FNCEntity.Generic> lgenerica = lobjetos as List<FNCEntity.Generic>;
                    for (int i = 0; i < lgenerica.Count; i++)
                    {
                        stringBuilder.Append("SELECT ");
                        stringBuilder.Append(lgenerica[i].iid.ToString());
                        stringBuilder.Append(" FROM DUAL");
                        if (i < lgenerica.Count - 1)
                        {
                            stringBuilder.Append(" UNION ALL ");
                        }
                    }
                    servinteInspira.ActualizaCentroUnidad(stringBuilder.ToString(), iestado);
                }
                if (sobjeto == "DescuentoTarifa")
                {
                    List<FNCEntity.InspiraTemporal> lgenerica = lobjetos as List<FNCEntity.InspiraTemporal>;
                    for (int i = 0; i < lgenerica.Count; i++)
                    {
                        stringBuilder.Append("SELECT ");
                        stringBuilder.Append(lgenerica[i].iid.ToString());
                        stringBuilder.Append(" FROM DUAL");
                        if (i < lgenerica.Count - 1)
                        {
                            stringBuilder.Append(" UNION ALL ");
                        }
                    }
                    servinteInspira.ActualizaDescuentoTarifa(stringBuilder.ToString(), iestado);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                servinteInspira.Dispose();
                servinteInspira = null;
                stringBuilder = null;
            }
        }

        
        public List<FNCEntity.InspiraTemporal> ObtenerTablasInspira()
        {
            Integrador integrador = new Integrador();            
            try
            {
                integrador.sconnection = this.sConnection;
                return integrador.GetInspiraTables();
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                integrador.Dispose();
                integrador = null;
            }

        }

        public List<FNCEntity.InspiraCita> GetTodayCharges()
        {

            List<FNCEntity.InspiraCita> linspiraCitas = new List<FNCEntity.InspiraCita>();
            DataTable dataTable = new DataTable();
            ServinteOracle oracle = new ServinteOracle();
            FNCEntity.InspiraCita inspiraCita = null;            
            try
            {
                oracle.sconnection = this.sConnection;
                dataTable = oracle.GetTodayCharges();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    inspiraCita = new FNCEntity.InspiraCita()
                    {
                        sagreement = dataRow["MOVCER"].ToString(),
                        sauthorization = dataRow["ORDDETORD"].ToString(),
                        scostcenter = dataRow["CARDETCCO"].ToString(),
                        sname = dataRow["PACIDE"].ToString(),
                        suser = dataRow["PACTID"].ToString(),
                        sservicegroup = dataRow["CARDETCON"].ToString(),
                        sunit = dataRow["CARDETUFU"].ToString(),
                        scie10 = dataRow["CARDETCOD"].ToString(),
                        scontract = dataRow["MOVUAD"].ToString(),
                    };
                    linspiraCitas.Add(inspiraCita);
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("FNCInspira", "Facade", ex);
            }
            finally
            {
                oracle.Dispose();
                oracle = null;
                inspiraCita = null;
            }
            return linspiraCitas;
        }

        public List<FNCEntity.Charge> GetChargesWithoutSupport(string scompany)
        {
            ServinteOracle servinteOracle = new ServinteOracle();
            servinteOracle.sconnection = this.sConnection;
            try
            {
                return servinteOracle.GetChargesWithoutSupport(scompany);
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "Facade", ex);
                throw;
            }
            finally
            {
                servinteOracle.Dispose();
                servinteOracle=null;
            }
        }

        public List<FNCEntity.Generic> CreateChargeSupports(Charge charge, List<FNCEntity.Generic> list)
        {
            ServinteOracle servinteOracle = new ServinteOracle();
            servinteOracle.sconnection = this.sConnection;
            try
            {
                return servinteOracle.CreateChargeSupports(charge, list);
            }
            catch (Exception ex)
            {
                LogError.WriteError("CreaSoporteCargos", "Facade", ex);
                throw;
            }
            finally
            {
                servinteOracle.Dispose();
                servinteOracle = null;
            }
        }

        #endregion

        #region Métodos de servicios web

        /// <summary>
        /// Método para actualizar los objetos bases en Inspira
        /// </summary>
        /// <param name="linspiratemporal"></param>
        /// <param name="sobject"></param>
        /// <returns>Lista genérica inspira temporal</returns>
        public List<FNCEntity.InspiraTemporal> ActualizaInspira(List<FNCEntity.InspiraTemporal> linspiratemporal, string sobject)
        {
            WSDigiturno wSDigiturno = null;
            FNCESB.InspiraTemporal[] inspiraTemporals = null;
            FNCESB.InspiraTemporal[] inspiraResult = null;
            try
            {
                inspiraTemporals = this.ConvertList(linspiratemporal);
                wSDigiturno = new WSDigiturno();
                wSDigiturno.Timeout = FNCFacade.Properties.Settings.Default.ServiceTimeOut;
                inspiraResult = wSDigiturno.UpdateInspiraObject(inspiraTemporals, sobject);
                return this.CastInspiraTemporal(inspiraResult);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                inspiraTemporals = null;
                wSDigiturno.Dispose();
                wSDigiturno = null;
            }
        }


        /// <summary>
        /// Método para enviar a Inspira las tarifas por producto a crear o actualizar.
        /// </summary>
        /// <param name="ltarifaProductos">Lista genérica de tarifas y productos</param>
        public void ActualizaTarifasInspira(List<FNCEntity.TarifaProducto> ltarifaProductos)
        {
            WSDigiturno wSDigiturno = null;
            FNCESB.TarifaProducto[] tarifaProductos = null;
            try
            {
                tarifaProductos = this.ConvertirListaTarifas(ltarifaProductos);
                wSDigiturno = new WSDigiturno();
                wSDigiturno.Timeout = FNCFacade.Properties.Settings.Default.ServiceTimeOut;
                wSDigiturno.UpdateProductsByRate(tarifaProductos);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                tarifaProductos = null;
                wSDigiturno.Dispose();
                wSDigiturno = null;
            }
        }

        public void ActualizaDescuentoTarifa(List<FNCEntity.InspiraTemporal> ldescuentoTarifa)
        {
            WSDigiturno wSDigiturno = null;
            FNCESB.InspiraTemporal[] inspiraTemporals = null;
            try
            {
                inspiraTemporals = this.ConvertList(ldescuentoTarifa);
                wSDigiturno = new WSDigiturno();
                wSDigiturno.Timeout = FNCFacade.Properties.Settings.Default.ServiceTimeOut;
                wSDigiturno.UpdateDiscountsByRate(inspiraTemporals);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                inspiraTemporals = null;
                wSDigiturno.Dispose();
                wSDigiturno = null;
            }
        }

        public void ActualizaTarifaEmpresaInspira(List<FNCEntity.InspiraTemporal> ltarifaEmpresas)
        {
            WSDigiturno wSDigiturno = null;
            FNCESB.InspiraTemporal[] inspiraTemporals = null;
            try
            {
                inspiraTemporals = this.ConvertList(ltarifaEmpresas);
                wSDigiturno = new WSDigiturno();
                wSDigiturno.Timeout = FNCFacade.Properties.Settings.Default.ServiceTimeOut;
                wSDigiturno.UpdateRatesByAgreement(inspiraTemporals);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                inspiraTemporals = null;
                wSDigiturno.Dispose();
                wSDigiturno = null;
            }
        }

        public void ActualizaCentroUnidad(List<FNCEntity.Generic> lgeneric)
        {
            WSDigiturno wSDigiturno = null;
            FNCESB.Generic[] generic = null;
            try
            {
                generic = this.ConvertirListaGenerica(lgeneric);
                wSDigiturno = new WSDigiturno();
                wSDigiturno.Timeout = FNCFacade.Properties.Settings.Default.ServiceTimeOut;
                wSDigiturno.UpdateCostcentersByUnit(generic);
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                wSDigiturno.Dispose();
                wSDigiturno = null;
                generic = null;
            }
        }

        private FNCESB.Generic[] ConvertirListaGenerica(List<FNCEntity.Generic> lgenerica)
        {
            FNCESB.Generic[] generics = new FNCESB.Generic[lgenerica.Count];
            FNCESB.Generic generic = null;
            for (int i = 0; i < lgenerica.Count; i++)
            {
                generic = Tools.Cast<FNCESB.Generic>(lgenerica[i]);
                generics[i] = generic;
            }
            return generics;
        }
        

        /// <summary>
        /// Método para convertir la lista genérica de tarifas y productos en un array de tarifas y productos reconocido por el servicio web de integración
        /// </summary>
        /// <param name="ltarifaProductos">Lista genérica de tarifas y productos</param>
        /// <returns>Arreglo de tarifas y productos</returns>
        private FNCESB.TarifaProducto[] ConvertirListaTarifas(List<FNCEntity.TarifaProducto> ltarifaProductos)
        {
            FNCESB.TarifaProducto[] tarifaProductos = new FNCESB.TarifaProducto[ltarifaProductos.Count];
            FNCESB.TarifaProducto tarifaProducto = null;
            for (int i = 0; i < ltarifaProductos.Count; i++)
            {
                tarifaProducto = Tools.Cast<FNCESB.TarifaProducto>(ltarifaProductos[i]);
                tarifaProductos[i] = tarifaProducto;
            }
            return tarifaProductos;
        }

        private FNCESB.InspiraTemporal[] ConvertList(List<FNCEntity.InspiraTemporal> linspiratemporal)
        {
            FNCESB.InspiraTemporal[] inspiraTemporals = new FNCESB.InspiraTemporal[linspiratemporal.Count];
            FNCESB.InspiraTemporal inspiraTemporal = null;
            int i = 0;
            foreach (FNCEntity.InspiraTemporal item in linspiratemporal)
            {
                inspiraTemporal = Tools.Cast<FNCESB.InspiraTemporal>(item);
                inspiraTemporals[i] = inspiraTemporal;
                i++;
            }
            return inspiraTemporals;
        }

        /// <summary>
        /// Método para actualizar la cuenta del paciente con el dato de investigación
        /// </summary>
        /// <param name="lpacientes">Lista genérica objeto paciente</param>
        public void ActualizaCuentaInspra(List<Patient> lpacientes)
        {
            string[] aResponse = null;
            using (WSDigiturno wSDigiturno = new WSDigiturno())
            {
                foreach (Patient item in lpacientes)
                {
                    aResponse = new string[] { (item.istatus == 2) ? "Si" : "No" };
                    wSDigiturno.UpdateAccount(item.sdocument, item.sdocumenttype, aResponse);
                }
            }            
        }

        /// <summary>
        /// Método para generar ingresos de forma masiva en la tabla AYMOV de Servite e InspiraServinte en la BD de integración
        /// </summary>
        /// <param name="linspiraRequest">Lista genérica objeto integración</param>
        /// <returns>Objeto respuesta integración</returns>
        public InspiraServinteResponse GenerateEntries(List<InspiraRequest> linspiraRequest)
        {
            List<EntryResponse> lentryResponses = new List<EntryResponse>();
            InspiraServinteResponse inspiraServinteResponse = new InspiraServinteResponse();
            try
            {
                using (ServinteOracle servinteOracle = new ServinteOracle(this.sConnection))
                {
                    lentryResponses = servinteOracle.GenerateEntries(linspiraRequest);
                }
                if (lentryResponses.Count > 0)
                {
                    using (Integrador integrador = new Integrador())
                    {
                        integrador.sconnection = this.sConnection2;
                        lentryResponses = integrador.InsertRecord(lentryResponses);
                    }
                    inspiraServinteResponse.lentry = lentryResponses;
                }
                else
                {
                    throw new ApplicationException("No se han generado ingresos para la información enviada");
                }
            }
            catch (ApplicationException ex)
            {
                inspiraServinteResponse.error = new ErrorResponse()
                {
                    icode = 1,
                    smessage = ex.Message,
                };
            }
            catch (Exception ex)
            {
                inspiraServinteResponse.error = new ErrorResponse()
                {
                    icode = 2,
                    smessage = ex.Message,
                };
            }
            return inspiraServinteResponse;
        }

        /// <summary>
        /// Método para generar el ingreso en las tablas de servinte y las tablas de integración
        /// </summary>
        /// <param name="inspiraRequest">Objeto integración Inspira</param>
        /// <param name="bloadestatistics">Boolean que indica si el proceso se carga a la estadística</param>
        /// <returns>Objeto respuesta integración</returns>
        public InspiraServinteResponse GenerateEntry(InspiraRequest inspiraRequest, bool bloadstatistics = true)
        {
            List<EntryResponse> lentryResponses = new List<EntryResponse>();
            InspiraServinteResponse inspiraServinteResponse = new InspiraServinteResponse();
            try
            {
                using (ServinteOracle servinteOracle = new ServinteOracle(this.sConnection))
                {
                    if (inspiraRequest.stype == "Servicio")
                    {
                        lentryResponses = servinteOracle.GenerateEntryForService(inspiraRequest);
                    }
                    else if (inspiraRequest.stype == "Investigacion")
                    {
                        lentryResponses = servinteOracle.GenerateEntryForInvestigation(inspiraRequest);
                    }
                    else if (inspiraRequest.stype == "Educacion")
                    {
                        lentryResponses = servinteOracle.GenerateEntryForEducation(inspiraRequest);
                    }
                    else if (inspiraRequest.stype == "Hospitalizacion")
                    {
                        lentryResponses = servinteOracle.CreateHospitalizationCharges(inspiraRequest.lpatients);
                    }
                    else if (inspiraRequest.stype == "Programas")
                    {
                        lentryResponses = servinteOracle.CreateEntryForPrograms(inspiraRequest.lpatients);
                    }
                    else if (inspiraRequest.stype == "Urgencias")
                    {
                        lentryResponses = servinteOracle.CreateEmergenciesCharges(inspiraRequest.lpatients);
                    }
                    else if (inspiraRequest.stype == "Fibrosis")
                    {
                        lentryResponses = servinteOracle.CreateFibrosisCharges(inspiraRequest.lpatients);
                    }
                    else if (inspiraRequest.stype == "Otros servicios")
                    {
                        lentryResponses = servinteOracle.CreatServicesCharges(inspiraRequest.lpatients);
                    }
                    else if (inspiraRequest.stype == "Valoraciones")
                    {
                        lentryResponses = servinteOracle.CreateValuationServices(inspiraRequest.lpatients);
                    }
                }
                inspiraServinteResponse.lentry = lentryResponses;
                if (lentryResponses.Count > 0 && bloadstatistics)
                {
                    using (Integrador integrador = new Integrador())
                    {
                        integrador.sconnection = this.sConnection2;
                        lentryResponses = integrador.InsertRecord(lentryResponses);
                    }                    
                    inspiraServinteResponse.lentry = lentryResponses;
                }
                else if (lentryResponses.Count == 0 && bloadstatistics)
                {
                    throw new ApplicationException("No se han generado ingresos para la información enviada");
                }
            }
            catch (ApplicationException ex)
            {
                inspiraServinteResponse.error = new ErrorResponse()
                {
                    icode = 1,
                    smessage = ex.Message,
                };
            }
            catch (Exception ex)
            {
                inspiraServinteResponse.error = new ErrorResponse()
                {
                    icode = 2,
                    smessage = ex.Message.Replace("\"", string.Empty).Replace("'", string.Empty),
                };                
            }
            return inspiraServinteResponse;
        }

        public List<FNCEntity.ServintePatient> GetPatientsForPrograms(int iyear, int imonth)
        {
            WSDigiturno wSDigiturno = null;
            List<FNCEntity.ServintePatient> lPatients = new List<FNCEntity.ServintePatient>();
            FNCESB.ServintePatient[] servintePatients = null;
            try
            {
                wSDigiturno = new WSDigiturno();
                servintePatients = wSDigiturno.GetAppointmentsForPrograms(iyear, imonth);
                foreach (FNCESB.ServintePatient item in servintePatients)
                {
                    lPatients.Add(Tools.Cast<FNCEntity.ServintePatient>(item));
                }
                return lPatients;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Integrador", "Facade", ex);
                throw;
            }
            finally
            {
                wSDigiturno.Dispose();
                wSDigiturno = null;
                servintePatients = null;
            }
        }

        public InspiraServinteResponse Update(InspiraRequest inspiraRequest, bool bloadstatistics)
        {
            List<EntryResponse> lentryResponses = new List<EntryResponse>();
            InspiraServinteResponse inspiraServinteResponse = new InspiraServinteResponse();
            try
            {
                using (ServinteOracle servinteOracle = new ServinteOracle(this.sConnection))
                {
                    lentryResponses = servinteOracle.UpdateEntry(inspiraRequest.lpatients[0].lappointments[0], inspiraRequest.lpatients[0], inspiraRequest.sid);
                }
                inspiraServinteResponse.lentry = lentryResponses;
                if (lentryResponses.Count > 0 && bloadstatistics)
                {
                    using (Integrador integrador = new Integrador())
                    {
                        integrador.sconnection = this.sConnection2;
                        lentryResponses = integrador.InsertRecord(lentryResponses);
                    }
                    inspiraServinteResponse.lentry = lentryResponses;
                }
            }
            catch (ApplicationException ex)
            {
                inspiraServinteResponse.error = new ErrorResponse()
                {
                    icode = 1,
                    smessage = ex.Message,
                };
            }
            catch (Exception ex)
            {
                inspiraServinteResponse.error = new ErrorResponse()
                {
                    icode = 2,
                    smessage = ex.Message.Replace("\"", string.Empty).Replace("'", string.Empty),
                };
            }
            return inspiraServinteResponse;
        }

        public bool SpecificEntryExists(FNCEntity.ServintePatient servintePatient, FNCEntity.InspiraCita inspiraCita, FNCEntity.ServiceRequest serviceRequest)
        {
            using (ServinteOracle servinteOracle = new ServinteOracle(this.sConnection))
            {
                // El objeto Oracle se crea aquí, pero no se conecta hasta que el método en ServinteOracle lo necesite.
                // Asumimos que el constructor de ServinteOracle no abre la conexión.
                Oracle oracle = new Oracle();
                oracle.sConnection = this.sConnection;
                return servinteOracle.SpecificEntryExists(servintePatient, inspiraCita, serviceRequest, oracle);
            }
        }

        /// <summary>
        /// (MÉTODO FALTANTE) Método de paso para invocar la validación específica de una autorización.
        /// </summary>
        public bool SpecificAuthorizationExists(string documentType, string document, string plan, string authorization, string serviceCode)
        {
            using (ServinteOracle servinteOracle = new ServinteOracle(this.sConnection))
            {
                Oracle oracle = new Oracle();
                oracle.sConnection = this.sConnection;
                return servinteOracle.SpecificAuthorizationExists(documentType, document, plan, authorization, serviceCode, oracle);
            }
        }
        #endregion

        #region Métodos auxiliares

        private List<FNCEntity.InspiraTemporal> CastInspiraTemporal(FNCESB.InspiraTemporal[] inspiraTemporals)
        {
            List<FNCEntity.InspiraTemporal> linspiraTemporal = new List<FNCEntity.InspiraTemporal>();            
            FNCEntity.InspiraTemporal inspiraTemporal = null;
            if (inspiraTemporals != null)
            {
                foreach (var item in inspiraTemporals)
                {
                    inspiraTemporal = new FNCEntity.InspiraTemporal()
                    {
                        scod = item.scod,
                        snombre = item.snombre,
                        sid = item.sid,
                    };
                    linspiraTemporal.Add(inspiraTemporal);
                }
                inspiraTemporal = null;
            }            
            return linspiraTemporal;
        }

        #endregion

        /// <summary>
        /// Método para eliminar el objeto
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            GC.Collect();
        }
    }
}
