    // ==========================================================================
    // The Regal Inn Hotel — Concierge Chatbot Widget
    // Self-contained rule-based FAQ bot. Injects its own HTML into the page,
    // so it only needs to be included once (from Site.Master) to appear
    // site-wide. Swap answerFor() with an API call later for AI responses.
    // ==========================================================================
    (function () {
        "use strict";

        var FAQ = [
            { keys: ["check in", "checkin", "arrival"], answer: "Check-in is from 14:00. Early check-in is subject to availability — just ask at Reception on arrival." },
            { keys: ["check out", "checkout", "departure"], answer: "Check-out is at 11:00. Late check-out can be arranged for a small fee, availability permitting." },
            { keys: ["cancel", "refund", "cancellation"], answer: "Free cancellation up to 48 hours before arrival. Within 48 hours, the first night is charged." },
            { keys: ["parking"], answer: "Complimentary secure on-site parking is available for all guests." },
            { keys: ["wifi", "wi-fi", "internet"], answer: "Free high-speed Wi-Fi is available throughout the hotel and in all rooms." },
            { keys: ["breakfast", "meal", "restaurant"], answer: "Breakfast is served 06:30–10:00 in our restaurant, and can be added to any booking." },
            { keys: ["pet", "dog", "cat"], answer: "We're a pet-friendly hotel in select rooms — let us know when booking so we can prepare accordingly." },
            { keys: ["book", "booking", "reserve"], answer: "You can browse rooms and book directly on our Rooms & Rates page — I can take you there now." },
            { keys: ["price", "rate", "cost"], answer: "Rates vary by room type and season — check the Rooms & Rates page for live pricing, or tell me your dates and I'll point you in the right direction." },
            { keys: ["contact", "phone", "email", "human", "agent", "person"], answer: "I'll connect you with our reservations team — head to the Contact page and someone will respond shortly." }
        ];

        var QUICK_REPLIES = ["Booking a room", "Check-in time", "Cancellation policy", "Talk to a human"];

        function answerFor(text) {
            var lower = text.toLowerCase();
            for (var i = 0; i < FAQ.length; i++) {
                for (var j = 0; j < FAQ[i].keys.length; j++) {
                    if (lower.indexOf(FAQ[i].keys[j]) !== -1) return FAQ[i].answer;
                }
            }
            return "I'm not fully sure on that one — the fastest way to get an answer is via our Contact page, or call the front desk directly. Want me to take you there?";
        }

        function build() {
            var bubble = document.createElement("div");
            bubble.id = "regal-chat-bubble";
            bubble.innerHTML = '<svg width="26" height="26" viewBox="0 0 24 24" fill="none"><path d="M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z" stroke="#FFFFFF" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>';

            var win = document.createElement("div");
            win.id = "regal-chat-window";
            win.innerHTML =
                '<div class="chat-header">Regal Inn Concierge</div>' +
                '<div class="chat-body" id="regal-chat-body">' +
                '<div class="chat-msg bot">Good day! I\'m your virtual concierge. Ask me about check-in, bookings, or policies — or tap a quick option below.</div>' +
                "</div>" +
                '<div class="chat-quick" id="regal-chat-quick"></div>' +
                '<div class="chat-input-row">' +
                '<input id="regal-chat-input" type="text" placeholder="Type a question..." autocomplete="off" />' +
                '<button id="regal-chat-send" type="button">Send</button>' +
                "</div>";

            document.body.appendChild(bubble);
            document.body.appendChild(win);

            var quickWrap = win.querySelector("#regal-chat-quick");
            QUICK_REPLIES.forEach(function (label) {
                var b = document.createElement("button");
                b.type = "button";
                b.textContent = label;
                b.addEventListener("click", function () { handleUserMessage(label); });
                quickWrap.appendChild(b);
            });

            bubble.addEventListener("click", function () {
                win.classList.toggle("open");
            });

            win.querySelector("#regal-chat-send").addEventListener("click", sendFromInput);
            win.querySelector("#regal-chat-input").addEventListener("keydown", function (e) {
                if (e.key === "Enter") sendFromInput();
            });
        }

        function sendFromInput() {
            var input = document.getElementById("regal-chat-input");
            var text = input.value.trim();
            if (!text) return;
            handleUserMessage(text);
            input.value = "";
        }

        function handleUserMessage(text) {
            appendMessage(text, "user");
            var reply = answerFor(text);
            setTimeout(function () { appendMessage(reply, "bot"); }, 350);
        }

        function appendMessage(text, who) {
            var body = document.getElementById("regal-chat-body");
            var msg = document.createElement("div");
            msg.className = "chat-msg " + who;
            msg.textContent = text;
            body.appendChild(msg);
            body.scrollTop = body.scrollHeight;
        }

        document.addEventListener("DOMContentLoaded", build);
    })();
