<%@ Page Title="Staff Management" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Staff Management.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Private.WebForm1" %>
<asp:Content ID="TitleContent" ContentPlaceHolderID="TitleContent" runat="server">Staff Management | The Regal Inn Hotel</asp:Content>
<asp:Content ID="StaffManagementContent" ContentPlaceHolderID="MainContent" runat="server">
    <section class="section staff-management-page">
        <span class="dashboard-kicker">REGAL INN · ADMINISTRATION</span>
        <h1 class="section-title">Staff management</h1>
        <p class="staff-caption">Update staff contact details and access status. Manager records are protected from edits by other managers.</p>
        <asp:Literal ID="StaffFeedback" runat="server" EnableViewState="false" />
        <div class="staff-grid-wrap">
            <asp:GridView ID="StaffGrid" runat="server" AutoGenerateColumns="false" DataKeyNames="Email,FirstName,Surname,Phone,Status"
                CssClass="staff-grid" GridLines="None" EmptyDataText="No staff records were found."
                OnRowCommand="StaffGrid_RowCommand" OnRowDataBound="StaffGrid_RowDataBound">
                <Columns>
                    <asp:TemplateField HeaderText="First name"><ItemTemplate><asp:TextBox ID="FirstNameInput" runat="server" CssClass="staff-input" MaxLength="50" Text='<%# Bind("FirstName") %>' /></ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Surname"><ItemTemplate><asp:TextBox ID="SurnameInput" runat="server" CssClass="staff-input" MaxLength="50" Text='<%# Bind("Surname") %>' /></ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="Email" HeaderText="Email" HtmlEncode="true" />
                    <asp:TemplateField HeaderText="Phone"><ItemTemplate><asp:TextBox ID="PhoneInput" runat="server" CssClass="staff-input" MaxLength="20" Text='<%# Bind("Phone") %>' /></ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="Role" HeaderText="Role" HtmlEncode="true" />
                    <asp:TemplateField HeaderText="Status"><ItemTemplate>
                        <asp:DropDownList ID="StatusInput" runat="server" CssClass="staff-input">
                            <asp:ListItem Text="Active" Value="Active" /><asp:ListItem Text="Inactive" Value="Inactive" />
                        </asp:DropDownList>
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Action"><ItemTemplate>
                        <asp:Button ID="SaveStaffButton" runat="server" Text="Save" CommandName="SaveStaff"
                            CommandArgument='<%# Container.DataItemIndex %>' CssClass="btn-regal staff-save"
                            OnClientClick="return confirmStaffUpdate(this);" />
                        <asp:Label ID="ProtectedLabel" runat="server" Text="Protected" CssClass="protected-label" Visible="false" />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </div>
    </section>
</asp:Content>
<asp:Content ID="StaffScripts" ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        function confirmStaffUpdate(button) {
            var row = button.closest("tr");
            var first = row && row.querySelector("input[id$='FirstNameInput']");
            var surname = row && row.querySelector("input[id$='SurnameInput']");
            var phone = row && row.querySelector("input[id$='PhoneInput']");
            var status = row && row.querySelector("select[id$='StatusInput']");
            if (!first || !surname || !phone || !status) return false;
            var phonePattern = /^[0-9+() .-]{7,20}$/;
            var message = !first.value.trim() ? "Enter the staff member's first name." :
                first.value.trim().length > 50 ? "First name must be 50 characters or fewer." :
                !surname.value.trim() ? "Enter the staff member's surname." :
                surname.value.trim().length > 50 ? "Surname must be 50 characters or fewer." :
                !phonePattern.test(phone.value.trim()) ? "Enter a valid phone number (7–20 characters)." :
                !["Active", "Inactive"].includes(status.value) ? "Choose Active or Inactive." : "";
            if (message) {
                window.RegalDialog.alert(message, { title: "Check staff details" });
                return false;
            }

            var staffName = first.value.trim() + " " + surname.value.trim();
            var confirmation = status.value === "Inactive"
                ? (row.dataset.isSelf === "true"
                    ? "Set your manager account to inactive? You will be signed out and need an administrator to reactivate it."
                    : "Set " + staffName + " to inactive? They will lose staff access.")
                : "Save the staff details and status for " + staffName + "?";
            if (!window.RegalDialog) return window.confirm(confirmation);
            window.RegalDialog.confirm(confirmation, { title: "Confirm staff update", okText: "Save changes" }).then(function (confirmed) {
                if (!confirmed) return;
                button.disabled = true;
                if (window.__doPostBack) window.__doPostBack(button.name, "");
            });
            return false;
        }
    </script>
</asp:Content>
<asp:Content ID="StaffStyles" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .staff-management-page{max-width:1250px;margin:0 auto;padding-top:3rem;padding-bottom:4rem}
        .staff-management-page .section-title{margin:.35rem 0 .5rem}
        .staff-caption{color:var(--cream-dim);margin:0 0 1.25rem}
        .staff-grid-wrap{background:#fff;border:1px solid var(--border);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm);overflow-x:auto}
        .staff-grid{width:100%;min-width:980px;border-collapse:collapse}
        .staff-grid th,.staff-grid td{padding:.8rem;border-bottom:1px solid var(--border);text-align:left;vertical-align:middle}
        .staff-grid th{background:#f8fafc;color:var(--cream-dim);font-size:.75rem;text-transform:uppercase;letter-spacing:.04em}
        .staff-grid td{color:var(--text);font-size:.87rem}
        .staff-input{width:100%;min-width:110px;padding:.6rem;border:1px solid var(--border);border-radius:var(--radius-sm);font:inherit;color:var(--text);background:#fff}
        .staff-input:disabled{background:#f1f5f9;color:#64748b;border-color:#e2e8f0}
        .staff-save{padding:.55rem .85rem;white-space:nowrap}.protected-label{font-size:.78rem;color:var(--cream-dim)}
    </style>
</asp:Content>
