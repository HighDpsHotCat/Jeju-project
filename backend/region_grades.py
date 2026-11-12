"""Load verified intersections of public GBIF records and Jeju ecology polygons."""
import csv
from pathlib import Path

SOURCE = Path(__file__).with_name("data") / "region_grade_records.csv"


def load_records(db):
    """Run inside the caller's transaction."""
    db.execute("""CREATE TABLE IF NOT EXISTS region_grade_record (
        gbif_id TEXT PRIMARY KEY NOT NULL, species_id TEXT NOT NULL,
        scientific_name TEXT NOT NULL, area TEXT NOT NULL CHECK(area IN ('제주시','서귀포시')),
        grade INTEGER NOT NULL CHECK(grade IN (1,2,3,9)), dataset_key TEXT NOT NULL,
        event_date TEXT NOT NULL, uncertainty_m REAL NOT NULL CHECK(uncertainty_m BETWEEN 1 AND 100),
        polygon_id TEXT NOT NULL, source_dataset TEXT NOT NULL, attribution TEXT NOT NULL,
        license_url TEXT NOT NULL)""")
    db.execute("DELETE FROM region_grade_record")
    with SOURCE.open(encoding="utf-8", newline="") as stream:
        db.executemany("""INSERT INTO region_grade_record VALUES
            (:gbif_id,:species_id,:scientific_name,:area,:grade,:dataset_key,:event_date,
             :uncertainty_m,:polygon_id,:source_dataset,:attribution,:license_url)""", csv.DictReader(stream))
    if db.execute("""SELECT 1 FROM region_grade_record r WHERE NOT EXISTS
        (SELECT 1 FROM species s WHERE s.species_id=r.species_id
         AND s.scientific_name=r.scientific_name) LIMIT 1""").fetchone():
        raise ValueError("지역 등급 자료와 제주 종 기준표의 식별자가 일치하지 않습니다.")
    db.execute("CREATE INDEX IF NOT EXISTS grade_species_area ON region_grade_record(species_id,area,grade)")
    db.execute("""CREATE VIEW IF NOT EXISTS species_region_grade AS
        SELECT species_id,area,grade,CASE grade WHEN 9 THEN '별도관리지역'
              ELSE grade || '등급' END AS grade_label, count(*) AS record_count
        FROM region_grade_record GROUP BY species_id,area,grade""")
