<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="filelist.aspx.cs" Inherits="FNCWSDigiturno.filelist" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
<meta http-equiv="Content-Type" content="text/html; charset=utf-8"/>
    <title></title>    
    <script type="text/javascript">
        function OpenWindow(sUrl)
        {
            window.open('http://localhost:8082/test.bat');
            //window.open("file:///" + sUrl);
        }
    </script>
</head>
<body style="font-size:9px; font-family:Verdana">
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager" EnablePageMethods="true" runat="server"></asp:ScriptManager>
        <h1>Listado de archivos del paciente</h1>
        <br />
        <asp:GridView ID="gvFiles" runat="server" AllowPaging="false" AutoGenerateColumns="false" Width="30%" OnRowCommand="gvFiles_RowCommand">            
            <Columns>
                <asp:TemplateField HeaderText="Archivos" ItemStyle-HorizontalAlign="Center">
                    <ItemTemplate>
                        <asp:LinkButton ID="lbtArchivo" runat="server" ToolTip='<%# Eval("sname") %>' Text='<%# Eval("sname") %>' CommandName="Descargar"></asp:LinkButton> <br />                                                
                    </ItemTemplate>
                </asp:TemplateField>
            </Columns>
        </asp:GridView>
    </form>
</body>
</html>
