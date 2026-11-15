<%@ Page Title="" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Payments.aspx.cs" Inherits="CodeCrafters_Major_Project_Website.Payments" %>

<asp:Content ID="TitleC" ContentPlaceHolderID="TitleContent" runat="server">
    Payment | The Regal Inn Hotel
</asp:Content>

<asp:Content ID="BodyC" ContentPlaceHolderID="MainContent" runat="server">

    <section class="section" style="padding-top:3.5rem; max-width:900px;">
        <h1 class="section-title">Complete Your Booking</h1>
        <div style="display:flex;gap:1rem;margin:1rem 0 2rem;" aria-label="Booking progress"><strong>1. Rooms</strong><span>→</span><strong>2. Payment</strong><span>→</span><span>3. Confirmation</span></div>

        <div style="display:grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap:2rem; align-items:start;">

            <!-- ============ PAYMENT FORM ============ -->
          <div class="form-regal" style="max-width:none;">
    <h3 style="margin-top:0;">Stay Details &amp; Payment</h3>

    <div style="display:flex; gap:1rem;">
        <div class="field" style="flex:1;">
            <label for="payCheckIn">Check-in</label>
            <input type="date" id="payCheckIn" class="regal-input" value="<%: BookingCheckIn %>" readonly="readonly" />
        </div>
        <div class="field" style="flex:1;">
            <label for="payCheckOut">Check-out</label>
            <input type="date" id="payCheckOut" class="regal-input" value="<%: BookingCheckOut %>" readonly="readonly" />
        </div>
        <a href="<%= ResolveUrl("~/Rooms.aspx") %>">Change dates</a>
    </div>

    <div id="paymentMsg" class="form-msg" style="display:none;"></div>
    <% if (SmsOtpConfigured) { %>
        <div id="paypal-button-container" style="margin-top:1rem;"></div>
    <% } else { %>
        <div class="form-msg error">SMS verification is currently unavailable, so payment cannot be completed.</div>
    <% } %>
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
    <div id="bookingOtpDialog" class="regal-dialog-backdrop" hidden>
        <section class="regal-dialog" role="dialog" aria-modal="true" aria-labelledby="otpTitle">
            <h2 id="otpTitle">Verify your phone</h2>
            <p>Enter the six-digit SMS code sent to your mobile number. The code expires in five minutes.</p>
            <div id="bookingOtpError" class="form-msg error" role="alert" style="display:none;"></div>
            <label for="bookingOtpCode">SMS verification code</label>
            <input id="bookingOtpCode" type="text" inputmode="numeric" autocomplete="one-time-code" maxlength="6" class="regal-input" />
            <div class="regal-dialog-actions"><button id="verifyOtpButton" type="button" class="btn-regal">Verify and confirm booking</button><button id="cancelOtpButton" type="button" class="btn-outline">Cancel</button></div>
        </section>
    </div>
</asp:Content>

