"""Refresh a bounded sample of Jeju wildlife observations for the shared guide."""
import csv
import json
from datetime import datetime, timezone
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
API = "https://api.gbif.org/v1/occurrence/search"
JEJU = "KOR.12_1"


def main():
    with next(ROOT.glob("*ID*.csv")).open(encoding="utf-8-sig", newline="") as source:
        catalog = {}
        for row in csv.DictReader(source):
            catalog.setdefault(row["scientific_name"].strip().casefold(), []).append(row["species_id"])
    records = {}
    seen = set()
    for offset in range(0, 900, 300):
        query = urlencode(dict(gadmGid=JEJU, country="KR", kingdomKey=1,
                               basisOfRecord="HUMAN_OBSERVATION", occurrenceStatus="PRESENT",
                               hasCoordinate="true", hasGeospatialIssue="false", limit=300, offset=offset)) + "&license=CC0_1_0&license=CC_BY_4_0"
        with urlopen(API + "?" + query, timeout=45) as response:
            page = json.load(response)
        for row in page["results"]:
            matches = catalog.get((row.get("species") or "").strip().casefold(), [])
            species = matches[0] if len(matches) == 1 else None
            key = str(row["key"])
            license_url = row.get("license", "").replace("http://", "https://")
            if (not species or key in seen or not row.get("eventDate")
                    or row.get("gadm", {}).get("level1", {}).get("gid") != JEJU
                    or not license_url.startswith(("https://creativecommons.org/licenses/by/4.0/",
                                                   "https://creativecommons.org/publicdomain/zero/1.0/"))):
                continue
            seen.add(key)
            records.setdefault(species, []).append(dict(id=key, date=row["eventDate"],
                credit=row.get("recordedBy") or "", dataset=row.get("datasetName") or row["datasetKey"],
                license=license_url))
        if page["endOfRecords"]:
            break
    if not records:
        raise ValueError("Empty sample; keeping previous snapshot")
    for group in records.values():
        group.sort(key=lambda r: (r["date"], r["id"]), reverse=True)
    output = ROOT / "field_guide/gbif.json"
    tmp = output.with_suffix(".tmp")
    tmp.write_text(json.dumps(dict(collected=datetime.now(timezone.utc).date().isoformat(),
                                  records=records), ensure_ascii=False), encoding="utf8")
    tmp.replace(output)
    print(len(records), "species;", len(seen), "observations")


if __name__ == "__main__":
    main()
