   <%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Rooms.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Rooms" %>
<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Rooms &amp; Rates | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="section" style="padding-top:3.5rem;">
        <h1 class="section-title">Rooms &amp; Rates</h1>

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
      <label for="ddlGuests">Guests</label>
      <asp:DropDownList ID="ddlGuests" runat="server">
          <asp:ListItem Text="1 Guest" Value="1" />
          <asp:ListItem Text="2 Guests" Value="2" Selected="True" />
          <asp:ListItem Text="3 Guests" Value="3" />
          <asp:ListItem Text="4+ Guests" Value="4" />
      </asp:DropDownList>
  </div>
            <div class="field">
                <label>Branch</label>
                <asp:DropDownList ID="ddlBranch" runat="server" />
            </div>
            <div class="field">
                <label>Room Type</label>
                <asp:DropDownList ID="ddlRoomType" runat="server" />
            </div>
            <div class="field">
                <label>Max Price</label>
                <asp:DropDownList ID="ddlMaxPrice" runat="server">
                    <asp:ListItem Text="Any" Value="0" />
                    <asp:ListItem Text="Up to R1800" Value="1800" />
                    <asp:ListItem Text="Up to R2500" Value="2500" />
                    <asp:ListItem Text="Up to R3000" Value="3000" />
                </asp:DropDownList>
            </div>
            <asp:Button ID="btnFilter" runat="server" Text="Search" CssClass="btn-regal" OnClick="btnFilter_Click" OnClientClick="return validateRoomSearch();" />
            <asp:Label ID="lblStayValidation" runat="server" Visible="false" CssClass="form-msg error" style="grid-column:1/-1;" />
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
            <p style="color:var(--text-muted); font-size:.78rem; margin-top:-.5rem;">Room <%# Eval("RoomNumber") %> · <%# Eval("BranchName") %></p>
            <div class="amenities">
                <span class="badge-amenity">Sleeps <%# Eval("MaxGuests") %></span>
                <span class="badge-amenity">Free Wi-Fi</span>
                <span class="badge-amenity">Breakfast Included</span>
                <asp:Repeater ID="rptAdditionalAmenities" runat="server" DataSource='<%# Eval("AdditionalAmenities") %>'>
                    <ItemTemplate><span class="badge-amenity"><%# Container.DataItem %></span></ItemTemplate>
                </asp:Repeater>
            </div>
            <div class="price">R<%# Eval("PricePerNight") %> / night</div>
            <div style="display:flex; gap:.6rem;">
              <%--  <asp:Button runat="server" Text="Book Now" CssClass="btn-regal"
                    CommandName="Book" CommandArgument='<%# Eval("RoomId") %>' />--%>
                <button type="button" class="btn-outline room-add-to-cart"
                    data-room-id='<%# Eval("RoomId") %>' data-room-name='<%# System.Web.HttpUtility.HtmlAttributeEncode(Convert.ToString(Eval("RoomName"))) %>'
                    data-branch-name='<%# System.Web.HttpUtility.HtmlAttributeEncode(Convert.ToString(Eval("BranchName"))) %>' data-price='<%# Eval("PricePerNight") %>'>
                    Add to Cart
                </button>
            </div>
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
            <a href="~/Account/Login.aspx" runat="server" class="btn-regal">Log In</a>
            <a href="~/Account/Register.aspx" runat="server" class="btn-outline">Create Account</a>
        </asp:Panel>

        <asp:Button ID="btnConfirmBooking" runat="server" Text="Confirm Booking" CssClass="btn-regal" OnClick="btnConfirmBooking_Click" />
              
        <button type="button" class="btn-outline" onclick="
            document.getElementById('regalBookingModalWrap').style.display='none';
            document.getElementById('<%= pnlBookingModal.ClientID %>').style.display='none';
        ">Cancel</button>
    </asp:Panel>
    <div id="regalBookingModalWrap" style="display:none; position:fixed; inset:0; background:rgba(7,21,34,.75); z-index:1400;"></div>

</asp:Content>

<asp:Content ID="ScriptC" ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        var MAX_STAY_NIGHTS = <%= MaximumStayNights %>;
        (function () {
            var checkIn = document.getElementById('<%= txtCheckIn.ClientID %>');
            var checkOut = document.getElementById('<%= txtCheckOut.ClientID %>');
            var search = document.getElementById('<%= btnFilter.ClientID %>');
            function stayValidationMessage() {
                if (!checkIn.value || !checkOut.value) return "Choose both check-in and check-out dates before adding a room to your cart.";
                var now = new Date();
                var today = now.getFullYear() + '-' + String(now.getMonth() + 1).padStart(2, '0') + '-' + String(now.getDate()).padStart(2, '0');
                if (checkIn.value < today) return "Check-in cannot be in the past. Choose another date.";
                if (checkOut.value <= checkIn.value) return "Check-out must be after check-in.";
                var nights = (new Date(checkOut.value + "T00:00:00") - new Date(checkIn.value + "T00:00:00")) / 86400000;
                if (nights > MAX_STAY_NIGHTS) return "Your stay cannot exceed " + MAX_STAY_NIGHTS + " nights.";
                return "";
            }
            window.validateRoomSearch = function () {
                if (!checkIn.value && !checkOut.value) return true;
                var error = stayValidationMessage();
                if (!error) return true;
                RegalDialog.alert(error, { title: "Check your stay dates" });
                return false;
            };
            function changeStay() {
                if (!checkIn.value || !checkOut.value) return;
                var error = stayValidationMessage();
                if (error) { RegalDialog.alert(error, { title: "Check your stay dates" }); return; }
                search.click();
            }
            document.querySelectorAll('.room-add-to-cart').forEach(function (button) {
                button.addEventListener('click', function () {
                    var error = stayValidationMessage();
                    if (error) { RegalDialog.alert(error, { title: "Choose stay dates" }); return; }
                    RegalCart.add({ roomId: button.getAttribute('data-room-id'), roomName: button.getAttribute('data-room-name'),
                        branchName: button.getAttribute('data-branch-name'), pricePerNight: button.getAttribute('data-price') },
                        { checkIn: checkIn.value, checkOut: checkOut.value, guestCount: document.getElementById('<%= ddlGuests.ClientID %>').value });
                });
            });
            checkIn.addEventListener('change', changeStay);
            checkOut.addEventListener('change', changeStay);
            document.getElementById('<%= ddlGuests.ClientID %>').addEventListener('change', function () {
                if (checkIn.value && checkOut.value) search.click();
            });
        })();

    </script>
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
