<%@ Page Title="About" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="About.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.About" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    About Us | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="hero" style="min-height:55vh; background-image:linear-gradient(180deg, rgba(0,53,128,.55), rgba(0,53,128,.78)), url('<%= ResolveUrl("~/Images/Pictures/Regal.jpg") %>');">
        <div class="hero-inner">
            <div class="hero-eyebrow">Our Story</div>
            <h1>Hospitality, Elevated</h1>
            <p>Two decades of welcoming guests across South Africa with warmth, precision, and genuine care.</p>
        </div>
    </section>

    <section class="section">
        <h2 class="section-title">Find a Regal Inn</h2>
        <p class="section-sub">Five welcoming locations across KwaZulu-Natal and Gauteng.</p>
        <div class="form-regal" style="max-width:560px; margin-bottom:2rem; padding:1.25rem;">
            <label for="ddlMapBranch">Choose a branch to view its location</label>
            <asp:DropDownList ID="ddlMapBranch" runat="server" ClientIDMode="Static" CssClass="regal-input" />
        </div>
        <div class="room-grid">
       <asp:Repeater ID="BranchesRepeater" runat="server">
    <ItemTemplate>
        <article class="room-card">
            <img src='<%# ResolveUrl("~/Images/Pictures/branches/" + Eval("Image")) %>' alt='<%# Eval("Name") %>' class="branch-image" />
            <div class="body">
                <h3><%# Eval("Name") %></h3>
                <p><%# Eval("Address") %></p>
                <p style="color:var(--text-muted)"><%# Eval("Phone") %><br /><%# Eval("Email") %></p>
                <a target="_blank" rel="noopener" href='<%# "https://www.google.com/maps/search/?api=1&query=" + Server.UrlEncode(Eval("Address").ToString()) %>'>View on Google Maps</a>
            </div>
        </article>
    </ItemTemplate>
</asp:Repeater>
        </div>
        <div style="margin-top:2rem; border:1px solid var(--border); border-radius:var(--radius-lg); overflow:hidden; height:410px;">
            <iframe id="branchMap" title="Regal Inn branch location" width="100%" height="100%" style="border:0" loading="lazy" allowfullscreen=""></iframe>
        </div>
    </section>

    <section class="section">
        <h2 class="section-title">Who We Are</h2>
        <p class="section-sub" style="max-width:800px;">
            The Regal Inn Hotel was founded on a simple idea: every guest deserves to feel like the
            only guest. From our front desk to our housekeeping team, every role exists to make your
            stay effortless — whether you're here for business, a weekend escape, or a celebration.
        </p>
    </section>

    <section class="section">
        <h2 class="section-title">Our Values</h2>
        <div class="room-grid">
            <div class="room-card"><div class="body">
                <h3>Genuine Care</h3>
                <p>We treat every booking as a relationship, not a transaction.</p>
            </div></div>
            <div class="room-card"><div class="body">
                <h3>Consistency</h3>
                <p>The same high standard, every room, every visit, every time.</p>
            </div></div>
            <div class="room-card"><div class="body">
                <h3>Local Roots</h3>
                <p>Proudly South African, built for South African travellers.</p>
            </div></div>
        </div>
    </section>

    <section class="section" style="text-align:center;">
        <h2 class="section-title">Ready to Experience It?</h2>
        <a href="~/Rooms.aspx" runat="server" class="btn-regal">Browse Rooms</a>
    </section>

</asp:Content>

<asp:Content ID="Scripts" ContentPlaceHolderID="ScriptContent" runat="server">
    <script>
        document.addEventListener("DOMContentLoaded", function () {
            var selector = document.getElementById("ddlMapBranch");
            var map = document.getElementById("branchMap");
            function updateMap() {
                map.src = "https://www.google.com/maps?q=" + encodeURIComponent(selector.value) + "&output=embed";
            }
            selector.addEventListener("change", updateMap);
            updateMap();
        });
    </script>
</asp:Content>
