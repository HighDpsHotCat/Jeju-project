"""Export a GBIF region_grade_records.csv snapshot for the public observation map."""
import argparse
import csv
import json
from collections import defaultdict
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path, help="backend/data/region_grade_records.csv")
    args = parser.parse_args()
    with next(ROOT.glob("*ID*.csv")).open(encoding="utf-8-sig", newline="") as source:
        names = {row["species_id"]: row["korean_name"] or row["scientific_name"]
                 for row in csv.DictReader(source)}

    regions = defaultdict(list)
    seen = set()
    with args.source.open(encoding="utf-8-sig", newline="") as source:
        for row in csv.DictReader(source):
            key = row["gbif_id"]
            if key in seen or row["area"] not in ("제주시", "서귀포시"):
                raise ValueError(f"Invalid occurrence or municipality for {key}")
            seen.add(key)
            regions[row["area"]].append({
                "id": key, "species": row["species_id"], "name": names[row["species_id"]],
                "date": row["event_date"], "dataset": row["source_dataset"],
                "credit": row["attribution"], "license": row["license_url"],
            })
    for records in regions.values():
        records.sort(key=lambda record: (record["date"], record["id"]), reverse=True)
    output = ROOT / "field_guide" / "observations.json"
    output.write_text(json.dumps(regions, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
