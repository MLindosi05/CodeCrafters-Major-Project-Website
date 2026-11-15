<%@ Page Title="Room inventory" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Room Management.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Private.Room_Management" %>
<asp:Content ID="TitleContent" ContentPlaceHolderID="TitleContent" runat="server">Room Inventory | The Regal Inn Hotel</asp:Content>
<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <section class="section manager-rooms">
        <div class="rooms-heading">
            <div><span class="dashboard-kicker">REGAL INN · INVENTORY</span><h1 class="section-title">Room inventory</h1></div>
            <a href="Business%20Intelligence.aspx" class="btn-outline-regal">Back to workspace</a>
        </div>
        <p class="rooms-caption">Search by room, type, or property. Rooms with active or future bookings are locked until those bookings end.</p>
        <asp:Literal ID="RoomFeedback" runat="server" EnableViewState="false" />
        <div class="room-search-panel">
            <div class="room-search-field"><label for="RoomSearch">Search rooms</label><asp:TextBox ID="RoomSearch" runat="server" MaxLength="100" CssClass="room-filter-input" placeholder="Room number, type, or property" /></div>
            <div class="room-search-field"><label for="StatusFilter">Room status</label><asp:DropDownList ID="StatusFilter" runat="server" CssClass="room-filter-input">
                <asp:ListItem Text="All statuses" Value="All" /><asp:ListItem Text="Available" Value="Available" />
                <asp:ListItem Text="Booked" Value="Booked" /><asp:ListItem Text="Unavailable" Value="Unavailable" />
                <asp:ListItem Text="Under maintenance" Value="Under Maintenance" />
            </asp:DropDownList></div>
            <asp:Button ID="SearchRoomsButton" runat="server" Text="Search" CssClass="btn-regal room-search-button" OnClick="SearchRooms_Click" />
        </div>
        <div class="room-inventory-wrap">
            <div class="table-scroll"><asp:GridView ID="RoomGrid" runat="server" AutoGenerateColumns="false" DataKeyNames="RoomId"
                CssClass="room-inventory" GridLines="None" EmptyDataText="No rooms match this search."
                OnRowDataBound="RoomGrid_RowDataBound" OnRowCommand="RoomGrid_RowCommand">
                <Columns>
                    <asp:BoundField DataField="BranchName" HeaderText="Property" HtmlEncode="true" />
                    <asp:BoundField DataField="RoomNumber" HeaderText="Room" HtmlEncode="true" />
                    <asp:BoundField DataField="RoomType" HeaderText="Room type" HtmlEncode="true" />
                    <asp:BoundField DataField="Capacity" HeaderText="Sleeps" HtmlEncode="true" />
                    <asp:TemplateField HeaderText="Rate / night"><ItemTemplate>R<%# Eval("Rate", "{0:N2}") %></ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Status"><ItemTemplate>
                        <asp:DropDownList ID="RoomStatusInput" runat="server" CssClass="room-status-input">
                            <asp:ListItem Text="Available" Value="Available" /><asp:ListItem Text="Booked" Value="Booked" />
                            <asp:ListItem Text="Unavailable" Value="Unavailable" /><asp:ListItem Text="Under maintenance" Value="Under Maintenance" />
                        </asp:DropDownList>
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Current / next booking"><ItemTemplate>
                        <%# Eval("BookingId") == DBNull.Value ? "—" : "#" + Eval("BookingId") %>
                        <asp:Label ID="BookingStay" runat="server" Text='<%# Eval("Checkin_Date") == DBNull.Value ? "" : String.Format(" · {0:dd MMM}–{1:dd MMM yyyy}", Eval("Checkin_Date"), Eval("Checkout_Date")) %>' />
                    </ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Action"><ItemTemplate>
                        <asp:Button ID="SaveRoomButton" runat="server" Text="Save" CommandName="SaveRoomStatus"
                            CommandArgument='<%# Container.DataItemIndex %>' CssClass="btn-regal room-save-button"
                            OnClientClick="return confirmRoomStatusUpdate(this);" />
                        <asp:Label ID="RoomBookingLock" runat="server" Text="Booked" CssClass="room-locked-label" Visible="false" />
                    </ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView></div>
        </div>
    </section>
</asp:Content>
<asp:Content ID="InventoryScripts" ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        function confirmRoomStatusUpdate(button) {
            var row = button.closest("tr");
            var status = row && row.querySelector("select[id$='RoomStatusInput']");
            if (!status || !["Available", "Unavailable", "Under Maintenance"].includes(status.value)) {
                RegalDialog.alert("Choose a valid room status.", { title: "Check room status" });
                return false;
            }
            return RegalDialog.confirmPostback(button, "Change this room's status to " + status.options[status.selectedIndex].text + "?");
        }
    </script>
</asp:Content>
<asp:Content ID="InventoryStyles" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .manager-rooms{max-width:1280px;margin:0 auto;padding-top:3rem;padding-bottom:4rem}
        .rooms-heading{display:flex;align-items:flex-end;justify-content:space-between;gap:1rem;margin-bottom:.5rem}
        .rooms-heading .section-title{text-align:left;margin:.3rem 0 0}
        .dashboard-kicker{font-size:.72rem;font-weight:700;letter-spacing:.12em;color:var(--blue-700)}
        .rooms-caption{color:var(--cream-dim);margin:0 0 1.25rem}
        .room-search-panel{display:grid;grid-template-columns:minmax(220px,1fr) minmax(190px,.55fr) auto;align-items:end;gap:.9rem;margin:0 0 1rem;padding:1rem;background:#fff;border:1px solid var(--border);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm)}
        .room-search-field{display:flex;flex-direction:column;gap:.4rem}.room-search-field label{font-size:.8rem;font-weight:700;color:var(--cream-dim)}
        .room-filter-input,.room-status-input{min-height:42px;padding:.55rem .7rem;border:1px solid var(--border);border-radius:var(--radius-sm);font:inherit;color:var(--text);background:#fff}
        .room-search-button{min-height:42px;padding:.6rem 1.25rem}
        .room-inventory-wrap{background:#fff;border:1px solid var(--border);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm);overflow:hidden}
        .room-inventory{width:100%;min-width:1050px;border-collapse:collapse}
        .room-inventory th,.room-inventory td{padding:.8rem .75rem;border-bottom:1px solid var(--border);text-align:left;vertical-align:middle}
        .room-inventory th{background:#f8fafc;color:var(--cream-dim);font-size:.7rem;text-transform:uppercase;letter-spacing:.04em;white-space:nowrap}
        .room-inventory td{color:var(--text);font-size:.85rem}.room-status-input{min-width:145px;padding:.45rem}
        .room-save-button{padding:.5rem .8rem;white-space:nowrap}.room-locked-label{color:var(--cream-dim);font-size:.78rem}
        .btn-outline-regal{display:inline-flex;align-items:center;justify-content:center;padding:.75rem 1rem;border:1px solid var(--blue-700);border-radius:var(--radius-sm);color:var(--blue-700);font-weight:600;text-decoration:none}
        .btn-outline-regal:hover{background:var(--blue-50)}
        @media(max-width:700px){.rooms-heading{align-items:flex-start;flex-direction:column}.rooms-heading .btn-outline-regal{width:100%}.room-search-panel{grid-template-columns:1fr}.room-search-button{width:100%}}
    </style>
</asp:Content>
