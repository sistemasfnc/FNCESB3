using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using FNCUtils;
using FNCSalesforce;
using FNCEntity;
using System.IO;
using System.Web.Configuration;
using System.Configuration;

namespace FNCPortalVideos
{
    public partial class vervideo : System.Web.UI.Page
    {
        public string VideoUrl
        {
            get { return ViewState["VideoUrl"].ToString(); }
            set { ViewState["VideoUrl"] = value; }
        }

        protected Patient patient
        {
            get { return Session["patient"] as Patient; }
        }


        protected void Page_Load(object sender, EventArgs e)
        {
            if (!this.IsPostBack)
            {
                if (Request.QueryString["id"] != null && (Request.QueryString["patient"] != null || this.patient != null))
                {
                    this.VideoUrl = Tools.DecryptUrlSafe(Request.QueryString["id"].ToString(), ConfigurationManager.AppSettings["URLPassKey"].ToString());
                    if (this.VideoUrl.IndexOf('\\') > 1)
                    {
                        string start = this.VideoUrl.Substring(0, this.VideoUrl.IndexOf('\\') + 1);
                        string replacechars = ConfigurationManager.AppSettings["VideoPath"].ToString();
                        this.VideoUrl = this.VideoUrl.Replace(start, replacechars);
                        this.DownloadVideo(this.VideoUrl);
                    }
                }
            }                      
        }

        private void DownloadVideo(string videoPath)
        {
            if (System.IO.File.Exists(videoPath))
            {
                string fileName = Path.GetFileName(videoPath);

                Response.Clear();
                Response.ContentType = "video/mp4"; // Asegúrate de que el tipo MIME sea correcto
                Response.AppendHeader("Content-Disposition", "inline; filename=" + fileName);
                Response.WriteFile(videoPath);
                Response.End(); // Finaliza la respuesta
            }
            else
            {
                Response.Write("<script>alert('El archivo no se encontró.');</script>");
            }
        }
    }
}