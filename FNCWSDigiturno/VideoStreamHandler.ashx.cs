using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace FNCWSDigiturno
{
    /// <summary>
    /// Descripción breve de VideoStreamHandler
    /// </summary>
    public class VideoStreamHandler : IHttpHandler
    {

        public void ProcessRequest(HttpContext context)
        {
            string path = context.Request.QueryString["path"];

            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                context.Response.StatusCode = 404;
                context.Response.Write("Archivo no encontrado.");
                return;
            }

            try
            {
                using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    context.Response.ContentType = "video/mp4";
                    context.Response.AddHeader("Content-Length", fs.Length.ToString());
                    context.Response.AddHeader("Content-Disposition", $"inline; filename={Path.GetFileName(path)}");

                    byte[] buffer = new byte[1024 * 64];
                    int bytesRead;

                    while ((bytesRead = fs.Read(buffer, 0, buffer.Length)) > 0 && context.Response.IsClientConnected)
                    {
                        context.Response.OutputStream.Write(buffer, 0, bytesRead);
                        context.Response.Flush();
                    }
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = 500;
                context.Response.Write("Error al transmitir el archivo: " + ex.Message);
            }
        }

        public bool IsReusable => false;
    }
}