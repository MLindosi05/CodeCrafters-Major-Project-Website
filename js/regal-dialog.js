(function () {
    var backdrop, message, title, ok, cancel, resolver;
    function openDialog(text, options) {
        options = options || {};
        backdrop = backdrop || document.getElementById("regalDialogBackdrop");
        if (!backdrop) return Promise.resolve(false);
        message = document.getElementById("regalDialogMessage"); title = document.getElementById("regalDialogTitle");
        ok = document.getElementById("regalDialogOk"); cancel = document.getElementById("regalDialogCancel");
        title.textContent = options.title || "The Regal Inn"; message.textContent = text;
        cancel.hidden = options.alert === true; ok.textContent = options.okText || (options.alert ? "Close" : "Continue");
        backdrop.hidden = false; ok.focus();
        return new Promise(function (resolve) { resolver = resolve; });
    }
    function close(result) { if (!backdrop || backdrop.hidden) return; backdrop.hidden = true; var done = resolver; resolver = null; if (done) done(result); }
    function initialize() {
        backdrop = document.getElementById("regalDialogBackdrop");
        if (!backdrop) return;
        document.getElementById("regalDialogOk").addEventListener("click", function () { close(true); });
        document.getElementById("regalDialogCancel").addEventListener("click", function () { close(false); });
        backdrop.addEventListener("click", function (event) { if (event.target === backdrop) close(false); });
        document.addEventListener("keydown", function (event) { if (event.key === "Escape") close(false); });
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", initialize);
    else initialize();
    window.RegalDialog = {
        confirm: function (text, options) { return openDialog(text, options); },
        alert: function (text, options) { options = options || {}; options.alert = true; return openDialog(text, options); },
        confirmPostback: function (button, text) {
            openDialog(text, { title: "Please confirm" }).then(function (yes) {
                if (!yes) return;
                button.disabled = true;
                if (!window.__doPostBack) {
                    button.disabled = false;
                    return;
                }

                // ASP.NET LinkButtons render as anchors and have no `name`.
                // Their event target is embedded in the javascript href.
                var href = button.getAttribute("href") || "";
                var match = href.match(/__doPostBack\(\s*(['"])(.*?)\1\s*,\s*(['"])(.*?)\3\s*\)/i);
                var target = match ? match[2] : button.name;
                var argument = match ? match[4] : "";
                if (target) window.__doPostBack(target, argument);
                else button.disabled = false;
            });
            return false;
        }
    };
}());
