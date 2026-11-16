"""Build the offline district asset from the GeoJSON revision in its credit file.

Usage: python3 tool/build_jeju_districts.py /path/to/HangJeongDong_ver20261001.geojson
"""
import gzip
import hashlib
import json
from pathlib import Path
import sys

SOURCE_SHA256 = "843ece3a0b3429742cd4a1b699cc13987a88b5e5401f382e7fdaaa194ea5e605"
TARGET = Path(__file__).resolve().parents[1] / "assets/catalog/jeju_districts.json.gz"


def main(source):
    data = source.read_bytes()
    if hashlib.sha256(data).hexdigest() != SOURCE_SHA256:
        raise ValueError("Source does not match the credited 2026-10-01 revision")
    districts = []
    for feature in json.loads(data)["features"]:
        props = feature["properties"]
        if props["sido"] != "50":
            continue
        geom = feature["geometry"]
        if geom["type"] != "MultiPolygon":
            raise ValueError(geom["type"])
        polygons = []
        for polygon in geom["coordinates"]:
            rings = []
            for ring in polygon:
                # Flat, absolute integer microdegrees. Retain all vertices, rings and islands.
                rings.append([round(value * 1_000_000) for point in ring for value in point])
            polygons.append(rings)
        _, city, area = props["adm_nm"].split()
        districts.append({"name": city + " " + area, "polygons": polygons})
    if len({d["name"] for d in districts}) != 43:
        raise ValueError("Expected all 43 distinct Jeju administrative districts")
    payload = json.dumps(districts, ensure_ascii=False, separators=(",", ":")).encode()
    with TARGET.open("wb") as target, gzip.GzipFile(fileobj=target, filename="", mode="wb", mtime=0) as compressed:
        compressed.write(payload)


if __name__ == "__main__":
    main(Path(sys.argv[1]))
