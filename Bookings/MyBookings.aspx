<%@ Page Title="My bookings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyBookings.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Bookings.MyBookings" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
 <section class="section"><h1 class="section-title">My bookings</h1><p class="section-sub">Your current and past Regal Inn reservations.</p>
     <div>
         <asp:GridView ID="GVCustDetails" runat="server" style="margin: 0 auto; text-align: center;" Width="353px" BackColor="White" BorderColor="#CCCCCC" BorderStyle="None" BorderWidth="1px" CellPadding="3" DataSourceID="SqlDsCust">
             <FooterStyle BackColor="White" ForeColor="#000066" />
             <HeaderStyle BackColor="#006699" Font-Bold="True" ForeColor="White" />
             <PagerStyle BackColor="White" ForeColor="#000066" HorizontalAlign="Left" />
             <RowStyle ForeColor="#000066" />
             <SelectedRowStyle BackColor="#669999" Font-Bold="True" ForeColor="White" />
             <SortedAscendingCellStyle BackColor="#F1F1F1" />
             <SortedAscendingHeaderStyle BackColor="#007DBB" />
             <SortedDescendingCellStyle BackColor="#CAC9C9" />
             <SortedDescendingHeaderStyle BackColor="#00547E" />
         </asp:GridView>
         <asp:SqlDataSource ID="SqlDsCust" runat="server" ConnectionString="<%$ ConnectionStrings:ConnString %>" ProviderName="<%$ ConnectionStrings:ConnString.ProviderName %>" SelectCommand="SELECT * FROM [Client] WHERE ([Email_Address] = @Email_Address)">
             <SelectParameters>
                 <asp:Parameter Name="Email_Address" Type="String" />
             </SelectParameters>
         </asp:SqlDataSource>
     </div>
     <hr />
     <hr />
     <div>
         <asp:SqlDataSource ID="sQLDSBookings" runat="server" ConnectionString="<%$ ConnectionStrings:ConnString %>" SelectCommand="SELECT [Booking_ID], [Client_ID], [Branch_ID], [Booking_Date], [Checkin_Date], [Checkout_Date], [Booking_Total_Amount], [Booking_Status], [Booking_Type] FROM [Booking] WHERE ([Client_ID] = @Client_ID)">
             <SelectParameters>
                 <asp:Parameter Name="Client_ID" Type="Int32" />
             </SelectParameters>
         </asp:SqlDataSource>
         <asp:GridView ID="GVBookings" runat="server"  style="margin: 0 auto; text-align: center;" Width="242px" AutoGenerateColumns="False" BackColor="White" BorderColor="#CCCCCC" BorderStyle="None" BorderWidth="1px" CellPadding="3" DataKeyNames="Booking_ID" DataSourceID="sQLDSBookings">
             <Columns>
                 <asp:BoundField DataField="Booking_ID" HeaderText="Booking_ID" InsertVisible="False" ReadOnly="True" SortExpression="Booking_ID" />
                 <asp:BoundField DataField="Client_ID" HeaderText="Client_ID" SortExpression="Client_ID" />
                 <asp:BoundField DataField="Branch_ID" HeaderText="Branch_ID" SortExpression="Branch_ID" />
                 <asp:BoundField DataField="Booking_Date" HeaderText="Booking_Date" SortExpression="Booking_Date" />
                 <asp:BoundField DataField="Checkin_Date" HeaderText="Checkin_Date" SortExpression="Checkin_Date" />
                 <asp:BoundField DataField="Checkout_Date" HeaderText="Checkout_Date" SortExpression="Checkout_Date" />
                 <asp:BoundField DataField="Booking_Total_Amount" HeaderText="Booking_Total_Amount" SortExpression="Booking_Total_Amount" />
                 <asp:BoundField DataField="Booking_Status" HeaderText="Booking_Status" SortExpression="Booking_Status" />
                 <asp:BoundField DataField="Booking_Type" HeaderText="Booking_Type" SortExpression="Booking_Type" />
             </Columns>
             <FooterStyle BackColor="White" ForeColor="#000066" />
             <HeaderStyle BackColor="#006699" Font-Bold="True" ForeColor="White" />
             <PagerStyle BackColor="White" ForeColor="#000066" HorizontalAlign="Left" />
             <RowStyle ForeColor="#000066" />
             <SelectedRowStyle BackColor="#669999" Font-Bold="True" ForeColor="White" />
             <SortedAscendingCellStyle BackColor="#F1F1F1" />
             <SortedAscendingHeaderStyle BackColor="#007DBB" />
             <SortedDescendingCellStyle BackColor="#CAC9C9" />
             <SortedDescendingHeaderStyle BackColor="#00547E" />
         </asp:GridView>
     </div>



     <%--Previous Code--%> 
 <%-- <asp:Literal ID="Message" runat="server" />
  <asp:Repeater ID="BookingsRepeater" runat="server"><HeaderTemplate><table class="regal-table"><tr><th>Reference</th><th>Branch</th><th>Room</th><th>Stay</th><th>Total</th><th>Status</th></tr></HeaderTemplate><ItemTemplate><tr><td>#<%# Eval("BookingId") %></td><td><%# Eval("BranchName") %></td><td><%# Eval("RoomType") %></td><td><%# Eval("CheckIn", "{0:dd MMM yyyy}") %> – <%# Eval("CheckOut", "{0:dd MMM yyyy}") %></td><td>R<%# Eval("Total", "{0:N2}") %></td><td><span class="status-pill confirmed"><%# Eval("Status") %></span></td></tr></ItemTemplate><FooterTemplate></table></FooterTemplate></asp:Repeater>
 --%></section>
</asp:Content>
