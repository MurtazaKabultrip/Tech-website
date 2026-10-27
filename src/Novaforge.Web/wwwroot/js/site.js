// Loaded synchronously in <head>: applies the saved theme before first paint (no flash),
// then wires up theme toggle and mobile menu with event delegation, which survives
// Blazor's enhanced navigation (the <body> content is patched, not reloaded).
(function () {
  var root = document.documentElement;
  root.classList.add("js");

  function readTheme() {
    try { return localStorage.getItem("nf-theme"); } catch (e) { return null; }
  }
  function writeTheme(value) {
    try { localStorage.setItem("nf-theme", value); } catch (e) { /* storage blocked: theme lasts for this page */ }
  }
  function currentTheme() {
    var explicit = root.getAttribute("data-theme");
    if (explicit) return explicit;
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
  }

  var saved = readTheme();
  if (saved === "light" || saved === "dark") root.setAttribute("data-theme", saved);

  document.addEventListener("click", function (e) {
    var toggle = e.target.closest("[data-theme-toggle]");
    if (toggle) {
      var next = currentTheme() === "dark" ? "light" : "dark";
      root.setAttribute("data-theme", next);
      writeTheme(next);
      toggle.setAttribute("aria-label", next === "dark" ? "Switch to light theme" : "Switch to dark theme");
      return;
    }

    var menu = e.target.closest("[data-menu-toggle]");
    if (menu) {
      var open = root.classList.toggle("nav-open");
      menu.setAttribute("aria-expanded", open ? "true" : "false");
      return;
    }

    if (e.target.closest("[data-print]")) { window.print(); return; }

    // Close the mobile menu after following a nav link.
    if (e.target.closest(".nav a")) root.classList.remove("nav-open");
  });

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && root.classList.contains("nav-open")) {
      root.classList.remove("nav-open");
      var btn = document.querySelector("[data-menu-toggle]");
      if (btn) { btn.setAttribute("aria-expanded", "false"); btn.focus(); }
    }
  });

  // Keep the portal message thread scrolled to the newest message.
  function scrollThreads() {
    document.querySelectorAll("[data-autoscroll]").forEach(function (el) { el.scrollTop = el.scrollHeight; });
  }
  window.novaforge = { scrollThreads: scrollThreads };
  document.addEventListener("DOMContentLoaded", scrollThreads);
})();
