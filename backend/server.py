import argparse
import csv
import json
import sqlite3
import unicodedata
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlsplit

HERE = Path(__file__).resolve().parent
DB = HERE / "species.sqlite3"
FIELDS = ("species_id", "gbif_species_key", "scientific_name", "korean_name",
          "class", "source_occurrence_count", "name_match_status",
          "matched_scientific_name", "name_source")


def import_species():
    source = next(p for p in HERE.parent.glob("*.csv")
                  if unicodedata.normalize("NFC", p.name) == "제주 동물 종 ID 기준표.csv")
    with source.open(encoding="utf-8-sig", newline="") as stream, sqlite3.connect(DB) as db:
        db.execute("BEGIN")
        db.execute("DROP TABLE IF EXISTS species")
        db.execute("""CREATE TABLE species (
            species_id TEXT PRIMARY KEY, gbif_species_key TEXT, scientific_name TEXT,
            korean_name TEXT, class TEXT, source_occurrence_count INTEGER,
            name_match_status TEXT, matched_scientific_name TEXT, name_source TEXT)""")
        rows = csv.DictReader(stream)
        db.executemany("INSERT INTO species VALUES (?,?,?,?,?,?,?,?,?)",
                       (tuple(row[k] for k in FIELDS) for row in rows))


class Handler(BaseHTTPRequestHandler):
    def reply(self, status, body, mime="application/json"):
        data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", mime + "; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        url = urlsplit(self.path)
        if url.path == "/":
            return self.reply(200, (HERE / "index.html").read_bytes(), "text/html")
        with sqlite3.connect(DB) as db:
            db.row_factory = sqlite3.Row
            if url.path == "/species":
                query = parse_qs(url.query).get("q", [""])[0].strip().lower()
                rows = db.execute("""SELECT * FROM species WHERE instr(lower(species_id), ?)
                    OR instr(korean_name, ?) OR instr(lower(scientific_name), ?)
                    ORDER BY species_id LIMIT 30""", (query, query, query))
                return self.reply(200, [dict(row) for row in rows])
            if url.path.startswith("/species/"):
                row = db.execute("SELECT * FROM species WHERE species_id=?",
                                 (url.path[9:],)).fetchone()
                if row:
                    return self.reply(200, dict(row))
        self.reply(404, {"error": "찾을 수 없습니다."})


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    import_species()
    ThreadingHTTPServer((args.host, args.port), Handler).serve_forever()
