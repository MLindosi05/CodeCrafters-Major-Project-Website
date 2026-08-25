<%@ Page Title="Log in" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Account.Login" Async="true" %>

<%@ Register Src="~/Account/OpenAuthProviders.ascx" TagPrefix="uc" TagName="OpenAuthProviders" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Log In | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="section reveal" style="padding-top:3.5rem; min-height:70vh; display:flex; align-items:center; justify-content:center;">
        <div class="form-regal" style="max-width:440px;">

            <div style="text-align:center; margin-bottom:2rem;">
                <div style="font-family:var(--font-head); font-size:1.1rem; color:var(--gold-300); letter-spacing:1px;">THE REGAL INN HOTEL</div>
                <h1 style="margin:.4rem 0 0; font-size:1.8rem;">Welcome Back</h1>
                <p style="color:var(--cream-dim); margin:.4rem 0 0; font-size:.9rem;">Log in to manage your bookings.</p>
            </div>

            <asp:ValidationSummary runat="server" CssClass="form-msg error" DisplayMode="BulletList" />
            <asp:Literal runat="server" ID="FailureText" EnableViewState="false" />

            <div class="field">
                <label for="Email">Email</label>
                <div style="position:relative;">
                    <asp:TextBox runat="server" ID="Email" CssClass="regal-input" placeholder="you@example.com" TabIndex="1" />
                </div>
                <asp:RequiredFieldValidator runat="server" ControlToValidate="Email"
                    CssClass="field-error" ErrorMessage="Email is required." Display="Dynamic" />
            </div>

            <div class="field">
                <label for="Password">Password</label>
                <div style="position:relative;">
                    <asp:TextBox runat="server" ID="Password" TextMode="Password" CssClass="regal-input" placeholder="••••••••" TabIndex="2" />
                    <button type="button" class="pw-toggle" data-target="<%= Password.ClientID %>" aria-label="Show password">👁</button>
                </div>
                <asp:RequiredFieldValidator runat="server" ControlToValidate="Password"
                    CssClass="field-error" ErrorMessage="Password is required." Display="Dynamic" />
            </div>

            <div style="display:flex; align-items:center; justify-content:space-between; margin-bottom:1.5rem;">
                <label style="display:flex; align-items:center; gap:.5rem; font-size:.85rem; color:var(--cream-dim); font-weight:400;">
                    <asp:CheckBox runat="server" ID="RememberMe" TabIndex="3" />
                    Remember me
                </label>
                <a href="~/Account/Forgot.aspx" runat="server" style="font-size:.85rem;">Forgot password?</a>
            </div>

            <asp:Button runat="server" OnClick="LogIn_Click" Text="Log In" ID="LoginButton"
                CssClass="btn-regal" TabIndex="4" style="width:100%; font-size:1rem;" />

            <p style="text-align:center; margin-top:1.5rem; font-size:.9rem; color:var(--cream-dim);">
                Don't have an account?
                <a href="~/Account/Register.aspx" runat="server">Create one</a>
            </p>
        </div>
    </section>

</asp:Content>

<asp:Content ID="ScriptC" ContentPlaceHolderID="ScriptContent" runat="server">
    <style>
        .regal-input{width:100%; background:#fff; border:1px solid var(--border); border-radius:var(--radius-sm);
            padding:.85rem 2.6rem .85rem 1rem; color:var(--text); font-family:var(--font-body); font-size:.95rem;
            transition:border-color .2s ease, box-shadow .2s ease;}
        .regal-input:focus{outline:none; border-color:var(--blue-700); box-shadow:0 0 0 3px var(--blue-50);}
        .pw-toggle{position:absolute; right:.9rem; top:50%; transform:translateY(-50%); background:none; border:none;
            cursor:pointer; opacity:.6; font-size:1rem;}
        .pw-toggle:hover{opacity:1;}
        .field-error{display:block; color:var(--danger); font-size:.78rem; margin-top:.3rem;}
    </style>
    <script>
        document.addEventListener("DOMContentLoaded", function () {
            document.querySelectorAll(".pw-toggle").forEach(function (btn) {
                btn.addEventListener("click", function () {
                    var input = document.getElementById(btn.dataset.target);
                    if (!input) return;
                    input.type = input.type === "password" ? "text" : "password";
                    btn.textContent = input.type === "password" ? "👁" : "🙈";
                });
            });
        });
    </script>
</asp:Content>
