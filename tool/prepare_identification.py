"""Fetch pinned inference assets before flutter build/run. Uses Python's standard library."""
import argparse
import base64
import hashlib
import tempfile
from pathlib import Path
from urllib.request import urlopen

ROOT = Path(__file__).resolve().parents[1]
MODEL = ROOT / "assets/identification/dinov2-small-3afdc8bc.onnx"
MODEL_URL = ("https://huggingface.co/Xenova/dinov2-small/resolve/"
             "c2bb04a51fab207c420665f1946016107bffc701/onnx/model_quantized.onnx")
MODEL_HASH = "3afdc8bc63b50558d6e5770f5b799bb82455c2311183a2de43803f343a29d917"
WEB_FILES = {
    "ort.wasm.min.js": "ZeCTdt9pEH6IG1w00tN67TM6NmttlBBz7FGBaOJpuH0=",
    "ort-wasm-simd-threaded.mjs": "MN2FHZwAYilAUA9x3dL/iCDFy2UnCBYIAXW5WHBThag=",
    "ort-wasm-simd-threaded.wasm": "ca7wSVnFwbbeRhtlOOIFjjBmEANKharSdC0Mf9RTP+Q=",
}


def fetch(path, url, digest):
    if path.exists() and hashlib.sha256(path.read_bytes()).hexdigest() == digest:
        return
    path.parent.mkdir(parents=True, exist_ok=True)
    with urlopen(url, timeout=90) as response:
        data = response.read()
    if hashlib.sha256(data).hexdigest() != digest:
        raise ValueError(f"Checksum mismatch: {path.name}")
    with tempfile.NamedTemporaryFile(dir=path.parent, delete=False) as tmp:
        tmp.write(data)
    Path(tmp.name).replace(path)
    print(f"Prepared {path.name}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--web", action="store_true", help="Also bundle the offline browser runtime")
    args = parser.parse_args()
    fetch(MODEL, MODEL_URL, MODEL_HASH)
    if args.web:
        for name, digest in WEB_FILES.items():
            fetch(ROOT / "web/ort" / name, f"https://unpkg.com/onnxruntime-web@1.22.0/dist/{name}",
                  base64.b64decode(digest).hex())
