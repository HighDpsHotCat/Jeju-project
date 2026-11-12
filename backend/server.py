import argparse
import csv
import json
import sqlite3
import unicodedata
from contextlib import closing
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import parse_qs, urlsplit
import ecology
import guide
import region_grades
import observations

HERE = Path(__file__).resolve().parent
DB = HERE / "species.sqlite3"
FIELDS = ("species_id", "gbif_species_key", "scientific_name", "korean_name",
          "class", "source_occurrence_count", "name_match_status",
          "matched_scientific_name", "name_source")


def import_species(database=None):
    source = next(p for p in HERE.parent.glob("*.csv")
                  if unicodedata.normalize("NFC", p.name) == "제주 동물 종 ID 기준표.csv")
    with source.open(encoding="utf-8-sig", newline="") as stream, closing(sqlite3.connect(database or DB)) as db, db:
        db.execute("BEGIN")
        db.execute("DROP TABLE IF EXISTS species")
        db.execute("""CREATE TABLE species (
            species_id TEXT PRIMARY KEY, gbif_species_key TEXT, scientific_name TEXT,
            korean_name TEXT, class TEXT, source_occurrence_count INTEGER,
            name_match_status TEXT, matched_scientific_name TEXT, name_source TEXT)""")
        rows = csv.DictReader(stream)
        missing = set(FIELDS).difference(rows.fieldnames or ())
        if missing:
            raise ValueError("CSV 필수 열 누락: " + ", ".join(sorted(missing)))
        for row in rows:
            if not (row["species_id"] or "").strip():
                raise ValueError(f"CSV {rows.line_num}행에 종 ID가 없습니다.")
            db.execute("INSERT INTO species VALUES (?,?,?,?,?,?,?,?,?)",
                       tuple(row[k] for k in FIELDS))
        if not db.execute("SELECT 1 FROM species LIMIT 1").fetchone():
            raise ValueError("CSV에 종 데이터가 없습니다.")
        region_grades.load_records(db)


class Handler(BaseHTTPRequestHandler):
    def device_request(self):
        url = urlsplit(self.path)
        database = getattr(self.server, "demo_catalog", None)
        if not database or not url.path.startswith("/guide/profiles/"):
            return self.reply(404, {"error": "없는 개발용 경로입니다."})
        try:
            data = {}
            if self.command in ("PUT", "PATCH"):
                if self.headers.get_content_type() != "application/json":
                    return self.reply(415, {"error": "application/json 요청이 필요합니다."})
                size = int(self.headers.get("Content-Length", "0"))
                if not 0 < size <= 65536:
                    return self.reply(400, {"error": "요청 본문 크기를 확인하세요."})
                data = json.loads(self.rfile.read(size))
                if not isinstance(data, dict):
                    raise ValueError("JSON 객체를 입력하세요.")
            status, result = observations.dispatch(database, self.command, url, data)
        except (ValueError, OverflowError) as error:
            return self.reply(400, {"error": str(error)})
        return self.reply(status, result)

    do_PUT = do_PATCH = do_DELETE = device_request

    def reply(self, status, body, mime="application/json"):
        data = body if isinstance(body, bytes) else json.dumps(body, ensure_ascii=False).encode()
        self.send_response(status)
        self.send_header("Content-Type", mime if mime == "image/png" else mime + "; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        url = urlsplit(self.path)
        if url.path.startswith("/guide/profiles/"):
            return self.device_request()
        if url.path.startswith("/guide/") and getattr(self.server, "demo_catalog", None):
            status, body = guide.read(self.server.demo_catalog, url.path, parse_qs(url.query))
            return self.reply(status, body)
        if url.path == "/":
            return self.reply(200, (HERE / "index.html").read_bytes(), "text/html")
        if url.path == "/map":
            return self.reply(200, (HERE / "map.html").read_bytes(), "text/html")
        if url.path == "/jeju-grades.png":
            return self.reply(200, (HERE / "jeju-grades.png").read_bytes(), "image/png")
        if url.path == "/ecology":
            params = parse_qs(url.query, keep_blank_values=True)
            try:
                number = int(params.get("number", ["1"])[0])
                limit = int(params.get("limit", ["10"])[0])
                rank = int(params["wetlandRank"][0]) if "wetlandRank" in params else None
                if (not 1 <= number <= 2**31 or not 1 <= limit <= 100
                        or rank not in (None, 1, 2, 3) or params.keys() - {"number", "limit", "wetlandRank"}):
                    raise ValueError
            except ValueError:
                return self.reply(400, {"error": "number, limit(1~100), wetlandRank(1~3)를 확인하세요."})
            try:
                return self.reply(200, ecology.read_page(number, limit, rank))
            except sqlite3.OperationalError:
                return self.reply(503, {"error": "생태자연도 DB를 준비 중입니다. 동기화 후 다시 조회하세요."})
        with closing(sqlite3.connect(DB)) as db:
            db.row_factory = sqlite3.Row
            if url.path == "/species":
                params = parse_qs(url.query, keep_blank_values=True)
                query = params.get("q", [""])[0].strip().lower()
                try:
                    limit = int(params.get("limit", ["30"])[0])
                    offset = int(params.get("offset", ["0"])[0])
                    if not (1 <= limit <= 100 and 0 <= offset <= 2**63 - 1):
                        raise ValueError
                except ValueError:
                    return self.reply(400, {"error": "limit은 1~100, offset은 0 이상의 64비트 정수를 입력하세요."})
                rows = db.execute("""SELECT * FROM species WHERE instr(lower(species_id), ?)
                    OR instr(korean_name, ?) OR instr(lower(scientific_name), ?)
                    ORDER BY species_id LIMIT ? OFFSET ?""", (query, query, query, limit, offset))
                return self.reply(200, [dict(row) for row in rows])
            if url.path.startswith("/species/"):
                parts = url.path.split("/")
                if len(parts) == 4 and parts[3] == "region-grades":
                    sid = parts[2]
                    if not db.execute("SELECT 1 FROM species WHERE species_id=?", (sid,)).fetchone():
                        return self.reply(404, {"error": "없는 제주 종 ID입니다."})
                    groups = db.execute("SELECT * FROM species_region_grade WHERE species_id=? ORDER BY area,grade", (sid,))
                    records = db.execute("SELECT * FROM region_grade_record WHERE species_id=? ORDER BY gbif_id", (sid,))
                    return self.reply(200, {"species_id": sid, "groups": [dict(r) for r in groups],
                                            "records": [dict(r) for r in records]})
                row = db.execute("SELECT * FROM species WHERE species_id=?",
                                 (url.path[9:],)).fetchone()
                if row:
                    return self.reply(200, dict(row))
        self.reply(404, {"error": "찾을 수 없습니다."})


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    parser.add_argument("--demo-catalog", type=Path, help="가상 관찰 테스트용 기기 DB 파일")
    args = parser.parse_args()
    if args.demo_catalog:
        if args.host not in ("127.0.0.1", "localhost"):
            parser.error("--demo-catalog는 로컬 테스트 전용입니다.")
        guide.upgrade(args.demo_catalog)
    import_species()
    with ThreadingHTTPServer((args.host, args.port), Handler) as server:
        server.demo_catalog = args.demo_catalog
        server.serve_forever()
