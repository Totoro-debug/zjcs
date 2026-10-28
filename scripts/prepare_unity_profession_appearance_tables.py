"""Decode the pinned current official appearance tables without running client code."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from prepare_unity_profession_native_inputs import CACHE
from prepare_unity_profession_photographer import record


TABLES = ("profession_base", "spine_skin", "character_skin", "character_skin_id_group",
          "character_skin_dye", "character_skin_color", "append_appearance")


def build():
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Appearance tables require the selected 871_156598 source")
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    constructors = Constructors(code)
    mapping = {m[2]: m[1] for m in re.finditer(
        r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))}
    tables, inputs = {}, []
    for name in TABLES:
        source = apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = source.read().m_Script.encode("utf-8", "surrogateescape")
        tables[name] = constructors.table(raw, mapping[name])
        inputs.append(dict(name=name, table_type=mapping[name], source=apk.provenance(source),
            payload_sha256=hashlib.sha256(source.get_raw_data()).hexdigest(),
            sha256=hashlib.sha256(raw).hexdigest(), consumed_bytes=len(raw), rows=len(tables[name])))
        print(name, len(tables[name]), "rows;", len(raw), "fully consumed bytes", flush=True)
    dest = CACHE / "appearance-tables.json"
    dest.write_text(json.dumps(tables, ensure_ascii=False, separators=(",", ":")) + "\n",
                    encoding="utf-8", newline="\n")
    sources = []
    for name in sorted(constructors.sources):
        path = code / "common-source/Common" / (name + ".cs")
        if not path.exists() or name in ("Pos2d", "Vector2", "Vector3"):
            path = code / "common-source/Common.LubanGen" / (name + ".cs")
        if constructors.sources[name]:
            sources.append(record(path))
    result = dict(format=1, manifest=apk.version, tables=inputs, constructor_sources=sources,
        interpreter=record(ROOT / "scripts/official_tables.py"), output=record(dest),
        generator=record(Path(__file__)),
        scope="All seven original tables fully decoded in original row and collection order. "
              "No character selection or skin-resolution rule is implemented by this extraction.",
        limitations=["Conversion cache only; not yet a packaged runtime configuration or UI validation."])
    proof = ROOT / "assets/reference/profession-ui/appearance-tables-source-manifest.json"
    proof.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    return result


if __name__ == "__main__":
    build()
