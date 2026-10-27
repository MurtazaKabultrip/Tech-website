"""Smoke test + visual check: drives the running site in a real browser and saves screenshots.

Usage: python scripts/screenshots.py http://localhost:5180 screenshots/
Requires: pip install playwright && python -m playwright install --with-deps chromium
Signs in with the Development admin account from appsettings.Development.json.
"""
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright, expect

BASE = sys.argv[1].rstrip("/") if len(sys.argv) > 1 else "http://localhost:5180"
OUT = Path(sys.argv[2] if len(sys.argv) > 2 else "screenshots")
OUT.mkdir(parents=True, exist_ok=True)

PUBLIC = ["/", "/services", "/services/payments", "/process", "/contact", "/not-a-page"]
errors: list[str] = []


def shoot(page, name: str) -> None:
    # Scroll through the page so scroll-triggered reveals and counters run, then back to the top.
    page.evaluate("""async () => {
        for (let y = 0; y < document.body.scrollHeight; y += 500) { window.scrollTo(0, y); await new Promise(r => setTimeout(r, 60)); }
        window.scrollTo(0, 0);
    }""")
    page.wait_for_timeout(1600)
    hidden = page.evaluate("[...document.querySelectorAll('[data-reveal]')].filter(e => getComputedStyle(e).opacity === '0').length")
    if hidden:
        info = page.evaluate("({y: scrollY, h: document.documentElement.scrollHeight, cls: document.documentElement.className, first: (document.querySelector('[data-reveal]:not(.is-in)')||{}).outerHTML?.slice(0,120)})")
        errors.append(f"{name}: {hidden} section(s) still invisible after scrolling {info}")
    page.screenshot(path=str(OUT / f"{name}.png"), full_page=True)


with sync_playwright() as p:
    browser = p.chromium.launch()
    for theme in ("dark", "light"):
        for label, viewport in (("desktop", {"width": 1440, "height": 900}), ("mobile", {"width": 390, "height": 844})):
            ctx = browser.new_context(viewport=viewport, color_scheme=theme)
            page = ctx.new_page()
            # The deliberate 404 page logs a "failed to load resource" console error; ignore only that.
            page.on("console", lambda m: m.type == "error" and "404" not in m.text and errors.append(f"console: {m.text}"))
            page.on("pageerror", lambda e: errors.append(f"pageerror: {e}"))
            for path in PUBLIC:
                page.goto(BASE + path, wait_until="networkidle")
                slug = path.strip("/").replace("/", "_").replace("?", "_").replace("=", "-") or "home"
                shoot(page, f"{theme}-{label}-{slug}")
            ctx.close()

    # Portal flow: sign in, submit a request, send a message.
    ctx = browser.new_context(viewport={"width": 1440, "height": 900}, color_scheme="dark")
    page = ctx.new_page()
    page.on("pageerror", lambda e: errors.append(f"pageerror: {e}"))
    try:
        page.goto(BASE + "/account/login")
        page.fill("#email", "admin@novaforge.local")
        page.fill("#password", "ChangeMe-Dev-123")
        page.click("button[type=submit]")
        page.wait_for_url("**/portal")
        page.wait_for_load_state("networkidle")
        shoot(page, "portal-dashboard-empty")
        page.goto(BASE + "/portal/new", wait_until="networkidle")
        page.wait_for_timeout(1000)  # interactive circuit
        page.click("label[for=type-BugFix]")
        page.fill("#title", "Checkout page crashes on iPhone")
        page.fill("#desc", "When a coupon code is entered on iOS Safari the checkout page goes blank. Started after the last plugin update.")
        page.fill("#url", "https://shop.example.com/checkout")
        shoot(page, "portal-new-request")
        page.click("main button[type=submit]")  # not the sidebar's "Sign out"
        # Interactive (client-side) navigation: wait for the project page itself, not a page load.
        page.wait_for_selector("#composer", timeout=20000)
        page.fill("#composer", "Reproduced on iOS 18. Looking into the coupon script now.")
        page.click(".composer button[type=submit]")
        expect(page.locator(".msg")).to_have_count(1, timeout=10000)
        shoot(page, "portal-project-detail")
        page.goto(BASE + "/portal", wait_until="networkidle")
        shoot(page, "portal-dashboard")
        page.goto(BASE + "/admin", wait_until="networkidle")
        shoot(page, "admin-home")
    except Exception as ex:  # keep evidence of what the page looked like
        errors.append(f"portal flow failed: {ex}")
        page.screenshot(path=str(OUT / "portal-failure.png"), full_page=True)
        (OUT / "portal-failure.txt").write_text(page.url + "\n\n" + page.inner_text("body"))
    ctx.close()
    browser.close()

(OUT / "errors.txt").write_text("\n".join(errors) or "no browser errors\n")
print("\n".join(errors) or "no browser errors")
sys.exit(1 if errors else 0)
