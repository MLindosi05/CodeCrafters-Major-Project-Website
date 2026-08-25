<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="Rooms.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Rooms" %>
<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Rooms &amp; Rates | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="section reveal" style="padding-top:3.5rem;">
        <h1 class="section-title">Rooms &amp; Rates</h1>
        <p class="section-sub">Browse freely — you only need to sign in when you're ready to confirm.</p>

        <!-- Filter bar -->
        <div class="search-widget" style="margin-top:0; margin-bottom:3rem;">
            <div class="field">
                <label>Check-in</label>
                <asp:TextBox ID="txtCheckIn" runat="server" TextMode="Date" />
            </div>
            <div class="field">
                <label>Check-out</label>
                <asp:TextBox ID="txtCheckOut" runat="server" TextMode="Date" />
            </div>
            <div class="field">
                <label>Room Type</label>
                <asp:DropDownList ID="ddlRoomType" runat="server">
                    <asp:ListItem Text="Any" Value="" />
                    <asp:ListItem Text="Standard" Value="Standard" />
                    <asp:ListItem Text="Deluxe" Value="Deluxe" />
                    <asp:ListItem Text="Suite" Value="Suite" />
                </asp:DropDownList>
            </div>
            <div class="field">
                <label>Max Price</label>
                <asp:DropDownList ID="ddlMaxPrice" runat="server">
                    <asp:ListItem Text="Any" Value="0" />
                    <asp:ListItem Text="Up to R1500" Value="1500" />
                    <asp:ListItem Text="Up to R3000" Value="3000" />
                    <asp:ListItem Text="Up to R6000" Value="6000" />
                </asp:DropDownList>
            </div>
            <asp:Button ID="btnFilter" runat="server" Text="Filter" CssClass="btn-regal" OnClick="btnFilter_Click" />
        </div>

        <!-- Room results -->
        <asp:Repeater ID="rptRooms" runat="server" OnItemCommand="rptRooms_ItemCommand">
            <HeaderTemplate><div class="room-grid"></HeaderTemplate>
            <ItemTemplate>
                <div class="room-card">
                    <div class="img-wrap">
                        <img src='<%# Eval("ImageUrl") %>' alt='<%# Eval("RoomName") %>' />
                    </div>
                    <div class="body">
                        <h3><%# Eval("RoomName") %></h3>
                        <p style="color:var(--cream-dim); font-size:.9rem;"><%# Eval("Blurb") %></p>
                        <p style="color:var(--cream-dim); font-size:.78rem; margin-top:-.5rem;">Room <%# Eval("RoomNumber") %> · Branch <%# Eval("BranchId") %></p>
                        <div class="amenities">
                            <span class="badge-amenity">Sleeps <%# Eval("MaxGuests") %></span>
                            <span class="badge-amenity">Free Wi-Fi</span>
                            <span class="badge-amenity">Breakfast Included</span>
                        </div>
                        <div class="price">R<%# Eval("PricePerNight") %> / night</div>
                        <asp:Button runat="server" Text="Book Now" CssClass="btn-regal"
                            CommandName="Book" CommandArgument='<%# Eval("RoomId") %>' />
                    </div>
                </div>
            </ItemTemplate>
            <FooterTemplate></div></FooterTemplate>
        </asp:Repeater>

        <asp:Panel ID="pnlNoResults" runat="server" Visible="false" style="text-align:center; padding:3rem 0; color:var(--cream-dim);">
            No rooms match those filters — try widening your search.
        </asp:Panel>
    </section>

    <!-- ================= BOOKING MODAL (guest-friendly, gates on confirm) ================= -->
    <asp:Panel ID="pnlBookingModal" runat="server" CssClass="form-regal" Style="display:none; position:fixed; top:8%; left:50%; transform:translateX(-50%); z-index:1500; max-height:84vh; overflow:auto;">
        <h3>Confirm Your Booking</h3>
        <asp:HiddenField ID="hdnSelectedRoomId" runat="server" />
        <div class="field">
            <label>Room</label>
            <asp:Label ID="lblSelectedRoom" runat="server" />
        </div>
        <div class="field">
            <label>Check-in</label>
            <asp:TextBox ID="txtModalCheckIn" runat="server" TextMode="Date" />
        </div>
        <div class="field">
            <label>Check-out</label>
            <asp:TextBox ID="txtModalCheckOut" runat="server" TextMode="Date" />
        </div>

        <!-- If the user isn't authenticated, prompt inline instead of a hard redirect wall -->
        <asp:Panel ID="pnlLoginPrompt" runat="server" Visible="false" style="background:rgba(212,160,23,.1); padding:1rem; border-radius:8px; margin-bottom:1rem;">
            <p style="margin:0 0 .8rem;">You'll need an account to confirm this booking — it only takes a minute.</p>
            <a href="~/Account/Login.aspx" runat="server" class="btn-regal">Log In</a>
            <a href="~/Account/Register.aspx" runat="server" class="btn-outline">Create Account</a>
        </asp:Panel>

        <asp:Button ID="btnConfirmBooking" runat="server" Text="Confirm Booking" CssClass="btn-regal" OnClick="btnConfirmBooking_Click" />
        <asp:Button ID="btnCloseModal" runat="server" Text="Cancel" CssClass="btn-outline" CausesValidation="false" OnClientClick="document.getElementById('regalBookingModalWrap').style.display='none'; return false;" />
    </asp:Panel>
    <div id="regalBookingModalWrap" style="display:none; position:fixed; inset:0; background:rgba(7,21,34,.75); z-index:1400;"></div>

</asp:Content>

<asp:Content ID="ScriptC" ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        // Show the dimmed backdrop whenever the modal panel is server-rendered visible
        document.addEventListener("DOMContentLoaded", function () {
            var modal = document.getElementById("<%= pnlBookingModal.ClientID %>");
            var backdrop = document.getElementById("regalBookingModalWrap");
            if (modal && modal.style.display !== "none") {
                backdrop.style.display = "block";
            }
        });
    </script>
</asp:Content>
<%--  --%>