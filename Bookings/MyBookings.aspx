<%@ Page Title="My bookings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyBookings.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Bookings.MyBookings" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
 <section class="section"><h1 class="section-title">My bookings</h1><p class="section-sub">Your current and past Regal Inn reservations.</p>
  <asp:Literal ID="Message" runat="server" />
  <asp:Repeater ID="BookingsRepeater" runat="server"><HeaderTemplate><table class="regal-table"><tr><th>Reference</th><th>Branch</th><th>Room</th><th>Stay</th><th>Total</th><th>Status</th></tr></HeaderTemplate><ItemTemplate><tr><td>#<%# Eval("BookingId") %></td><td><%# Eval("BranchName") %></td><td><%# Eval("RoomType") %></td><td><%# Eval("CheckIn", "{0:dd MMM yyyy}") %> – <%# Eval("CheckOut", "{0:dd MMM yyyy}") %></td><td>R<%# Eval("Total", "{0:N2}") %></td><td><span class="status-pill confirmed"><%# Eval("Status") %></span></td></tr></ItemTemplate><FooterTemplate></table></FooterTemplate></asp:Repeater>
 </section>
</asp:Content>
