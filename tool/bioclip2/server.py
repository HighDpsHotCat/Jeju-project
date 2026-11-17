"""Serve the Jeju photo identifier and the compiled Flutter web app."""

import argparse
import asyncio
import hashlib
import hmac
import json
import os
import re
import time
import io
import struct
from contextlib import asynccontextmanager

import uvicorn
from PIL import ExifTags, Image, ImageOps
from pillow_heif import register_heif_opener
from starlette.applications import Starlette
from starlette.concurrency import run_in_threadpool
from starlette.formparsers import MultiPartException, MultiPartParser
from starlette.responses import JSONResponse
from starlette.routing import Mount, Route
from starlette.staticfiles import StaticFiles

from identify import HERE, Identifier
from recapture import suspect_recapture

MAX_BYTES = 12 * 1024 * 1024
MAX_PHOTOS = 5
MAX_FORM_BYTES = MAX_PHOTOS * MAX_BYTES + 64 * 1024
MAX_PIXELS = 32_000_000
WEB = HERE.parents[1] / "build/web"


@asynccontextmanager
async def lifespan(app):
    register_heif_opener()
    app.state.identifier = Identifier()
    app.state.inference_lock = asyncio.Lock()
    yield


def gps_degrees(values, ref, positive, negative, limit):
    if ref not in (positive, negative) or not isinstance(values, tuple) or len(values) != 3:
        return None
    degrees, minutes, seconds = map(float, values)
    if not (0 <= degrees <= limit and 0 <= minutes < 60 and 0 <= seconds < 60):
        return None
    value = degrees + minutes / 60 + seconds / 3600
    if value > limit:
        return None
    return value if ref == positive else -value


def capture_metadata(image, *, include_gps):
    # DateTimeOriginal refers to the exposure, even when the file was edited later.
    try:
        exif = image.getexif()
        original = exif.get_ifd(ExifTags.IFD.Exif).get(ExifTags.Base.DateTimeOriginal)
    except (TypeError, ValueError, OSError, SyntaxError, struct.error):
        original = None
    day = original[:10].replace(":", "-") if isinstance(original, str) else None
    if not include_gps:
        return {"date": day}
    try:
        exif = image.getexif()
        gps = exif.get_ifd(ExifTags.IFD.GPSInfo)
        if gps.get(9) == "V":  # EXIF marks this as an invalid GPS measurement.
            return None
        lat = gps_degrees(gps[2], gps[1], "N", "S", 90)
        lon = gps_degrees(gps[4], gps[3], "E", "W", 180)
        if lat is None or lon is None:
            return None
    except (KeyError, IndexError, TypeError, ValueError, ArithmeticError, OSError, SyntaxError, struct.error):
        return None
    return {"latitude": lat, "longitude": lon, "date": day}


