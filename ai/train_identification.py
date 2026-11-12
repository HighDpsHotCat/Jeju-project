"""Fit and evaluate the five-species transfer classifier. Photos stay outside Git."""
import argparse
import gzip
import hashlib
import json
from pathlib import Path

import numpy as np
import onnxruntime as ort
from PIL import Image, ImageOps, UnidentifiedImageError
from sklearn.linear_model import LogisticRegression
from sklearn.metrics import f1_score

from collect_photos import POSITIVES, OTHERS

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets/identification"
LABELS = list(POSITIVES) + list(OTHERS)
MODEL = ASSETS / "dinov2-small-3afdc8bc.onnx"
MEAN = np.array([.485, .456, .406], dtype=np.float32)
STD = np.array([.229, .224, .225], dtype=np.float32)


def feature(session, path):
    with Image.open(path) as src:
        rgb = ImageOps.exif_transpose(src).convert("RGB").resize((224, 224), Image.Resampling.BOX)
    pixels = (np.asarray(rgb, dtype=np.float32) / 255 - MEAN) / STD
    logits = session.run(None, {"pixel_values": pixels.transpose(2, 0, 1)[None]})[0][0,0].copy()
    logits -= logits.mean()
    return logits / max(np.linalg.norm(logits), 1e-8)


def rounded(values):
    return np.asarray(values, dtype=np.float64).round(7).tolist()


def main(directory):
    opts = ort.SessionOptions()
    opts.intra_op_num_threads = 2
    session = ort.InferenceSession(str(MODEL), opts, providers=["CPUExecutionProvider"])
    features, rows, seen = [], [], set()
    for key in LABELS:
        source = json.loads((directory / (key + ".json")).read_text(encoding="utf-8"))
        for row in source:
            path = directory / "photos" / f"{row['photo']}.jpg"
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            if digest in seen:
                continue
            try:
                vector = feature(session, path)
            except (UnidentifiedImageError, OSError):
                continue
            seen.add(digest)
            rows.append(dict(row, sha256=digest))
            features.append(vector)
        print("Embedded", key, flush=True)
    partitions = np.array([r["partition"] for r in rows])
    labels = np.array([LABELS.index(r["label"]) for r in rows])
    features = np.stack(features)
    train, val, test = [partitions == k for k in ("train", "val", "test")]
    head = LogisticRegression(C=55, max_iter=900, class_weight="balanced", random_state=41)
    head.fit(features[train], labels[train])
    assert list(head.classes_) == list(range(len(LABELS)))
    probs = head.predict_proba(features)
    n = len(POSITIVES)
    # The UI has one "other" result, including close relatives and animal-free photos.
    combined = np.column_stack([probs[:, :n], probs[:, n:].sum(axis=1)])
    best = combined.argmax(axis=1)
    margin = np.sort(combined, axis=1)[:, -1] - np.sort(combined, axis=1)[:, -2]
    species = json.loads((ROOT / "ai/demo_species_candidates.json").read_text(encoding="utf-8"))["species"]
    items, suggested = [], np.full(len(rows), n, dtype=int)
    for i, name in enumerate(POSITIVES.values()):
        entry = next(x for x in species if x["demo_round"] == "first_five" and
                     (x["scientific_name"] == name or name.startswith(x["scientific_name"] + " ")))
        centroid = features[train & (labels == i)].mean(axis=0)
        centroid /= np.linalg.norm(centroid)
        similarity = features @ centroid
        minimum = max(.10, np.quantile(similarity[val & (labels == i)], .05) - .025)
        eligible = (best == i) & (margin >= .12) & (similarity >= minimum)
        threshold = 1.01
        for candidate in np.arange(.50, .991, .01):
            selected = eligible & val & (probs[:, i] >= candidate)
            if selected.sum() >= 2 and (labels[selected] == i).mean() >= .95:
                threshold = round(float(candidate), 2)
                break
        suggested[eligible & (probs[:, i] >= threshold)] = i
        items.append(dict(id=entry["species_id"], name=entry["korean_name"], scientific=name,
                          group=entry["taxon_class"], minScore=threshold, minCos=float(minimum),
                          centroid=rounded(centroid)))
    blob = dict(species=items, weights=rounded(head.coef_), bias=rounded(head.intercept_),
                margin=.12)
    content = json.dumps(blob, ensure_ascii=False, separators=(",", ":"))
    version = "jeju-5-" + hashlib.sha256(content.encode()).hexdigest()[:10]
    (ASSETS / "classifier.json").write_text(json.dumps(dict(version=version, **blob), ensure_ascii=False,
                                               separators=(",", ":")), encoding="utf-8")
    groups = {}
    for i, key in enumerate(LABELS):
        mask = test & (labels == i)
        groups[key] = dict(total=int(mask.sum()), suggested=int((suggested[mask] != n).sum()))
        if i < n:
            groups[key].update(correct_suggestions=int((suggested[mask] == i).sum()),
                               top1_among_five=int((probs[mask, :n].argmax(axis=1) == i).sum()))
    truth = np.minimum(labels, n)
    accepted = test & (suggested != n)
    report = dict(version=version, split={k: int((partitions == k).sum()) for k in ("train","val","test")},
                  suggested_correct=int((suggested[accepted] == truth[accepted]).sum()),
                  suggested_total=int(accepted.sum()), supported_test=int((test & (labels < n)).sum()),
                  other_test=int((test & (labels >= n)).sum()),
                  other_false_suggestions=int((accepted & (labels >= n)).sum()),
                  macro_f1=float(f1_score(truth[test], suggested[test], average="macro")), groups=groups)
    (ROOT / "ai/identification_evaluation.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    with gzip.open(ROOT / "ai/identification_observations.jsonl.gz", "wt", encoding="utf-8") as output:
        for row in rows:
            output.write(json.dumps(row, ensure_ascii=False) + "\n")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    main(parser.parse_args().directory)
