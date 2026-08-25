// ==========================================================================
// The Regal Inn Hotel — site-wide interactions
// Navbar condense on scroll, scroll-reveal animations, smooth anchor scroll,
// generic form submit UX (loading state + success/error message)
// ==========================================================================
(function () {
  "use strict";

  document.addEventListener("DOMContentLoaded", function () {
    initNavbarScroll();
    initScrollReveal();
    initSmoothAnchors();
    initFormFeedback();
  });

  // ---- Navbar shrinks + gets shadow after scrolling past 40px ----
  function initNavbarScroll() {
    var nav = document.querySelector(".navbar-regal");
    if (!nav) return;
    window.addEventListener("scroll", function () {
      if (window.scrollY > 40) {
        nav.classList.add("scrolled");
      } else {
        nav.classList.remove("scrolled");
      }
    });
  }

  // ---- Fade/slide up elements with class "reveal" as they enter viewport ----
  function initScrollReveal() {
    var targets = document.querySelectorAll(".reveal");
    if (!targets.length) return;

    if (!("IntersectionObserver" in window)) {
      targets.forEach(function (el) { el.classList.add("is-visible"); });
      return;
    }

    var observer = new IntersectionObserver(
      function (entries) {
        entries.forEach(function (entry) {
          if (entry.isIntersecting) {
            entry.target.classList.add("is-visible");
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.15 }
    );

    targets.forEach(function (el) { observer.observe(el); });
  }

  // ---- Smooth scrolling for in-page anchor links (e.g. Home -> #rooms) ----
  function initSmoothAnchors() {
    document.querySelectorAll('a[href^="#"]').forEach(function (link) {
      link.addEventListener("click", function (e) {
        var targetId = this.getAttribute("href");
        if (targetId.length < 2) return;
        var target = document.querySelector(targetId);
        if (!target) return;
        e.preventDefault();
        target.scrollIntoView({ behavior: "smooth", block: "start" });
      });
    });
  }

  // ---- Generic UX helper: any form with data-regal-form="true" gets a
  // disabled/"Sending..." state on submit. Actual submission still happens
  // via ASP.NET postback / your code-behind; this only manages UI feedback.
  // Call window.regalShowFormMessage(formEl, 'success'|'error', text) from
  // code-behind-triggered script (see Contact.aspx.cs example) to surface
  // a message after postback completes.
  function initFormFeedback() {
    document.querySelectorAll('[data-regal-form="true"]').forEach(function (form) {
      form.addEventListener("submit", function () {
        var btn = form.querySelector('[type="submit"]');
        if (btn) {
          btn.dataset.originalText = btn.value || btn.textContent;
          btn.disabled = true;
          if (btn.tagName === "INPUT") btn.value = "Sending...";
          else btn.textContent = "Sending...";
        }
      });
    });
  }

  window.regalShowFormMessage = function (formEl, type, text) {
    if (!formEl) return;
    var msg = formEl.querySelector(".form-msg");
    if (!msg) {
      msg = document.createElement("div");
      msg.className = "form-msg";
      formEl.prepend(msg);
    }
    msg.className = "form-msg " + type;
    msg.textContent = text;
  };
})();
