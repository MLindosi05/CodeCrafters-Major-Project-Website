<%@ Page Title="Home Page" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Default" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Home | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <!-- ================= HERO ================= -->
    <section class="hero" style="min-height:65vh; background-image:linear-gradient(180deg, rgba(0,0,0,.20), rgba(0,0,0,.20)), url('<%= ResolveUrl("~/Images/Pictures/Regal.jpg") %>');">
        <div class="hero-inner">
            <div class="hero-eyebrow">Welcome to</div>
            <h1>The Regal Inn Hotel</h1>
            <p>Timeless comfort, modern hospitality. Discover a stay crafted around you, wherever you are in South Africa.</p>
            <div class="hero-actions">
                <a href="#search" class="btn-regal">Check Availability</a>
                <a href="~/Rooms.aspx" runat="server" class="btn-outline">Explore Rooms</a>
            </div>
        </div>
    </section>

    <!-- ================= SEARCH / BOOKING WIDGET ================= -->
    <div class="search-widget" id="search">
        <div class="field">
            <label for="txtCheckIn">Check-in</label>
            <asp:TextBox ID="txtCheckIn" runat="server" TextMode="Date" CssClass="form-control" />
        </div>
        <div class="field">
            <label for="txtCheckOut">Check-out</label>
            <asp:TextBox ID="txtCheckOut" runat="server" TextMode="Date" CssClass="form-control" />
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
            <label for="ddlRoomType">Room Type</label>
          <asp:DropDownList ID="ddlRoomType" runat="server" />>
        </div>
        <asp:Button ID="btnSearch" runat="server" Text="Search Rooms" CssClass="btn-regal" OnClick="btnSearch_Click" />
    </div>

    <!-- ================= WHY CHOOSE US ================= -->
    <section class="section reveal">
        <h2 class="section-title">Why Stay With Us</h2>
        <p class="section-sub">Every detail considered, so your stay never feels routine.</p>
        <div class="room-grid">
            <div class="room-card">
                <div class="body">
                    <h3>Prime Locations</h3>
                    <p>Conveniently positioned across South Africa's major hubs, close to business and leisure alike.</p>
                </div>
            </div>
            <div class="room-card">
                <div class="body">
                    <h3>24/7 Concierge</h3>
                    <p>Our team — and our virtual concierge — are on hand around the clock for anything you need.</p>
                </div>
            </div>
            <div class="room-card">
                <div class="body">
                    <h3>Flexible Booking</h3>
                    <p>Free cancellation up to 48 hours before arrival, with instant confirmation on every reservation.</p>
                </div>
            </div>
        </div>
    </section>

    <!-- ================= FEATURED ROOMS ================= -->
    <section class="section reveal" id="rooms">
        <h2 class="section-title">Featured Rooms</h2>
        <p class="section-sub">A glimpse of what's waiting for you.</p>
        <asp:Repeater ID="rptFeaturedRooms" runat="server">
            <HeaderTemplate><div class="room-grid"></HeaderTemplate>
            <ItemTemplate>
                <div class="room-card">
                    <div class="img-wrap">
                    <img src='<%# Eval("ImageUrl") %>' alt='<%# Eval("RoomName") %>' />
                    </div>
                    <div class="body">
                        <h3><%# Eval("RoomName") %></h3>
                        <div class="amenities">
                            <span class="badge-amenity">Free Wi-Fi</span>
                            <span class="badge-amenity">Breakfast</span>
                            <span class="badge-amenity">Air-con</span>
                        </div>
                        <div class="price">R<%# Eval("PricePerNight") %> / night</div>
                        <a class="btn-regal" href='<%# "Rooms.aspx?roomId=" + Eval("RoomId") %>'>View &amp; Book</a>
                    </div>
                </div>
            </ItemTemplate>
            <FooterTemplate></div></FooterTemplate>
        </asp:Repeater>
    </section>

    <!-- ================= TESTIMONIALS ================= -->
    <section class="section reveal">
        <h2 class="section-title">Guests Love Us</h2>
        <div class="testimonial-track">
            <div class="testimonial-card">
                <div class="stars">★★★★★</div>
                <p>"Spotless rooms, warm staff, and the booking process was effortless from start to finish."</p>
                <strong>— Thando M.</strong>
            </div>
            <div class="testimonial-card">
                <div class="stars">★★★★★</div>
                <p>"Felt like a five-star stay without the five-star hassle. Will definitely be back."</p>
                <strong>— Kwazi N.</strong>
            </div>
            <div class="testimonial-card">
                <div class="stars">★★★★☆</div>
                <p>"Great location and the chatbot actually answered my questions instantly — nice touch."</p>
                <strong>— Amahle P.</strong>
            </div>
        </div>
    </section>

    <!-- ================= NEWSLETTER CTA ================= -->
    <section class="section reveal" style="text-align:center;">
        <h2 class="section-title">Stay in the Know</h2>
        <p class="section-sub">Exclusive offers and seasonal rates, straight to your inbox.</p>
        <div style="display:flex; gap:1rem; justify-content:center; flex-wrap:wrap;">
            <asp:TextBox ID="txtNewsletterEmail" runat="server" placeholder="you@example.com" style="min-width:280px;" />
            <asp:Button ID="btnSubscribe" runat="server" Text="Subscribe" CssClass="btn-regal" OnClick="btnSubscribe_Click" />
        </div>
        <asp:Literal ID="litNewsletterMsg" runat="server" />
    </section>

</asp:Content>