<asp:Content ID="ScriptC" ContentPlaceHolderID="ScriptContent" runat="server">
    <script src="https://www.paypal.com/sdk/js?client-id=<%= PayPalClientId %>&currency=USD"></script>
    <script>
        var CART_KEY = "regalCartItems";
        var STAY_KEY = "regalStaySelection";
        var PAYMENT_CSRF = "<%: PaymentCsrfToken %>";
        var PAYMENT_ATTEMPT = "<%: PaymentAttemptToken %>";
        var GUEST_COUNT = "<%: BookingGuestCount %>";
        var SMS_OTP_READY = <%= SmsOtpConfigured ? "true" : "false" %>;
        var RESUMING_BOOKING = <%= IsResumingBooking ? "true" : "false" %>;
        var RESUME_CART = JSON.parse("<%= System.Web.HttpUtility.JavaScriptStringEncode(ResumeCartJson) %>");
        var TAX_PERCENT = parseFloat("<%: BookingTaxPercentage %>") || 0;
        var pendingPayPalOrderId = null;

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

            try {
                var stay = JSON.parse(sessionStorage.getItem(STAY_KEY) || "null");
                if (!stay || stay.checkIn !== document.getElementById("payCheckIn").value || stay.checkOut !== document.getElementById("payCheckOut").value) {
                    showMsg("Your cart dates do not match the selected stay. Return to Rooms, search again, and add the rooms again.", false);
                    items = [];
                }
            } catch (e) { items = []; }

            if (items.length === 0) {
                body.innerHTML = '<p style="color:var(--text-muted);">Your cart is empty. <a href="<%= ResolveUrl("~/Rooms.aspx") %>">Browse rooms</a> to add some.</p>';
                totalEl.textContent = "R0";
                return;
            }

            var html = "", subtotal = 0;
            items.forEach(function (item) {
                var price = parsePrice(item.pricePerNight);
                var line = price * (nights || 1);
                subtotal += line;
                html += '<div style="display:flex; justify-content:space-between; border-bottom:1px solid var(--border); padding-bottom:.8rem;">' +
                    '<div><div style="font-weight:600;">' + escapeHtml(item.roomName) + '</div>' +
                    '<div style="font-size:.8rem; color:var(--text-muted);">' + escapeHtml(item.branchName || "") +
                    ' · R' + price + ' × ' + (nights || 1) + ' night(s)</div></div>' +
                    '<div style="font-weight:700; color:var(--blue-700);">R' + line + '</div></div>';
            });
            var tax = subtotal * TAX_PERCENT / 100;
            body.innerHTML = html + '<div style="display:flex;justify-content:space-between"><span>Subtotal</span><span>R' + subtotal.toFixed(2) + '</span></div>' +
                '<div style="display:flex;justify-content:space-between"><span>Taxes and fees (' + TAX_PERCENT + '%)</span><span>R' + tax.toFixed(2) + '</span></div>' +
                '<div style="display:flex;justify-content:space-between"><span>Guests</span><span>' + escapeHtml(GUEST_COUNT) + '</span></div>';
            totalEl.textContent = "R" + (subtotal + tax).toFixed(2);
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
                body: JSON.stringify(Object.assign({}, data, { csrfToken: PAYMENT_CSRF }))
            }).then(function (r) {
                return r.json().then(function (j) {
                    if (!r.ok) throw new Error(j.error || "Request failed");
                    return j;
                });
            });
        }

        function renderPayPal() {
        paypal.Buttons({
            createOrder: function () {
                var items = getCart();
                if (items.length === 0) { showMsg("Your cart is empty.", false); throw new Error("empty"); }
                if (nightsBetween() < 1) { showMsg("Please choose valid check-in and check-out dates.", false); throw new Error("dates"); }
                return postJson("<%= ResolveUrl("~/PayPal/CreateOrder.ashx") %>", {
                    roomIds: items.map(function (i) { return i.roomId; }),
                    checkIn: document.getElementById("payCheckIn").value,
                    checkOut: document.getElementById("payCheckOut").value,
                    guestCount: GUEST_COUNT,
                    attemptToken: PAYMENT_ATTEMPT
                }).then(function (res) { return res.id; });
            },

            onApprove: function (data) {
                pendingPayPalOrderId = data.orderID;
                var items = getCart();
                var summary = items.map(function (i) { return i.roomName + " — " + (i.branchName || ""); }).join("\n");
                var prompt = "PayPal has collected your payment details. Confirm this booking to send the phone verification code and continue. No payment is captured until you confirm here and verify the SMS code.\n\n" + summary + "\n" + document.getElementById("payCheckIn").value + " to " + document.getElementById("payCheckOut").value + "\nTotal: " + document.getElementById("paymentCartTotal").textContent;
                RegalDialog.confirm(prompt, { title: "Confirm booking", okText: "Confirm and continue" }).then(function (yes) {
                    if (!yes) {
                        showMsg("Booking cancelled. No payment was captured and the booking was not confirmed.", false);
                        return;
                    }
                    return postJson("<%= ResolveUrl("~/PayPal/CreateOrder.ashx?action=otp-send") %>", { orderID: data.orderID })
                        .then(function () {
                            document.getElementById("bookingOtpCode").value = "";
                            document.getElementById("bookingOtpDialog").hidden = false;
                            document.getElementById("bookingOtpCode").focus();
                            showMsg("A verification code was sent by SMS to your profile phone number.", true);
                        })
                        .catch(function (err) { showMsg(err.message, false); });
                });
            },

            onCancel: function () { showMsg("PayPal checkout was cancelled. No payment was captured and the booking was not confirmed.", false); },
            onError: function (err) { if (err && err.message) showMsg("PayPal error: " + err.message, false); }
        }).render("#paypal-button-container");
        }

        function verifyOtpAndCapture() {
            var code = document.getElementById("bookingOtpCode").value.trim();
            var error = document.getElementById("bookingOtpError");
            if (!/^\d{6}$/.test(code)) { error.textContent = "Enter the six-digit SMS code."; error.style.display = "block"; return; }
            error.style.display = "none";
            var button = document.getElementById("verifyOtpButton"); button.disabled = true;
            button.textContent = "Verifying…";
            postJson("<%= ResolveUrl("~/PayPal/CreateOrder.ashx?action=otp-verify") %>", { orderID: pendingPayPalOrderId, code: code })
                .then(function () { return postJson("<%= ResolveUrl("~/PayPal/CreateOrder.ashx?action=capture") %>", { orderID: pendingPayPalOrderId }); })
                .then(function (res) {
                    document.getElementById("bookingOtpDialog").hidden = true;
                        sessionStorage.removeItem(CART_KEY);
                        if (window.RegalCart && RegalCart.render) RegalCart.render();
                        RegalDialog.alert("Payment was captured and booking #" + res.bookingIds[0] + " is confirmed.", { title: "Booking confirmed", okText: "View confirmation" }).then(function () {
                            window.location = "<%= ResolveUrl("~/Bookings/Confirmation.aspx") %>?bookingId=" + res.bookingIds[0];
                        });
                    })
                .catch(function (err) { error.textContent = err.message || "Verification failed. Please try again."; error.style.display = "block"; button.disabled = false; button.textContent = "Verify and confirm booking"; });
        }

        document.addEventListener("DOMContentLoaded", function () {
            document.getElementById("verifyOtpButton").addEventListener("click", verifyOtpAndCapture);
            document.getElementById("cancelOtpButton").addEventListener("click", function () { document.getElementById("bookingOtpDialog").hidden = true; showMsg("Booking not confirmed. Enter the SMS code to continue.", false); });
            if (RESUMING_BOOKING && RESUME_CART.length) {
                sessionStorage.setItem(CART_KEY, JSON.stringify(RESUME_CART));
                sessionStorage.setItem(STAY_KEY, JSON.stringify({ checkIn: document.getElementById("payCheckIn").value,
                    checkOut: document.getElementById("payCheckOut").value, guestCount: GUEST_COUNT }));
            }
            loadOrderSummary();
            if (!SMS_OTP_READY) return;
            var items = getCart();
            var stay = null;
            try { stay = JSON.parse(sessionStorage.getItem(STAY_KEY) || "null"); } catch (e) { }
            if (!items.length) { showMsg("Your cart is empty. Browse Rooms to add a room.", false); return; }
            if (!stay || stay.checkIn !== document.getElementById("payCheckIn").value || stay.checkOut !== document.getElementById("payCheckOut").value) {
                showMsg("Your cart dates do not match. Return to Rooms, search for the correct dates, and add the rooms again.", false);
                return;
            }
            postJson("<%= ResolveUrl("~/PayPal/CreateOrder.ashx?action=validate") %>", {
                roomIds: items.map(function (i) { return i.roomId; }),
                checkIn: document.getElementById("payCheckIn").value,
                checkOut: document.getElementById("payCheckOut").value
            })
                .then(renderPayPal)
                .catch(function (err) { showMsg(err.message, false); });
        });
    </script>
</asp:Content>

