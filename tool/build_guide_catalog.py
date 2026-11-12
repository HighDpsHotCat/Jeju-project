"""Build the Flutter search index from the two taxonomy files in the repository.

Run with Python 3 and openpyxl. The application reads the bundled result offline.
"""

import csv
import gzip
from collections import Counter, defaultdict
from contextlib import closing
from pathlib import Path

from openpyxl import load_workbook


ROOT = Path(__file__).resolve().parents[1]
ANIMAL_GROUPS = {
    "포유류", "조류", "양서류", "파충류", "어류", "미삭동물",
    "무척추동물류(곤충제외)", "곤충류",
}
FEATURED = ["JJ-000837", "JJ-004873", "JJ-003382", "JJ-004729"]


def build():
    # Each scope keeps the names and classification published by its own source.
    jeju_rows = []
    jeju_by_name = defaultdict(list)
    with next(ROOT.glob("*ID*.csv")).open(encoding="utf-8-sig", newline="") as stream:
        for item in csv.DictReader(stream):
            if item["name_match_status"] in ("학명 일치", "GBIF 이명 일치"):
                key = (item["matched_scientific_name"], item["korean_name"])
                if all(key):
                    jeju_by_name[key].append(item["species_id"])
            count = item["source_occurrence_count"].strip()
            if count.isdecimal() and int(count) > 0:
                jeju_rows.append([item["species_id"], "", item["korean_name"],
                                  item["scientific_name"], item["class"], "jeju"])

    with closing(load_workbook(next(ROOT.glob("*v1.0.xlsx")), read_only=True, data_only=True)) as book:
        sheet = book["62,604종"]
        headers = next(sheet.values)
        col = {name: headers.index(name) for name in ("KTSN", "관리분류군", "대표국명", "학명", "Class")}
        national_rows = []
        name_counts = Counter()
        for item in sheet.iter_rows(min_row=3, values_only=True):
            key = (item[col["학명"]], item[col["대표국명"]])
            name_counts[key] += 1
            if item[col["관리분류군"]] not in ANIMAL_GROUPS:
                continue
            ktsn = str(item[col["KTSN"]])
            national_rows.append(["", ktsn, key[1] or "", key[0], item[col["Class"]] or "", "national"])

    national_ids = {}
    for row in national_rows:
        key = (row[3], row[2])
        matches = jeju_by_name.get(key, ())
        if len(matches) == 1 and name_counts[key] == 1:
            row[0] = matches[0]
            national_ids[row[0]] = row[1]
    for row in jeju_rows:
        row[1] = national_ids.get(row[0], "")
    rows = jeju_rows + national_rows
    for row in rows:
        if row[2] == "[국명없음]":
            row[2] = ""
    priority = {species_id: i for i, species_id in enumerate(FEATURED)}
    rows.sort(key=lambda r: (priority.get(r[0], len(FEATURED)), not bool(r[2]), r[2], r[3], r[0], r[1]))
    result = ROOT / "assets/catalog/animals.tsv.gz"
    with result.open("wb") as out, gzip.GzipFile(fileobj=out, filename="", mode="wb", mtime=0) as zipped:
        for row in rows:
            clean = [" ".join(str(cell).split()) for cell in row]
            zipped.write(("\t".join(clean) + "\n").encode("utf-8"))
    print(f"제주 목격 {len(jeju_rows):,} · 전국 {len(national_rows):,}")


if __name__ == "__main__":
    build()
