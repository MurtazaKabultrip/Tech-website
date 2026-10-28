// Loaded synchronously in <head>. Applies the saved theme before first paint, wires global
// handlers by event delegation, and (re)initialises page effects after every Blazor
// enhanced navigation. No inline scripts anywhere, so the strict CSP stays intact.
(function () {
  var root = document.documentElement;
  root.classList.add("js");
  var isTouch = window.matchMedia && window.matchMedia("(pointer: coarse), (max-width: 700px)").matches;
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
  // The theme is chosen by the site admin and rendered on <html data-theme> by the server.
  if (!root.getAttribute("data-theme")) root.setAttribute("data-theme", "dark");

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
      if (open) setTimeout(function () { window.dispatchEvent(new Event("resize")); }, 30);
      return;
    }
    if (e.target.closest("[data-print]")) { window.print(); return; }

    // Logo: always does something — on the home page it scrolls back to the top.
    var brand = e.target.closest(".brand");
    if (brand) {
      root.classList.remove("nav-open");
      var mt = document.querySelector("[data-menu-toggle]");
      if (mt) mt.setAttribute("aria-expanded", "false");
      if (location.pathname === "/" || location.pathname === "") {
        e.preventDefault();
        if (location.hash) history.replaceState(null, "", "/");
        window.scrollTo({ top: 0, behavior: reduceMotion ? "auto" : "smooth" });
      }
      return;
    }
    if (e.target.closest(".nav a")) root.classList.remove("nav-open");

    // Tap outside the open menu closes it.
    if (root.classList.contains("nav-open") && !e.target.closest(".nav")) {
      root.classList.remove("nav-open");
      var tg = document.querySelector("[data-menu-toggle]");
      if (tg) tg.setAttribute("aria-expanded", "false");
    }

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
  var scrollIdle = 0;
  function onScroll() {
    root.classList.toggle("scrolled", window.scrollY > 8);
    // While the page is moving, CSS pauses decorative animations so scrolling stays smooth.
    if (!root.classList.contains("is-scrolling")) root.classList.add("is-scrolling");
    clearTimeout(scrollIdle);
    scrollIdle = setTimeout(function () { root.classList.remove("is-scrolling"); }, 180);
  }
  window.addEventListener("scroll", onScroll, { passive: true });

  // ---------- Per-page effects ----------
  var cleanups = [];
  function cleanup() { cleanups.forEach(function (fn) { try { fn(); } catch (e) { } }); cleanups = []; }

  // Shrink any rotating word that is wider than its line, so long words never get cut off.
  function fitRotator(el) {
    var words = el.querySelectorAll(".x-rotator-word");
    var avail = el.clientWidth;
    words.forEach(function (w) {
      w.style.fontSize = "";
      var natural = w.scrollWidth;
      if (natural > avail && avail > 0) w.style.fontSize = (Math.floor(avail / natural * 1000) / 1000) + "em";
    });
  }

  function initRotator() {
    var el = document.querySelector("[data-rotator]");
    if (!el) return;
    fitRotator(el);
    var refit = function () { fitRotator(el); };
    window.addEventListener("resize", refit);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(refit);
    cleanups.push(function () { window.removeEventListener("resize", refit); });
    if (reduceMotion) return;
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

  // Scroll reveal. Checks positions (not only IntersectionObserver) and re-queries the live DOM on
  // every pass: Blazor may patch/replace page elements after load, and content a visitor skips past
  // (fast scroll, anchor links, End key) must never stay invisible.
  var revealTicking = false;
  // Auto-tag section parts with an entrance style, so each one arrives differently:
  // day panels open like an iris, headings sharpen from a blur, grid items fly in from the side
  // they sit on, small items zoom in one after another.
  var AUTO_REVEAL = [
    [".tone-alt", "iris"],
    [".x-head, .section-head, .page-head .container > *:not(.about-hero-grid), .about-hero-grid > *", "blur"],
    [".x-panel-art, .x-term, .about-exp-num", "zoom"],
    [".x-tablist, .x-faq details, .about-story-text p, .about-exp-list li", "up"],
    [".stack-chips .chip, .x-marquee li, .x-trust li", "pop"],
    [".x-cta", "flip"]
  ];
  function autoTag() {
    AUTO_REVEAL.forEach(function (rule) {
      document.querySelectorAll(rule[0]).forEach(function (el) {
        if (!el.hasAttribute("data-reveal") && !el.closest(".x-hero")) el.setAttribute("data-reveal", rule[1]);
      });
    });
    // Untyped grid items (cards, tiles, stats, packages): direction from their column.
    document.querySelectorAll("[data-reveal='']").forEach(function (el) {
      var parent = el.parentElement, pr = parent.getBoundingClientRect(), r = el.getBoundingClientRect();
      var c = (r.left + r.width / 2 - pr.left) / (pr.width || 1);
      el.setAttribute("data-reveal", c < 0.4 ? "left" : c > 0.6 ? "right" : "up");
    });
  }
  function revealCheck() {
    revealTicking = false;
    autoTag();
    var limit = window.innerHeight * 0.9, revealed = false;
    document.querySelectorAll("[data-reveal]:not(.is-in)").forEach(function (el) {
      if (el.getBoundingClientRect().top < limit) {
        // Stagger siblings that enter together.
        var i = 0, sib = el;
        while ((sib = sib.previousElementSibling)) { if (sib.hasAttribute("data-reveal")) i++; }
        el.style.transitionDelay = Math.min(i, 8) * (el.getAttribute("data-reveal") === "pop" ? 35 : 90) + "ms";
        el.classList.add("is-in");
        revealed = true;
      }
    });
    if (revealed) setTimeout(scheduleFit, 1200);
  }
  function scheduleReveal() { if (!revealTicking) { revealTicking = true; requestAnimationFrame(revealCheck); } }
  window.addEventListener("scroll", scheduleReveal, { passive: true });
  window.addEventListener("resize", scheduleReveal, { passive: true });
  function initReveal() { revealCheck(); }

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
    if (!stage || reduceMotion || isTouch) return;
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
    if (!canvas || !canvas.getContext || isTouch) return;
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

  // Gliding indicator in the desktop nav: follows the hovered link, rests on the current page.
  function initNavIndicator() {
    var nav = document.querySelector(".nav");
    var ind = nav && nav.querySelector(".nav-ind");
    if (!ind) return;
    function place(link) {
      nav.querySelectorAll("a.is-lit").forEach(function (a) { if (a !== link) a.classList.remove("is-lit"); });
      if (!link || getComputedStyle(ind).display === "none") { nav.style.setProperty("--ind-o", "0"); return; }
      link.classList.add("is-lit");
      nav.style.setProperty("--ind-x", link.offsetLeft + "px");
      nav.style.setProperty("--ind-y", link.offsetTop + "px");
      nav.style.setProperty("--ind-w", link.offsetWidth + "px");
      nav.style.setProperty("--ind-h", link.offsetHeight + "px");
      nav.style.setProperty("--ind-o", "1");
    }

    // Phone menu: hold and slide a finger over the items — the highlight follows the finger;
    // lifting the finger on an item opens it.
    var dragging = false, startLink = null, current = null;
    function linkAt(x, y) {
      var el = document.elementFromPoint(x, y);
      var a = el && el.closest && el.closest(".nav a");
      return a && nav.contains(a) ? a : null;
    }
    function onDown(e) {
      if (e.pointerType !== "touch" || !root.classList.contains("nav-open")) return;
      dragging = true; startLink = current = linkAt(e.clientX, e.clientY);
      if (current && !current.classList.contains("nav-cta")) place(current);
      if (navigator.vibrate) { try { navigator.vibrate(5); } catch (x) { } }
    }
    function onMove(e) {
      if (!dragging) return;
      var a = linkAt(e.clientX, e.clientY);
      // In the small gaps between items keep the last highlight; clear only outside the menu.
      if (!a) {
        var r = nav.getBoundingClientRect();
        if (e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom) return;
      }
      if (a !== current) {
        current = a;
        if (a && !a.classList.contains("nav-cta")) place(a); else place(null);
        if (a && navigator.vibrate) { try { navigator.vibrate(3); } catch (x) { } }
      }
    }
    function onUp() {
      if (!dragging) return;
      dragging = false;
      // Same item as where the finger landed: let the normal tap/click happen.
      if (current && current !== startLink) current.click();
      if (!current) rest();
    }
    nav.addEventListener("pointerdown", onDown);
    nav.addEventListener("pointermove", onMove);
    window.addEventListener("pointerup", onUp);
    window.addEventListener("pointercancel", onUp);
    cleanups.push(function () {
      nav.removeEventListener("pointerdown", onDown); nav.removeEventListener("pointermove", onMove);
      window.removeEventListener("pointerup", onUp); window.removeEventListener("pointercancel", onUp);
    });
    function rest() { place(nav.querySelector("a.active:not(.nav-cta)")); }
    function over(e) { var a = e.target.closest("a:not(.nav-cta)"); if (a && nav.contains(a)) place(a); }
    nav.addEventListener("pointerover", over);
    nav.addEventListener("focusin", over);
    nav.addEventListener("pointerleave", rest);
    nav.addEventListener("focusout", rest);
    window.addEventListener("resize", rest);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(rest);
    rest();
    cleanups.push(function () {
      nav.removeEventListener("pointerover", over); nav.removeEventListener("focusin", over);
      nav.removeEventListener("pointerleave", rest); nav.removeEventListener("focusout", rest);
      window.removeEventListener("resize", rest);
    });
  }

  // Same gliding pill for the showcase tab bar: follows hover, rests on the selected tab.
  function initTabGlide() {
    var list = document.querySelector(".x-tablist");
    var ind = list && list.querySelector(".glide-ind");
    if (!ind) return;
    function place(btn) {
      list.querySelectorAll("button.is-lit").forEach(function (b) { if (b !== btn) b.classList.remove("is-lit"); });
      if (!btn) { list.style.setProperty("--ind-o", "0"); return; }
      btn.classList.add("is-lit");
      list.style.setProperty("--ind-x", btn.offsetLeft + "px");
      list.style.setProperty("--ind-y", btn.offsetTop + "px");
      list.style.setProperty("--ind-w", btn.offsetWidth + "px");
      list.style.setProperty("--ind-h", btn.offsetHeight + "px");
      list.style.setProperty("--ind-o", "1");
    }
    function rest() { place(list.querySelector("[aria-selected=true]")); }
    function over(e) { var b = e.target.closest("button"); if (b && list.contains(b)) place(b); }
    function onClick() { requestAnimationFrame(rest); }
    list.addEventListener("pointerover", over);
    list.addEventListener("pointerleave", rest);
    list.addEventListener("focusin", over);
    list.addEventListener("click", onClick);
    window.addEventListener("resize", rest);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(rest);
    rest();
    cleanups.push(function () {
      list.removeEventListener("pointerover", over); list.removeEventListener("pointerleave", rest);
      list.removeEventListener("focusin", over); list.removeEventListener("click", onClick);
      window.removeEventListener("resize", rest);
    });
  }

  // One section = one screen. Marks the top-level sections of Home/About and, when a section's
  // content is taller than the window, zooms its content down to fit (never below a readable size).
  function fitSections() {
    var main = document.querySelector("main");
    if (!main || !main.querySelector(".x-hero, .about-hero")) return;
    var units = Array.prototype.filter.call(main.children, function (el) {
      return el.matches("section, .tone-alt");
    });
    units.forEach(function (sec) {
      sec.classList.add("fs-section");
      Array.prototype.forEach.call(sec.children, function (k) { k.style.zoom = ""; });
    });
  }
  var fitQueued = false;
  function scheduleFit() { if (!fitQueued) { fitQueued = true; requestAnimationFrame(function () { fitQueued = false; fitSections(); }); } }
  var lastW = window.innerWidth;
  window.addEventListener("resize", function () {
    if (isTouch && window.innerWidth === lastW) return; // address bar moved: nothing to refit
    lastW = window.innerWidth;
    scheduleFit();
  });
  if (document.fonts && document.fonts.ready) document.fonts.ready.then(scheduleFit);
  window.addEventListener("load", function () { scheduleFit(); setTimeout(scheduleFit, 1500); });

  function initLegalToc() {
    var links = Array.prototype.slice.call(document.querySelectorAll(".legal-toc a"));
    if (!links.length || !("IntersectionObserver" in window)) return;
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (!en.isIntersecting) return;
        links.forEach(function (a) { a.classList.toggle("is-current", a.getAttribute("href") === "#" + en.target.id); });
      });
    }, { rootMargin: "-30% 0px -60% 0px" });
    document.querySelectorAll(".legal-sec[id]").forEach(function (sec) { io.observe(sec); });
    cleanups.push(function () { io.disconnect(); });
  }

  function initPage() {
    cleanup();
    root.classList.remove("nav-open");
    onScroll();
    fitSections(); initLegalToc(); initNavIndicator(); initTabGlide(); initRotator(); initReveal(); initCounters(); initTilt(); initParticles(); scrollThreads();
  }

  // New page opened from a link: start at the top (or at the #section the link points to).
  // Back/forward navigation keeps the browser's own scroll restore.
  var linkNav = false;
  document.addEventListener("click", function (e) {
    var a = e.target.closest && e.target.closest("a[href]");
    if (!a || a.target === "_blank" || e.defaultPrevented || e.metaKey || e.ctrlKey || e.shiftKey) return;
    var url = new URL(a.href, location.href);
    if (url.origin === location.origin && (url.pathname !== location.pathname || url.search !== location.search)) linkNav = true;
  }, true);
  function scrollForNewPage() {
    if (!linkNav) return;
    linkNav = false;
    // "instant" overrides the page's CSS smooth scrolling; re-apply after Blazor finishes patching.
    function go() {
      var target = location.hash && document.getElementById(location.hash.slice(1));
      if (target) target.scrollIntoView({ behavior: "instant" });
      else window.scrollTo({ top: 0, left: 0, behavior: "instant" });
    }
    go();
    requestAnimationFrame(go);
  }

  document.addEventListener("DOMContentLoaded", initPage);
  window.addEventListener("load", revealCheck);
  // Safety net: if the DOM is patched without an event we hear about, re-check shortly after.
  if ("MutationObserver" in window) {
    new MutationObserver(scheduleReveal).observe(document.documentElement, { subtree: true, childList: true, attributes: true, attributeFilter: ["class"] });
  }
  // Blazor enhanced navigation swaps page content without a reload.
  window.addEventListener("load", function () {
    if (window.Blazor && window.Blazor.addEventListener) window.Blazor.addEventListener("enhancedload", function () { scrollForNewPage(); initPage(); });
  });
})();
