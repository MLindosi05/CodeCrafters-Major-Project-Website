<%@ Page Title="My bookings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="MyBookings.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Bookings.MyBookings" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <section class="section">
        <h1 class="section-title">My bookings</h1>
        <asp:Label ID="lblBookingsMessage" runat="server" Visible="false" />

        <nav class="booking-tabs" aria-label="Booking categories">
            <a href="MyBookings.aspx?tab=upcoming">Upcoming</a>
            <a href="MyBookings.aspx?tab=active">Active</a>
            <a href="MyBookings.aspx?tab=past">Past</a>
            <a href="MyBookings.aspx?tab=cancelled">Cancelled</a>
            <a href="MyBookings.aspx?tab=all">All</a>
        </nav>

        <asp:Panel ID="pnlNoBookings" runat="server" Visible="false" CssClass="form-regal">
            <p>No bookings in this section.</p>
            <a href="../Rooms.aspx" class="btn-regal">Browse rooms</a>
        </asp:Panel>

        <asp:Repeater ID="rptBookings" runat="server" OnItemCommand="rptBookings_ItemCommand">
            <ItemTemplate>
                <article class="booking-summary-card">
                    <header class="booking-summary-header">
                        <div><h2>Booking #<%# Eval("BookingId") %></h2><span><%# Eval("Status") %></span></div>
                        <div><strong><%# Eval("BranchName") %></strong><br /><small>Booked <%# Eval("BookingDate", "{0:dd MMM yyyy}") %></small></div>
                    </header>
                    <p><strong>Stay:</strong> <%# Eval("CheckIn", "{0:dd MMM yyyy}") %> &ndash; <%# Eval("CheckOut", "{0:dd MMM yyyy}") %> &middot; <%# Eval("Nights") %> night(s) &middot; <%# Eval("GuestCount") %> guest(s)</p>
                    <p><strong>Payment:</strong> <%# Eval("PaymentStatus") %></p>
                    <asp:Panel runat="server" Visible='<%# Eval("HasRefund") %>' CssClass="booking-policy-note"><strong>Refund status:</strong> <%# Eval("RefundStatus") %></asp:Panel>
                    <h3>Rooms</h3>
                    <ul class="booking-room-list">
                        <asp:Repeater ID="rptBookingRooms" runat="server" DataSource='<%# Eval("Rooms") %>'>
                            <ItemTemplate><li><strong><%# Eval("RoomType") %></strong> &middot; Room <%# Eval("RoomNumber") %></li></ItemTemplate>
                        </asp:Repeater>
                    </ul>
                    <p class="booking-summary-total"><strong>Total:</strong> R<%# Eval("Total", "{0:N2}") %></p>
                    <asp:Panel runat="server" Visible='<%# Eval("CanCancel") %>' CssClass="booking-policy-note">
                        <strong>Cancellation refund policy</strong>
                        <div style="display:grid;grid-template-columns:repeat(auto-fit,minmax(135px,1fr));gap:.65rem;margin:.65rem 0;">
                            <div style="background:#fff;padding:.65rem;border-radius:7px;"><strong>7+ days before</strong><br />100% credit</div>
                            <div style="background:#fff;padding:.65rem;border-radius:7px;"><strong>48 hours to under 7 days</strong><br />50% credit</div>
                            <div style="background:#fff;padding:.65rem;border-radius:7px;"><strong>Under 48 hours / no-show</strong><br />0% credit</div>
                        </div>
                        <div><strong>Estimated credit if cancelled now:</strong> R<%# Eval("RefundAmount", "{0:N2}") %> (<%# Eval("RefundPercentage") %>%).</div>
                        <small>Demo credit is recorded on the booking; no PayPal refund is sent.</small>
                    </asp:Panel>
                    <div class="booking-actions">
                        <a class="btn-regal" href='<%# "../Payments.aspx?resumeBookingId=" + Eval("BookingId") %>' runat="server" visible='<%# Eval("CanContinuePayment") %>'>Continue to payment</a>
                        <a class="btn-outline" href='<%# "Invoice.ashx?bookingId=" + Eval("BookingId") %>' visible='<%# Eval("CanDownloadInvoice") %>' runat="server">Download invoice</a>
                        <a class="btn-outline" href='<%# "Invoice.ashx?bookingId=" + Eval("BookingId") + "&amp;type=amended" %>' visible='<%# Eval("CanDownloadAmendedInvoice") %>' runat="server">Download amended invoice</a>
                        <a class="btn-outline" href='<%# "Invoice.ashx?bookingId=" + Eval("BookingId") + "&amp;type=credit" %>' visible='<%# Eval("CanDownloadCreditNote") %>' runat="server">Download credit note</a>
                        <asp:LinkButton runat="server" Text="Resend confirmation email" CssClass="btn-outline" CommandName="ResendConfirmation" CommandArgument='<%# Eval("BookingId") %>' Visible='<%# Eval("CanResendConfirmation") %>' OnClientClick="return RegalDialog.confirmPostback(this,'Send another booking confirmation email to the email address on your account?');" />
                        <asp:Panel runat="server" Visible='<%# Eval("CanMobileCheckIn") %>' CssClass="booking-date-editor">
                            <p class="booking-help">Bring the ID or passport entered here for reception verification.</p>
                            <label>ID or passport <asp:TextBox ID="txtCheckInDocument" runat="server" MaxLength="80" /></label>
                            <label>Arrival time <asp:TextBox ID="txtArrivalTime" runat="server" MaxLength="40" /></label>
                            <label>Special requests <asp:TextBox ID="txtSpecialRequests" runat="server" MaxLength="500" /></label>
                            <asp:LinkButton runat="server" Text="Mobile check in" CssClass="btn-regal" CommandName="MobileCheckIn" CommandArgument='<%# Eval("BookingId") %>' OnClientClick="return RegalDialog.confirmPostback(this,'Are you sure you want to complete mobile check-in for this booking?');" />
                        </asp:Panel>
                        <asp:LinkButton runat="server" Text="Mobile check out" CssClass="btn-outline" CommandName="MobileCheckOut" CommandArgument='<%# Eval("BookingId") %>' Visible='<%# Eval("CanMobileCheckOut") %>' OnClientClick="return RegalDialog.confirmPostback(this,'Are you sure you want to complete mobile check-out for this stay?');" />
                        <asp:LinkButton runat="server" Text="Cancel booking" CssClass="btn-outline" CommandName="CancelBooking" CommandArgument='<%# Eval("BookingId") %>' Visible='<%# Eval("CanCancel") %>' OnClientClick="return RegalDialog.confirmPostback(this,'Are you sure you want to cancel this booking? The refund credit shown above follows the cancellation policy. No PayPal refund will be sent.');" />
                        <a class="btn-regal" href='<%# "ManageBooking.aspx?bookingId=" + Eval("BookingId") %>' runat="server" visible='<%# Eval("CanModifyDates") %>'>Change dates, upgrade, or change branch</a>
                    </div>
                </article>
            </ItemTemplate>
        </asp:Repeater>
    </section>
</asp:Content>
