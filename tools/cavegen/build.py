"""Inlines cavegen.js into page.html, writing index.html: the self-contained prototype page."""
import pathlib
here = pathlib.Path(__file__).parent
page = (here / "page.html").read_text()
gen = (here / "cavegen.js").read_text()
assert "/*CAVEGEN*/" in page
(here / "index.html").write_text(page.replace("/*CAVEGEN*/", gen))
print("index.html", len(page) + len(gen), "bytes")
