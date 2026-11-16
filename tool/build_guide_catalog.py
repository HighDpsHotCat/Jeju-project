"""Build the Flutter search index from the two taxonomy files in the repository.

Run with Python 3 and openpyxl. The application reads the bundled result offline.
"""

import csv
import gzip
from collections import defaultdict
from contextlib import closing
from pathlib import Path

from openpyxl import load_workbook


ROOT = Path(__file__).resolve().parents[1]
ANIMAL_GROUPS = {
    "포유류", "조류", "양서류", "파충류", "어류", "미삭동물",
    "무척추동물류(곤충제외)", "곤충류",
}
# These source classes cover roundworms, flukes and tapeworms.
EXCLUDED_CLASSES = {"Chromadorea", "Enoplea", "Adenophorea", "Trematoda", "Monogenea", "Cestoda"}
MITE_ORDERS = {
    "Acari", "Astigmata", "Ixodida", "Mesostigmata", "Oribatida",
    "Prostigmata", "Sarcoptiformes", "Trombidiformes",
}
CORAL_ORDERS = {"Alcyonacea", "Pennatulacea", "Scleractinia", "Antipatharia"}
HYDROCORAL_FAMILIES = {"Stylasteridae", "Milleporidae"}
FEATURED = ["JJ-000837", "JJ-004873", "JJ-003382", "JJ-004729"]


def build():
    # Jeju ID, national KTSN, Korean name, scientific name, taxonomic class.
    rows = []
    jeju_by_scientific = defaultdict(set)
    with next(ROOT.glob("*ID*.csv")).open(encoding="utf-8-sig", newline="") as stream:
        for item in csv.DictReader(stream):
            sci = item["scientific_name"].strip()
            index = len(rows)
            rows.append([item["species_id"], "", item["korean_name"], sci, item["class"]])
            jeju_by_scientific[sci.casefold()].add(index)
            if "일치" in item["name_match_status"] and item["matched_scientific_name"]:
                jeju_by_scientific[item["matched_scientific_name"].strip().casefold()].add(index)

    mite_genera = set()
    # This black coral genus occurs in the Jeju source only.
    coral_genera = {("Anthozoa", "Bathypathes")}
    with closing(load_workbook(next(ROOT.glob("*v1.0.xlsx")), read_only=True, data_only=True)) as book:
        sheet = book["62,604종"]
        headers = next(sheet.values)
        col = {name: headers.index(name) for name in ("KTSN", "관리분류군", "대표국명", "학명", "Class", "Order", "Genus", "Family")}
        for item in sheet.iter_rows(min_row=3, values_only=True):
            if item[col["관리분류군"]] not in ANIMAL_GROUPS:
                continue
            if item[col["Class"]] == "Arachnida" and item[col["Order"]] in MITE_ORDERS:
                mite_genera.add(item[col["Genus"]])
                continue
            taxon = item[col["Class"]]
            if (taxon == "Anthozoa" and item[col["Order"]] in CORAL_ORDERS
                    or taxon == "Hydrozoa" and item[col["Family"]] in HYDROCORAL_FAMILIES):
                coral_genera.add((taxon, item[col["Genus"]]))
                continue
            if taxon == "Octocorallia" or taxon in EXCLUDED_CLASSES:
                continue
            ktsn = str(item[col["KTSN"]])
            sci = item[col["학명"]].strip()
            matches = jeju_by_scientific.get(sci.casefold(), ())
            if len(matches) == 1:
                jeju = rows[next(iter(matches))]
                if not jeju[1]:
                    jeju[1] = ktsn
                    continue
            rows.append(["", ktsn, item[col["대표국명"]] or "", sci, item[col["Class"]] or ""])

    rows = [
        row for row in rows if row[4] not in EXCLUDED_CLASSES
        and row[4] != "Octocorallia"
        and (row[4], row[3].split()[0]) not in coral_genera
        and not (row[4] == "Arachnida" and row[3].split()[0] in mite_genera)
    ]
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
    print(f"제주 {sum(bool(r[0]) for r in rows):,} · 전국 {sum(bool(r[1]) for r in rows):,} · 통합 {len(rows):,}")


if __name__ == "__main__":
    build()
