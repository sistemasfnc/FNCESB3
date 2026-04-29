using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.IO;

namespace FNCWSDigiturno
{
    public partial class GetAppointmentVideo : System.Web.UI.Page
    {
        private string strAppointment
        {
            get { return !string.IsNullOrEmpty(Request.QueryString["appointment"]) ? Request.QueryString["appointment"] : string.Empty; }
        }

        private string strDate
        {
            get { return !string.IsNullOrEmpty(Request.QueryString["date"]) ? Request.QueryString["date"] : string.Empty; }
        }

        private string strPath
        {
            get { return ConfigurationManager.AppSettings["VideosPath"]; }
        }

        public string strUrlVideo
        {
            get { return !string.IsNullOrEmpty(ViewState["strUrlVideo"].ToString()) ? ViewState["strUrlVideo"].ToString() : string.Empty; }
            set { ViewState["strUrlVideo"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(this.strAppointment) && !string.IsNullOrEmpty(this.strDate))
                {
                    DateTime dt = Convert.ToDateTime(this.strDate);
                    string[] paths = new string[] { strPath, dt.Year.ToString(), dt.Month.ToString(), dt.Day.ToString() };
                    string ssource = Path.Combine(paths);
                    string[] directories = Directory.GetDirectories(ssource, $"*{this.strAppointment}*");
                    if (directories.Length > 0)
                    {
                        string spath = Path.Combine(directories[0], "Videos");
                        string[] files = Directory.GetFiles(spath, "*.mp4");
                        if (files.Length > 0)
                        {
                            this.strUrlVideo = files[0];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Server.Transfer("~/ErrorPage.aspx?msg=" + Server.UrlEncode(ex.Message));
            }
            
        }
    }
}