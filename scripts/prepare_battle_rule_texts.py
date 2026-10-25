"""Extract battle skill and status wording from the verified active official language files."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parent))

from official_apk import ROOT
from official_source import OfficialSource
from refresh_current_localization import language
import xxhash


OUTPUT = ROOT / "unity/Assets/Official/Combat/battle-texts.json"
RECEIPT = ROOT / "assets/reference/battle-rules/text-source-manifest.json"


def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build() -> dict:
    texts, sources = language(ROOT / ".tools/source-packages/WwiseBnk")
    entity_ids = set()
    inputs = []
    for name in ("skill-rules", "battle-rules"):
        path = ROOT / f"assets/official/{name}.json"
        data = json.loads(path.read_bytes())
        entity_ids.update(int(identity) for identity in data["entities"])
        inputs.append({"path": path.relative_to(ROOT).as_posix(), "sha256": digest(path)})
    rules = ROOT / "assets/official/rules.json"
    item_ids = {int(row["ClassId"]) for row in json.loads(rules.read_bytes())["skill"]}
    inputs.append({"path": rules.relative_to(ROOT).as_posix(), "sha256": digest(rules)})
    keys = {f"entry_{identity}_{suffix}" for identity in entity_ids for suffix in ("name", "desc")}
    keys.update(f"item_{identity}_{suffix}" for identity in item_ids for suffix in ("name", "func_desc"))
    rows = []
    for key in sorted(keys):
        identity = xxhash.xxh64_intdigest(key.encode("utf-8"))
        if identity in texts:
            rows.append({"key": key, "text": texts[identity]})
    document = {"language_manifest": OfficialSource().version, "texts": rows}
    payload = (json.dumps(document, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    if OUTPUT.exists():
        prior = json.loads(RECEIPT.read_bytes())
        if digest(OUTPUT) != prior["output_sha256"]:
            raise ValueError("Battle wording was modified outside its source conversion")
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(payload)
    receipt = {"language_manifest": OfficialSource().version, "inputs": sources,
               "configuration_inputs": inputs, "output": OUTPUT.relative_to(ROOT).as_posix(),
               "output_sha256": digest(OUTPUT), "text_count": len(rows),
               "operation": "Official LanguageUtils entry_{id}_name/desc and item_{id}_name/func_desc keys; xxHash64 lookup in verified Chinese language rawfiles. Omit absent keys; do not synthesize descriptions.",
               "generator_sha256": digest(Path(__file__))}
    RECEIPT.parent.mkdir(parents=True, exist_ok=True)
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


if __name__ == "__main__":
    print("Official battle wording:", build()["text_count"])
