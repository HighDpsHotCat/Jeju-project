"""Serve the shared web guide from this checkout, using only the Python standard library."""
import argparse
import csv
import gzip
import json
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import urlsplit

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent
FILES = {
    "/": (HERE / "index.html", "text/html"),
    "/map": (HERE / "map.html", "text/html"),
    "/jeju-grades.png": (HERE / "jeju-grades.png", "image/png"),
    "/jeju-map.png": (HERE / "jeju-map.png", "image/png"),
    "/observations.json": (HERE / "observations.json", "application/json"),
    "/gbif.json": (HERE / "gbif.json", "application/json"),
    "/visit-jeju.json": (HERE / "visit-jeju.json", "application/json"),
    "/nearby.json": (HERE / "nearby.json", "application/json"),
}
for name in ("field.css", "map.css", "ui.js", "guide.js", "map.js", "map-data.js", "visit-map.js", "pin-map.js", "observation-analysis.js"):
    FILES["/" + name] = (HERE / name, "text/css" if name.endswith(".css") else "text/javascript")
for path in (ROOT / "assets/art").glob("*.jpg"):
    FILES["/art/" + path.name] = (path, "image/jpeg")


def catalog_data():
    with next(ROOT.glob("*ID*.csv")).open(encoding="utf-8-sig", newline="") as source:
        jeju = list(csv.DictReader(source))
    national = []
    with gzip.open(ROOT / "assets/catalog/animals.tsv.gz", "rt", encoding="utf-8") as stream:
        for jeju_id, ktsn, korean, scientific, taxon, scope in csv.reader(stream, delimiter="\t"):
            if scope == "national":
                national.append([ktsn, korean, scientific, taxon, jeju_id])
    return {route: json.dumps(rows, ensure_ascii=False).encode()
            for route, rows in (("/species.json", jeju), ("/national.json", national))}


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        route = urlsplit(self.path).path
        if route == "/ecology":
            self.send_response(302)
            self.send_header("Location", "https://jeju-wildlife-field-guide.gimhoyeon.chatgpt.site/ecology")
            self.end_headers()
            return
        if route in self.server.catalogs:
            content, mime = self.server.catalogs[route], "application/json"
        elif route in FILES:
            path, mime = FILES[route]
            content = path.read_bytes()
        else:
            self.send_error(404)
            return
        self.send_response(200)
        self.send_header("Content-Type", mime if mime.startswith("image/") else mime + "; charset=utf-8")
        self.send_header("Content-Length", str(len(content)))
        self.end_headers()
        self.wfile.write(content)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=8787)
    args = parser.parse_args()
    with ThreadingHTTPServer(("127.0.0.1", args.port), Handler) as server:
        server.catalogs = catalog_data()
        print(f"http://127.0.0.1:{server.server_port}", flush=True)
        server.serve_forever()
