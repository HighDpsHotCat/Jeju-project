"""Synchronize Jeju Data Hub's full ecological naturalness dataset."""
import json
import os
import sqlite3
from contextlib import closing
from pathlib import Path
from urllib.error import HTTPError, URLError
from urllib.parse import quote
from urllib.request import urlopen

DATABASE = Path(__file__).with_name("ecology.sqlite3")
BASE = "https://open.jejudatahub.net/api/proxy/6bttat6t84Daa1b18aat1a6DD4648Daa/"


def fetch_page(key, number):
    url = BASE + quote(key, safe="") + f"?number={number}&limit=100"
    try:
        with urlopen(url, timeout=30) as response:
            return json.load(response)
    except HTTPError as error:
        raise ValueError(f"허브 HTTP {error.code}. 프로젝트 권한과 호출 한도를 확인하세요.") from None
    except (URLError, OSError, UnicodeError, ValueError):
        raise ValueError("허브 연결 또는 JSON 응답을 확인하세요.") from None


def sync(key, database=DATABASE, fetch=fetch_page):
    if not key:
        raise ValueError("JEJU_DATAHUB_PROJECT_KEY 설정이 필요합니다.")
    with closing(sqlite3.connect(database)) as db, db:
        db.execute("BEGIN IMMEDIATE")
        db.execute("DROP TABLE IF EXISTS ecology")
        db.execute("CREATE TABLE ecology (id INTEGER PRIMARY KEY, wetland_rank INTEGER, record TEXT NOT NULL)")
        number, count, expected = 1, 0, None
        while True:
            page = fetch(key, number)
            if not isinstance(page, dict):
                raise ValueError("허브 응답 형식이 올바르지 않습니다.")
            rows, total, more = (page.get(k) for k in ("data", "totCnt", "hasMore"))
            if (type(total) is not int or total < 0 or type(more) is not bool
                    or not isinstance(rows, list) or len(rows) > 100
                    or any(not isinstance(row, dict) for row in rows)):
                raise ValueError("허브 응답 형식 또는 프로젝트 권한을 확인하세요.")
            if expected is None:
                expected = total
            count += len(rows)
            if total != expected or count > total or more != (count < total) or (more and not rows):
                raise ValueError("페이지 합계가 일치하지 않습니다. 다시 동기화하세요.")
            db.executemany("INSERT INTO ecology(wetland_rank,record) VALUES (?,?)",
                           ((r.get("wetlandRank"), json.dumps(r, ensure_ascii=False)) for r in rows))
            if not more:
                break
            number += 1
        db.execute("CREATE INDEX ecology_wetland ON ecology(wetland_rank)")
    return count


def read_page(number, limit, rank, database=DATABASE):
    with closing(sqlite3.connect(database.as_uri() + "?mode=ro", uri=True)) as db:
        db.execute("BEGIN")
        where, args = ("", ()) if rank is None else (" WHERE wetland_rank=?", (rank,))
        total = db.execute("SELECT count(*) FROM ecology" + where, args).fetchone()[0]
        offset = (number - 1) * limit
        records = db.execute("SELECT record FROM ecology" + where + " ORDER BY id LIMIT ? OFFSET ?",
                             (*args, limit, offset))
        return {"totCnt": total, "hasMore": offset + limit < total,
                "data": [json.loads(record) for record, in records]}


if __name__ == "__main__":
    try:
        print(f"{sync(os.getenv('JEJU_DATAHUB_PROJECT_KEY'))}건 동기화 완료")
    except (ValueError, sqlite3.Error) as error:
        raise SystemExit(str(error))
