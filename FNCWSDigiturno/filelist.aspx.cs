using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using System.Configuration;
using EventLog;
using FNCEntity;
using System.Diagnostics;

namespace FNCWSDigiturno
{
    public partial class filelist : System.Web.UI.Page
    {
        /// <summary>
        /// Propiedad de la clase que lee el número de documento del paciente enviada como parámetro URL GET
        /// </summary>
        private string sDocument
        {
            get { return (Request.QueryString["sdocument"] != null) ? Request.QueryString["sdocument"] : string.Empty; }
        }

        /// <summary>
        /// Método que se ejecuta al cargar la página
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void Page_Load(object sender, EventArgs e)
        {
            //Si el documento no es vacío y no es Postback se carga la grilla con la información
            if (!string.IsNullOrEmpty(this.sDocument) && !this.IsPostBack)
            {
                this.BindGrid();
            }
        }

        /// <summary>
        /// Método que lee los archivos de Excel que pertenecen al documento del paciente enviado
        /// </summary>
        /// <returns>Lista genérica con los nombres y rutas de los archivos encontrados que coinciden con el documento del paciente</returns>
        private List<Generic> GetFiles()
        {
            List<Generic> lFiles = new List<Generic>();
            string sDir = ConfigurationManager.AppSettings["CarpetaArchivos"].ToString();
            string sPatthern = this.sDocument + "*" + ".xls*";
            Generic oGeneric = null;
            try
            {
                foreach (string f in Directory.GetFiles(sDir, sPatthern))
                {
                    oGeneric = new Generic()
                    {                        
                        sname = this.GetFileName(f),
                        scode = f,
                    };
                    //oGeneric.scode = ConfigurationManager.AppSettings["CarpetaWeb"] + oGeneric.sname;
                    lFiles.Add(oGeneric);
                }
                return lFiles;
            }
            catch (Exception ex)
            {
                LogError.WriteError("Application", "WSInspira", ex);
                return null;
            }
        }

        /// <summary>
        /// Método que carga la grilla de archivos del paciente
        /// </summary>
        private void BindGrid()
        {
            //Obtiene los archivos
            List<Generic> lFiles = this.GetFiles();
            //Asigna lista a la grilla
            this.gvFiles.DataSource = lFiles;
            this.gvFiles.DataKeyNames = new string[] { "scode" };
            this.gvFiles.DataBind();
        }

        /// <summary>
        /// Método que obtiene el nombre del archivo mediante el objeto Información del Archivo
        /// </summary>
        /// <param name="sFile">String ruta del archivo</param>
        /// <returns>String nombre del archivo</returns>
        private string GetFileName(string sFile)
        {
            FileInfo oInfo = new FileInfo(sFile);
            return oInfo.Name;
        }

        /// <summary>
        /// Método que se ejecuta cuando se acciona un comando sobre la fila de la grilla
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void gvFiles_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            GridViewRow gr = ((e.CommandSource) as Control).NamingContainer as GridViewRow;
            //Si el comando es Descargar
            if (e.CommandName == "Descargar")
            {
                //Lee el nombre del archivo y arma la ruta para descarga
                string sFile = this.GetFileName(this.gvFiles.DataKeys[gr.RowIndex]["scode"].ToString());                
                sFile = Path.Combine(ConfigurationManager.AppSettings["CarpetaArchivos"].ToString(), sFile);                
                //Ejecuta aplicación ClickOnce que se encarga de abrir el Excel en el equipo cliente y envía la ruta del archivo. 
                //Tener en cuenta que se debe tener instalada la extensión Meta4Click para Google Chrome
                //El usuario debe tener permisos sobre ExcelInformes
                //El equipo debe tener instalado Excel
                string app = "http://192.168.101.42:8082/ExcelLauncher.application?sFile=" + sFile;
                Process.Start("rundll32.exe", "dfshim.dll,ShOpenVerbApplication " + app);                
                Response.Redirect(app);

            }
        }
    }
}