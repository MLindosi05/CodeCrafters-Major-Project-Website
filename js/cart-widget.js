var RegalCart = (function () {
    var STORAGE_KEY = "regalCartItems";
    var STAY_KEY = "regalStaySelection";

    function getItems() {
        try {
            var raw = sessionStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch (e) {
            return [];
        }
    }

    function getStay() {
        try {
            var raw = sessionStorage.getItem(STAY_KEY);
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    function calculateNights() {
        var stay = getStay();
        if (!stay || !stay.checkIn || !stay.checkOut) return 1;
        var start = new Date(stay.checkIn + "T00:00:00");
        var end = new Date(stay.checkOut + "T00:00:00");
        var diff = Math.round((end - start) / 86400000);
        return diff > 0 ? diff : 1;
    }

    function saveItems(items) {
        try {
            sessionStorage.setItem(STORAGE_KEY, JSON.stringify(items));
            render();
            return true;
        } catch (e) {
            if (window.RegalDialog) RegalDialog.alert("Your cart could not be saved. Check your browser storage settings and try again.", { title: "Unable to update cart" });
            return false;
        }
    }

    function clearCart() {
        try {
            sessionStorage.removeItem(STORAGE_KEY);
            sessionStorage.removeItem(STAY_KEY);
            render();
        } catch (e) { }
    }

    function addItem(room, stay) {
        if (!stay || !stay.checkIn || !stay.checkOut || stay.checkOut <= stay.checkIn) {
            if (window.RegalDialog) RegalDialog.alert("Choose your check-in and check-out dates, then search for available rooms first.", { title: "Choose stay dates" });
            return;
        }

        var items = getItems();

        if (items.length > 0) {
            var existingBranch = (items[0].branchName || "").trim().toLowerCase();
            var newBranch = (room.branchName || "").trim().toLowerCase();
            if (existingBranch && newBranch && existingBranch !== newBranch) {
                if (window.RegalDialog) {
                    RegalDialog.alert("You can only book rooms from the same hotel branch in a single reservation. Please clear your cart or finish booking current items before adding rooms from " + room.branchName + ".", { title: "Different branch" });
                }
                return;
            }
        }

        try {
            sessionStorage.setItem(STAY_KEY, JSON.stringify(stay));
        } catch (e) {
            if (window.RegalDialog) RegalDialog.alert("Your stay details could not be saved. Please enable session storage and try again.", { title: "Unable to save stay" });
            return;
        }

        if (items.some(function (i) { return i.roomId === room.roomId; })) {
            if (window.RegalDialog) RegalDialog.alert("That room is already in your cart.", { title: "Room already added" });
            return;
        }
        items.push(room);
        saveItems(items);
    }

    function removeItem(roomId) {
        var items = getItems().filter(function (i) { return i.roomId !== roomId; });
        saveItems(items);
        if (items.length === 0) {
            sessionStorage.removeItem(STAY_KEY);
        }
    }

    function toggle() {
        var win = document.getElementById("regal-cart-window");
        if (!win) return;
        win.style.display = (win.style.display === "flex") ? "none" : "flex";
    }

    function updateTotal() {
        var items = getItems();
        var nights = calculateNights();
        var total = 0;
        items.forEach(function (item) {
            var cleaned = String(item.pricePerNight).replace(/[^0-9,.]/g, "").replace(",", ".");
            var price = parseFloat(cleaned);
            if (!isNaN(price)) total += (price * nights);
        });
        document.querySelectorAll("#regal-cart-total").forEach(function (el) {
            el.textContent = "R" + total.toFixed(2).replace(".", ",");
        });
    }

    function render() {
        updateTotal();
        var items = getItems();
        var nights = calculateNights();
        var body = document.getElementById("regal-cart-body");
        var footer = document.getElementById("regal-cart-footer");
        var countBadge = document.getElementById("regal-cart-count");
        var totalEl = document.getElementById("regal-cart-total");

        if (!body) return;

        if (items.length === 0) {
            body.innerHTML = '<div class="cart-empty">No rooms selected yet. Browse Rooms &amp; Rates to add some.</div>';
            if (footer) footer.style.display = "none";
            if (countBadge) countBadge.style.display = "none";
            return;
        }

        var html = "";
        var grandTotal = 0;
        items.forEach(function (item) {
            var cleaned = String(item.pricePerNight).replace(/[^0-9,.]/g, "").replace(",", ".");
            var price = parseFloat(cleaned) || 0;
            var lineTotal = price * nights;
            grandTotal += lineTotal;

            html += '<div class="cart-item">' +
                '<div class="cart-item-info">' +
                '<h4>' + escapeHtml(item.roomName) + '</h4>' +
                '<div class="cart-item-meta">' + escapeHtml(item.branchName || "") + ' · R' + price.toFixed(2) + ' × ' + nights + ' night(s)</div>' +
                '<div class="cart-item-price">R' + lineTotal.toFixed(2).replace(".", ",") + '</div>' +
                '<button type="button" class="cart-item-remove" onclick="RegalCart.remove(\'' + item.roomId + '\')">Remove</button>' +
                '</div>' +
                '</div>';
        });

        body.innerHTML = html;
        if (footer) footer.style.display = "block";
        if (totalEl) totalEl.textContent = "R" + grandTotal.toFixed(2).replace(".", ",");
        if (countBadge) {
            countBadge.style.display = items.length > 0 ? "flex" : "none";
            countBadge.textContent = items.length;
        }
    }

    function escapeHtml(str) {
        var div = document.createElement("div");
        div.textContent = str;
        return div.innerHTML;
    }

    document.addEventListener("DOMContentLoaded", function () {
        render();
        var checkInInput = document.getElementById("txtCheckIn");
        var checkOutInput = document.getElementById("txtCheckOut");
        var guestsInput = document.getElementById("ddlGuests");

        function updateSessionStay() {
            if (!checkInInput || !checkOutInput || !checkInInput.value || !checkOutInput.value) return;
            try {
                var stay = {
                    checkIn: checkInInput.value,
                    checkOut: checkOutInput.value,
                    guestCount: guestsInput ? guestsInput.value : "2"
                };
                sessionStorage.setItem(STAY_KEY, JSON.stringify(stay));
                render();
            } catch (e) { }
        }

        if (checkInInput) checkInInput.addEventListener("change", updateSessionStay);
        if (checkOutInput) checkOutInput.addEventListener("change", updateSessionStay);
        if (guestsInput) guestsInput.addEventListener("change", updateSessionStay);

        var checkout = document.getElementById("regalCartCheckout");
        if (!checkout) return;
        checkout.addEventListener("click", function (event) {
            var items = getItems();
            if (!items.length) {
                event.preventDefault();
                if (window.RegalDialog) RegalDialog.alert("Add a room to your cart before continuing to booking.", { title: "Your cart is empty" });
                return;
            }
            var stay = getStay();
            var checkIn = stay && stay.checkIn ? new Date(stay.checkIn + "T00:00:00") : null;
            var checkOut = stay && stay.checkOut ? new Date(stay.checkOut + "T00:00:00") : null;
            var today = new Date(); today.setHours(0, 0, 0, 0);
            if (!checkIn || !checkOut || isNaN(checkIn.getTime()) || isNaN(checkOut.getTime()) || checkIn < today || checkOut <= checkIn) {
                event.preventDefault();
                if (window.RegalDialog) RegalDialog.alert("Choose valid check-in and check-out dates in Rooms before continuing to booking.", { title: "Check your stay dates" });
            }
        });
    });

    return {
        add: addItem,
        remove: removeItem,
        toggle: toggle,
        render: render,
        clear: clearCart
    };
})();