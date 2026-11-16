import io
import json
import unittest
from unittest.mock import Mock

import numpy as np
from PIL import Image, ImageOps

from identify import HERE
from recapture import suspect_recapture
from server import classify


class RecaptureTest(unittest.TestCase):
    def setUp(self):
        with Image.open(HERE.parents[1] / "assets/guide_photos/120000002157.jpg") as image:
            self.sparrow = image.convert("RGB")

    def framed(self, color):
        print_or_screen = ImageOps.expand(self.sparrow, border=18, fill=color)
        return ImageOps.expand(print_or_screen, border=32, fill=(126, 112, 91))

    def test_detects_visible_paper_and_screen_borders(self):
        self.assertFalse(suspect_recapture(self.sparrow))
        for color in ("black", "white"):
            with self.subTest(color=color):
                self.assertTrue(suspect_recapture(self.framed(color)))

    def test_detects_colour_grid_when_edges_are_cropped(self):
        rgb = np.array(self.sparrow, dtype=np.float32)
        y, x = np.indices(rgb.shape[:2])
        wave = 12 * np.sin(2 * np.pi * (x / 7 + y / 16))
        rgb += np.stack([wave, -wave, wave], axis=2)
        self.assertTrue(suspect_recapture(Image.fromarray(rgb.clip(0, 255).astype(np.uint8))))

    def test_one_suspect_view_withholds_batch_before_species_inference(self):
        files = []
        for image in [self.sparrow, self.framed("white")]:
            file = io.BytesIO()
            image.save(file, "JPEG")
            files.append(file)
        identifier = Mock()
        response = classify(files, identifier, "date", batch=True)
        self.assertEqual(json.loads(response.body), {
            "decision": "recapture", "photo_index": 1, "candidates": [],
        })
        identifier.predict_views.assert_not_called()
