// Loaded synchronously in <head>. Applies the saved theme before first paint, wires global
// handlers by event delegation, and (re)initialises page effects after every Blazor
// enhanced navigation. No inline scripts anywhere, so the strict CSP stays intact.
(function () {
  var root = document.documentElement;
  root.classList.add("js");
  var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (!reduceMotion) root.classList.add("reveal-on");

  // ---------- Theme ----------
  function readTheme() { try { return localStorage.getItem("nf-theme"); } catch (e) { return null; } }
  function writeTheme(v) { try { localStorage.setItem("nf-theme", v); } catch (e) { /* storage blocked */ } }
  function currentTheme() {
    var explicit = root.getAttribute("data-theme");
    if (explicit) return explicit;
    return window.matchMedia && window.matchMedia("(prefers-color-scheme: light)").matches ? "light" : "dark";
  }
  var saved = readTheme();
  if (saved === "light" || saved === "dark") root.setAttribute("data-theme", saved);

  // ---------- Global delegated handlers ----------
  document.addEventListener("click", function (e) {
    var toggle = e.target.closest("[data-theme-toggle]");
    if (toggle) {
      var next = currentTheme() === "dark" ? "light" : "dark";
      root.setAttribute("data-theme", next);
      writeTheme(next);
      return;
    }
    var menu = e.target.closest("[data-menu-toggle]");
    if (menu) {
      var open = root.classList.toggle("nav-open");
      menu.setAttribute("aria-expanded", open ? "true" : "false");
      return;
    }
    if (e.target.closest("[data-print]")) { window.print(); return; }
    if (e.target.closest(".nav a")) root.classList.remove("nav-open");

    var tab = e.target.closest("[data-tabs] [role=tab]");
    if (tab) selectTab(tab);
  });

  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape" && root.classList.contains("nav-open")) {
      root.classList.remove("nav-open");
      var btn = document.querySelector("[data-menu-toggle]");
      if (btn) { btn.setAttribute("aria-expanded", "false"); btn.focus(); }
    }
    // Arrow-key navigation between tabs (WAI-ARIA tabs pattern).
    var tab = e.target.closest && e.target.closest("[data-tabs] [role=tab]");
    if (tab && (e.key === "ArrowRight" || e.key === "ArrowLeft")) {
      var tabs = Array.prototype.slice.call(tab.parentElement.querySelectorAll("[role=tab]"));
      var i = tabs.indexOf(tab) + (e.key === "ArrowRight" ? 1 : -1);
      var target = tabs[(i + tabs.length) % tabs.length];
      selectTab(target); target.focus(); e.preventDefault();
    }
  });

  function selectTab(tab) {
    var list = tab.parentElement;
    list.querySelectorAll("[role=tab]").forEach(function (t) {
      var on = t === tab;
      t.setAttribute("aria-selected", on ? "true" : "false");
      t.setAttribute("tabindex", on ? "0" : "-1");
      var panel = document.getElementById(t.getAttribute("aria-controls"));
      if (panel) panel.hidden = !on;
    });
  }

  // Mouse-following light on .spot cards.
  document.addEventListener("pointermove", function (e) {
    var card = e.target.closest && e.target.closest(".spot");
    if (!card) return;
    var r = card.getBoundingClientRect();
    card.style.setProperty("--mx", (e.clientX - r.left) + "px");
    card.style.setProperty("--my", (e.clientY - r.top) + "px");
  }, { passive: true });

  // Header turns to glass once the page scrolls.
  function onScroll() { root.classList.toggle("scrolled", window.scrollY > 8); }
  window.addEventListener("scroll", onScroll, { passive: true });

  // ---------- Per-page effects ----------
  var cleanups = [];
  function cleanup() { cleanups.forEach(function (fn) { try { fn(); } catch (e) { } }); cleanups = []; }

  function initRotator() {
    var el = document.querySelector("[data-rotator]");
    if (!el || reduceMotion) return;
    var words = el.querySelectorAll(".x-rotator-word");
    if (words.length < 2) return;
    var i = 0;
    var timer = setInterval(function () {
      var cur = words[i];
      i = (i + 1) % words.length;
      cur.classList.remove("is-on"); cur.classList.add("is-out");
      words[i].classList.remove("is-out"); words[i].classList.add("is-on");
      setTimeout(function () { cur.classList.remove("is-out"); }, 700);
    }, 2400);
    cleanups.push(function () { clearInterval(timer); });
  }

  // Scroll reveal. Uses positions, not just IntersectionObserver, so content a visitor skips past
  // (fast scroll, anchor links, End key) is never left invisible.
  function initReveal() {
    var items = Array.prototype.slice.call(document.querySelectorAll("[data-reveal]"));
    if (!items.length) return;
    items.forEach(function (el, idx) { el.style.transitionDelay = (idx % 4) * 70 + "ms"; });
    var pending = items.slice(), ticking = false;
    function check() {
      ticking = false;
      var limit = window.innerHeight * 0.92;
      pending = pending.filter(function (el) {
        if (el.getBoundingClientRect().top < limit) { el.classList.add("is-in"); return false; }
        return true;
      });
      if (!pending.length) detach();
    }
    function onScrollReveal() { if (!ticking) { ticking = true; requestAnimationFrame(check); } }
    function detach() { window.removeEventListener("scroll", onScrollReveal); window.removeEventListener("resize", onScrollReveal); }
    window.addEventListener("scroll", onScrollReveal, { passive: true });
    window.addEventListener("resize", onScrollReveal, { passive: true });
    check();
    cleanups.push(detach);
  }

  function initCounters() {
    var els = document.querySelectorAll("[data-count]");
    if (!els.length || reduceMotion || !("IntersectionObserver" in window)) return;
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (entry) {
        if (!entry.isIntersecting) return;
        io.unobserve(entry.target);
        var el = entry.target, to = parseInt(el.getAttribute("data-count"), 10), start = performance.now();
        (function step(t) {
          var p = Math.min(1, (t - start) / 1200);
          el.textContent = Math.round(to * (1 - Math.pow(1 - p, 3)));
          if (p < 1) requestAnimationFrame(step);
        })(start);
      });
    }, { threshold: 0.6 });
    els.forEach(function (el) { io.observe(el); });
    cleanups.push(function () { io.disconnect(); });
  }

  // Tilt the floating mock-up stack toward the pointer.
  function initTilt() {
    var stage = document.querySelector("[data-tilt-group]");
    if (!stage || reduceMotion) return;
    function move(e) {
      var r = stage.getBoundingClientRect();
      var x = (e.clientX - r.left) / r.width - 0.5, y = (e.clientY - r.top) / r.height - 0.5;
      stage.style.transform = "rotateY(" + (x * 8) + "deg) rotateX(" + (-y * 6) + "deg)";
    }
    function leave() { stage.style.transform = ""; }
    var hero = stage.closest(".x-hero") || stage;
    hero.addEventListener("pointermove", move);
    hero.addEventListener("pointerleave", leave);
    cleanups.push(function () { hero.removeEventListener("pointermove", move); hero.removeEventListener("pointerleave", leave); });
  }

  // Constellation particle field that reacts to the pointer.
  function initParticles() {
    var canvas = document.querySelector("[data-particles]");
    if (!canvas || !canvas.getContext) return;
    var ctx = canvas.getContext("2d");
    var dpr = Math.min(window.devicePixelRatio || 1, 2);
    var w = 0, h = 0, pts = [], mouse = { x: -9999, y: -9999 }, raf = 0, running = true;

    function color(alpha) {
      return currentTheme() === "light" ? "rgba(91,69,255," + alpha + ")" : "rgba(160,170,255," + alpha + ")";
    }
    function resize() {
      var r = canvas.getBoundingClientRect();
      w = r.width; h = r.height;
      canvas.width = w * dpr; canvas.height = h * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      var count = Math.min(110, Math.floor(w * h / 11000));
      pts = [];
      for (var i = 0; i < count; i++) {
        pts.push({ x: Math.random() * w, y: Math.random() * h, vx: (Math.random() - 0.5) * 0.25, vy: (Math.random() - 0.5) * 0.25, r: Math.random() * 1.6 + 0.4 });
      }
    }
    function frame() {
      if (!running) return;
      ctx.clearRect(0, 0, w, h);
      for (var i = 0; i < pts.length; i++) {
        var p = pts[i];
        if (!reduceMotion) { p.x += p.vx; p.y += p.vy; }
        if (p.x < 0 || p.x > w) p.vx *= -1;
        if (p.y < 0 || p.y > h) p.vy *= -1;
        var dxm = p.x - mouse.x, dym = p.y - mouse.y, dm = dxm * dxm + dym * dym;
        if (dm < 22000) { p.x += dxm * 0.006; p.y += dym * 0.006; }
        ctx.beginPath(); ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2); ctx.fillStyle = color(0.7); ctx.fill();
        for (var j = i + 1; j < pts.length; j++) {
          var q = pts[j], dx = p.x - q.x, dy = p.y - q.y, d = dx * dx + dy * dy;
          if (d < 14000) {
            ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(q.x, q.y);
            ctx.strokeStyle = color(0.16 * (1 - d / 14000)); ctx.lineWidth = 1; ctx.stroke();
          }
        }
        if (dm < 26000) {
          ctx.beginPath(); ctx.moveTo(p.x, p.y); ctx.lineTo(mouse.x, mouse.y);
          ctx.strokeStyle = "rgba(34,211,238," + (0.35 * (1 - dm / 26000)) + ")"; ctx.stroke();
        }
      }
      if (!reduceMotion) raf = requestAnimationFrame(frame);
    }
    function onMove(e) { var r = canvas.getBoundingClientRect(); mouse.x = e.clientX - r.left; mouse.y = e.clientY - r.top; if (reduceMotion) frame(); }
    function onLeave() { mouse.x = mouse.y = -9999; }
    function onVisibility() {
      if (document.hidden) { running = false; cancelAnimationFrame(raf); }
      else if (!running) { running = true; frame(); }
    }
    resize(); frame();
    var host = canvas.parentElement;
    window.addEventListener("resize", resize);
    host.addEventListener("pointermove", onMove);
    host.addEventListener("pointerleave", onLeave);
    document.addEventListener("visibilitychange", onVisibility);
    cleanups.push(function () {
      running = false; cancelAnimationFrame(raf);
      window.removeEventListener("resize", resize);
      host.removeEventListener("pointermove", onMove);
      host.removeEventListener("pointerleave", onLeave);
      document.removeEventListener("visibilitychange", onVisibility);
    });
  }

  function scrollThreads() {
    document.querySelectorAll("[data-autoscroll]").forEach(function (el) { el.scrollTop = el.scrollHeight; });
  }
  window.novaforge = { scrollThreads: scrollThreads };

  function initPage() {
    cleanup();
    root.classList.remove("nav-open");
    onScroll();
    initRotator(); initReveal(); initCounters(); initTilt(); initParticles(); scrollThreads();
  }

  document.addEventListener("DOMContentLoaded", initPage);
  // Blazor enhanced navigation swaps page content without a reload.
  window.addEventListener("load", function () {
    if (window.Blazor && window.Blazor.addEventListener) window.Blazor.addEventListener("enhancedload", initPage);
  });
})();
