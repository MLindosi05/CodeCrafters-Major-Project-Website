<%@ Page Title="Register" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Account.Register" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Create Account | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="section reveal" style="padding-top:3.5rem; min-height:70vh; display:flex; align-items:center; justify-content:center;">
        <div class="form-regal" style="max-width:460px;">

            <div style="text-align:center; margin-bottom:2rem;">
                <div style="font-family:var(--font-head); font-size:1.1rem; color:var(--gold-300); letter-spacing:1px;">THE REGAL INN HOTEL</div>
                <h1 style="margin:.4rem 0 0; font-size:1.8rem;">Create Your Account</h1>
                <p style="color:var(--cream-dim); margin:.4rem 0 0; font-size:.9rem;">Book faster, track stays, unlock member rates.</p>
            </div>

            <asp:ValidationSummary runat="server" CssClass="form-msg error" DisplayMode="BulletList" />
            <asp:Literal runat="server" ID="ErrorMessage" EnableViewState="false" />

            <div class="field">
                <label for="FirstName">First name</label>
                <asp:TextBox runat="server" ID="FirstName" CssClass="regal-input" placeholder="Your first name" TabIndex="1" />
                <asp:RequiredFieldValidator runat="server" ControlToValidate="FirstName" CssClass="field-error" ErrorMessage="First name is required." Display="Dynamic" />
            </div>
            <div class="field">
                <label for="LastName">Last name</label>
                <asp:TextBox runat="server" ID="LastName" CssClass="regal-input" placeholder="Your last name" TabIndex="2" />
                <asp:RequiredFieldValidator runat="server" ControlToValidate="LastName" CssClass="field-error" ErrorMessage="Last name is required." Display="Dynamic" />
            </div>

            <div class="field">
                <label for="Email">Email</label>
                <asp:TextBox runat="server" ID="Email" TextMode="Email" CssClass="regal-input" placeholder="you@example.com" TabIndex="3" />
                <asp:RequiredFieldValidator runat="server" ControlToValidate="Email"
                    CssClass="field-error" ErrorMessage="Email is required." Display="Dynamic" />
                <asp:RegularExpressionValidator runat="server" ControlToValidate="Email"
                    ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$"
                    CssClass="field-error" ErrorMessage="Enter a valid email." Display="Dynamic" />
            </div>
                  
            <div class="field">
                 <label for="Phone">Phone number</label>
                 <asp:TextBox runat="server" ID="Phone" CssClass="regal-input" placeholder="e.g. 0821234567" TabIndex="6" />
                 <asp:RequiredFieldValidator runat="server" ControlToValidate="Phone" CssClass="field-error" ErrorMessage="Phone number is required." Display="Dynamic" />
            </div>

            <div class="field">
                <label for="Password">Password</label>
                <div style="position:relative;">
                    <asp:TextBox runat="server" ID="Password" TextMode="Password" CssClass="regal-input"
                        placeholder="At least 6 characters" TabIndex="4" onkeyup="regalCheckStrength(this.value)" />
                    <button type="button" class="pw-toggle" data-target="<%= Password.ClientID %>" aria-label="Show password">👁</button>
                </div>
                <div class="strength-track"><div id="strengthBar" class="strength-fill"></div></div>
                <span id="strengthLabel" style="font-size:.75rem; color:var(--cream-dim);"></span>
                <asp:RequiredFieldValidator runat="server" ControlToValidate="Password"
                    CssClass="field-error" ErrorMessage="Password is required." Display="Dynamic" />
            </div>

            <div class="field">
                <label for="ConfirmPassword">Confirm Password</label>
                <asp:TextBox runat="server" ID="ConfirmPassword" TextMode="Password" CssClass="regal-input"
                    placeholder="Re-enter your password" TabIndex="5" />
                <asp:CompareValidator runat="server" ControlToValidate="ConfirmPassword" ControlToCompare="Password"
                    CssClass="field-error" ErrorMessage="Passwords do not match." Display="Dynamic" />
            </div>

            <p style="font-size:.78rem; color:var(--cream-dim); margin:-.5rem 0 1.3rem;">
                By creating an account you agree to receive a verification email to confirm your address before logging in.
            </p>

            <asp:Button runat="server" OnClick="CreateUser_Click" Text="Create Account" ID="RegisterButton"
                CssClass="btn-regal" TabIndex="6" style="width:100%; font-size:1rem;" />

            <p style="text-align:center; margin-top:1.5rem; font-size:.9rem; color:var(--cream-dim);">
                Already have an account?
                <a href="~/Account/Login.aspx" runat="server">Log in</a>
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
        .strength-track{height:5px; background:var(--border); border-radius:4px; margin-top:.5rem; overflow:hidden;}
        .strength-fill{height:100%; width:0%; background:var(--danger); transition:width .3s ease, background .3s ease;}
    </style>
    <script>
        // Client-side UX hint only — the real password rules are still
        // enforced server-side by ASP.NET Identity's PasswordValidator.
        function regalCheckStrength(value) {
            var bar = document.getElementById("strengthBar");
            var label = document.getElementById("strengthLabel");
            var score = 0;

            if (value.length >= 6) score++;
            if (value.length >= 10) score++;
            if (/[A-Z]/.test(value)) score++;
            if (/[0-9]/.test(value)) score++;
            if (/[^A-Za-z0-9]/.test(value)) score++;

            var pct = (score / 5) * 100;
            var color = "#D3564A", text = "Weak";
            if (score >= 4) { color = "#3FAE6B"; text = "Strong"; }
            else if (score >= 2) { color = "#E8B84B"; text = "Fair"; }

            bar.style.width = pct + "%";
            bar.style.background = color;
            label.textContent = value.length ? text + " password" : "";
        }

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
