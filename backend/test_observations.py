import json
import sqlite3
import tempfile
import threading
import unittest
from concurrent.futures import ThreadPoolExecutor
from contextlib import closing
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen

from server import Handler, ThreadingHTTPServer


class ObservationAPI(unittest.TestCase):
    def setUp(self):
        tmp = tempfile.TemporaryDirectory()
        self.addCleanup(tmp.cleanup)
        db_path = Path(tmp.name) / "device.db"
        with closing(sqlite3.connect(db_path)) as db:
            db.executescript(Path(__file__).with_name("device_schema.sql").read_text(encoding="utf8"))
        self.http = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
        self.http.demo_catalog = db_path
        threading.Thread(target=self.http.serve_forever, daemon=True).start()
        self.addCleanup(self.http.server_close)
        self.addCleanup(self.http.shutdown)
        self.root = "/guide/profiles/alice"
        self.path = self.root + "/observations/o1"
        self.photo = dict(photo_id="p1", photo_ref="photos/1.jpg", captured_at="2026-11-12T09:00:00+09:00",
                          gps_at="2026-11-12T00:00:00Z", latitude=33.4, longitude=126.6, accuracy_m=5,
                          boundary_version="fixture", capture_source="camera", location_verdict="inside",
                          mode="general", identification="unknown", species_id=None)
        self.assertEqual(self.call("PUT", self.root, {})[0], 201)

    def call(self, method, path, data=None):
        req = Request(f"http://127.0.0.1:{self.http.server_port}" + path, method=method,
                      data=None if data is None else json.dumps(data).encode(),
                      headers={"Content-Type": "application/json"})
        try:
            response = urlopen(req, timeout=5)
        except HTTPError as error:
            response = error
        with response:
            return response.status, json.load(response)

    def test_lifecycle_and_progress(self):
        self.assertEqual(self.call("PUT", self.root, {})[0], 200)
        with ThreadPoolExecutor(max_workers=2) as pool:
            codes = list(pool.map(lambda _: self.call("PUT", self.path, self.photo)[0], range(2)))
        self.assertEqual(sorted(codes), [200, 201])
        def counts():
            _, home = self.call("GET", "/guide/home?user_id=alice")
            return home["species_count"], home["observation_count"]
        self.assertEqual(counts(), (0, 1))
        identity = dict(identification="confirmed", species_id="JJ-000837")
        self.assertEqual(self.call("PATCH", self.path, identity)[0], 200)
        self.assertEqual(self.call("PUT", self.path, self.photo)[0], 409)
        second = {**self.photo, **identity, "photo_id": "p2", "photo_ref": "photos/2.jpg"}
        self.assertEqual(self.call("PUT", self.path+"b", second)[0], 201)
        self.assertEqual(counts(), (1, 2))
        self.assertEqual(self.call("PATCH", self.path, {**identity, "species_id":"JJ-004873"})[0], 200)
        self.assertEqual(counts(), (2, 2))
        self.assertEqual(len(self.call("GET", self.root+"/observations?species_id=JJ-004873")[1]), 1)
        self.assertEqual(self.call("DELETE", self.path)[0], 200)
        self.assertEqual(counts(), (1, 1))

    def test_validation_and_separation(self):
        for change in ({"location_verdict":"outside"}, {"location_verdict":"uncertain"},
                       {"capture_source":"gallery"}, {"latitude":91}, {"accuracy_m":-1},
                       {"captured_at":"2026-11-12"}, {"species_id":"JJ-000837"}):
            self.assertEqual(self.call("PUT", self.path, {**self.photo, **change})[0], 400)
        self.assertEqual(self.call("PUT", self.path, {**self.photo, "mode":"research_only"})[0], 201)
        self.assertEqual(self.call("GET", self.root+"/observations")[1], [])
        self.assertEqual(self.call("GET", "/guide/home?user_id=alice")[1]["observation_count"], 0)
        self.call("PUT", "/guide/profiles/bob", {})
        other = self.path.replace("alice","bob")
        self.assertEqual(self.call("GET", other)[0], 404)
        self.assertEqual(self.call("DELETE", other)[0], 404)
        self.assertEqual(self.call("GET", self.path)[1]["photo_ref"], "photos/1.jpg")
        self.http.demo_catalog = None
        self.assertEqual(self.call("DELETE", self.path)[0], 404)
