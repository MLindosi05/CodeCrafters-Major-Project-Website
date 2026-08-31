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
                <h3 style="margin-top:0;">Payment Details</h3>
                <p style="color:var(--text-muted); font-size:.85rem; margin-top:-.5rem;">
                    This is a placeholder form — no real payment is processed yet.
                </p>

                <div class="field">
                    <label for="cardName">Name on Card</label>
                    <input type="text" id="cardName" class="regal-input" placeholder="e.g. J. Dlamini" />
                </div>

                <div class="field">
                    <label for="cardNumber">Card Number</label>
                    <input type="text" id="cardNumber" class="regal-input" placeholder="0000 0000 0000 0000" maxlength="19" />
                </div>

                <div style="display:flex; gap:1rem;">
                    <div class="field" style="flex:1;">
                        <label for="cardExpiry">Expiry</label>
                        <input type="text" id="cardExpiry" class="regal-input" placeholder="MM/YY" maxlength="5" />
                    </div>
                    <div class="field" style="flex:1;">
                        <label for="cardCvv">CVV</label>
                        <input type="text" id="cardCvv" class="regal-input" placeholder="123" maxlength="4" />
                    </div>
                </div>

                <div id="paymentMsg" class="form-msg" style="display:none;"></div>

                <button type="button" class="btn-regal" style="width:100%; font-size:1rem; margin-top:.5rem;" onclick="submitMockPayment()">
                    Pay Now
                </button>
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
    <script>
        // Reads the cart items already stored by RegalCart (see cart-widget.js)
        // and renders them here as an order summary.
        function loadOrderSummary() {
            var body = document.getElementById("paymentCartBody");
            var totalEl = document.getElementById("paymentCartTotal");
            var items = [];

            try {
                var raw = sessionStorage.getItem("regalCartItems");
                items = raw ? JSON.parse(raw) : [];
            } catch (e) {
                items = [];
            }

            if (items.length === 0) {
                body.innerHTML = '<p style="color:var(--text-muted);">Your cart is empty. <a href="<%= ResolveUrl("~/Rooms.aspx") %>">Browse rooms</a> to add some.</p>';
                totalEl.textContent = "R0";
                return;
            }

            var html = "";
            var total = 0;
            items.forEach(function (item) {
                var price = Number(item.pricePerNight) || 0;
                total += price;
                html += '<div style="display:flex; justify-content:space-between; align-items:flex-start; border-bottom:1px solid var(--border); padding-bottom:.8rem;">' +
                    '<div>' +
                        '<div style="font-weight:600;">' + escapeHtml(item.roomName) + '</div>' +
                        '<div style="font-size:.8rem; color:var(--text-muted);">' + escapeHtml(item.branchName || "") + '</div>' +
                    '</div>' +
                    '<div style="font-weight:700; color:var(--blue-700); white-space:nowrap;">R' + price + '</div>' +
                '</div>';
            });

            body.innerHTML = html;
            totalEl.textContent = "R" + total;
        }

        function escapeHtml(str) {
            var div = document.createElement("div");
            div.textContent = str;
            return div.innerHTML;
        }

        // Placeholder "payment" — validates nothing real, just simulates success
        // and clears the cart. Swap this out once real payment processing
        // and a booking-creation endpoint are wired up.
        function submitMockPayment() {
            var msg = document.getElementById("paymentMsg");
            var cardNumber = document.getElementById("cardNumber").value.trim();

            if (!cardNumber) {
                msg.className = "form-msg error";
                msg.style.display = "block";
                msg.textContent = "Please enter card details to continue.";
                return;
            }

            sessionStorage.removeItem("regalCartItems");
            if (window.RegalCart && RegalCart.render) RegalCart.render();

            msg.className = "form-msg success";
            msg.style.display = "block";
            msg.textContent = "Payment Complete Thank you .";

            loadOrderSummary();
        }

        document.addEventListener("DOMContentLoaded", loadOrderSummary);
    </script>
</asp:Content>

