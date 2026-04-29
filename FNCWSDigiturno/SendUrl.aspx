<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="SendUrl.aspx.cs" Inherits="FNCWSDigiturno.SendUrl" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
<meta http-equiv="Content-Type" content="text/html; charset=utf-8"/>
    <title></title>
    <script type="text/javascript">
        function OpenWindow(sUrl)
        {
            var shell = new ActiveXObject("WScript.Shell");
            shell.run("start chrome " + sUrl);            
        }
    </script>
</head>
<body>
    <form id="form1" runat="server">
        <div>            
            <a href="<% Response.Write(this.sUrl); %>">Ver Paciente en Inspira</a>
        </div>
    </form>
</body>
</html>
