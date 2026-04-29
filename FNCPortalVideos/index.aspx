<%@ Page Title="Inicio" Language="C#" MasterPageFile="~/master/Main.Master" AutoEventWireup="true" CodeBehind="index.aspx.cs" Inherits="FNCPortalVideos.index" %>
<%@ Register Assembly="AjaxControlToolkit" Namespace="AjaxControlToolkit" TagPrefix="asp" %>
<asp:Content ID="Content1" ContentPlaceHolderID="head" runat="server">
</asp:Content>
<asp:Content ID="Content2" ContentPlaceHolderID="ContentPlaceHolder1" runat="server">
    <div class="login-10">
	    <div class="tenth-login">
		    <h4>Validaci&oacute;n de datos de usuario</h4>		    			
            <asp:Label ID="lblError" runat="server" CssClass="error"></asp:Label>	
			<ul>
				<li class="cream">
					Tipo de documento: 
					<asp:DropDownList ID="ddlTipoDocumento" runat="server">
						<asp:ListItem Text="Seleccione el tipo de documento" Value=""></asp:ListItem>
						<asp:ListItem Text="Cédula de Ciudadanía" Value="Cédula de Ciudadanía"></asp:ListItem>
						<asp:ListItem Text="Tarjeta de Identidad" Value="Tarjeta de Identidad"></asp:ListItem>
						<asp:ListItem Text="Registro Civil" Value="Registro Civil"></asp:ListItem>
						<asp:ListItem Text="Pasaporte" Value="Pasaporte"></asp:ListItem>
						<asp:ListItem Text="Cédula de Extranjería" Value="Cédula de Extranjería"></asp:ListItem>
					</asp:DropDownList>
				</li>
				<li class="cream">
					Documento: 
					<asp:TextBox ID="txtDocumento" runat="server" CssClass="text" MaxLength="20"></asp:TextBox>
				</li>
				<li class="cream">
					Correo electr&oacute;nico: 
					<asp:TextBox ID="txtCorreo" runat="server" CssClass="text" MaxLength="80"></asp:TextBox>
				</li>
				<li class="cream">
					Fecha de nacimiento: 
					<asp:TextBox ID="txtFechaNacimiento" runat="server"></asp:TextBox>
					<asp:ImageButton ID="imbFechaNacimiento" runat="server" ImageUrl="~/images/calendar.png" Width="16" Height="16" />
					<asp:CalendarExtender ID="ceFechaNacimiento" runat="server" TargetControlID="txtFechaNacimiento" Format="yyyy-MM-dd" PopupButtonID="imbFechaNacimiento"></asp:CalendarExtender>
				</li>
			</ul>			    				
			<div class="submit-ten">
                <asp:Button ID="btnIngreso" runat="server" Text="Validar" OnClick="btnIngreso_Click" />				    
			</div>
	    </div>
    </div>  
</asp:Content>
