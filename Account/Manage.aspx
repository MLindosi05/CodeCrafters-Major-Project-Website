<%@ Page Title="Manage Account" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Manage.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Account.Manage" %>
<%@ Register Src="~/Account/OpenAuthProviders.ascx" TagPrefix="uc" TagName="OpenAuthProviders" %>
<asp:Content ContentPlaceHolderID="MainContent" runat="server">
    <main aria-labelledby="title">

        <section class="section reveal" style="padding-top:3.5rem; display:flex; align-items:center; justify-content:center;">
            <div class="form-regal" style="max-width:520px;">

                <div style="text-align:center; margin-bottom:2rem;">
                    <div style="font-family:var(--font-head); font-size:1.1rem; color:var(--blue-900); letter-spacing:1px;">THE REGAL INN HOTEL</div>
                    <h1 id="title" style="margin:.4rem 0 0; font-size:1.8rem;"><%: Title %></h1>
                    <p style="color:var(--text-muted); margin:.4rem 0 0; font-size:.9rem;">Manage your login, security, and profile details.</p>
                </div>

                <asp:PlaceHolder runat="server" ID="successMessage" Visible="false" ViewStateMode="Disabled">
                    <div class="form-msg success"><%: SuccessMessage %></div>
                </asp:PlaceHolder>

                <h4 style="margin-bottom:.5rem;">Account Security</h4>
                <dl class="dl-horizontal">
                    <dt>Password:</dt>
                    <dd>
                        <asp:HyperLink NavigateUrl="/Account/ManagePassword" Text="[Change]" Visible="false" ID="ChangePassword" runat="server" />
                        <asp:HyperLink NavigateUrl="/Account/ManagePassword" Text="[Create]" Visible="false" ID="CreatePassword" runat="server" />
                    </dd>
                   <%-- <dt>External Logins:</dt>
                    <dd><%: LoginsCount %>
                        <asp:HyperLink NavigateUrl="/Account/ManageLogins" Text="[Manage]" runat="server" />
                    </dd>
                    <%--
                        Phone Numbers can used as a second factor of verification in a two-factor authentication system.
                        See <a href="https://go.microsoft.com/fwlink/?LinkId=403804">this article</a>
                        for details on setting up this ASP.NET application to support two-factor authentication using SMS.
                        Uncomment the following blocks after you have set up two-factor authentication
                    --%>
                    <%--
                    <dt>Phone Number:</dt>
                    <% if (HasPhoneNumber)
                        { %>
                    <dd>
                        <asp:HyperLink NavigateUrl="/Account/AddPhoneNumber" runat="server" Text="[Add]" />
                    </dd>
                    <% }
                        else
                        { %>
                    <dd>
                        <asp:Label Text="" ID="PhoneNumber" runat="server" />
                        <asp:HyperLink NavigateUrl="/Account/AddPhoneNumber" runat="server" Text="[Change]" /> &nbsp;|&nbsp;
                        <asp:LinkButton Text="[Remove]" OnClick="RemovePhone_Click" runat="server" />
                    </dd>
                    <% } %>
                    --%>
                    <dt>Two-Factor Authentication:</dt>
                    <dd>
                        <p>
                          <%--  There are no two-factor authentication providers configured. See <a href="https://go.microsoft.com/fwlink/?LinkId=403804">this article</a>
                            for details on setting up this ASP.NET application to support two-factor authentication.--%>
                        </p>
                        <% if (TwoFactorEnabled)
                            { %> 
                        <%--
                        Enabled
                        <asp:LinkButton Text="[Disable]" runat="server" CommandArgument="false" OnClick="TwoFactorDisable_Click" />
                        --%>
                        <% }
                            else
                            { %> 
                        <%--
                        Disabled
                        <asp:LinkButton Text="[Enable]" CommandArgument="true" OnClick="TwoFactorEnable_Click" runat="server" />
                        --%>
                        <% } %>
                    </dd>
                </dl>

                <hr />

                <h4 style="margin-bottom:.5rem;">Update Your Profile</h4>

                <asp:PlaceHolder runat="server" ID="profileSuccessMessage" Visible="false" ViewStateMode="Disabled">
                    <div class="form-msg success"><%: ProfileSuccessMessage %></div>
                </asp:PlaceHolder>
                <asp:Literal ID="ProfileErrorMessage" runat="server" EnableViewState="false" />
                <asp:ValidationSummary ID="ProfileValidationSummary" runat="server" ValidationGroup="Profile" CssClass="form-msg error" DisplayMode="BulletList" />

                <asp:HiddenField ID="hdnOriginalEmail" runat="server" />

                <div class="field">
                    <label for="txtProfileUsername">Username</label>
                    <asp:TextBox ID="txtProfileUsername" runat="server" CssClass="regal-input" MaxLength="256" />
                    <asp:RequiredFieldValidator ID="rfvProfileUsername" runat="server" ControlToValidate="txtProfileUsername" ValidationGroup="Profile" CssClass="field-error" ErrorMessage="Username is required." Display="Dynamic" />
                </div>
                <div class="field">
                    <label for="txtProfileFirstName">First Name</label>
                    <asp:TextBox ID="txtProfileFirstName" runat="server" CssClass="regal-input" MaxLength="50" />
                    <asp:RequiredFieldValidator ID="rfvProfileFirstName" runat="server" ControlToValidate="txtProfileFirstName" ValidationGroup="Profile" CssClass="field-error" ErrorMessage="First name is required." Display="Dynamic" />
                </div>
                <div class="field">
                    <label for="txtProfileLastName">Last Name</label>
                    <asp:TextBox ID="txtProfileLastName" runat="server" CssClass="regal-input" MaxLength="50" />
                    <asp:RequiredFieldValidator ID="rfvProfileLastName" runat="server" ControlToValidate="txtProfileLastName" ValidationGroup="Profile" CssClass="field-error" ErrorMessage="Surname is required." Display="Dynamic" />
                </div>
                <div class="field">
                    <label for="txtProfileEmail">Email</label>
                    <asp:TextBox ID="txtProfileEmail" runat="server" TextMode="Email" CssClass="regal-input" MaxLength="100" />
                    <asp:RequiredFieldValidator ID="rfvProfileEmail" runat="server" ControlToValidate="txtProfileEmail" ValidationGroup="Profile" CssClass="field-error" ErrorMessage="Email is required." Display="Dynamic" />
                    <asp:RegularExpressionValidator ID="revProfileEmail" runat="server" ControlToValidate="txtProfileEmail" ValidationGroup="Profile" ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" CssClass="field-error" ErrorMessage="Enter a valid email address." Display="Dynamic" />
                    <asp:Label ID="lblStaffAccountHint" runat="server" Visible="false" CssClass="field-hint" Text="Staff email and login role are linked to your employee record." />
                </div>
                <div class="field">
                    <label for="txtProfilePhone">Phone</label>
                    <asp:TextBox ID="txtProfilePhone" runat="server" CssClass="regal-input" MaxLength="20" />
                    <asp:RequiredFieldValidator ID="rfvProfilePhone" runat="server" ControlToValidate="txtProfilePhone" ValidationGroup="Profile" CssClass="field-error" ErrorMessage="Phone number is required." Display="Dynamic" />
                    <asp:RegularExpressionValidator ID="revProfilePhone" runat="server" ControlToValidate="txtProfilePhone" ValidationGroup="Profile" ValidationExpression="^[0-9+() .-]{7,20}$" CssClass="field-error" ErrorMessage="Enter a valid phone number (7–20 characters)." Display="Dynamic" />
                </div>
                <asp:PlaceHolder ID="clientAddressSection" runat="server">
                <div class="field">
                    <label for="txtProfileAddress">Address</label>
                    <asp:TextBox ID="txtProfileAddress" runat="server" CssClass="regal-input" />
                </div>
                </asp:PlaceHolder>

                <asp:PlaceHolder ID="staffStatusSection" runat="server" Visible="false">
                    <div class="field">
                        <label for="ddlProfileStatus">Your staff status</label>
                        <asp:DropDownList ID="ddlProfileStatus" runat="server" CssClass="regal-input">
                            <asp:ListItem Text="Active" Value="Active" />
                            <asp:ListItem Text="Inactive" Value="Inactive" />
                        </asp:DropDownList>
                    </div>
                </asp:PlaceHolder>

                <asp:Button ID="btnSaveProfile" runat="server" Text="Save Changes" CssClass="btn-regal"
                    style="width:100%; font-size:1rem;" OnClick="SaveProfile_Click" ValidationGroup="Profile"
                    OnClientClick="return confirmProfileUpdate(this);" />

            </div>
        </section>

    </main>
