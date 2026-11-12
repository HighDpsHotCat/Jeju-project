"""Local-only mock storage for fictional device observations."""
import math
import sqlite3
from contextlib import closing
from datetime import datetime, timezone
from urllib.parse import parse_qs, unquote

CAPTURE = ("photo_id", "photo_ref", "captured_at", "gps_at", "latitude", "longitude",
           "accuracy_m", "boundary_version", "capture_source", "location_verdict", "mode")
IDENTITY = ("identification", "species_id")


def require(ok, message):
    if not ok:
        raise ValueError(message)


def identification(data):
    state = data.get("identification")
    species = data.get("species_id")
    require((state == "unknown" and species is None) or
            (state == "confirmed" and isinstance(species, str) and species.strip()),
            "미확인은 species_id=null, 확인된 관찰은 종 ID가 필요합니다.")
    return state, species


def capture(data):
    require(set(data) == set(CAPTURE + IDENTITY), "관찰 필드를 확인하세요.")
    for name in ("photo_id", "photo_ref", "boundary_version"):
        require(isinstance(data[name], str) and data[name].strip(), f"{name} 값이 필요합니다.")
    for name in ("captured_at", "gps_at"):
        require(isinstance(data[name], str), f"{name}은 시간대가 있는 ISO 날짜여야 합니다.")
        moment = datetime.fromisoformat(data[name].replace("Z", "+00:00"))
        require(moment.tzinfo is not None, f"{name}에 시간대를 포함하세요.")
        data[name] = moment.astimezone(timezone.utc).isoformat(timespec="microseconds")
    for name, low, high in (("latitude", -90, 90), ("longitude", -180, 180),
                            ("accuracy_m", 0, math.inf)):
        n = data[name]
        require(type(n) in (int, float) and low <= n <= high and math.isfinite(n),
                f"{name}의 숫자 범위를 확인하세요.")
    require(data["capture_source"] == "camera" and data["location_verdict"] == "inside",
            "앱 카메라로 촬영하고 제주 안으로 판정된 관찰만 저장합니다.")
    require(data["mode"] in ("general", "research_only"), "관찰의 공개 구분을 확인하세요.")
    identification(data)


def dispatch(database, method, url, data):
    segments = [unquote(s) for s in url.path.split("/")[3:]]
    require(segments and all(s.strip() and "/" not in s for s in segments), "ID가 필요합니다.")
    user = segments[0]
    missing = (404, {"error": "해당 사용자의 기록을 찾을 수 없습니다."})
    with closing(sqlite3.connect(database.resolve().as_uri()+"?mode=rw", uri=True)) as db, db:
        db.row_factory = sqlite3.Row
        db.execute("PRAGMA foreign_keys=ON")
        db.execute("BEGIN" if method == "GET" else "BEGIN IMMEDIATE")
        if len(segments) == 1 and method == "PUT":
            require(not data, "프로필 초기화에는 빈 JSON 객체를 사용하세요.")
            cur = db.execute("INSERT INTO device_profile(user_id) VALUES (?) ON CONFLICT(user_id) DO NOTHING", (user,))
            return (201 if cur.rowcount else 200), {"user_id": user}
        if segments[1:2] != ["observations"] or not db.execute(
                "SELECT 1 FROM device_profile WHERE user_id=?", (user,)).fetchone():
            return missing
        if len(segments) == 2 and method == "GET":
            params = parse_qs(url.query)
            limit = int(params.get("limit", ["30"])[0])
            offset = int(params.get("offset", ["0"])[0])
            require(1 <= limit <= 100 and 0 <= offset < 2**63, "페이지 범위를 확인하세요.")
            sid = params.get("species_id", [None])[0]
            rows = db.execute("""SELECT * FROM observation WHERE user_id=? AND mode='general'
                AND (? IS NULL OR species_id=?) ORDER BY julianday(captured_at) DESC, observation_id DESC
                LIMIT ? OFFSET ?""", (user, sid, sid, limit, offset))
            return 200, [dict(r) for r in rows]
        if len(segments) != 3:
            return missing
        obs = segments[2]
        ids = (user, obs)
        row = db.execute("SELECT * FROM observation WHERE user_id=? AND observation_id=?", ids).fetchone()
        if method == "PUT":
            capture(data)
            if row:
                if all(row[k] == v for k, v in data.items()):
                    return 200, dict(row)
                return 409, {"error": "이미 다른 내용으로 등록된 관찰 ID입니다."}
            try:
                db.execute("""INSERT INTO observation(user_id,observation_id,photo_id,photo_ref,
                    captured_at,gps_at,latitude,longitude,accuracy_m,boundary_version,capture_source,
                    location_verdict,mode,identification,species_id) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)""",
                           (*ids, *(data[k] for k in CAPTURE + IDENTITY)))
            except sqlite3.IntegrityError:
                return 409, {"error": "관찰 또는 사진 ID가 이미 사용 중입니다."}
            return 201, {"user_id": user, "observation_id": obs, **data}
        if not row:
            return missing
        if method == "GET":
            return 200, dict(row)
        if method == "PATCH":
            require(set(data) == set(IDENTITY), "종 정정에는 identification과 species_id만 사용하세요.")
            state, species = identification(data)
            db.execute("UPDATE observation SET identification=?,species_id=? WHERE user_id=? AND observation_id=?",
                       (state, species, *ids))
            return 200, {**dict(row), **data}
        if method == "DELETE":
            db.execute("DELETE FROM observation WHERE user_id=? AND observation_id=?", ids)
            return 200, {"deleted": obs}
        return 405, {"error": "지원하지 않는 요청입니다."}
