<%@ Page Title="About" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="About.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.About" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    About Us | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="hero" style="min-height:55vh; background-image:linear-gradient(180deg, rgba(7,21,34,.6), rgba(7,21,34,.95)), url('https://images.unsplash.com/photo-1551882547-ff40c63fe5fa?q=80&w=1974&auto=format&fit=crop');">
        <div class="hero-inner">
            <div class="hero-eyebrow">Our Story</div>
            <h1>Hospitality, Elevated</h1>
            <p>Two decades of welcoming guests across South Africa with warmth, precision, and genuine care.</p>
        </div>
    </section>

    <section class="section reveal">
        <h2 class="section-title">Who We Are</h2>
        <p class="section-sub" style="max-width:800px;">
            The Regal Inn Hotel was founded on a simple idea: every guest deserves to feel like the
            only guest. From our front desk to our housekeeping team, every role exists to make your
            stay effortless — whether you're here for business, a weekend escape, or a celebration.
        </p>
    </section>

    <section class="section reveal">
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

    <section class="section reveal" style="text-align:center;">
        <h2 class="section-title">Ready to Experience It?</h2>
        <a href="~/Rooms.aspx" runat="server" class="btn-regal">Browse Rooms</a>
    </section>

</asp:Content>
