using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;

namespace FNCWSDigiturno
{
    public partial class CreateConsent : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.QueryString["id"] != null)
            {
                WSDigiturno wSDigiturno = new WSDigiturno();
                string sAppointment = wSDigiturno.CreateConsent(Request.QueryString["id"].ToString());
                if (!string.IsNullOrEmpty(sAppointment))
                {
                    string sUrl = (Request.QueryString["internal"] == null) ? ConfigurationManager.AppSettings["ConsentsURL"].ToString() : ConfigurationManager.AppSettings["InternalConsentsURL"].ToString();
                    Response.Redirect(sUrl + sAppointment);
                }
                else
                {
                    Response.Write("No se ha podido generar el consentimiento informado, favor intente más tarde");
                    Response.End();
                }
            }
        }
    }
}