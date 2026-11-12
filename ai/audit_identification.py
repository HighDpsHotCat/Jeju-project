"""Freeze a second, photographer-disjoint audit set, then score the exported model."""
import argparse
import concurrent.futures
import gzip
import json
from collections import Counter
from pathlib import Path

import numpy as np
import onnxruntime as ort

from collect_photos import POSITIVES, OTHERS, api, fetch_photo
from train_identification import ASSETS, MODEL, ROOT, feature

MANIFEST = ROOT / "ai/identification_audit.jsonl.gz"


def read_rows(path):
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        return [json.loads(line) for line in stream]


def collect(complete=False):
    used = read_rows(ROOT / "ai/identification_observations.jsonl.gz")
    selected = read_rows(MANIFEST) if complete else []
    authors = {r["author"] for r in used if not complete or r["partition"] != "test"}
    authors.update(r["author"] for r in selected)
    seen = {r["photo"] for r in used}
    seen.update(r["photo"] for r in selected)
    observations = {r["observation"] for r in used + selected}
    for label, name in {**POSITIVES, **OTHERS}.items():
        quota = (8 if label in POSITIVES else 3) - sum(r["label"] == label for r in selected)
        if not quota:
            continue
        taxon = next(t for t in api("taxa/autocomplete", q=name) if t["is_active"] and t["name"] == name)
        group = []
        for page in range(1,7 if complete else 4):
            results = api("observations", taxon_id=taxon["id"], photos="true", quality_grade="research",
                          captive="false", photo_license="cc0,cc-by,cc-by-sa", order_by="id" if complete else "votes",
                          order="asc" if complete else "desc",
                          per_page=100, page=page)
            for obs in results:
                author = obs["user"]["id"]
                if author in authors or obs["id"] in observations:
                    continue
                p = next((p for p in obs["photos"] if p.get("license_code") in ("cc0","cc-by","cc-by-sa")
                          and "/square." in p["url"] and p["id"] not in seen), None)
                if not p:
                    continue
                authors.add(author)
                seen.add(p["id"])
                observations.add(obs["id"])
                group.append(dict(label=label, observation=obs["id"], photo=p["id"], author=author,
                                  attribution=p["attribution"], license=p["license_code"],
                                  url=p["url"].replace("/square.","/medium.")))
                if len(group) == quota:
                    break
            if len(group) == quota or len(results) < 100:
                break
        selected.extend(group)
        print(label, len(group), flush=True)
    with gzip.open(MANIFEST, "wt", encoding="utf-8") as stream:
        for row in selected:
            stream.write(json.dumps(row, ensure_ascii=False) + "\n")


def evaluate(directory):
    opts = ort.SessionOptions()
    opts.intra_op_num_threads = 2
    session = ort.InferenceSession(str(MODEL), opts, providers=["CPUExecutionProvider"])
    head = json.loads((ASSETS / "classifier.json").read_text(encoding="utf-8"))
    weights, bias = np.array(head["weights"]), np.array(head["bias"])
    names = list(POSITIVES)
    directory.mkdir(exist_ok=True, parents=True)
    rows = read_rows(MANIFEST)
    with concurrent.futures.ThreadPoolExecutor(max_workers=8) as pool:
        list(pool.map(fetch_photo, [(directory / f"{r['photo']}.jpg", r) for r in rows]))
    results = []
    for row in rows:
        v = feature(session, directory / f"{row['photo']}.jpg")
        raw = weights @ v + bias
        p = np.exp(raw - raw.max())
        p /= p.sum()
        probs = np.append(p[:len(names)], p[len(names):].sum())
        idx = int(np.argmax(p[:len(names)]))
        s = head["species"][idx]
        similarity = float(v @ s["centroid"])
        other = probs[-1]
        delta = probs[idx] - np.delete(probs, idx).max()
        if probs[idx] >= s["minScore"] and delta >= head["margin"] and similarity >= s["minCos"]:
            status = "suggested"
        elif probs[idx] < .28 or other >= probs[idx] or similarity < s["minCos"] - .06:
            status = "unknown"
        else:
            status = "review"
        results.append(dict(photo=row["photo"], truth=row["label"], top=names[idx], decision=status))
    metrics = dict(version=head["version"], groups={})
    for label in names + ["other"]:
        group = [r for r in results if (r["truth"] if r["truth"] in names else "other") == label]
        metrics["groups"][label] = dict(total=len(group), **Counter(r["decision"] for r in group),
                    correct_suggestions=sum(r["decision"] == "suggested" and r["top"] == label for r in group))
    metrics["results"] = results
    (ROOT / "ai/identification_audit_results.json").write_text(json.dumps(metrics, indent=2), encoding="utf-8")
    print(metrics["groups"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("mode", choices=["collect","complete","evaluate"])
    parser.add_argument("directory", type=Path, nargs="?", default=Path("audit_photos"))
    args = parser.parse_args()
    if args.mode in ("collect","complete"):
        if MANIFEST.exists() and args.mode == "collect":
            raise SystemExit("Audit manifest already exists. Use evaluate to replay it.")
        collect(complete=args.mode == "complete")
    else:
        evaluate(args.directory)
