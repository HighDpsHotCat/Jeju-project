"""Build a public GBIF sample around the ten visitor map pins (WGS84)."""
import csv
import json
import math
from datetime import datetime, timezone
from urllib.parse import urlencode
from urllib.request import urlopen

from collect_gbif import API, ROOT

CENTERS = {
    "arboretum": (33.469604, 126.4918), "cheonjiyeon": (33.2447173, 126.5598201),
    "gotjawal": (33.282955, 126.27152), "gwangchigi": (33.4524527554, 126.9245813942),
    "dongbaek": (33.5095241, 126.7152691), "sindo": (33.27611896, 126.170891),
    "highland": (33.35762241, 126.4629887), "hado": (33.51021, 126.896255),
    "park": (33.43972902, 126.823609), "oreum": (33.368611, 126.692222),
}
RADIUS = 2000


def distance(lat, lon, y, x):
    a = (math.sin(math.radians(y-lat)/2)**2 + math.cos(math.radians(lat))
         * math.cos(math.radians(y)) * math.sin(math.radians(x-lon)/2)**2)
    return 12742000 * math.asin(math.sqrt(a))


def collect(lat, lon, catalog):
    dy = RADIUS / 110000
    dx = dy / math.cos(math.radians(lat))
    found = {}
    seen = set()
    for offset in (0, 300, 600):
        query = urlencode(dict(country="KR", kingdomKey=1, basisOfRecord="HUMAN_OBSERVATION",
            occurrenceStatus="PRESENT", hasGeospatialIssue="false", hasCoordinate="true",
            decimalLatitude=f"{lat-dy},{lat+dy}", decimalLongitude=f"{lon-dx},{lon+dx}",
            coordinateUncertaintyInMeters="0,1000", limit=300, offset=offset))
        with urlopen(API+"?"+query+"&license=CC0_1_0&license=CC_BY_4_0", timeout=60) as response:
            page = json.load(response)
        for r in page["results"]:
            matches = catalog.get((r.get("species") or "").strip().casefold(), [])
            if len(matches) != 1 or not r.get("eventDate") or r["key"] in seen:
                continue
            metres = distance(lat, lon, r["decimalLatitude"], r["decimalLongitude"])
            uncertainty = r.get("coordinateUncertaintyInMeters")
            if uncertainty is None or metres + uncertainty > RADIUS:
                continue
            seen.add(r["key"])
            species, name = matches[0]
            group = found.setdefault(species, dict(species=species, name=name, count=0))
            group["count"] += 1
            if r["eventDate"] >= group.get("date", ""):
                group.update(date=r["eventDate"], metres=round(metres), id=str(r["key"]),
                    credit=r.get("recordedBy") or "", dataset=r.get("datasetName") or r["datasetKey"],
                    license=r["license"])
        if page["endOfRecords"]:
            break
    return sorted(found.values(), key=lambda g: (g["date"], g["id"]), reverse=True)


if __name__ == "__main__":
    from concurrent.futures import ThreadPoolExecutor
    catalog = {}
    with next(ROOT.glob("*ID*.csv")).open(encoding="utf-8-sig", newline="") as f:
        for r in csv.DictReader(f):
            catalog.setdefault(r["scientific_name"].strip().casefold(), []).append(
                (r["species_id"], r["korean_name"] or r["scientific_name"]))
    with ThreadPoolExecutor(max_workers=3) as pool:
        jobs = {k: pool.submit(collect, *point, catalog) for k, point in CENTERS.items()}
        places = {k: job.result() for k, job in jobs.items()}
    out = ROOT / "field_guide/nearby.json"
    tmp = out.with_suffix(".tmp")
    tmp.write_text(json.dumps(dict(radiusM=RADIUS, collected=datetime.now(timezone.utc).date().isoformat(),
                                   places=places), ensure_ascii=False), encoding="utf8")
    tmp.replace(out)
    print({k: len(v) for k, v in places.items()})
