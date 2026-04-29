using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using EventLog;
using FNCSalesforce;
using System.Configuration;
using FNCSalesforce.Sfdc;
using FNCEntity;

namespace FNCPortalVideos
{
    public partial class index : System.Web.UI.Page
    {
        protected Patient patient 
        {
            get { return Session["patient"] as Patient; } 
            set { Session["patient"]  = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {

        }

        protected void btnIngreso_Click(object sender, EventArgs e)
        {

            SalesforceViaRestApi salesforceViaRestApi = new SalesforceViaRestApi();
            try
            {
                salesforceViaRestApi.sLogingEndPoint = ConfigurationManager.AppSettings["SalesforceURL"].ToString();
                salesforceViaRestApi.sApiEndpoint = ConfigurationManager.AppSettings["SalesforceEndPoint"].ToString();
                string susr = ConfigurationManager.AppSettings["SalesforceUser"].ToString();
                string spwd = ConfigurationManager.AppSettings["SalesforcePassword"].ToString() + ConfigurationManager.AppSettings["SalesforceToken"].ToString();
                string ssectet = ConfigurationManager.AppSettings["SalesforceSecret"].ToString();
                string sclientid = ConfigurationManager.AppSettings["SalesforceClient"].ToString();
                salesforceViaRestApi.DoLogin(susr, spwd, sclientid, ssectet);
                if (salesforceViaRestApi.salesforceSession != null)
                {
                    patient = salesforceViaRestApi.GetPatientInfo(this.ddlTipoDocumento.SelectedValue, this.txtDocumento.Text, this.txtFechaNacimiento.Text, this.txtCorreo.Text);
                    if (patient != null)
                    {
                        Response.Redirect("listadovideos.aspx");
                    }
                    else
                    {
                        this.lblError.Text = "Ha ocurrido un error al consultar los datos del paciente";
                    }
                }
            }
            catch (Exception ex)
            {
                LogError.WriteError("VisorProcedimientos", "VisorProcedimientos", ex);
                this.lblError.Text = "Ha ocurrido un error al consultar los datos del paciente";                
            }
        }
    }
}