</asp:Content>
<asp:Content ID="ProfileScripts" ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        function confirmProfileUpdate(button) {
            if (window.Page_ClientValidate && !window.Page_ClientValidate("Profile")) {
                if (window.RegalDialog) window.RegalDialog.alert("Check the highlighted profile fields and try again.", { title: "Check profile details" });
                return false;
            }
            var managerProfile = <%= (User.IsInRole(CodeCrafters_Major_Project_Website.Models.RoleBootstrapper.Manager) || User.IsInRole(CodeCrafters_Major_Project_Website.Models.RoleBootstrapper.Admin)).ToString().ToLowerInvariant() %>;
            var status = document.getElementById("<%= ddlProfileStatus.ClientID %>");
            var goingInactive = managerProfile && status && status.value === "Inactive";
            var text = goingInactive
                ? "Set your manager account to inactive? You will be signed out and need an administrator to reactivate it."
                : "Save your profile changes?";
            if (!window.RegalDialog) return window.confirm(text);
            window.RegalDialog.confirm(text, { title: "Confirm profile update", okText: "Save changes" }).then(function (confirmed) {
                if (!confirmed) return;
                button.disabled = true;
                if (window.__doPostBack) window.__doPostBack(button.name, "");
            });
            return false;
        }
    </script>
</asp:Content>
