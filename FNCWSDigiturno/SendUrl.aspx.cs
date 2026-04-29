using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;

namespace FNCWSDigiturno
{
    public partial class SendUrl : System.Web.UI.Page
    {
        protected string sUrl
        {
            get
            {
                return (Request.QueryString["sUrl"] != null) ? "chromehtml:// " + ConfigurationManager.AppSettings["SalesforceURL"].ToString()  + Request.QueryString["sUrl"].ToString() : "chromehtml:// " + ConfigurationManager.AppSettings["SalesforceURL"].ToString();
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            
        }
        
    }
}