"""Verify and preserve the official MiniMap_13 native export and source data."""
import argparse
import json
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_unity_map13_sources import encode, meta, sha, source_path, write
from prepare_unity_map13_minimap_inputs import ROOT_BUNDLE
from prepare_unity_map13_minimap_mask_fixture import build as mask_fixture
from prepare_unity_map13_water_entity_export import build as verify_export, convert, native_io_path

DEST = ROOT / "unity/Assets/Official/World/Map/13/Source/NativeMiniMapSource"
RECEIPT = ROOT / "assets/reference/official-map/map13-native-minimap-source-manifest.json"


def record(path):
    raw=native_io_path(path).read_bytes()
    relative=source_path(path)
    if relative.startswith("scripts/") and path.suffix==".py":
        canonical=raw.replace(b"\r\n",b"\n")
        return dict(path=relative,sha256=sha(canonical),worktree_sha256=sha(raw),normalization="Repository LF text; original executed worktree bytes recorded separately")
    return dict(path=relative,sha256=sha(raw))


def build(cache, run_converter=False):
    cache=cache.resolve()
    if not cache.is_relative_to(ROOT / "output/validation"):
        raise ValueError("An owned validation workspace is required")
    original_path=cache / "source-manifest.json"
    receipt=json.loads(original_path.read_bytes())
    if receipt["manifest"]!="871_156598" or receipt["roots"]!=[ROOT_BUNDLE]:
        raise ValueError("Incorrect original minimap source")
    checked=dict(receipt,outputs=[])
    check_path=cache / "export-input-receipt.json"
    write(check_path,encode(checked))
    if run_converter: convert(cache)
    verify_export(receipt_path=check_path,component_path=original_path,roots=[ROOT_BUNDLE],
        cache_root=cache,proof_path=cache / "export-proof.json",expected_counts=(12,8),
        entry_path=Path(__file__).resolve(),subject="MiniMap_13")
    proof=json.loads((cache / "export-proof.json").read_bytes())
    apk=Apk.current()
    try:
        objects=apk.objects(ROOT_BUNDLE)
        cab=objects[0].assets_file.name
        roots=[obj for obj in objects if obj.type.name=="GameObject" and obj.read().m_Name=="MiniMap_13"]
        if len(roots)!=1:raise ValueError("Original minimap root is not unique")
        component_records=[c for c in receipt["components"] if c["cab"]==cab]
        native_objects=[dict(cab=o.assets_file.name,path_id=str(o.path_id),type=o.type.name,object_sha256=sha(o.get_raw_data())) for o in objects]
        output=DEST / "native-minimap-components.json"
        write(output,encode(dict(manifest=receipt["manifest"],mapClassId=13,
            root=dict(apk.provenance(roots[0]),object_sha256=sha(roots[0].get_raw_data())),
            objects=native_objects,components=component_records)))
        meta(output)
        outputs=[record(output)]
        exported_inputs=[]
        for entry in proof["outputs"]:
            path=ROOT / entry["path"]
            relative=path.relative_to(cache / "restored")
            target=DEST / "OriginalPrefabs" / relative.with_name(relative.name+".txt")
            raw=native_io_path(path).read_bytes()
            if sha(raw)!=entry["sha256"]:raise ValueError("Verified minimap export changed")
            write(target,raw);meta(target)
            outputs.append(record(target))
            exported_inputs.append(dict(relative=relative.as_posix(),sha256=entry["sha256"],registries=entry["registries"]))
        fixture=mask_fixture(cache);outputs.append(record(fixture))
        for folder in [DEST,*sorted(p for p in DEST.rglob("*") if p.is_dir())]:meta(folder,folder=True)
        code=ROOT / ".tools/source-packages/current/871_156598/official-code"
        source_paths=[]
        for name in sorted({c["type"] for c in component_records}):
            candidates=[code/"client-source"/(name+".cs"),code/"framework-source"/(name+".cs")]
            source_paths.extend(record(p) for p in candidates if p.is_file())
        tools=[Path(__file__).resolve(),ROOT/"scripts/prepare_unity_map13_minimap_inputs.py",
            ROOT/"scripts/prepare_unity_map13_minimap_mask_fixture.py",ROOT/"scripts/prepare_unity_map13_water_entity_inputs.py",
            ROOT/"scripts/prepare_unity_map13_water_entity_export.py",ROOT/"scripts/map13_native_schemas.py",
            ROOT/"scripts/map13_entity_native_schemas.py",ROOT/"scripts/official_managed_reference_conversion.py"]
        result=dict(format=1,manifest=receipt["manifest"],mapClassId=13,
            root_bundle=ROOT_BUNDLE,inputs=receipt["inputs"],assemblies=receipt["assemblies"],
            code_manifest=receipt["code_manifest"],schema_proof=receipt["schema_proof"],
            reference_assemblies=receipt["camera_reference_assemblies"],counts=receipt["counts"],
            export_counts=proof["counts"],exported_inputs=exported_inputs,
            original_sources=source_paths,tools=[record(p) for p in tools],outputs=outputs,
            converter=dict(version=proof["tool"]["version"],sha256=proof["tool"]["sha256"]),
            operation="Verify every original CAB dependency and native object payload. Reversibly expose original managed-reference registries for AssetRipper, restore original RID/descriptor/field ordering and verify source pointer identities and exact float bits. Preserve full exported prefab text as source-only TextAssets, original component fields and source object hashes; capture the original ten mask patterns and all 256 neighborhood cases. These source files are not runtime prefabs.",
            limitations=["Complete runtime script bindings, original minimap asset import/shaders, region masks/marks, formal input/save validation and Windows Player remain required."])
        write(RECEIPT,encode(result))
        print("Original MiniMap_13 source captured:",len(component_records),"components,",len(outputs),"source outputs",flush=True)
        return result
    finally:apk.archive.close()


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache",type=Path,required=True)
    parser.add_argument("--convert",action="store_true")
    options=parser.parse_args();build(options.cache,options.convert)
