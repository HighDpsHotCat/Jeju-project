"""Build the offline SQLite catalog distributed with the app."""
import argparse
import os
from contextlib import closing
from pathlib import Path
import sqlite3
import tempfile

from server import HERE, import_species
from reference_data import import_references


def build_catalog(output):
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(dir=output.parent) as staging:
        database = Path(staging) / "catalog.sqlite3"
        import_species(database)
        with closing(sqlite3.connect(database)) as db:
            db.executescript((HERE / "device_schema.sql").read_text(encoding="utf-8"))
            with db:
                import_references(db)
        # Never overwrite a database that may already contain personal observations.
        os.link(database, output)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    build_catalog(parser.parse_args().output)
