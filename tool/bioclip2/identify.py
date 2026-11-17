"""Rank Jeju field guide species for a photograph using local BioCLIP 2 weights."""

import argparse
import csv
import gzip
import hashlib
import json
import math
import os
import sys
import tempfile
from pathlib import Path

import open_clip
import torch
from PIL import Image, ImageOps
from pillow_heif import register_heif_opener

from recapture import suspect_recapture


HERE = Path(__file__).resolve().parent
CATALOG = HERE.parents[1] / "assets/catalog/animals.tsv.gz"
MODEL_DIR = HERE / ".model"
MODEL = f"local-dir:{MODEL_DIR}"
REVISION = "2957b322090f9cb17ae72c71981c7218a28d81e0"
PROMPT = "a photo of {}."


# Cosine similarities for this model revision and prompt, not probabilities.
MIN_SPECIES_SCORE = 0.70
MIN_BACKGROUND_MARGIN = 0.05
MIN_SPECIES_MARGIN = 0.04
BACKGROUND_LABELS = (
    "Homo sapiens", "a person", "a selfie of a human face", "a man", "a woman",
    "a group of people", "a child", "an empty landscape", "a city street",
    "an indoor room", "a building", "a household object", "a car", "food",
    "a flower", "a tree", "grass", "leaves", "rocks", "soil", "water",
    "the sky", "a computer screen", "a drawing", "an empty blurry background",
)


def registration_decision(best, runner_up, background):
    if not all(math.isfinite(value) for value in (best, runner_up, background)):
        return "uncertain"
    if best - background < MIN_BACKGROUND_MARGIN:
        return "no_animal"
    if best < MIN_SPECIES_SCORE or best - runner_up < MIN_SPECIES_MARGIN:
        return "uncertain"
    return "accepted"


def rank_observation(local_scores, other_scores, background_scores):
    # Each row describes a different view of the same animal.
    scores = local_scores.mean(dim=0)
    values, indices = scores.topk(3)
    backgrounds = background_scores.amax(dim=1)
    local_best = local_scores.amax(dim=1)
    if (local_best - backgrounds < MIN_BACKGROUND_MARGIN).any().item():
        return "no_animal", scores, indices
    winner = local_scores[:, indices[0]]
    strongest = torch.maximum(local_best, other_scores.amax(dim=1))
    if (strongest - winner >= MIN_SPECIES_MARGIN).any().item():
        return "uncertain", scores, indices
    runner_up = torch.maximum(values[1], other_scores.mean(dim=0).max())
    decision = registration_decision(
        values[0].item(), runner_up.item(), backgrounds.mean().item()
    )
    return decision, scores, indices


def catalog_species():
    species = {}
    all_names = set()
    with gzip.open(CATALOG, "rt", encoding="utf-8") as file:
        for jeju_id, national_id, korean, scientific, taxon in csv.reader(file, delimiter="\t"):
            all_names.add(scientific)
            if jeju_id:
                item = species.setdefault(scientific, {"name": korean or scientific, "jeju_ids": []})
                item["jeju_ids"].append(jeju_id)
            elif (korean == "삵" or (taxon == "Insecta" and
                  korean.endswith(("사슴벌레", "장수풍뎅이")))):
                species.setdefault(scientific, {"name": korean, "jeju_ids": [], "national_id": national_id})
    # A national subspecies is compatible with a species-level Jeju entry.
    other_names = sorted(name for name in all_names - species.keys()
                         if " ".join(name.split()[:2]) not in species)
    return species, other_names


def species_vectors(names, model, device):
    prompts = [PROMPT.format(name) for name in names]
    digest = hashlib.sha256("\n".join([REVISION, *prompts]).encode()).hexdigest()
    cache = MODEL_DIR / f"jeju-{digest}.pt"
    if cache.exists():
        return torch.load(cache, map_location="cpu", weights_only=True).to(device)

    tokenizer = open_clip.get_tokenizer(MODEL)
    batches = []
    for start in range(0, len(names), 32):
        tokens = tokenizer(prompts[start:start + 32]).to(device)
        batches.append(model.encode_text(tokens, normalize=True).float().cpu())
        if start % 512 == 0:
            print(f"판별 문구 준비 중 · {start + len(tokens)}/{len(names)}", file=sys.stderr, flush=True)
    vectors = torch.cat(batches)
    with tempfile.NamedTemporaryFile(dir=MODEL_DIR, suffix=".tmp", delete=False) as file:
        temp = Path(file.name)
    try:
        torch.save(vectors, temp)
        os.replace(temp, cache)
    finally:
        temp.unlink(missing_ok=True)
    return vectors.to(device)


class Identifier:
    @torch.inference_mode()
    def __init__(self):
        if not (MODEL_DIR / "open_clip_model.safetensors").is_file():
            raise FileNotFoundError("공식 BioCLIP 2 모델을 먼저 설치하세요. README.md를 참고하세요.")
        device = "mps" if torch.backends.mps.is_available() else "cpu"
        model, _, preprocess = open_clip.create_model_and_transforms(MODEL, device=device)
        model.eval()
        species, other_names = catalog_species()
        names = list(species)
        self.vectors = species_vectors(names, model, device)
        self.other_vectors = species_vectors(other_names, model, device)
        self.background_vectors = species_vectors(BACKGROUND_LABELS, model, device)
        self.names = names
        self.species = species
        self.model = model
        self.preprocess = preprocess
        self.device = device

    def predict(self, photo):
        if suspect_recapture(photo):
            return "recapture", []
        return self.predict_views([self.preprocess(photo)])

    @torch.inference_mode()
    def predict_views(self, pictures):
        batch = torch.stack(pictures).to(self.device)
        features = self.model.encode_image(batch, normalize=True).float()
        decision, scores, indices = rank_observation(
            features @ self.vectors.T,
            features @ self.other_vectors.T,
            features @ self.background_vectors.T,
        )
        if decision != "accepted":
            return decision, []
        candidates = []
        for index in indices.tolist():
            name = self.names[index]
            candidates.append({**self.species[name], "scientific": name, "similarity": round(scores[index].item(), 4)})
        return decision, candidates


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("photo", type=Path)
    args = parser.parse_args()
    register_heif_opener()
    try:
        with Image.open(args.photo) as image:
            photo = ImageOps.exif_transpose(image).convert("RGB")
    except (OSError, ValueError) as error:
        parser.exit(1, f"사진을 열 수 없습니다. {error}\n")
    _, candidates = Identifier().predict(photo)
    print(json.dumps(candidates, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
