"""Run smoke checks against the delivered HTML in a real browser.
Repro: install ../requirements-test.txt and `python -m playwright install chromium`.
No report or screenshot is written to tracked source paths.
"""
from pathlib import Path
from playwright.sync_api import sync_playwright
import hashlib, json, os, shutil, tempfile
ROOT=Path(__file__).resolve().parents[1]
report={"htmlSHA256":hashlib.sha256((ROOT/"index.html").read_bytes()).hexdigest(),"checks":[]}
with sync_playwright() as pw:
    options={"headless":True,"args":["--no-sandbox","--disable-dev-shm-usage"]}
    executable=os.environ.get("CHROMIUM_PATH") or shutil.which("chromium")
    if executable: options["executable_path"]=executable
    browser=pw.chromium.launch(**options)
    page=browser.new_page(viewport={"width":1440,"height":1000},accept_downloads=True)
    page.set_default_timeout(30000)
    errors=[]; logs=[]; requests=[]
    page.on("pageerror",lambda e:errors.append(str(e)))
    page.on("console",lambda m:logs.append(m.text) if m.type in ("warning","error") else None)
    page.on("request",lambda r:requests.append(r.url))
    page.set_content((ROOT/"index.html").read_text(encoding="utf-8"),wait_until="load")
    report["checks"]=page.evaluate((ROOT/"tests/checks.js").read_text(encoding="utf-8"))
    before=page.evaluate("morphogenesisLab.getState()")
    with page.expect_download() as item:
        page.locator("#exportStateBtn").click()
    saved=Path(item.value.path())
    payload=json.loads(saved.read_text())
    page.evaluate("morphogenesisLab.stepMany(20)")
    assert page.evaluate("morphogenesisLab.getState().hash")!=before["hash"]
    page.locator("#stateFile").set_input_files(str(saved))
    page.wait_for_function("h=>morphogenesisLab.getState().hash===h",arg=before["hash"])
    report["checks"].append({"name":"real downloaded checkpoint restores through file-input control","status":"passed"})
    future=page.evaluate("morphogenesisLab.stepMany(20).hash")
    page.locator("#stateFile").set_input_files(str(saved))
    page.wait_for_function("h=>morphogenesisLab.getState().hash===h",arg=before["hash"])
    assert page.evaluate("morphogenesisLab.stepMany(20).hash")==future
    report["checks"].append({"name":"restored field checkpoint reproduces future integration","status":"passed"})
    bad=json.loads(json.dumps(payload));bad["config"]["diffA"]=None
    previous=page.evaluate("morphogenesisLab.getState()")
    page.locator("#stateFile").set_input_files({"name":"invalid.json","mimeType":"application/json","buffer":json.dumps(bad).encode()})
    page.wait_for_timeout(150)
    assert page.evaluate("morphogenesisLab.getState()") == previous
    assert len(logs)==1 and "Invalid checkpoint parameter" in logs[0],logs
    report["expectedValidationMessages"]=list(logs);logs.clear()
    report["checks"].append({"name":"null diffusion parameter rejected without replacing field or configuration","status":"passed"})
    assert page.locator("canvas").count()>0,"No rendering canvas"
    report["checks"].append({"name":"canvas is present","status":"passed"})
    page.wait_for_timeout(80)
    assert not errors,errors
    assert not logs,logs
    assert not requests,requests
    report.update({"browser":browser.version,"loading":"Exact HTML content, no external navigation","pageErrors":errors,"consoleWarningsOrErrors":logs,"networkRequests":requests})
    report["checks"].append({"name":"no browser errors, warnings or network requests","status":"passed"})
    browser.close()
report["passed"]=len(report["checks"])
print(json.dumps(report,indent=2))
if os.environ.get("SMOKE_REPORT"):
    target=Path(os.environ["SMOKE_REPORT"]);target.parent.mkdir(parents=True,exist_ok=True);target.write_text(json.dumps(report,indent=2)+"\n")
