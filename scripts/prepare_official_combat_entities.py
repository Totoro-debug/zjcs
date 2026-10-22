"""Enrich the pinned combat EC payload with the components the first export skipped.

The committed ``assets/official/skill-rules.json`` only carries six component types
(FightSkill / FightStatus / four FightHit*). The APK stores many more components on the same
entities (status prop components, StatusEnd, extra skill components, ...). This script keeps every
existing entity and value untouched and only *adds* the missing components, so the runtime can opt
into them without changing anything that is already verified.

Outputs (local, gitignored like the rest of ``assets/official``):

* ``assets/official/combat-entities-full.json`` - existing entities plus extra components.
* ``assets/reference/combat/combat-entities-full-manifest.json`` - counts, failures and hashes.
"""
from __future__ import annotations

import argparse
import collections
import hashlib
import json
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent
ROOT = SCRIPTS.parent
sys.path.insert(0, str(SCRIPTS))

from official_apk import Apk  # noqa: E402
from official_ec_codec import Formats  # noqa: E402
from official_map_codec import unpack  # noqa: E402

SOURCE = ROOT / "assets/official/skill-rules.json"
OUTPUT = ROOT / "assets/official/combat-entities-full.json"
MANIFEST = ROOT / "assets/reference/combat/combat-entities-full-manifest.json"
VERSION = "854_156383"


def sha256_bytes(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def iter_rows(apk):
    for path, ptr in apk.container("config_ec"):
        name = path.rsplit("/", 1)[-1]
        try:
            raw = ptr.deref().read().m_Script.encode("utf8", "surrogateescape")
            rows = unpack(raw)[0]
        except Exception:
            try:
                rows = unpack(raw[2:])[0]
            except Exception:
                continue
        if not isinstance(rows, list):
            continue
        yield name, rows


def build() -> tuple[dict, dict]:
    payload = json.loads(SOURCE.read_text(encoding="utf-8"))
    entities = payload["entities"]
    formats = Formats()
    added = collections.Counter()
    failures = []
    seen_entities = set()
    apk = Apk()
    for name, rows in iter_rows(apk):
        for row in rows:
            if not isinstance(row, (list, tuple)) or len(row) != 3:
                continue
            parent, identity, components = row
            key = str(identity)
            if key not in entities or not isinstance(components, list):
                continue
            seen_entities.add(key)
            target = entities[key]
            for component in components:
                if not isinstance(component, (list, tuple)) or len(component) != 2:
                    continue
                typ, data = component
                if not isinstance(typ, str):
                    continue
                short = typ.removeprefix("Common.")
                if short in target:
                    continue
                try:
                    target[short] = formats.component(typ, data)
                    added[short] += 1
                except Exception as error:
                    failures.append({"file": name, "identity": identity, "component": short,
                                     "error": str(error)[:200]})
    result = {
        "manifest": VERSION,
        "source": "skill-rules.json entities enriched with components decoded from the fixed APK config_ec containers",
        "entities": entities,
    }
    manifest = {
        "format": 1,
        "manifest": VERSION,
        "source": SOURCE.relative_to(ROOT).as_posix(),
        "sourceSha256": sha256_bytes(SOURCE.read_bytes()),
        "entities": len(entities),
        "entitiesMatched": len(seen_entities),
        "addedComponents": dict(sorted(added.items())),
        "failureCount": len(failures),
        "failures": failures[:200],
        "output": OUTPUT.relative_to(ROOT).as_posix(),
    }
    manifest["outputSha256"] = sha256_bytes(
        (json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8"))
    return result, manifest


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--verify", action="store_true", help="rebuild and compare with the local output")
    args = parser.parse_args()
    result, manifest = build()
    payload_bytes = (json.dumps(result, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    manifest_bytes = (json.dumps(manifest, ensure_ascii=False, indent=2, sort_keys=True) + "\n").encode("utf-8")
    if args.verify:
        if not OUTPUT.is_file() or OUTPUT.read_bytes() != payload_bytes:
            raise SystemExit("combat-entities-full.json differs from the rebuilt payload")
        if not MANIFEST.is_file() or MANIFEST.read_bytes() != manifest_bytes:
            raise SystemExit("manifest differs from the rebuilt manifest")
        print("combat entity payload is up to date:", manifest["entities"], "entities",
              manifest["addedComponents"])
        return
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(payload_bytes)
    MANIFEST.parent.mkdir(parents=True, exist_ok=True)
    MANIFEST.write_bytes(manifest_bytes)
    print("entities:", manifest["entities"], "matched:", manifest["entitiesMatched"])
    print("added components:", json.dumps(manifest["addedComponents"], ensure_ascii=False))
    print("decode failures:", manifest["failureCount"])
    print("output sha256:", manifest["outputSha256"])


if __name__ == "__main__":
    main()
