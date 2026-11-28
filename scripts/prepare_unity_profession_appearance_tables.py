"""Decode the selected official appearance tables without running client code."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from prepare_unity_profession_photographer import record


TABLES = ("profession_base", "spine_skin", "character_skin", "character_skin_id_group",
          "character_skin_dye", "character_skin_color", "append_appearance")


def build():
    apk = Apk.current()
    if apk.version != "911_156995":
        raise ValueError("Appearance tables require the selected 911_156995 source")
    code = apk.source.prepared_path / "official-code"
    from prepare_battle_scene_player_appearance import CurrentConstructors
    constructors = CurrentConstructors(code)
    mapping = {m[2].rsplit('/', 1)[-1]: m[1] for m in re.finditer(
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
    dest = apk.source.prepared_path / "profession-ui/appearance-tables.json"
    dest.parent.mkdir(parents=True, exist_ok=True)
    dest.write_text(json.dumps(tables, ensure_ascii=False, separators=(",", ":")) + "\n",
                    encoding="utf-8")
    sources = []
    for name in sorted(constructors.sources):
        if constructors.sources[name]:
            sources.append(record(constructors.paths[name]))
    result = dict(format=1, manifest=apk.version, tables=inputs, constructor_sources=sources,
        interpreter=record(ROOT / "scripts/official_tables.py"), output=record(dest),
        generator=record(Path(__file__)),
        scope="All seven original tables fully decoded in original row and collection order. "
              "No character selection or skin-resolution rule is implemented by this extraction.",
        limitations=["Conversion cache only; not yet a packaged runtime configuration or UI validation."])
    proof = ROOT / "assets/reference/profession-ui/appearance-tables-source-manifest.json"
    proof.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return result


if __name__ == "__main__":
    build()
