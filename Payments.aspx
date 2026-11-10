<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Payments.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Payments" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Payment | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="section" style="padding-top:3.5rem; max-width:900px;">
        <h1 class="section-title">Complete Your Booking</h1>
        <p class="section-sub">Review your selected rooms and enter payment details.</p>

        <div style="display:grid; grid-template-columns: 1.1fr .9fr; gap:2rem; align-items:start;">

            <!-- ============ PAYMENT FORM ============ -->
          <div class="form-regal" style="max-width:none;">
    <h3 style="margin-top:0;">Stay Details &amp; Payment</h3>

    <div style="display:flex; gap:1rem;">
        <div class="field" style="flex:1;">
            <label for="payCheckIn">Check-in</label>
            <input type="date" id="payCheckIn" class="regal-input" />
        </div>
        <div class="field" style="flex:1;">
            <label for="payCheckOut">Check-out</label>
            <input type="date" id="payCheckOut" class="regal-input" />
        </div>
    </div>

    <div id="paymentMsg" class="form-msg" style="display:none;"></div>
    <div id="paypal-button-container" style="margin-top:1rem;"></div>
</div>

            <!-- ============ ORDER SUMMARY ============ -->
            <div class="form-regal" style="max-width:none; background:var(--blue-50);">
                <h3 style="margin-top:0;">Order Summary</h3>
                <div id="paymentCartBody" style="display:flex; flex-direction:column; gap:1rem;">
                    <p style="color:var(--text-muted);">Loading your selected rooms…</p>
                </div>
                <div style="border-top:1px solid var(--border); margin-top:1rem; padding-top:1rem; display:flex; justify-content:space-between; font-weight:700; font-size:1.1rem;">
                    <span>Total</span>
                    <span id="paymentCartTotal">R0</span>
                </div>
            </div>

        </div>
    </section>

</asp:Content>

<asp:Content ID="ScriptC" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="https://www.paypal.com/sdk/js?client-id=<%= PayPalClientId %>&currency=USD"></script>
    <script>
        var CART_KEY = "regalCartItems";

        function getCart() {
            try { return JSON.parse(sessionStorage.getItem(CART_KEY)) || []; }
            catch (e) { return []; }
        }

        function nightsBetween() {
            var a = document.getElementById("payCheckIn").value;
            var b = document.getElementById("payCheckOut").value;
            if (!a || !b) return 0;
            return Math.round((new Date(b) - new Date(a)) / 86400000);
        }

        // Display-only estimate. The server recalculates the real amount.
        function loadOrderSummary() {
            var body = document.getElementById("paymentCartBody");
            var totalEl = document.getElementById("paymentCartTotal");
            var items = getCart();
            var nights = Math.max(nightsBetween(), 0);

            if (items.length === 0) {
                body.innerHTML = '<p style="color:var(--text-muted);">Your cart is empty. <a href="<%= ResolveUrl("~/Rooms.aspx") %>">Browse rooms</a> to add some.</p>';
                totalEl.textContent = "R0";
                return;
            }

            var html = "", total = 0;
            items.forEach(function (item) {
                var price = parsePrice(item.pricePerNight);
                var line = price * (nights || 1);
                total += line;
                html += '<div style="display:flex; justify-content:space-between; border-bottom:1px solid var(--border); padding-bottom:.8rem;">' +
                    '<div><div style="font-weight:600;">' + escapeHtml(item.roomName) + '</div>' +
                    '<div style="font-size:.8rem; color:var(--text-muted);">' + escapeHtml(item.branchName || "") +
                    ' · R' + price + ' × ' + (nights || 1) + ' night(s)</div></div>' +
                    '<div style="font-weight:700; color:var(--blue-700);">R' + line + '</div></div>';
            });
            body.innerHTML = html;
            totalEl.textContent = "R" + total;
        }

        function escapeHtml(str) {
            var div = document.createElement("div");
            div.textContent = str;
            return div.innerHTML;
        }
        function parsePrice(v) {
            var cleaned = String(v).replace(/[^0-9,.]/g, "").replace(",", ".");
            var n = parseFloat(cleaned);
            return isNaN(n) ? 0 : n;
        }

        function showMsg(text, ok) {
            var msg = document.getElementById("paymentMsg");
            msg.className = "form-msg " + (ok ? "success" : "error");
            msg.style.display = "block";
            msg.textContent = text;
        }

        function postJson(url, data) {
            return fetch(url, {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                credentials: "same-origin",
                body: JSON.stringify(data)
            }).then(function (r) {
                return r.json().then(function (j) {
                    if (!r.ok) throw new Error(j.error || "Request failed");
                    return j;
                });
            });
        }

        paypal.Buttons({
            createOrder: function () {
                var items = getCart();
                if (items.length === 0) { showMsg("Your cart is empty.", false); throw new Error("empty"); }
                if (nightsBetween() < 1) { showMsg("Please choose valid check-in and check-out dates.", false); throw new Error("dates"); }

                return postJson("<%= ResolveUrl("~/PayPal/CreateOrder.ashx") %>", {
                    roomIds: items.map(function (i) { return i.roomId; }),
                    checkIn: document.getElementById("payCheckIn").value,
                    checkOut: document.getElementById("payCheckOut").value
                }).then(function (res) { return res.id; });
            },

            onApprove: function (data) {
                return postJson("<%= ResolveUrl("~/PayPal/CaptureOrder.ashx") %>", { orderID: data.orderID })
                    .then(function (res) {
                        sessionStorage.removeItem(CART_KEY);
                        if (window.RegalCart && RegalCart.render) RegalCart.render();
                        window.location = "<%= ResolveUrl("~/Bookings/Confirmation.aspx") %>?bookingId=" + res.bookingIds[0];
                    })
                    .catch(function (err) { showMsg(err.message, false); });
            },

            onCancel: function () { showMsg("Payment cancelled. You have not been charged.", false); },
            onError: function (err) { if (err && err.message) showMsg("PayPal error: " + err.message, false); }
        }).render("#paypal-button-container");

        document.addEventListener("DOMContentLoaded", function () {
            loadOrderSummary();
            document.getElementById("payCheckIn").addEventListener("change", loadOrderSummary);
            document.getElementById("payCheckOut").addEventListener("change", loadOrderSummary);
        });
      f
    </script>
</asp:Content>

