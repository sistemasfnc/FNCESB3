<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="DigiturnoScreen.aspx.cs" Inherits="FNCWSDigiturno.DigiturnoScreen" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta http-equiv="Content-Type" content="text/html; charset=utf-8" />
    <title>Listado de turnos</title>
    <style id="CssStyle">
        table { border-collapse: collapse; }
        .HourRow { border-bottom: 3px solid #70BFBA; }
        .ListRow {
            border-bottom: 3px solid;
            vertical-align: initial;
        }

        .AlternRow {
            border-bottom: 3px solid;
            vertical-align: initial;
        }
        

        body .bRelatedList .pbBody table.list, body .apexp .pbBody table.list {
            border: 1px solid white;
        }

        .headerRow {
            font-family: sans-serif !important;
            font-size: 50px !important;
            border-radius: 15px 15px 3px 3px;
            border-color: white !important;
            text-transform: uppercase;
            text-align: center !important;
            background-color: yellow !important;
        }

        .dataCell {
            font-family: sans-serif;
            font-size: 50px;
            font-weight: bold;
            text-transform: uppercase;
        }        

        
        @-webkit-keyframes blink {
            50% {
                visibility: hidden;
            }
        }

        @keyframes blink {
            50% {
                visibility: hidden;
            }
        }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <asp:ScriptManager ID="ScriptManager1" runat="server"></asp:ScriptManager>
        <asp:Timer ID="Timer1" runat="server" Interval="5000" OnTick="Timer1_Tick"></asp:Timer>
        <div>
            <asp:UpdatePanel ID="upDatos" runat="server">
                <ContentTemplate>
                    <asp:GridView ID="gvTurnos" runat="server" Width="100%" AutoGenerateColumns="false" ShowHeaderWhenEmpty="true">
                        <Columns>
                            <asp:BoundField DataField="patientname" HeaderText="Paciente" ItemStyle-CssClass="dataCell" />
                            <asp:BoundField DataField="roomcode" HeaderText="Caja" ItemStyle-CssClass="dataCell" ItemStyle-HorizontalAlign="Center" />
                        </Columns>
                        <RowStyle CssClass="ListRow" />
                        <HeaderStyle CssClass="headerRow" />   
                        <AlternatingRowStyle CssClass="AlternRow" />
                    </asp:GridView>
                </ContentTemplate>
                <Triggers>
                    <asp:AsyncPostBackTrigger ControlID="Timer1" EventName="Tick" />
                </Triggers>
            </asp:UpdatePanel>
        </div>               
    </form>
</body>
</html>
