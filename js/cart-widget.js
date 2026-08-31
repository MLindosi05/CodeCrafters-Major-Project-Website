// Regal Inn — shopping cart widget (front-end only for now).
// Stores selected rooms in sessionStorage as a simple array of objects:
// { roomId, roomName, branchName, pricePerNight }
// Not yet wired into the real booking flow — this just gives the UI shell.

var RegalCart = (function () {
    var STORAGE_KEY = "regalCartItems";

    function getItems() {
        try {
            var raw = sessionStorage.getItem(STORAGE_KEY);
            return raw ? JSON.parse(raw) : [];
        } catch (e) {
            return [];
        }
    }

    function saveItems(items) {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(items));
        render();
    }

    function addItem(room) {
        var items = getItems();
        // Avoid adding the exact same room twice
        if (items.some(function (i) { return i.roomId === room.roomId; })) return;
        items.push(room);
        saveItems(items);
    }

    function removeItem(roomId) {
        var items = getItems().filter(function (i) { return i.roomId !== roomId; });
        saveItems(items);
    }
    function toggle() {
        var win = document.getElementById("regal-cart-window");
        if (!win) return;
        win.style.display = (win.style.display === "flex") ? "none" : "flex";
    }

    function render() {
        var items = getItems();
        var body = document.getElementById("regal-cart-body");
        var footer = document.getElementById("regal-cart-footer");
        var countBadge = document.getElementById("regal-cart-count");
        var totalEl = document.getElementById("regal-cart-total");

        if (!body) return; // widget not on this page yet

        if (items.length === 0) {
            body.innerHTML = '<div class="cart-empty">No rooms selected yet. Browse Rooms &amp; Rates to add some.</div>';
            footer.style.display = "none";
            countBadge.style.display = "none";
            return;
        }

        var html = "";
        var total = 0;
        items.forEach(function (item) {
            total += Number(item.pricePerNight) || 0;
            html += '<div class="cart-item">' +
                '<div class="cart-item-info">' +
                    '<h4>' + escapeHtml(item.roomName) + '</h4>' +
                    '<div class="cart-item-meta">' + escapeHtml(item.branchName || "") + '</div>' +
                    '<div class="cart-item-price">R' + item.pricePerNight + ' / night</div>' +
                    '<button type="button" class="cart-item-remove" onclick="RegalCart.remove(\'' + item.roomId + '\')">Remove</button>' +
                '</div>' +
            '</div>';
        });

        body.innerHTML = html;
        footer.style.display = "block";
        totalEl.textContent = "R" + total;
        countBadge.style.display = items.length > 0 ? "flex" : "none";;
        countBadge.textContent = items.length;
    }

    function escapeHtml(str) {
        var div = document.createElement("div");
        div.textContent = str;
        return div.innerHTML;
    }

    document.addEventListener("DOMContentLoaded", render);

    return {
        add: addItem,
        remove: removeItem,
        toggle: toggle,
        render: render
    };
})();
