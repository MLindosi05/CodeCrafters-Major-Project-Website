<%@ Page Title="Registration complete" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="RegisterConfirmation.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.RegisterConfirmation" %>
<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <section class="section" style="min-height:55vh; display:flex; align-items:center; justify-content:center;">
        <div class="form-regal" style="text-align:center;">
            <h1>Your account is ready</h1>
            <p style="color:var(--text-muted);">You can now log in, choose a room, and manage your reservations online.</p>
            <a class="btn-regal" href="~/Account/Login.aspx" runat="server">Log in to book</a>
        </div>
    </section>
</asp:Content>
