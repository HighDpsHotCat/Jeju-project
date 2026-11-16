"""Serve the Jeju photo identifier and the compiled Flutter web app."""

import argparse
import asyncio
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


def classify(files, identifier, capture_mode, *, batch):
    pictures, captures = [], []
    try:
        for file in files:
            with Image.open(file, formats=("JPEG", "PNG", "WEBP", "HEIF", "AVIF")) as image:
                if image.width * image.height > MAX_PIXELS:
                    return JSONResponse({"error": "image_too_large"}, status_code=413)
                captures.append(capture_metadata(image, include_gps=capture_mode == "1")
                                if capture_mode in ("1", "date") else None)
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
    if batch:
        return JSONResponse({"decision": decision, "candidates": candidates, "captures": captures})
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
        async with request.app.state.inference_lock:
            return await run_in_threadpool(
                classify, [photo.file for photo in photos], request.app.state.identifier, mode,
                batch=True,
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
