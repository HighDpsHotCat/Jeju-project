"""Read-only development API for a device-format catalog with fictional observations."""
import sqlite3
from contextlib import closing
from pathlib import Path
import json
from region_grades import load_records
from recommendations import load_places

HERE = Path(__file__).resolve().parent
ENTRIES = (HERE / "guide_entries.sql").read_text(encoding="utf-8")


def upgrade(database):
    # Never create a new empty file when the supplied path is incorrect.
    with closing(sqlite3.connect(database.resolve().as_uri() + "?mode=rw", uri=True)) as db:
        db.executescript((HERE / "guide_schema.sql").read_text(encoding="utf-8"))
        with db:
            db.execute("BEGIN IMMEDIATE")
            load_records(db)
            load_places(db)


def read(database, route, params):
    if route not in ("/guide/home", "/guide/entries"):
        return 404, {"error": "없는 도감 경로입니다."}
    user_id = params.get("user_id", [""])[0]
    if not user_id:
        return 400, {"error": "user_id가 필요합니다."}
    with closing(sqlite3.connect(database.resolve().as_uri() + "?mode=ro", uri=True)) as db:
        db.row_factory = sqlite3.Row
        db.execute("BEGIN")
        home = db.execute("SELECT * FROM collection_progress WHERE user_id=?", (user_id,)).fetchone()
        if home is None:
            return 404, {"error": "등록되지 않은 기기 사용자입니다."}
        if route == "/guide/home":
            return 200, dict(home)
        region = params.get("region", ["jeju"])[0]
        taxon = params.get("class", [""])[0]
        try:
            limit = int(params.get("limit", ["30"])[0])
            offset = int(params.get("offset", ["0"])[0])
            if region not in ("jeju", "national") or not 1 <= limit <= 100 or not 0 <= offset < 2**63:
                raise ValueError
        except ValueError:
            return 400, {"error": "region(jeju/national), limit(1~100), offset을 확인하세요."}
        rows = db.execute(ENTRIES, dict(user_id=user_id, region=region, taxon=taxon,
                          q=params.get("q", [""])[0].strip(), limit=limit, offset=offset))
        return 200, [{**row, "registered": bool(row["registered"]),
                      "region_grades": json.loads(row["region_grades"])} for row in rows]
