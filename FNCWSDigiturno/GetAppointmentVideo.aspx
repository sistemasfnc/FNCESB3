<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="GetAppointmentVideo.aspx.cs" Inherits="FNCWSDigiturno.GetAppointmentVideo" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Reproducir video</title>
</head>
<body>
    <form id="form1" runat="server">
        <div>
            <% if (!string.IsNullOrEmpty(strUrlVideo)) { %>
                <video width="800" height="450" controls autoplay>
                    <source src='<%= ResolveUrl("~/VideoStreamHandler.ashx?path=" + Server.UrlEncode(strUrlVideo)) %>' type="video/mp4" />
                    Tu navegador no soporta el elemento de video.
                </video>
            <% } else { %>
                <p>No se encontró el video.</p>
            <% } %>
        </div>
    </form>
</body>
</html>
