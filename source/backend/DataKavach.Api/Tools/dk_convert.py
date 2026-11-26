# Data Kavach - DOCX to PDF conversion through LibreOffice UNO.
# Updates all indexes (table of contents) and fields so page numbers are correct, exports PDF,
# and writes the resolved table-of-contents entries (heading -> page) to a JSON file.
#
# Usage: python dk_convert.py <soffice-path> <input.docx> <output.pdf> <toc.json>
# On Windows run with LibreOffice's bundled interpreter: "C:\Program Files\LibreOffice\program\python.exe"
import json
import os
import shutil
import subprocess
import sys
import tempfile
import time

import uno
from com.sun.star.beans import PropertyValue


def prop(name, value):
    p = PropertyValue()
    p.Name = name
    p.Value = value
    return p


def main():
    soffice, src, pdf, toc_json = sys.argv[1:5]
    src = os.path.abspath(src)
    pdf = os.path.abspath(pdf)
    profile = tempfile.mkdtemp(prefix="dk_lo_")
    pipe = "dk_%d_%d" % (os.getpid(), int(time.time()))
    proc = subprocess.Popen([
        soffice, "--headless", "--invisible", "--nologo", "--norestore", "--nodefault", "--nolockcheck",
        "-env:UserInstallation=" + uno.systemPathToFileUrl(profile),
        "--accept=pipe,name=%s;urp;StarOffice.ComponentContext" % pipe,
    ], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        local = uno.getComponentContext()
        resolver = local.ServiceManager.createInstanceWithContext("com.sun.star.bridge.UnoUrlResolver", local)
        ctx = None
        deadline = time.time() + 90
        while time.time() < deadline:
            try:
                ctx = resolver.resolve("uno:pipe,name=%s;urp;StarOffice.ComponentContext" % pipe)
                break
            except Exception:
                if proc.poll() is not None:
                    raise RuntimeError("LibreOffice exited during start-up (code %s)" % proc.returncode)
                time.sleep(0.5)
        if ctx is None:
            raise RuntimeError("Could not connect to LibreOffice")
        desktop = ctx.ServiceManager.createInstanceWithContext("com.sun.star.frame.Desktop", ctx)
        doc = desktop.loadComponentFromURL(uno.systemPathToFileUrl(src), "_blank", 0, (prop("Hidden", True),))
        if doc is None:
            raise RuntimeError("LibreOffice could not open the document")
        toc = []
        try:
            indexes = doc.getDocumentIndexes()
            for _ in range(2):  # second pass settles page numbers after the TOC itself changes length
                for i in range(indexes.getCount()):
                    indexes.getByIndex(i).update()
                try:
                    doc.getTextFields().refresh()
                except Exception:
                    pass
            for i in range(indexes.getCount()):
                idx = indexes.getByIndex(i)
                if "ContentIndex" not in idx.getImplementationName() and not idx.supportsService("com.sun.star.text.ContentIndex"):
                    continue
                text = idx.getAnchor().getString()
                for line in text.splitlines():
                    if "\t" in line:
                        title, _, page = line.rpartition("\t")
                        page = page.strip()
                        if page.isdigit():
                            toc.append({"title": title.strip(), "page": int(page)})
            doc.storeToURL(uno.systemPathToFileUrl(pdf), (
                prop("FilterName", "writer_pdf_Export"),
            ))
        finally:
            doc.close(True)
        with open(toc_json, "w", encoding="utf-8") as f:
            json.dump(toc, f)
        try:
            desktop.terminate()
        except Exception:
            pass
    finally:
        try:
            proc.wait(timeout=20)
        except Exception:
            proc.kill()
        shutil.rmtree(profile, ignore_errors=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:  # noqa
        sys.stderr.write("dk_convert error: %s\n" % e)
        sys.exit(2)
