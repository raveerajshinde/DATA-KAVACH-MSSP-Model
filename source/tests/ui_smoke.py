"""UI smoke test: signs in and captures screenshots of every main page, failing on console errors.
Usage: python ui_smoke.py http://127.0.0.1:5080 email password outdir
"""
import os
import sys
from playwright.sync_api import sync_playwright

BASE, EMAIL, PW, OUT = sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4]
os.makedirs(OUT, exist_ok=True)
PAGES = ["/", "/organizations", "/assessments", "/assessments/1", "/inventory", "/dpia", "/dpia/1", "/security", "/vendors", "/vendors/1", "/consent", "/rights", "/rights/1",
         "/incidents", "/incidents/1", "/policies", "/policies/1", "/roadmap", "/evidence", "/reports", "/reports/2", "/templates", "/requirements", "/users", "/audit", "/settings"]
errors = []
with sync_playwright() as p:
    b = p.chromium.launch(executable_path="/usr/bin/google-chrome", args=["--no-sandbox"])
    pg = b.new_page(viewport={"width": 1500, "height": 950})
    pg.on("pageerror", lambda e: errors.append(f"{pg.url}: {e}"))
    pg.on("console", lambda m: m.type == "error" and errors.append(f"{pg.url}: console {m.text}"))
    pg.goto(BASE + "/")
    pg.get_by_label("Email").fill(EMAIL)
    pg.get_by_label("Password").fill(PW)
    pg.get_by_role("button", name="Sign in").click()
    pg.wait_for_selector("text=Dashboard", timeout=20000)
    for path in PAGES:
        pg.goto(BASE + path)
        pg.wait_for_load_state("networkidle")
        pg.wait_for_timeout(900)
        name = path.strip("/").replace("/", "_") or "dashboard"
        pg.screenshot(path=os.path.join(OUT, f"{name}.png"), full_page=False)
        print("ok", path)
    # expand one assessment item and the branding tab for richer screenshots
    pg.goto(BASE + "/assessments/1"); pg.wait_for_load_state("networkidle")
    pg.get_by_text("NTC-01").first.click(); pg.wait_for_timeout(800)
    pg.screenshot(path=os.path.join(OUT, "assessment_item.png"))
    pg.goto(BASE + "/organizations"); pg.wait_for_load_state("networkidle")
    pg.get_by_role("tab", name="Branding").click(); pg.wait_for_timeout(1500)
    pg.screenshot(path=os.path.join(OUT, "branding.png"), full_page=True)
    b.close()
print("ERRORS:" if errors else "No browser errors", *errors[:20], sep="\n")
sys.exit(1 if errors else 0)
