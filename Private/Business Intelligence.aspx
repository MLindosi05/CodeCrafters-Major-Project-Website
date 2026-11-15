<%@ Page Title="Operations Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Business Intelligence.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Private.Business_Intelligence" %>
<asp:Content ID="TitleContent" ContentPlaceHolderID="TitleContent" runat="server">Operations Dashboard | The Regal Inn Hotel</asp:Content>
<asp:Content ID="DashboardContent" ContentPlaceHolderID="MainContent" runat="server">
    <section class="section manager-dashboard">
        <div class="dashboard-heading">
            <div><span class="dashboard-kicker">REGAL INN · OPERATIONS</span><h1 class="section-title">Property dashboard</h1></div>
            <asp:Literal ID="AsOfDate" runat="server" />
        </div>
        <asp:Literal ID="DashboardError" runat="server" EnableViewState="false" />
        <div class="stat-grid manager-stat-grid">
            <article class="stat-card"><div class="stat-label">Occupancy today</div><div class="stat-value"><asp:Literal ID="OccupancyValue" runat="server" /></div><div class="stat-subtitle"><asp:Literal ID="OccupiedRoomsValue" runat="server" /> rooms occupied</div></article>
            <article class="stat-card"><div class="stat-label">Available today</div><div class="stat-value"><asp:Literal ID="AvailableRoomsValue" runat="server" /></div><div class="stat-subtitle">of <asp:Literal ID="InventoryRoomsValue" runat="server" /> rooms in service</div></article>
            <article class="stat-card"><div class="stat-label">Today's arrivals</div><div class="stat-value"><asp:Literal ID="ArrivalsValue" runat="server" /></div><div class="stat-subtitle">Reservations due to check in</div></article>
            <article class="stat-card"><div class="stat-label">Today's departures</div><div class="stat-value"><asp:Literal ID="DeparturesValue" runat="server" /></div><div class="stat-subtitle">Reservations due to check out</div></article>
            <article class="stat-card"><div class="stat-label">Active reservations</div><div class="stat-value"><asp:Literal ID="ActiveBookingsValue" runat="server" /></div><div class="stat-subtitle">Booked, confirmed, or checked in</div></article>
            <article class="stat-card"><div class="stat-label">Payment holds</div><div class="stat-value"><asp:Literal ID="PaymentHoldsValue" runat="server" /></div><div class="stat-subtitle">Pending and within the hold window</div></article>
        </div>

        <div class="dashboard-panel">
            <div class="panel-heading"><div><h2>Property activity</h2><p>Room capacity and guest movements for today</p></div></div>
            <div class="table-scroll"><table class="property-table">
                <thead><tr><th>Property</th><th>Occupancy</th><th>Available</th><th>Arrivals</th><th>Departures</th></tr></thead>
                <tbody><asp:Literal ID="PropertyRows" runat="server" /></tbody>
            </table></div>
        </div>

        <asp:PlaceHolder ID="PowerBiSection" runat="server" Visible="false">
            <div class="dashboard-panel powerbi-panel">
                <div class="panel-heading"><div><h2>Power BI reports</h2><p>Manager and administrator reporting</p></div></div>
                <asp:Literal ID="PowerBiEmbed" runat="server" />
            </div>
        </asp:PlaceHolder>

        <div class="dashboard-actions">
            <a class="btn-regal" href="~/Private/Room%20Management.aspx" runat="server">Manage rooms</a>
            <a class="btn-outline-regal" href="~/Private/Staff%20Management.aspx" runat="server">Staff management</a>
            <a class="btn-outline-regal" href="~/Bookings/MyBookings.aspx" runat="server">Guest bookings</a>
        </div>
    </section>
</asp:Content>
<asp:Content ID="DashboardStyles" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .manager-dashboard{max-width:1280px;margin:0 auto;padding-top:3rem;padding-bottom:4rem}
        .dashboard-heading{display:flex;align-items:flex-end;justify-content:space-between;gap:1rem;margin-bottom:1.5rem}
        .dashboard-kicker{font-size:.72rem;font-weight:700;letter-spacing:.12em;color:var(--blue-700)}
        .dashboard-heading .section-title{margin:.3rem 0 0;text-align:left}
        .dashboard-date{color:var(--cream-dim);font-size:.9rem}
        .manager-stat-grid{grid-template-columns:repeat(auto-fit,minmax(175px,1fr));gap:1rem}
        .manager-stat-grid .stat-card{min-height:130px}
        .stat-subtitle{color:var(--cream-dim);font-size:.8rem;margin-top:.35rem}
        .dashboard-panel{margin-top:1.5rem;background:#fff;border:1px solid var(--border);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm);overflow:hidden}
        .panel-heading{padding:1.2rem 1.4rem;border-bottom:1px solid var(--border)}
        .panel-heading h2{margin:0;font-size:1.15rem;color:var(--text)}
        .panel-heading p{margin:.25rem 0 0;color:var(--cream-dim);font-size:.85rem}
        .table-scroll{overflow-x:auto}.property-table{width:100%;border-collapse:collapse;min-width:620px}
        .property-table th,.property-table td{text-align:left;padding:.9rem 1.2rem;border-bottom:1px solid var(--border)}
        .property-table th{font-size:.75rem;text-transform:uppercase;letter-spacing:.05em;color:var(--cream-dim);background:#f8fafc}
        .property-table td{color:var(--text);font-size:.9rem}.property-name{font-weight:700}.occupancy-cell{min-width:150px}
        .occupancy-track{height:7px;background:var(--blue-50);border-radius:8px;overflow:hidden;margin-top:.4rem}
        .occupancy-fill{height:100%;background:var(--blue-700);border-radius:8px}
        .dashboard-actions{display:flex;flex-wrap:wrap;gap:.75rem;margin-top:1.25rem}
        .btn-outline-regal{display:inline-flex;align-items:center;justify-content:center;padding:.8rem 1.1rem;border:1px solid var(--blue-700);border-radius:var(--radius-sm);color:var(--blue-700);font-weight:600;text-decoration:none}
        .btn-outline-regal:hover{background:var(--blue-50)}
        .powerbi-frame{display:block;width:100%;height:min(78vh,850px);min-height:520px;border:0;background:#f8fafc}
        .powerbi-message{margin:1.25rem;padding:1rem;border:1px solid var(--border);border-radius:var(--radius-sm);color:var(--cream-dim);background:#f8fafc}
        @media(max-width:640px){.dashboard-heading{align-items:flex-start;flex-direction:column}.dashboard-actions>*{width:100%;text-align:center}}
    </style>
</asp:Content>
