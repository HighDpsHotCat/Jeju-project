"""Import national taxonomy and Jeju Data Hub preview into separate reference tables."""
import csv
from contextlib import closing

from openpyxl import load_workbook

from server import HERE


def import_references(db):
    workbook = HERE.parent / "2025년 국가생물종목록 v1.0.xlsx"
    with closing(load_workbook(workbook, read_only=True, data_only=True)) as book:
        rows = book["62,604종"].values
        groups = next(rows)
        subheaders = next(rows)
        columns = []
        for group, subheader in zip(groups, subheaders):
            if group:
                parent = group
            name = f"{parent}_{subheader}" if subheader else parent
            columns.append('"' + name.replace('"', '""') + '" TEXT')
        db.execute("CREATE TABLE national_species (" + ", ".join(columns) + ")")
        placeholders = ", ".join("?" for _ in columns)
        db.executemany("INSERT INTO national_species VALUES (" + placeholders + ")", rows)
        db.execute("CREATE INDEX national_species_name ON national_species(학명)")

    with (HERE / "data/ecology_preview.csv").open(encoding="cp949", newline="") as stream:
        rows = csv.reader(stream)
        header = next(rows)
        columns = ['"' + name.replace('"', '""') + '" TEXT' for name in header]
        db.execute("CREATE TABLE ecology_preview (" + ", ".join(columns) + ")")
        placeholders = ", ".join("?" for _ in header)
        db.executemany("INSERT INTO ecology_preview VALUES (" + placeholders + ")",
                       (row for row in rows if row))
