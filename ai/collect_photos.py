"""Collect one licensed image per wild research-grade observation for the demo."""
import argparse
import concurrent.futures
import gzip
import hashlib
import json
import time
from collections import Counter
from pathlib import Path
from urllib.parse import urlencode
from urllib.request import Request, urlopen

BASE = "https://api.inaturalist.org/v1/"
POSITIVES = {
    "roe": "Capreolus pygargus", "sparrow": "Passer montanus",
    "ladybird": "Coccinella septempunctata", "slider": "Trachemys scripta elegans",
    "frog": "Dryophytes japonicus",
}
OTHERS = {
    "fallow": "Dama dama", "deer": "Cervus nippon", "house_sparrow": "Passer domesticus",
    "swallow": "Hirundo rustica", "asian_ladybird": "Harmonia axyridis",
    "butterfly": "Pieris rapae", "pond_turtle": "Mauremys reevesii",
    "painted_turtle": "Chrysemys picta", "water_frog": "Pelophylax nigromaculatus",
    "toad": "Bufo bufo", "pine": "Pinus densiflora", "daisy": "Bellis perennis",
    "duck": "Anas platyrhynchos", "squirrel": "Sciurus vulgaris",
    "european_roe": "Capreolus capreolus", "american_treefrog": "Dryophytes cinereus",
    "yellow_slider": "Trachemys scripta scripta",
}


def download(url):
    for delay in (0, 2, 6):
        time.sleep(delay)
        try:
            req = Request(url, headers={"User-Agent": "Jeju-wildlife-guide/1.0 (photo research)"})
            with urlopen(req, timeout=25) as response:
                return response.read()
        except OSError:
            if delay == 6:
                raise


def api(endpoint, **params):
    time.sleep(1.2)
    return json.loads(download(BASE + endpoint + "?" + urlencode(params)))["results"]


def split(author):
    bucket = int(hashlib.sha256(f"jeju-v1:{author}".encode()).hexdigest()[:8], 16) % 100
    return "train" if bucket < 60 else "val" if bucket < 77 else "test"


def collect(directory, key, scientific):
    """Leave the exact observation IDs and photographer credits in the manifest."""
    path = directory / (key + ".json")
    if path.exists():
        return json.loads(path.read_text(encoding="utf-8"))
    taxa = api("taxa/autocomplete", q=scientific, per_page=15)
    taxon = next(t for t in taxa if t["name"] == scientific and t["is_active"])
    targets = dict(zip(("train", "val", "test"),
                       (120, 30, 50) if key in POSITIVES else (26, 8, 16)))
    counts, authors = Counter(), Counter()
    rows, seen, observations_seen = [], set(), set()
    for page in range(1, 7):
        observations = api("observations", taxon_id=taxon["id"], quality_grade="research",
                           photos="true", captive="false", photo_license="cc0,cc-by",
                           order_by="id", order="desc", per_page=200, page=page)
        for obs in observations:
            author = obs["user"]["id"]
            partition = split(author)
            if counts[partition] >= targets[partition] or authors[author] >= 3:
                continue
            photo = next((p for p in obs["photos"] if p.get("license_code") in ("cc0", "cc-by")
                          and "/square." in p["url"]), None)
            if not photo or photo["id"] in seen or obs["id"] in observations_seen:
                continue
            seen.add(photo["id"])
            observations_seen.add(obs["id"])
            rows.append(dict(label=key, partition=partition, observation=obs["id"],
                             photo=photo["id"], author=author, attribution=photo["attribution"],
                             license=photo["license_code"], url=photo["url"].replace("/square.", "/medium.")))
            counts[partition] += 1
            authors[author] += 1
        if all(counts[k] >= n for k, n in targets.items()) or len(observations) < 200:
            break
    path.write_text(json.dumps(rows, ensure_ascii=False), encoding="utf-8")
    return rows


def fetch_photo(item):
    path, row = item
    if not path.exists():
        data = download(row["url"])
        path.write_bytes(data)
    if row.get("sha256") and hashlib.sha256(path.read_bytes()).hexdigest() != row["sha256"]:
        raise ValueError(f"Observation image changed: {row['observation']}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--manifest", type=Path, help="Replay a pinned observation manifest")
    args = parser.parse_args()
    directory = args.directory
    directory.mkdir(parents=True, exist_ok=True)
    (directory / "photos").mkdir(exist_ok=True)
    if args.manifest:
        with gzip.open(args.manifest, "rt", encoding="utf-8") as stream:
            pinned = [json.loads(line) for line in stream]
        for key in {r["label"] for r in pinned}:
            (directory / (key + ".json")).write_text(
                json.dumps([r for r in pinned if r["label"] == key]), encoding="utf-8")
    with concurrent.futures.ThreadPoolExecutor(max_workers=9) as pool:
        futures = []
        for key, name in {**POSITIVES, **OTHERS}.items():
            rows = collect(directory, key, name)
            print(key, dict(Counter(r["partition"] for r in rows)), flush=True)
            for row in rows:
                futures.append(pool.submit(fetch_photo, (directory / "photos" / f"{row['photo']}.jpg", row)))
        for future in concurrent.futures.as_completed(futures):
            future.result()