def spot_receipt(owner, species, points):
    if not owner or not re.fullmatch(r"[0-9a-f]{64}", owner) or not all(points):
        return None
    key = os.environ.get("ADOPTION_SIGNING_KEY")
    if not key:
        try:
            key = (HERE / ".adoption-key").read_text().strip()
        except OSError:
            return None
    payload = json.dumps({"owner": owner, "species": species,
                          "issued": time.time_ns() // 1_000_000, "points": points},
                         separators=(",", ":"), allow_nan=False)
    return {"payload": payload, "signature": hmac.new(key.encode(), payload.encode(), hashlib.sha256).hexdigest()}


def classify(files, identifier, capture_mode, *, batch, owner=None, fixes=()):
    pictures, captures, points = [], [], []
    try:
        for file in files:
            with Image.open(file, formats=("JPEG", "PNG", "WEBP", "HEIF", "AVIF")) as image:
                if image.width * image.height > MAX_PIXELS:
                    return JSONResponse({"error": "image_too_large"}, status_code=413)
                gps = capture_metadata(image, include_gps=True) if owner or capture_mode == "1" else None
                captures.append(gps if capture_mode == "1" else
                                capture_metadata(image, include_gps=False) if capture_mode == "date" else None)
                point = {"lat": gps["latitude"], "lon": gps["longitude"]} if gps else None
                if not point and len(points) < len(fixes):
                    fix = fixes[len(points)]
                    if isinstance(fix, dict) and all(type(fix.get(k)) in (float, int)
                                                     and -180 <= fix[k] <= 180 for k in ("lat", "lon")):
                        point = {"lat": fix["lat"], "lon": fix["lon"]}
                points.append(point)
                photo = ImageOps.exif_transpose(image).convert("RGB")
                if suspect_recapture(photo):
                    if batch or capture_mode in ("1", "date"):
                        return JSONResponse({
                            "decision": "recapture", "candidates": [], "photo_index": len(pictures),
                        })
                    return JSONResponse([])
                pictures.append(identifier.preprocess(photo))
    except (ValueError, OSError, Image.DecompressionBombError):
        return JSONResponse({"error": "invalid_photo"}, status_code=422)
    decision, candidates = identifier.predict_views(pictures)
    if decision == "accepted" and not candidates[0]["jeju_ids"]:
        # National-only entries (e.g. leopard cat) stay national guide cards.
        # Offer them only for the location-aware Jeju observation flow.
        if not owner or not all(p and ((33.08 <= p["lat"] <= 33.59 and 126.13 <= p["lon"] <= 127.02)
                                  or (33.88 <= p["lat"] <= 34 and 126.24 <= p["lon"] <= 126.38))
                                for p in points):
            decision, candidates = "uncertain", []
    if batch:
        result = {"decision": decision, "candidates": candidates, "captures": captures}
        if decision == "accepted" and candidates and owner:
            winner = candidates[0]
            species = winner["jeju_ids"][0] if winner["jeju_ids"] else winner["national_id"]
            result["species_id"] = species
            result["spot_receipt"] = spot_receipt(owner, species, points)
        return JSONResponse(result)
    if capture_mode in ("1", "date"):
        return JSONResponse({"decision": decision, "candidates": candidates, "capture": captures[0]})
    return JSONResponse(candidates)


async def limited_form_stream(request):
    size = 0
    async for chunk in request.stream():
        size += len(chunk)
        if size > MAX_FORM_BYTES:
            raise OverflowError("Observation upload is too large")
        yield chunk


async def identify_batch(request, mode):
    try:
        form = await MultiPartParser(
            request.headers, limited_form_stream(request), max_files=MAX_PHOTOS, max_fields=0
        ).parse()
    except OverflowError:
        return JSONResponse({"error": "batch_too_large"}, status_code=413)
    except MultiPartException:
        return JSONResponse({"error": "invalid_photo_batch"}, status_code=422)
    try:
        photos = form.getlist("photos")
        if not 1 <= len(photos) <= MAX_PHOTOS or len(photos) != len(form.multi_items()):
            return JSONResponse({"error": "invalid_photo_batch"}, status_code=422)
        if any(photo.size > MAX_BYTES for photo in photos):
            return JSONResponse({"error": "image_too_large"}, status_code=413)
        try:
            fixes = json.loads(request.headers.get("x-field-fixes", "[]"))
            if not isinstance(fixes, list) or len(fixes) > MAX_PHOTOS:
                fixes = []
        except ValueError:
            fixes = []
        async with request.app.state.inference_lock:
            return await run_in_threadpool(
                classify, [photo.file for photo in photos], request.app.state.identifier, mode,
                batch=True, owner=request.headers.get("x-field-profile"), fixes=fixes,
            )
    finally:
        await form.close()


async def identify(request):
    mode = request.query_params.get("capture")
    media_type = request.headers.get("content-type", "").partition(";")[0].strip().lower()
    if media_type == "multipart/form-data":
        return await identify_batch(request, mode)
    if media_type != "application/octet-stream":
        return JSONResponse({"error": "unsupported_media"}, status_code=415)
    data = bytearray()
    async for chunk in request.stream():
        if len(data) + len(chunk) > MAX_BYTES:
            return JSONResponse({"error": "image_too_large"}, status_code=413)
        data.extend(chunk)
    async with request.app.state.inference_lock:
        return await run_in_threadpool(
            classify, [io.BytesIO(data)], request.app.state.identifier, mode, batch=False
        )


routes = [Route("/api/identify", identify, methods=["POST"])]
if (WEB / "index.html").is_file():
    routes.append(Mount("/", app=StaticFiles(directory=WEB, html=True)))
app = Starlette(routes=routes, lifespan=lifespan)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=8765)
    args = parser.parse_args()
    uvicorn.run(app, host=args.host, port=args.port, limit_concurrency=32)
