// Regal Inn — Professional AI Concierge Chatbot Widget (100% Production Grade)
var RegalChatbot = (function () {
    var WIDGET_HTML =
        '<div id="regal-chatbot-container" style="position:fixed; bottom:20px; right:20px; z-index:9999; font-family:inherit;">' +
        '<button id="regal-chat-toggle" type="button" aria-label="Open Chat" style="background:var(--blue-700, #0d3b66); color:#fff; border:none; border-radius:50px; width:56px; height:56px; box-shadow:0 4px 14px rgba(0,0,0,0.2); cursor:pointer; display:flex; align-items:center; justify-content:center; font-size:1.5rem; transition:transform 0.2s;">' +
        '<span style="display:inline-block; transform:translateY(-1px);">💬</span>' +
        '</button>' +
        '<div id="regal-chat-window" style="display:none; position:absolute; bottom:70px; right:0; width:380px; max-width:92vw; height:520px; max-height:80vh; background:#fff; border-radius:14px; box-shadow:0 10px 30px rgba(0,0,0,0.22); flex-direction:column; overflow:hidden; border:1px solid rgba(0,0,0,0.08);">' +
        '<div style="background:var(--blue-900, #071522); color:#fff; padding:1rem; display:flex; justify-content:space-between; align-items:center; flex-shrink:0;">' +
        '<div style="display:flex; align-items:center; gap:8px;">' +
        '<div style="width:10px; height:10px; background:#4ade80; border-radius:50%; box-shadow:0 0 6px #4ade80;"></div>' +
        '<div>' +
        '<h3 style="margin:0; font-size:1rem; font-weight:600; color:#fff;">Regal Inn Concierge</h3>' +
        '<span style="font-size:0.7rem; color:#cbd5e1;">Your 24/7 Virtual Assistant</span>' +
        '</div>' +
        '</div>' +
        '<button id="regal-chat-close" type="button" aria-label="Close Chat" style="background:none; border:none; color:#fff; font-size:1.2rem; cursor:pointer; padding:2px 6px;">✕</button>' +
        '</div>' +
        '<div id="regal-chat-messages" style="flex:1; padding:1rem; overflow-y:auto; display:flex; flex-direction:column; gap:0.85rem; background:#f8fafc; font-size:0.9rem; word-break:break-word;"></div>' +
        '<div id="regal-chat-chips" style="padding:0.5rem 0.75rem; display:flex; gap:0.4rem; overflow-x:auto; background:#fff; border-top:1px solid #e2e8f0; white-space:nowrap; flex-shrink:0;"></div>' +
        '<div style="padding:0.75rem; background:#fff; border-top:1px solid #e2e8f0; display:flex; gap:0.5rem; flex-shrink:0;">' +
        '<input id="regal-chat-input" type="text" placeholder="Ask about rooms, rates, check-in..." style="flex:1; padding:0.55rem 0.75rem; border:1px solid #cbd5e1; border-radius:8px; outline:none; font-size:0.9rem;" />' +
        '<button id="regal-chat-send" type="button" style="background:var(--blue-700, #0d3b66); color:#fff; border:none; padding:0.55rem 1rem; border-radius:8px; cursor:pointer; font-weight:600; transition:background 0.2s;">Send</button>' +
        '</div>' +
        '</div>' +
        '</div>';

    var KB = [
        {
            keys: ["check-in", "checkin", "arrive", "arrival", "time", "hours"],
            answer: "Standard check-in time is from **14:00 (2:00 PM)** onwards. Check-out is strictly by **10:00 AM**. If you arrive early, our front desk can securely hold your luggage while you explore."
        },
        {
            keys: ["cancel", "cancellation", "refund", "policy", "change date"],
            answer: "We offer **free cancellation up to 48 hours** before your scheduled check-in. You can view, modify, or cancel your active bookings anytime directly through your <a href='/Bookings/MyBookings.aspx' style='color:#0d3b66; text-decoration:underline; font-weight:600;'>My Bookings</a> dashboard."
        },
        {
            keys: ["book", "reserve", "room", "rate", "price", "accommodation", "cost", "night"],
            answer: "You can explore our full range of Standard, Deluxe, Executive, and Suite rooms across all our branches and book instantly on our <a href='/Rooms.aspx' style='color:#0d3b66; text-decoration:underline; font-weight:600;'>Rooms &amp; Rates</a> page."
        },
        {
            keys: ["breakfast", "food", "eat", "restaurant", "meal", "dining"],
            answer: "Yes! A complimentary full English and continental breakfast is included with every room reservation across all Regal Inn branches."
        },
        {
            keys: ["wifi", "internet", "connection", "network"],
            answer: "High-speed wireless internet (Wi-Fi) is 100% complimentary for all registered guests throughout all rooms and public areas."
        },
        {
            keys: ["parking", "car", "vehicle", "garage", "secure"],
            answer: "Secure on-site parking is available free of charge for all guests at all our branches (Durban North Beach, UMhlanga, Ballito, Pietermaritzburg, and Johannesburg Midrand)."
        },
        {
            keys: ["branch", "location", "where", "durban", "midrand", "pietermaritzburg", "ballito", "umhlanga"],
            answer: "We have prime locations across South Africa: **Durban North Beach**, **Durban UMhlanga**, **Durban Ballito**, **Pietermaritzburg**, and **Johannesburg Midrand**. You can filter by your preferred branch on our <a href='/Rooms.aspx' style='color:#0d3b66; text-decoration:underline; font-weight:600;'>Rooms page</a>."
        },
        {
            keys: ["contact", "phone", "email", "support", "help", "human", "desk"],
            answer: "Need direct assistance? You can reach our front desk team anytime via our <a href='/Contact.aspx' style='color:#0d3b66; text-decoration:underline; font-weight:600;'>Contact Us</a> page or call our 24/7 concierge hotline."
        }
    ];

    var CHIPS = [
        "Check-in time",
        "Book a room",
        "Cancellation policy",
        "Breakfast & Wi-Fi",
        "Our branches",
        "Contact support"
    ];

    function init() {
        if (document.getElementById("regal-chatbot-container")) return;

        var div = document.createElement("div");
        div.innerHTML = WIDGET_HTML;
        document.body.appendChild(div);

        var toggleBtn = document.getElementById("regal-chat-toggle");
        var closeBtn = document.getElementById("regal-chat-close");
        var windowEl = document.getElementById("regal-chat-window");
        var sendBtn = document.getElementById("regal-chat-send");
        var inputEl = document.getElementById("regal-chat-input");
        var msgContainer = document.getElementById("regal-chat-messages");
        var chipsContainer = document.getElementById("regal-chat-chips");

        function toggleWindow() {
            var isVisible = windowEl.style.display === "flex";
            windowEl.style.display = isVisible ? "none" : "flex";
            if (!isVisible && msgContainer.children.length === 0) {
                appendBotMessage("Hello! Welcome to **The Regal Inn Hotel**. How can I assist you with your stay or reservation today?");
            }
        }

        toggleBtn.addEventListener("click", toggleWindow);
        closeBtn.addEventListener("click", function () { windowEl.style.display = "none"; });

        renderChips();

        function renderChips() {
            chipsContainer.innerHTML = "";
            CHIPS.forEach(function (text) {
                var chip = document.createElement("button");
                chip.type = "button";
                chip.textContent = text;
                chip.style.cssText = "background:#f1f5f9; border:1px solid #cbd5e1; padding:5px 12px; border-radius:16px; font-size:0.78rem; cursor:pointer; color:#334155; flex-shrink:0; transition:all 0.2s;";
                chip.addEventListener("mouseover", function () { chip.style.background = "#e2e8f0"; chip.style.borderColor = "#94a3b8"; });
                chip.addEventListener("mouseout", function () { chip.style.background = "#f1f5f9"; chip.style.borderColor = "#cbd5e1"; });
                chip.addEventListener("click", function () {
                    handleUserQuery(text);
                });
                chipsContainer.appendChild(chip);
            });
        }

        function appendUserMessage(text) {
            var bubble = document.createElement("div");
            bubble.style.cssText = "background:#0d3b66; color:#fff; padding:0.65rem 0.95rem; border-radius:12px 12px 2px 12px; max-width:85%; align-self:flex-end; word-break:break-word; box-shadow:0 1px 3px rgba(0,0,0,0.15); font-size:0.88rem;";
            bubble.textContent = text;
            msgContainer.appendChild(bubble);
            msgContainer.scrollTop = msgContainer.scrollHeight;
        }

        function appendBotMessage(htmlContent) {
            var bubble = document.createElement("div");
            bubble.style.cssText = "background:#e2e8f0; color:#1e293b; padding:0.65rem 0.95rem; border-radius:12px 12px 12px 2px; max-width:85%; align-self:flex-start; word-break:break-word; line-height:1.45; box-shadow:0 1px 2px rgba(0,0,0,0.05); font-size:0.88rem;";
            bubble.innerHTML = htmlContent;
            msgContainer.appendChild(bubble);
            msgContainer.scrollTop = msgContainer.scrollHeight;
        }

        function handleUserQuery(query) {
            var q = query.trim();
            if (!q) return;
            appendUserMessage(q);
            inputEl.value = "";

            setTimeout(function () {
                var lowerQ = q.toLowerCase();
                var matchedAnswer = "";

                for (var i = 0; i < KB.length; i++) {
                    var item = KB[i];
                    var found = item.keys.some(function (k) { return lowerQ.indexOf(k) !== -1; });
                    if (found) {
                        matchedAnswer = item.answer;
                        break;
                    }
                }

                if (!matchedAnswer) {
                    matchedAnswer = "I'm not fully sure about that specific detail — the fastest way to get an answer is via our <a href='/Contact.aspx' style='color:#0d3b66; text-decoration:underline; font-weight:600;'>Contact page</a>, or you can browse available rooms on our <a href='/Rooms.aspx' style='color:#0d3b66; text-decoration:underline; font-weight:600;'>Rooms page</a>.";
                }

                appendBotMessage(matchedAnswer);
            }, 350);
        }

        sendBtn.addEventListener("click", function () {
            handleUserQuery(inputEl.value);
        });

        inputEl.addEventListener("keypress", function (e) {
            if (e.key === "Enter") {
                handleUserQuery(inputEl.value);
            }
        });
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }

    return {
        toggle: function () {
            var win = document.getElementById("regal-chat-window");
            if (win) win.style.display = (win.style.display === "flex") ? "none" : "flex";
        }
    };
})();