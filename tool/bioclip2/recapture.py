"""Visible screen/print cues checked before an observation can be registered."""

import cv2
import numpy as np
from PIL import Image, ImageOps


def has_display_frame(photo):
    # Inspect the whole scene. A center crop used for species identification
    # can remove the phone bezel or the margins of a printed photograph.
    view = ImageOps.contain(photo, (720, 720)) if max(photo.size) > 720 else photo
    rgb = np.asarray(view)
    gray = cv2.cvtColor(rgb, cv2.COLOR_RGB2GRAY)
    edges = cv2.Canny(cv2.GaussianBlur(gray, (5, 5), 1), 24, 72)
    closed = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, np.ones((5, 5), np.uint8))
    contours, _ = cv2.findContours(closed, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    height, width = gray.shape
    target = np.float32([(6, 6), (165, 6), (165, 165), (6, 165)])
    for contour in contours:
        if not 0.16 < cv2.contourArea(contour) / (width * height) < 0.96:
            continue
        quad = cv2.approxPolyDP(contour, 0.025 * cv2.arcLength(contour, True), True)
        if len(quad) != 4 or not cv2.isContourConvex(quad):
            continue
        corners = quad.reshape(4, 2).astype(np.float32)
        if cv2.pointPolygonTest(quad, (width / 2, height / 2), False) < 0:
            continue
        # Start at any corner; all four margins are evaluated symmetrically.
        matrix = cv2.getPerspectiveTransform(corners, target)
        face = cv2.warpPerspective(rgb, matrix, (172, 172), borderValue=(120, 120, 120))
        for dark in (True, False):
            tones = face.max(axis=2) < 68 if dark else face.min(axis=2) > 175
            if tones[34:-34, 34:-34].mean() > 0.55:
                continue
            # The detected edge can be the outside of the device/page or
            # the inside boundary between its margin and the displayed image.
            for start in (2, 7):
                end = start + 3
                margins = (tones[start:end, 21:-21], tones[-end:-start, 21:-21],
                           tones[21:-21, start:end], tones[21:-21, -end:-start])
                if all(strip.mean() > 0.80 for strip in margins):
                    return True
    return False


# Look for the same fine screen or printing pattern in separated parts of
# the original image. A single patch of fur or feathers is weak evidence.
PATCH = 96
WINDOW = np.outer(np.hanning(PATCH), np.hanning(PATCH)).astype(np.float32)
Y, X = np.meshgrid(np.fft.fftfreq(PATCH) * PATCH, np.arange(PATCH // 2 + 1), indexing="ij")
BAND = (np.hypot(X, Y) >= 6) & (np.hypot(X, Y) <= 42)


def has_repeated_grid(photo):
    width, height = photo.size
    if min(width, height) < 2 * PATCH:
        return False
    count = 5 if min(width, height) >= 4 * PATCH else 3
    peaks = {"luma": [], "chroma": []}
    for top in np.linspace(0, height - PATCH, count, dtype=int):
        for left in np.linspace(0, width - PATCH, count, dtype=int):
            tile = np.asarray(photo.crop((left, top, left + PATCH, top + PATCH)), dtype=np.float32)
            red, green, blue = tile.transpose(2, 0, 1)
            channels = np.stack([(red + 2 * green + blue) / 4, red - green, blue - green])
            channels -= channels.mean(axis=(1, 2), keepdims=True)
            spectra = np.abs(np.fft.rfft2(channels * WINDOW, axes=(1, 2))) ** 2
            for kind, spectrum, amplitude, share in zip(
                ("luma", "chroma", "chroma"), spectra, (3.0, 1.5, 1.5), (.18, .08, .08)
            ):
                # Check local maxima before masking low frequencies. Ordinary
                # edges decay from DC into the pass band without a sharp line.
                wrapped = np.pad(spectrum, ((2, 2), (0, 0)), mode="wrap")
                local_max = cv2.dilate(wrapped, np.ones((5, 5), np.uint8))[2:-2]
                power = np.where(BAND & (spectrum >= local_max), spectrum, 0)
                row, col = np.unravel_index(power.argmax(), power.shape)
                peak = power[row, col]
                if peak > (WINDOW.sum() * amplitude) ** 2 and peak > spectrum[BAND].sum() * share:
                    peaks[kind].append((top, left, Y[row, col], X[row, col]))
    for kind, signals in peaks.items():
        # Natural structures also repeat in brightness. Require stronger
        # spatial agreement for brightness alone than for colour fringes.
        minimum = 7 if count == 5 else 4
        if kind == "luma":
            minimum += 2
        for _, _, y, x in signals:
            matches = {(top, left) for top, left, py, px in signals
                       if abs(py - y) <= 2 and abs(px - x) <= 2}
            if len(matches) >= minimum:
                return True
    return False


def suspect_recapture(photo):
    if has_display_frame(photo) or has_repeated_grid(photo):
        return True
    # Broad moiré bands on a high-resolution capture span many native pixels.
    if max(photo.size) > 1000:
        return has_repeated_grid(ImageOps.contain(photo, (640, 640)))
    return False
