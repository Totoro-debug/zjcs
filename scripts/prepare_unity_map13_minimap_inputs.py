"""Restore the complete original MiniMap_13 CAB closure for native conversion.

These are converter inputs, not a substitute minimap or playable asset claim.
Every source object payload is checked by the shared strict hydrator.
"""
from pathlib import Path
import argparse
import json

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from map13_entity_native_schemas import install
from prepare_unity_bundle_inputs import collect, read_source, serialized_files
from prepare_unity_map13_water_entity_inputs import hydrate
from prepare_unity_map13_sources import encode, sha, source_path, write

ROOT_BUNDLE = "defaultpackage_assets_prefabs_map_map_13_minimap.bundle"


class CameraSchemas:
    """Use matching Unity reference assemblies for URP and UGUI schemas."""
    def __init__(self, original, native):
        self.original, self.native = original, native
        self.native_types = set()

    def __getattr__(self, name):
        return getattr(self.original, name)

    def tree(self, assembly, name):
        selected = self.native if self.is_native(name) else self.original
        try:
            return selected.tree(assembly, name)
        except ValueError:
            if selected is self.native:
                raise
            return self.native.tree(assembly, name)

    def read(self, obj):
        script = obj.read(check_read=False).m_Script.read()
        name = (script.m_Namespace + "." if script.m_Namespace else "") + script.m_ClassName
        selected = self.native if self.is_native(name) else self.original
        try:
            return selected.read(obj)
        except (ValueError, EOFError, AssertionError):
            if selected is self.native:
                raise
            value = self.native.read(obj)
            # Hydrators request the exact schema again after strict reading.
            self.native_types.add(name)
            return value

    def is_native(self, name):
        return (name in self.native_types or name == "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData"
                or name.startswith(("UnityEngine.UI.", "UGUIJszx."))
                or name in ("NoDrawingRayCast", "TMPro.TextMeshProUGUI"))


def build(cache):
    cache = cache.resolve()
    if not cache.is_relative_to(ROOT / "output/validation"):
        raise ValueError("Minimap conversion must use an owned validation workspace")
    inputs = cache / "inputs"
    if inputs.exists():
        raise FileExistsError(inputs)
    apk = Apk.current()
    assets, missing = collect(apk, [ROOT_BUNDLE])
    if missing:
        raise ValueError(missing)
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    code_manifest = code.parent / "code-manifest.json"
    provenance = json.loads(code_manifest.read_bytes())
    if provenance["manifest"] != apk.version or provenance["apk_sha256"] != apk.source.apk_sha256:
        raise ValueError("Schema code belongs to a different official source")
    assemblies = []
    for name, expected in provenance["assemblies"].items():
        path = code / "managed/decoded" / name
        if sha(path.read_bytes()) != expected["decoded_sha256"]:
            raise ValueError("Official schema assembly changed: " + name)
        assemblies.append(dict(path=source_path(path), sha256=expected["decoded_sha256"]))
    schemas = NativeSchemas(code)
    schema_proof = install(schemas, code)
    camera_assemblies = [dict(path=path.as_posix(), sha256=sha(path.read_bytes()))
                         for path in schemas.reference_inputs]
    schemas.default = CameraSchemas(schemas.default, schemas.native)
    inputs.mkdir(parents=True)
    records, components, changes, unions, count = [], [], [], [], 0
    try:
        for name in sorted(apk.loaded):
            decoded, record = read_source(apk, name)
            if name.startswith("assets/bin/Data/"):
                content, fields, altered, typed = decoded, [], [], []
                objects = sum(len(file.objects) for file in serialized_files(apk.loaded[name]))
            else:
                content, fields, altered, typed, objects = hydrate(apk.loaded[name], schemas, include_source_identity=True)
            record.update(output=Path(name).name, converter_input_sha256=sha(content))
            write(inputs / record["output"], content)
            records.append(record)
            components.extend(fields)
            changes.extend(altered)
            unions.extend(typed)
            count += objects
            print("Restored", name, len(fields), objects, flush=True)
        builtin = "assets/bin/Data/unity default resources"
        raw = apk.archive.read(builtin)
        if not (inputs / Path(builtin).name).exists():
            write(inputs / Path(builtin).name, raw)
        receipt = dict(format=1, manifest=apk.version, mapClassId=13,
            roots=[ROOT_BUNDLE], inputs=records, serialized_files=list(assets.values()),
            camera_reference_assemblies=camera_assemblies,
            assemblies=assemblies,code_manifest=dict(path=source_path(code_manifest),sha256=sha(code_manifest.read_bytes())),
            schema_proof=schema_proof,
            builtin=dict(apk_entry=builtin, sha256=sha(raw)),
            managed_reference_unions=unions, converted_objects=changes, components=components,
            counts=dict(bundles=len(records), checked_objects=count, components=len(components), converted_registries=len(changes)),
            tools=[dict(path=source_path(Path(__file__)), sha256=sha(Path(__file__).read_bytes())),
                   dict(path="scripts/prepare_unity_map13_water_entity_inputs.py", sha256=sha((ROOT / "scripts/prepare_unity_map13_water_entity_inputs.py").read_bytes()))])
        write(cache / "source-manifest.json", encode(receipt))
        print(receipt["counts"], flush=True)
        return receipt
    finally:
        apk.archive.close()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path, required=True)
    build(parser.parse_args().cache)
