"""Public viewing places supported by named-species records and visitor guides."""
import json
from pathlib import Path

SOURCE = Path(__file__).with_name("data") / "observation_places.json"


def load_places(db):
    db.execute("""CREATE TABLE IF NOT EXISTS observation_place(
        place_id TEXT PRIMARY KEY, name TEXT NOT NULL, area TEXT NOT NULL,
        kind TEXT NOT NULL CHECK(kind IN ('wild','managed')), latitude REAL NOT NULL,
        longitude REAL NOT NULL, point_grade INTEGER, reason TEXT NOT NULL, visit_note TEXT NOT NULL)""")
    db.execute("""CREATE TABLE IF NOT EXISTS place_species(
        species_id TEXT NOT NULL, place_id TEXT NOT NULL REFERENCES observation_place,
        source TEXT NOT NULL, PRIMARY KEY(species_id,place_id))""")
    db.execute("DELETE FROM place_species")
    db.execute("DELETE FROM observation_place")
    for place in json.loads(SOURCE.read_text(encoding="utf8")):
        db.execute("""INSERT INTO observation_place VALUES(
            :place_id,:name,:area,:kind,:latitude,:longitude,:point_grade,:reason,:visit_note)""", place)
        for sid, (name, source) in place["species"].items():
            if not db.execute("SELECT 1 FROM species WHERE species_id=? AND korean_name=?", (sid, name)).fetchone():
                raise ValueError(f"관찰 장소 자료의 종 ID를 확인하세요: {sid}")
            db.execute("INSERT INTO place_species VALUES(?,?,?)", (sid, place["place_id"], source))


def lookup(db, params):
    sid = params.get("species_id", [""])[0].strip()
    name = params.get("name", [""])[0].strip()
    if bool(sid) == bool(name):
        return 400, {"error": "species_id 또는 정확한 국명 중 하나를 입력하세요."}
    rows = db.execute("""SELECT species_id,korean_name,scientific_name FROM species
        WHERE species_id=? OR (?!='' AND korean_name=?) ORDER BY species_id""", (sid, name, name)).fetchall()
    if not rows:
        return 404, {"error": "제주 종 기준표에서 찾을 수 없습니다."}
    if len(rows) > 1:
        return 409, {"error": "같은 국명이 여러 건입니다. species_id를 선택하세요.", "matches": [dict(r) for r in rows]}
    species = dict(rows[0])
    places = db.execute("""SELECT p.*,link.source FROM place_species link
        JOIN observation_place p USING(place_id)
        WHERE link.species_id=? ORDER BY p.name""", (species["species_id"],))
    regions = db.execute("""SELECT area,grade,grade_label,record_count FROM species_region_grade
        WHERE species_id=? ORDER BY area,grade""", (species["species_id"],))
    return 200, {"species": species, "places": [dict(p) for p in places],
                 "regional_records": [dict(r) for r in regions]}
