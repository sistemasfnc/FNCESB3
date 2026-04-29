using EventLog;
using FNCEntity;
using FNCFacade;
using FNCSalesforce;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace FNCSincroniza
{
    class Integrador
    {
        static List<InspiraTemporal> linspiratemporal { get; set; }

        static FacadeInspiraServinte facadeInspiraServinte { get; set; }

        static List<InspiraTemporal> linspiraTablas { get; set; }

        static List<InspiraTemporal> linspiraActualizar { get; set; }

        static List<InspiraTemporal> lFinal { get; set; }

        static string sobject { get; set; }
        
        static Generic salesforcesession { get; set; }

        static SalesforceIntegrator salesforceIntegrator {  get; set; } 

        static void Main(string[] args)
        {            
            if (args != null)
            {
                sobject = args[0];
                try
                {
                    lFinal = new List<InspiraTemporal>();
                    linspiraActualizar = new List<InspiraTemporal>();
                    LogError.WriteMessage("Integrador", "Integrador", "Sincronizando maestros");
                    DoLogin();
                    ActualizaMaestros();
                    LogError.WriteMessage("Integrador", "Integrador", "Sincronizando relaciones");
                    ActualizaRelaciones();
                    LogError.WriteMessage("Integrador", "Integrador", "Sincronizando tarifas por concepto y producto");
                    if (sobject == "TarifaConcepto")
                    {
                        ProcesarTarifaConceptoProductos();
                    }
                    if (sobject == "ActualizaValores")
                    {
                        LogError.WriteMessage("Integrador", "Integrador", "Actualizando valores de tarifas");
                        ActualizarValoresTarifas();
                    }
                }
                catch (Exception ex)
                {
                    LogError.WriteError("Integrador", "Integrador", ex);
                }
            }            
        }

        static void DoLogin()
        {
            salesforceIntegrator = new SalesforceIntegrator();
            salesforcesession = salesforceIntegrator.Login(FNCSincroniza.Properties.Settings.Default.SalesforceCompany, FNCSincroniza.Properties.Settings.Default.SalesforceUser, 
                                                            FNCSincroniza.Properties.Settings.Default.SalesforcePassword, FNCSincroniza.Properties.Settings.Default.SalesforceToken); 
            if (salesforcesession != null)
            {
                salesforceIntegrator.sSession = salesforcesession.scode;
                salesforceIntegrator.sUrl = salesforcesession.sname;
                salesforceIntegrator.sConnection = FNCSincroniza.Properties.Settings.Default.OracleFNC;
            }
        }

        static void EliminaTemporal()
        {
            using (facadeInspiraServinte = new FacadeInspiraServinte(FNCSincroniza.Properties.Settings.Default.OracleFNC))
            {
                facadeInspiraServinte.TruncateTmpTable();
            }
        }

        static void ActualizaMaestros()
        {
            ObtenerTablasInspira();
            ObtenerListaTemporal();
            using (facadeInspiraServinte = new FacadeInspiraServinte())
            {

                try
                {
                    switch (sobject)
                    {
                        case "Convenios":
                            ProcesarConvenios();
                            break;
                        case "Planes":
                            ProcesarPlanes();
                            break;
                        case "Tarifas":
                            ProcesarTarifas();
                            break;
                        case "Centros":
                            ProcesarCentros();
                            break;
                        case "Conceptos":
                            ProcesarConceptos();
                            break;
                        case "Productos":
                            ProcesarProductos();
                            break;
                        default:
                            ProcesarUnidades();
                            break;
                    }
                    ActualizarEstadoListados();
                }
                catch (Exception ex)
                {
                    LogError.WriteError("Integrador", "Integrador", ex);
                }                                
            }
        }

        /// <summary>
        /// Método para actualizar los maestros de relaciones
        /// </summary>
        static void ActualizaRelaciones()
        {
            using (facadeInspiraServinte = new FacadeInspiraServinte(FNCSincroniza.Properties.Settings.Default.OracleFNC))
            {
                switch (sobject)
                {
                    case "UnidadFuncionalCentro":
                        List<FNCEntity.Generic> lgenerica = facadeInspiraServinte.ObtenerUnidadFuncionalCentro();
                        if (lgenerica.Count > 0 && salesforcesession != null)
                        {
                            LogError.WriteMessage("Integrador", "Integrador", "Sincronizando centros por unidad");
                            try
                            {
                                salesforceIntegrator.InsertCostUnit(lgenerica);
                                //facadeInspiraServinte.ActualizaCentroUnidad(lgenerica);
                                //facadeInspiraServinte.ActualizaRelacion("Centro de costo por unidad funcional");
                            }
                            catch (Exception ex)
                            {
                                LogError.WriteError("Integrador", "Integrador", ex);
                            }
                        }
                        break;
                    case "TarifaEmpresa":
                        List<FNCEntity.InspiraTemporal> ltarifaEmpresas = facadeInspiraServinte.ObtenerTarifaEmpresa();
                        if (ltarifaEmpresas.Count > 0 && salesforcesession != null)
                        {
                            LogError.WriteMessage("Integrador", "Integrador", "Sincronizando tarifas por empresa");
                            try
                            {
                                salesforceIntegrator.UpsertAgreementRates(ltarifaEmpresas);
                                //facadeInspiraServinte.ActualizaTarifaEmpresaInspira(ltarifaEmpresas);
                                facadeInspiraServinte.ActualizaRelacion("Tarifas por empresa");
                            }
                            catch (Exception ex)
                            {
                                LogError.WriteError("Integrador", "Integrador", ex);
                            }

                        }
                        break;
                    case "TarifaProducto":
                        List<FNCEntity.TarifaProducto> ltarifaProductos = facadeInspiraServinte.ObtenerTarifasProductos();
                        if (ltarifaProductos.Count > 0 && salesforcesession != null)
                        {
                            LogError.WriteMessage("Integrador", "Integrador", "Sincronizando productos por tarifa");
                            try
                            {
                                //facadeInspiraServinte.ActualizaTarifasInspira(ltarifaProductos);
                                salesforceIntegrator.UpsertProductRates(ltarifaProductos);
                                facadeInspiraServinte.ActualizaRelacion("Productos por tarifa");
                            }
                            catch (Exception ex)
                            {
                                LogError.WriteError("Integrador", "Integrador", ex);
                            }

                        }                        
                        break;
                        default:
                            EliminaTemporal();
                        break;
                }                                    
            }
        }

        /// <summary>
        /// Método para obtener todos los registros que aún no se han sincronizado con Inspira
        /// </summary>
        static void ObtenerListaTemporal()
        {
            using (facadeInspiraServinte = new FacadeInspiraServinte(FNCSincroniza.Properties.Settings.Default.OracleFNC))
            {
                linspiratemporal = facadeInspiraServinte.ObtenerInspiraTemporal();
            }
        }

        static void ObtenerTablasInspira()
        {
            using (facadeInspiraServinte = new FacadeInspiraServinte(FNCSincroniza.Properties.Settings.Default.InspiraAlejus))
            {
                linspiraTablas = facadeInspiraServinte.ObtenerTablasInspira();
            }
        }
        

        /// <summary>
        /// Método que actualiza el estado del envío de los registros a integrar
        /// </summary>
        static void ActualizarEstadoListados()
        {
            using (facadeInspiraServinte = new FacadeInspiraServinte(FNCSincroniza.Properties.Settings.Default.OracleFNC))
            {
                facadeInspiraServinte.sConnection2 = FNCSincroniza.Properties.Settings.Default.InspiraAlejus;
                facadeInspiraServinte.IngresaValoresCreados(lFinal);
                //facadeInspiraServinte.ActualizaEstadoTablaTemporal(linspiraActualizar);
            }
        }

        
        /// <summary>
        /// Método que envía las tarifas
        /// </summary>
        static void ProcesarTarifas()
        {
            List<InspiraTemporal> lTarifas = linspiratemporal.FindAll(x => x.stabla == "TARIFA").GroupBy(y => y.scod).Select(z => z.First()).ToList();
            List<InspiraTemporal> lInspiraTarifas = linspiraTablas.FindAll(x => x.stabla == "Tarifa");
            List<InspiraTemporal> lResultado = null;
            InspiraTemporal inspiraTemporal = null;
            int i = 0;
            if (lTarifas.Count > 0)
            {
                /*foreach (var item in lTarifas)
                {
                    inspiraTemporal = lInspiraTarifas.FirstOrDefault(x => x.scod == item.scod);
                    if (inspiraTemporal != null)
                    {
                        lTarifas[i].iedicion = 1;
                        lTarifas[i].sid = inspiraTemporal.santerior;
                    }
                    i++;
                }*/
                if (salesforcesession != null)
                {
                    lResultado = salesforceIntegrator.UpsertRates(lTarifas);
                    i = 0;
                    foreach (var item in lResultado)
                    {
                        if (lInspiraTarifas.FirstOrDefault(x => x.santerior == item.sid) == null)
                        {
                            inspiraTemporal = new InspiraTemporal()
                            {
                                stabla = "Rate__c",
                                sid = item.sid,
                                scod = item.scod,
                                snombre = item.snombre
                            };
                            lFinal.Add(inspiraTemporal);
                        }
                        lResultado[i].stabla = "TARIFA";
                        i++;
                    }
                    linspiraActualizar.AddRange(lResultado);
                }
                
            }
            inspiraTemporal = null;
            lResultado = null;
            lTarifas = null;
            lInspiraTarifas = null;
        }

        /// <summary>
        /// Método que envía los convenios
        /// </summary>
        static void ProcesarConvenios()
        {
            List<InspiraTemporal> lConvenios = linspiratemporal.FindAll(x => x.stabla == "CONVENIO").GroupBy(y => y.scod).Select(z => z.First()).ToList();
            List<InspiraTemporal> lInspiraConvenios = linspiraTablas.FindAll(x => x.stabla == "Convenio");
            List<InspiraTemporal> lResultado = null;            
            InspiraTemporal inspiraTemporal = null;
            int i = 0;
            if (lConvenios.Count > 0)
            {
                /*foreach (var item in lConvenios)
                {
                    inspiraTemporal = lInspiraConvenios.FirstOrDefault(x => x.scod == item.scod);
                    if (inspiraTemporal != null)
                    {
                        lConvenios[i].iedicion = 1;
                        lConvenios[i].sid = inspiraTemporal.santerior;
                    }
                    i++;
                }*/
                if (salesforcesession != null)
                {
                    lResultado = salesforceIntegrator.ActualizaConvenios(lConvenios);
                    foreach (var item in lResultado)
                    {
                        if (lInspiraConvenios.FirstOrDefault(x => x.santerior == item.sid) == null)
                        {
                            inspiraTemporal = new InspiraTemporal()
                            {
                                stabla = "Agreement__c",
                                sid = item.sid,
                                scod = item.scod,
                                snombre = item.snombre
                            };
                            lFinal.Add(inspiraTemporal);
                        }
                        lResultado[i].stabla = "CONVENIO";
                        i++;
                    }
                    linspiraActualizar.AddRange(lResultado);
                }                                
            }
            inspiraTemporal = null;
            lResultado = null;
            lConvenios = null;
            lInspiraConvenios = null;
        }

        /// <summary>
        /// Método que envía los centros de costo
        /// </summary>
        static void ProcesarCentros()
        {
            List<InspiraTemporal> lCentros = linspiratemporal.FindAll(x => x.stabla == "CENTROCOSTO").GroupBy(y => y.scod).Select(z => z.First()).ToList();
            List<InspiraTemporal> lInspiraCentros = linspiraTablas.FindAll(x => x.stabla == "CentroCosto");
            List<InspiraTemporal> lResultado = null;
            InspiraTemporal inspiraTemporal = null;
            int i = 0;
            if (lCentros.Count > 0)
            {
                /*
                foreach (var item in lCentros)
                {
                    inspiraTemporal = lInspiraCentros.FirstOrDefault(x => x.scod == item.scod);
                    if (inspiraTemporal != null)
                    {
                        lCentros[i].iedicion = 1;
                        lCentros[i].sid = inspiraTemporal.santerior;
                    }
                    i++;
                }*/
                if (salesforcesession != null)
                {
                    lResultado = salesforceIntegrator.UpsertCostCenters(lCentros);
                    i = 0;
                    foreach (var item in lResultado)
                    {
                        if (lInspiraCentros.FirstOrDefault(x => x.santerior == item.sid) == null)
                        {
                            inspiraTemporal = new InspiraTemporal()
                            {
                                stabla = "CostCenter__c",
                                sid = item.sid,
                                scod = item.scod,
                                snombre = item.snombre
                            };
                            lFinal.Add(inspiraTemporal);
                        }
                        lResultado[i].stabla = "CENTROCOSTO";
                        i++;
                    }
                    linspiraActualizar.AddRange(lResultado);
                }
            }
            inspiraTemporal = null;
            lResultado = null;
            lCentros = null;
            lInspiraCentros = null;
        }

        /// <summary>
        /// Método que envía los planes
        /// </summary>
        static void ProcesarPlanes()
        {
            List<InspiraTemporal> lPlanes = linspiratemporal.FindAll(x => x.stabla == "PLAN").GroupBy(y => y.scod).Select(z => z.First()).ToList();
            List<InspiraTemporal> lInspiraPlanes = linspiraTablas.FindAll(x => x.stabla == "Plan");
            List<InspiraTemporal> lResultado = null;
            InspiraTemporal inspiraTemporal = null;
            int i = 0;
            if (lPlanes.Count > 0)
            {
                /*
                foreach (var item in lPlanes)
                {
                    inspiraTemporal = lInspiraPlanes.FirstOrDefault(x => x.scod == item.scod);
                    if (inspiraTemporal != null)
                    {
                        lPlanes[i].iedicion = 1;
                        lPlanes[i].sid = inspiraTemporal.santerior;
                    }
                    i++;
                }
                */
                if (salesforcesession != null)
                {
                    lResultado = salesforceIntegrator.UpsertHealthCarePlan(lPlanes);
                    i = 0;
                    foreach (var item in lResultado)
                    {
                        if (lInspiraPlanes.FirstOrDefault(x => x.santerior == item.sid) == null)
                        {
                            inspiraTemporal = new InspiraTemporal()
                            {
                                stabla = "HealthCarePlan__c",
                                sid = item.sid,
                                scod = item.scod,
                                snombre = item.snombre
                            };
                            lFinal.Add(inspiraTemporal);
                        }
                        lResultado[i].stabla = "PLAN";
                        i++;
                    }
                    linspiraActualizar.AddRange(lResultado);
                }
            }
            inspiraTemporal = null;
            lResultado = null;
            lPlanes = null;
            lInspiraPlanes = null;
        }

        /// <summary>
        /// Método que envía los productos
        /// </summary>
        static void ProcesarProductos()
        {
            List<InspiraTemporal> lProductos = linspiratemporal.FindAll(x => x.stabla == "SERVICIO").GroupBy(y => y.scod).Select(z => z.First()).ToList();
            List<InspiraTemporal> lInspiraProductos = linspiraTablas.FindAll(x => x.stabla == "Producto");
            List<InspiraTemporal> lResultado = null;
            InspiraTemporal inspiraTemporal = null;
            int i = 0;
            if (lProductos.Count > 0)
            {
                /*
                foreach (var item in lProductos)
                {
                    inspiraTemporal = lInspiraProductos.FirstOrDefault(x => x.scod == item.scod);
                    if (inspiraTemporal != null)
                    {
                        lProductos[i].iedicion = 1;
                        lProductos[i].sid = inspiraTemporal.santerior;
                    }
                    i++;
                }*/
                if (salesforcesession != null)
                {
                    lResultado = facadeInspiraServinte.ActualizaInspira(lProductos, "Producto");
                    i = 0;
                    foreach (var item in lResultado)
                    {
                        if (lInspiraProductos.FirstOrDefault(x => x.santerior == item.sid) == null)
                        {
                            inspiraTemporal = new InspiraTemporal()
                            {
                                stabla = "Product__c",
                                sid = item.sid,
                                scod = item.scod,
                                snombre = item.snombre
                            };
                            lFinal.Add(inspiraTemporal);
                        }
                        lResultado[i].stabla = "PRODUCTO";
                        i++;
                    }
                    linspiraActualizar.AddRange(lResultado);
                }
                
            }
            inspiraTemporal = null;
            lResultado = null;
            lProductos = null;
            lInspiraProductos = null;
        }

        /// <summary>
        /// Método que envía los conceptos
        /// </summary>
        static void ProcesarConceptos()
        {
            List<InspiraTemporal> lconceptos = linspiratemporal.FindAll(x => x.stabla == "CONCEPTO");
            List<InspiraTemporal> lInspiraConceptos = linspiraTablas.FindAll(x => x.stabla == "Concepto");
            List<InspiraTemporal> lResultado = null;
            InspiraTemporal inspiraTemporal = null;
            int i = 0;
            if (lconceptos.Count > 0)
            {
                foreach (var item in lconceptos)
                {
                    inspiraTemporal = lInspiraConceptos.FirstOrDefault(x => x.scod == item.scod);
                    if (inspiraTemporal != null)
                    {
                        lconceptos[i].iedicion = 1;
                        lconceptos[i].sid = inspiraTemporal.santerior;
                    }
                    i++;
                }
                lResultado = facadeInspiraServinte.ActualizaInspira(lconceptos, "Concepto");                
                i = 0;
                foreach (var item in lResultado)
                {
                    if (lInspiraConceptos.FirstOrDefault(x => x.santerior == item.sid) == null)
                    {
                        inspiraTemporal = new InspiraTemporal()
                        {
                            stabla = "Concept__c",
                            sid = item.sid,
                            scod = item.scod,
                            snombre = item.snombre
                        };
                        lFinal.Add(inspiraTemporal);
                    }
                    lResultado[i].stabla = "CONCEPTO";                    
                    i++;
                }
                linspiraActualizar.AddRange(lResultado);
                inspiraTemporal = null;
                lResultado = null;
                lconceptos = null;
                lInspiraConceptos = null;
            }            
        }

        /// <summary>
        /// Método que envía las unidades funcionales
        /// </summary>
        static void ProcesarUnidades()
        {
            List<InspiraTemporal> lUnidad = linspiratemporal.FindAll(x => x.stabla == "UNIDAD");
            if (lUnidad.Count > 0)
            {
                facadeInspiraServinte.ActualizaInspira(lUnidad, "Unidad");
            }            
        }

        static void ProcesarTarifaConceptoProductos()
        {
            Console.WriteLine("  Sincronizador de Tarifas — InspiraAlejus");
            try
            {
                // -----------------------------------------------------------------
                // PASO 1: Truncar tabla InspiraTarifasConceptos
                // -----------------------------------------------------------------
                Console.WriteLine("\n[1/4] Truncando tabla InspiraTarifasConceptos...");
                using (FacadeIntegraBus facadeIntegraBus = new FacadeIntegraBus())
                {
                    facadeIntegraBus.sconnection = FNCSincroniza.Properties.Settings.Default.InspiraAlejus;
                    facadeIntegraBus.TruncarTabla("[dbo].[InspiraTarifasConceptos]");
                }
                Console.WriteLine("      Tabla truncada correctamente.");

                // -----------------------------------------------------------------
                // PASO 2: Login en Salesforce y descarga de todos los registros
                //         de RateByConceptByProduct__c (maneja paginación automática)
                // -----------------------------------------------------------------
                Console.WriteLine("\n[2/4] Conectando a Salesforce y descargando RateByConceptByProduct__c...");
                List<SalesforceViaRestApi.RateByConceptByProduct> registrosSF;

                using (SalesforceViaRestApi sf = new SalesforceViaRestApi())
                {
                    sf.sLogingEndPoint = FNCSincroniza.Properties.Settings.Default.SalesforceURL;
                    sf.sApiEndpoint = FNCSincroniza.Properties.Settings.Default.SalesforceEndPoint;
                    sf.DoLogin(FNCSincroniza.Properties.Settings.Default.SalesforceUser, FNCSincroniza.Properties.Settings.Default.SalesforcePassword, FNCSincroniza.Properties.Settings.Default.SalesforceClient, FNCSincroniza.Properties.Settings.Default.SalesforceSecret);
                    Console.WriteLine($"Login exitoso. Instance: {sf.salesforceSession.sname}");
                    registrosSF = sf.GetAllRateByConceptByProduct();
                    Console.WriteLine($"Total registros obtenidos de Salesforce: {registrosSF.Count}");

                    // -----------------------------------------------------------------
                    // PASO 3: Insertar registros en InspiraTarifasConceptos (Bulk Copy)
                    // -----------------------------------------------------------------
                    Console.WriteLine("\n[3/4] Insertando registros en InspiraTarifasConceptos (Bulk Copy)...");
                    DataTable dt = BuildDataTable(registrosSF);
                    using (FacadeIntegraBus db = new FacadeIntegraBus())
                    {
                        db.sconnection = FNCSincroniza.Properties.Settings.Default.InspiraAlejus;
                        db.BulkData("[dbo].[InspiraTarifasConceptos]", dt);
                    }
                    Console.WriteLine($"{registrosSF.Count} registros insertados correctamente.");

                    // -----------------------------------------------------------------
                    // PASO 4: Ejecutar query de diferencias y enviar nuevos a Salesforce
                    // -----------------------------------------------------------------
                    Console.WriteLine("\n[4/4] Buscando diferencias y enviando registros nuevos a Salesforce...");
                    List<SalesforceViaRestApi.RateProductNuevo> nuevos = ObtenerDiferencias();
                    Console.WriteLine($"      Registros nuevos encontrados: {nuevos.Count}");

                    if (nuevos.Count > 0)
                    {
                        sf.CrearRateByConceptByProducts(nuevos);
                        Console.WriteLine($"      Registros enviados a Salesforce correctamente.");
                    }
                    else
                    {
                        Console.WriteLine("      No hay registros nuevos para enviar.");
                    }
                }

                Console.WriteLine("\n=================================================");
                Console.WriteLine("  Proceso finalizado exitosamente.");
                Console.WriteLine("=================================================");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"        Detalle: {ex.InnerException.Message}");
                Console.ResetColor();
            }

            Console.WriteLine("\nPresione cualquier tecla para salir...");
            Console.ReadKey();
        }

        // =====================================================================
        // Ejecuta el query de diferencias sobre InspiraAlejus y retorna los
        // registros que existen en el ERP (integrabus) pero aún NO en Salesforce.
        // Usa SQLServer.GetDataTable() tal como lo hace el resto del sistema.
        // =====================================================================
        private static List<SalesforceViaRestApi.RateProductNuevo> ObtenerDiferencias()
        {
            List<SalesforceViaRestApi.RateProductNuevo> lista = new List<SalesforceViaRestApi.RateProductNuevo>();            
            using (FacadeIntegraBus integraBus = new FacadeIntegraBus())
            {
                integraBus.sconnection = FNCSincroniza.Properties.Settings.Default.InspiraAlejus;
                // GetDataTable de la clase SQLServer provista; sin parámetros adicionales
                DataTable dt = integraBus.ObtenerDiferencias();
                foreach (DataRow row in dt.Rows)
                {
                    if (row["IdTarifa"] != DBNull.Value && row["IdConcepto"] != DBNull.Value && row["IdCentro"] != DBNull.Value && row["IdProducto"] != DBNull.Value)
                    {
                        lista.Add(new SalesforceViaRestApi.RateProductNuevo
                        {
                            IdTarifa = row["IdTarifa"] == DBNull.Value ? null : row["IdTarifa"].ToString(),
                            IdConcepto = row["IdConcepto"] == DBNull.Value ? null : row["IdConcepto"].ToString(),
                            IdCentro = row["IdCentro"] == DBNull.Value ? null : row["IdCentro"].ToString(),
                            IdProducto = row["IdProducto"] == DBNull.Value ? null : row["IdProducto"].ToString(),
                            Valor = row["Valor"] == DBNull.Value ? 0 : Convert.ToInt32(row["Valor"])
                        });
                    }                    
                }
            }
            return lista;
        }

        // =====================================================================
        // Convierte la lista de registros de Salesforce al DataTable que
        // SQLServer.BulkData() necesita, respetando la estructura exacta de la
        // tabla InspiraTarifasConceptos definida en SQL Server.
        // =====================================================================
        static DataTable BuildDataTable(List<SalesforceViaRestApi.RateByConceptByProduct> registros)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("RateId__c", typeof(string));
            dt.Columns.Add("RateId__r.Code__c", typeof(string));
            dt.Columns.Add("ConceptId__c", typeof(string));
            dt.Columns.Add("ConceptId__r.Code__c", typeof(string));
            dt.Columns.Add("CostCenterId__c", typeof(string));
            dt.Columns.Add("CostCenterId__r.Code__c", typeof(string));
            dt.Columns.Add("ProductId__c", typeof(string));
            dt.Columns.Add("ProductId__r.Name", typeof(string));

            foreach (SalesforceViaRestApi.RateByConceptByProduct r in registros)
            {
                dt.Rows.Add(
                    r.RateId__c ?? (object)DBNull.Value,
                    r.RateIdCode__c ?? (object)DBNull.Value,
                    r.ConceptId__c ?? (object)DBNull.Value,
                    r.ConceptIdCode__c ?? (object)DBNull.Value,
                    r.CostCenterId__c ?? (object)DBNull.Value,
                    r.CostCenterIdCode__c ?? (object)DBNull.Value,
                    r.ProductId__c ?? (object)DBNull.Value,
                    r.ProductIdName ?? (object)DBNull.Value
                );
            }
            return dt;
        }

        // =====================================================================
        // Convierte la lista de registros de Salesforce (con Id y Value__c)
        // al DataTable que BulkData necesita para poblar InspiraTarifasValores.
        // =====================================================================
        static DataTable BuildDataTableValores(List<SalesforceViaRestApi.RateByConceptByProductValor> registros)
        {
            DataTable dt = new DataTable();
            dt.Columns.Add("Id", typeof(string));
            dt.Columns.Add("RateId__c", typeof(string));
            dt.Columns.Add("RateId__r.Code__c", typeof(string));
            dt.Columns.Add("ConceptId__c", typeof(string));
            dt.Columns.Add("ConceptId__r.Code__c", typeof(string));
            dt.Columns.Add("CostCenterId__c", typeof(string));
            dt.Columns.Add("CostCenterId__r.Code__c", typeof(string));
            dt.Columns.Add("ProductId__c", typeof(string));
            dt.Columns.Add("ProductId__r.Name", typeof(string));
            dt.Columns.Add("Value__c", typeof(decimal));

            foreach (SalesforceViaRestApi.RateByConceptByProductValor r in registros)
            {
                dt.Rows.Add(
                    r.Id ?? (object)DBNull.Value,
                    r.RateId__c ?? (object)DBNull.Value,
                    r.RateIdCode__c ?? (object)DBNull.Value,
                    r.ConceptId__c ?? (object)DBNull.Value,
                    r.ConceptIdCode__c ?? (object)DBNull.Value,
                    r.CostCenterId__c ?? (object)DBNull.Value,
                    r.CostCenterIdCode__c ?? (object)DBNull.Value,
                    r.ProductId__c ?? (object)DBNull.Value,
                    r.ProductIdName ?? (object)DBNull.Value,
                    r.Value__c.HasValue ? (object)r.Value__c.Value : DBNull.Value
                );
            }
            return dt;
        }

        /// <summary>
        /// Proceso completo de actualización de valores de tarifas:
        /// 1. Descarga RateByConceptByProduct__c con Id y Value__c desde Salesforce.
        /// 2. Trunca InspiraTarifasValores y carga los registros descargados.
        /// 3. Ejecuta el query de diferencias de valores contra el ERP.
        /// 4. Actualiza en Salesforce solo los registros cuyo valor cambió.
        /// </summary>
        static void ActualizarValoresTarifas()
        {
            Console.WriteLine("Actualizador de Valores de Tarifas — InspiraAlejus");
            try
            {
                using (SalesforceViaRestApi sf = new SalesforceViaRestApi())
                {
                    sf.sLogingEndPoint = FNCSincroniza.Properties.Settings.Default.SalesforceURL;
                    sf.sApiEndpoint = FNCSincroniza.Properties.Settings.Default.SalesforceEndPoint;
                    sf.DoLogin(
                        FNCSincroniza.Properties.Settings.Default.SalesforceUser,
                        FNCSincroniza.Properties.Settings.Default.SalesforcePassword,
                        FNCSincroniza.Properties.Settings.Default.SalesforceClient,
                        FNCSincroniza.Properties.Settings.Default.SalesforceSecret);
                    Console.WriteLine($"Login exitoso. Instance: {sf.salesforceSession.sname}");

                    // -----------------------------------------------------------------
                    // PASO 1: Descargar RateByConceptByProduct__c con Id y Value__c
                    // -----------------------------------------------------------------
                    Console.WriteLine("\n[1/4] Descargando RateByConceptByProduct__c (con Id y Value__c)...");
                    List<SalesforceViaRestApi.RateByConceptByProductValor> registrosSF =
                        sf.GetAllRateByConceptByProductValores();
                    Console.WriteLine($"      Total registros descargados: {registrosSF.Count}");

                    // -----------------------------------------------------------------
                    // PASO 2: Truncar InspiraTarifasValores y cargar registros (Bulk)
                    // -----------------------------------------------------------------
                    Console.WriteLine("\n[2/4] Truncando y cargando InspiraTarifasValores...");
                    using (FacadeIntegraBus facadeIntegraBus = new FacadeIntegraBus())
                    {
                        facadeIntegraBus.sconnection = FNCSincroniza.Properties.Settings.Default.InspiraAlejus;
                        facadeIntegraBus.TruncarTabla("[dbo].[InspiraTarifasValores]");
                        DataTable dt = BuildDataTableValores(registrosSF);
                        facadeIntegraBus.BulkData("[dbo].[InspiraTarifasValores]", dt);
                    }
                    Console.WriteLine($"      {registrosSF.Count} registros cargados correctamente.");

                    // -----------------------------------------------------------------
                    // PASO 3: Obtener registros cuyo valor cambió en el ERP
                    // -----------------------------------------------------------------
                    Console.WriteLine("\n[3/4] Detectando valores que cambiaron en el ERP...");
                    List<SalesforceViaRestApi.RateValorActualizar> cambios =
                        new List<SalesforceViaRestApi.RateValorActualizar>();

                    using (FacadeIntegraBus facadeIntegraBus = new FacadeIntegraBus())
                    {
                        facadeIntegraBus.sconnection = FNCSincroniza.Properties.Settings.Default.InspiraAlejus;
                        DataTable dtCambios = facadeIntegraBus.ObtenerValoresCambiados();
                        foreach (DataRow row in dtCambios.Rows)
                        {
                            if (row["Id"] != DBNull.Value && row["NuevoValor"] != DBNull.Value)
                            {
                                cambios.Add(new SalesforceViaRestApi.RateValorActualizar
                                {
                                    Id = row["Id"].ToString(),
                                    Valor = Convert.ToInt32(row["NuevoValor"])
                                });
                            }
                        }
                    }
                    Console.WriteLine($"      Registros con valor diferente: {cambios.Count}");

                    // -----------------------------------------------------------------
                    // PASO 4: Actualizar Value__c en Salesforce (Composite PATCH)
                    // -----------------------------------------------------------------
                    if (cambios.Count > 0)
                    {
                        Console.WriteLine("\n[4/4] Actualizando Value__c en Salesforce...");
                        sf.ActualizarValoresTarifas(cambios);
                        Console.WriteLine($"      Registros actualizados correctamente.");
                    }
                    else
                    {
                        Console.WriteLine("\n[4/4] No hay valores que actualizar en Salesforce.");
                    }
                }

                Console.WriteLine("\n=================================================");
                Console.WriteLine("  Proceso de actualización de valores finalizado.");
                Console.WriteLine("=================================================");
                LogError.WriteMessage("Integrador", "Integrador", "Actualización de valores de tarifas finalizada correctamente.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ERROR] {ex.Message}");
                if (ex.InnerException != null)
                    Console.WriteLine($"        Detalle: {ex.InnerException.Message}");
                Console.ResetColor();
                LogError.WriteError("Integrador", "Integrador", ex);
            }
        }
    }
}
