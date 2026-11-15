<%@ Page Title="Manager workspace" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Business Intelligence.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Private.Business_Intelligence" %>
<asp:Content ID="TitleContent" ContentPlaceHolderID="TitleContent" runat="server">Manager Workspace | The Regal Inn Hotel</asp:Content>
<asp:Content ID="DashboardContent" ContentPlaceHolderID="MainContent" runat="server">
    <section class="section manager-home">
        <div class="manager-welcome">
            <span class="dashboard-kicker">THE REGAL INN · MANAGER WORKSPACE</span>
            <h1>Welcome to your workspace</h1>
            <p>Manage hotel room availability and staff access, and open your reporting tools from one place.</p>
        </div>

        <div class="manager-tools" aria-label="Manager tools">
            <article class="manager-tool-card">
                <span class="tool-number">01</span>
                <h2>Room inventory</h2>
                <p>Find rooms by status, review current assignments, and update rooms that are unavailable or under maintenance.</p>
                <a class="btn-regal" href="~/Private/Room%20Management.aspx" runat="server">Manage rooms</a>
            </article>
            <article class="manager-tool-card">
                <span class="tool-number">02</span>
                <h2>Staff access</h2>
                <p>Update staff contact details and active access status. Other manager accounts remain protected.</p>
                <a class="btn-outline-regal" href="~/Private/Staff%20Management.aspx" runat="server">Manage staff</a>
            </article>
            <article class="manager-tool-card report-tool">
                <span class="tool-number">03</span>
                <h2>Business reports</h2>
                <p>Open the hotel’s Power BI report for operational and business insights.</p>
                <asp:HyperLink ID="PowerBiLink" runat="server" Target="_blank" rel="noopener" CssClass="btn-outline-regal" Text="Open Power BI report" Visible="false" />
                <asp:Label ID="PowerBiSetupNote" runat="server" CssClass="report-note" Text="Power BI report link is not configured." Visible="false" />
            </article>
        </div>
    </section>
</asp:Content>
<asp:Content ID="DashboardStyles" ContentPlaceHolderID="HeadContent" runat="server">
    <style>
        .manager-home{max-width:1180px;margin:0 auto;padding-top:4.5rem;padding-bottom:5rem}
        .manager-welcome{max-width:720px;margin:0 auto 2.5rem;text-align:center;padding:2.5rem 1.5rem 2rem}
        .manager-welcome h1{color:var(--text);font-size:clamp(2rem,4vw,3rem);line-height:1.15;margin:.75rem 0}
        .manager-welcome p{color:var(--cream-dim);font-size:1.05rem;line-height:1.7;margin:0}
        .dashboard-kicker{font-size:.72rem;font-weight:700;letter-spacing:.13em;color:var(--blue-700)}
        .manager-tools{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:1.1rem}
        .manager-tool-card{display:flex;flex-direction:column;align-items:flex-start;min-height:270px;padding:1.6rem;background:#fff;border:1px solid var(--border);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm)}
        .tool-number{color:var(--blue-700);font-size:.75rem;font-weight:800;letter-spacing:.12em}
        .manager-tool-card h2{color:var(--text);font-size:1.25rem;margin:1rem 0 .45rem}
        .manager-tool-card p{color:var(--cream-dim);font-size:.92rem;line-height:1.6;margin:0 0 1.3rem}
        .manager-tool-card .btn-regal,.manager-tool-card .btn-outline-regal{margin-top:auto}
        .btn-outline-regal{display:inline-flex;align-items:center;justify-content:center;padding:.8rem 1.1rem;border:1px solid var(--blue-700);border-radius:var(--radius-sm);color:var(--blue-700);font-weight:600;text-decoration:none}
        .btn-outline-regal:hover{background:var(--blue-50)}
        .report-note{margin-top:auto;color:var(--cream-dim);font-size:.85rem}
        @media(max-width:850px){.manager-tools{grid-template-columns:1fr}.manager-tool-card{min-height:220px}.manager-home{padding-top:2.5rem}}
    </style>
</asp:Content>
