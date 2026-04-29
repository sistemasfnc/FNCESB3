using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Drawing;
using FNCSalesforce;
using System.Configuration;
using FNCEntity;

namespace FNCWSDigiturno
{
    public partial class filefromsalesforce : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request != null)
            {

                SalesforceIntegrator salesforceIntegrator = new SalesforceIntegrator();
                Generic generic = salesforceIntegrator.Login(ConfigurationManager.AppSettings["SalesforceCompany"], ConfigurationManager.AppSettings["SalesforceUser"], ConfigurationManager.AppSettings["SalesforcePassword"], ConfigurationManager.AppSettings["SalesforceToken"]);
                            

                string sfilename = string.Empty;
                if (!string.IsNullOrEmpty(Request.ContentType))
                {
                    sfilename = GetFileName(Request.ContentType);
                }
                string documentContents = String.Empty;
                using (Stream receiveStream = Request.InputStream)
                {
                    /*System.Drawing.Image img = System.Drawing.Image.FromStream(receiveStream);
                    img.Save(Server.MapPath("~/Images/" + sfilename), System.Drawing.Imaging.ImageFormat.Jpeg);*/
                    
                    BinaryReader br = new BinaryReader(receiveStream);
                    byte[] bytes = br.ReadBytes((Int32)receiveStream.Length);
                    File.WriteAllBytes(Server.MapPath("~/Images/" + sfilename), bytes);

                    using (StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8))
                    {
                        documentContents = readStream.ReadToEnd();
                    }
                }
                
            }
        }

        private string GetFileName(string sheader)
        {
            string[] aheader = sheader.Split(';');
            if (aheader.Length > 1)
            {
                return aheader[1];
            }
            return string.Empty;
        }
    }
}