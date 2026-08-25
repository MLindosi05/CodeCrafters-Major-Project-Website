<%@ Page Title="Contact" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Contact.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Contact" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Contact Us | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="section reveal" style="padding-top:3.5rem;">
        <h1 class="section-title">Get In Touch</h1>
        <p class="section-sub">Questions about a booking, group rates, or anything else — we're here.</p>

        <div class="form-regal">
            <asp:Literal ID="litFormMsg" runat="server" />

            <div class="field">
                <label for="txtName">Full Name</label>
                <asp:TextBox ID="txtName" runat="server" required="required" />
                <asp:RequiredFieldValidator ControlToValidate="txtName" runat="server" Display="Dynamic" ErrorMessage="Please enter your name." ForeColor="#D3564A" />
            </div>
            <div class="field">
                <label for="txtEmail">Email Address</label>
                <asp:TextBox ID="txtEmail" runat="server" TextMode="Email" required="required" />
                <asp:RequiredFieldValidator ControlToValidate="txtEmail" runat="server" Display="Dynamic" ErrorMessage="Please enter your email." ForeColor="#D3564A" />
                <asp:RegularExpressionValidator ControlToValidate="txtEmail" runat="server" Display="Dynamic"
                    ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" ErrorMessage="Please enter a valid email." ForeColor="#D3564A" />
            </div>
            <div class="field">
                <label for="txtSubject">Subject</label>
                <asp:DropDownList ID="ddlSubject" runat="server">
                    <asp:ListItem Text="General Enquiry" Value="General" />
                    <asp:ListItem Text="Booking Question" Value="Booking" />
                    <asp:ListItem Text="Group / Corporate Rates" Value="Group" />
                    <asp:ListItem Text="Feedback" Value="Feedback" />
                </asp:DropDownList>
            </div>
            <div class="field">
                <label for="txtMessage">Message</label>
                <asp:TextBox ID="txtMessage" runat="server" TextMode="MultiLine" Rows="5" required="required" />
                <asp:RequiredFieldValidator ControlToValidate="txtMessage" runat="server" Display="Dynamic" ErrorMessage="Please enter a message." ForeColor="#D3564A" />
            </div>

            <asp:Button ID="btnSend" runat="server" Text="Send Message" CssClass="btn-regal" OnClick="btnSend_Click" style="width:100%;" />
        </div>
    </section>

    <section class="section reveal" style="text-align:center;">
        <h2 class="section-title">Prefer to Chat?</h2>
        <p class="section-sub">Use the concierge chat bubble in the corner for instant answers to common questions.</p>
    </section>

</asp:Content>
