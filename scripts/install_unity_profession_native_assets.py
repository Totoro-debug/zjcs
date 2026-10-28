"""Install a verified, fully bound closure of original profession native assets."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from prepare_unity_profession_native_bindings import CACHE, TEXT, guid, sha


def install(scope, project):
    plan_path = CACHE / "typed" / ("native-bindings-" + scope + ".json")
    plan = json.loads(plan_path.read_bytes())
    if plan["manifest"] != "871_156598" or plan["scope"] != scope:
        raise ValueError("Wrong native profession closure")
    if plan["pending_scripts"] or plan["pending_shaders"] or plan["missing"]:
        raise ValueError("Original closure still has unbound dependencies")
    for key in ("verified_export", "verified_fonts", "generator", "verified_dynamic_resources", "verified_item_configs", "verified_native_textures"):
        row = plan[key]
        if row is None:
            continue
        if sha(ROOT / row["path"]) != row["sha256"]:
            raise ValueError("Original closure input changed: " + row["path"])
    if any(row["binding"] is None for row in plan["managed_reference_types"]):
        raise ValueError("Original managed reference type is unbound")
    project = project.resolve()
    destination = project / "Assets/Official/ProfessionUI/Native"
    native_texture_guids = set()
    if plan["verified_native_textures"] is not None:
        textures = json.loads((ROOT / plan["verified_native_textures"]["path"]).read_bytes())
        selected = {guid(ROOT / row["path"]) for row in plan["assets"].values()}
        for row in textures["native_assets"]:
            native_texture_guids.add(row["guid"])
            old = row["replaces_import"]
            target = destination / old["native_relative_path"]
            if row["guid"] in selected and target.exists():
                meta = target.with_name(target.name + ".meta")
                # Unity rewrites importer defaults on import. Ownership is
                # established by exact PNG payload and original asset GUID;
                # the verified export separately checks the source metadata.
                if not target.resolve().is_relative_to(destination.resolve()) or sha(target) != old["sha256"] or guid(target) != row["guid"] or "TextureImporter:" not in meta.read_text(encoding="utf-8"):
                    raise ValueError("Existing PNG replacement is not the verified converter output")
                target.unlink()
                meta.unlink()
    exports = CACHE / "typed/export/ExportedProject/Assets"
    remap = {}
    for relative, binding in plan["script_bindings"].items():
        source = ROOT / binding["path"]
        target = project / source.relative_to(ROOT / "unity")
        if sha(source) != binding["sha256"] or sha(target) != binding["sha256"] or guid(target) != binding["guid"]:
            raise ValueError("Original runtime script binding differs: " + str(target))
        remap[guid(exports / relative)] = binding["guid"]
    for relative, binding in plan["shader_bindings"].items():
        source = ROOT / "unity" / binding["path"]
        target = project / binding["path"]
        if sha(source) != binding["sha256"] or sha(target) != binding["sha256"] or guid(target) != binding["guid"]:
            raise ValueError("Original Windows shader binding differs: " + str(target))
        remap[guid(exports / relative)] = binding["guid"]
    outputs = []
    folder_metadata = []
    for relative, row in plan["assets"].items():
        if relative in plan["shader_bindings"]:
            continue
        source = ROOT / row["path"]
        if sha(source) != row["sha256"]:
            raise ValueError("Original asset changed: " + relative)
        raw = source.read_bytes()
        if source.suffix.lower() in TEXT:
            text = raw.decode("utf-8")
            text = re.sub(r"guid: ([0-9a-f]{32})", lambda match: "guid: " + remap.get(match[1], match[1]), text)
            # PNG importer references use external type 3; native Texture2D
            # assets use type 2. Preserve the exact GUID and local object ID.
            text = re.sub(r"\{fileID: 2800000, guid: ([0-9a-f]{32}), type: 3\}",
                lambda match: match[0].replace("type: 3", "type: 2") if match[1] in native_texture_guids else match[0], text)
            for entry in plan["managed_reference_types"]:
                assembly, namespace, name = entry["binding"]
                old = "type:\n        class: " + entry["type"] + "\n        ns:" + (" " + entry["namespace"] if entry["namespace"] else "") + "\n        asm: " + entry["assembly"]
                new = "type:\n        class: " + name + "\n        ns: " + namespace + "\n        asm: " + assembly
                text = text.replace(old, new)
            raw = text.encode("utf-8")
        meta_source = source.with_name(source.name + ".meta")
        meta_raw = meta_source.read_bytes()
        target = destination / relative
        if not target.resolve().is_relative_to(destination.resolve()):
            raise ValueError("Native asset escapes its destination")
        target.parent.mkdir(parents=True, exist_ok=True)
        folder = target.parent
        while folder.is_relative_to(project / "Assets/Official/ProfessionUI"):
            meta = folder.with_name(folder.name + ".meta")
            if not meta.exists():
                identity = folder.relative_to(project).as_posix()
                folder_guid = hashlib.md5(("zjcs:profession:native-folder:" + identity).encode()).hexdigest()
                meta.write_text("fileFormatVersion: 2\nguid: " + folder_guid + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n", encoding="utf-8", newline="\n")
            folder_metadata.append(dict(path=meta.relative_to(project).as_posix(), sha256=sha(meta)))
            folder = folder.parent
        target.write_bytes(raw)
        target.with_name(target.name + ".meta").write_bytes(meta_raw)
        outputs.append(dict(native_relative_path=relative, source=row,
            installed_path=target.relative_to(project).as_posix(), sha256=hashlib.sha256(raw).hexdigest(),
            guid=guid(source), meta_sha256=hashlib.sha256(meta_raw).hexdigest()))
    result = dict(format=1, manifest=plan["manifest"], scope=scope,
        binding_plan=dict(path=plan_path.relative_to(ROOT).as_posix(), sha256=sha(plan_path)),
        installer=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__))),
        outputs=outputs, guid_remap=remap, folder_metadata=list({row["path"]: row for row in folder_metadata}.values()),
        native_texture_guids=sorted(native_texture_guids),
        operation="Copy verified original native assets and import metadata; replace explicitly bound script/shader GUIDs and managed-reference assembly/type names. For PNG-to-native Texture2D conversion change external reference type 3 to 2, retaining GUID/fileID. Preserve native hierarchy, other serialized values and asset identity.")
    proof = CACHE / "typed" / ("installed-" + scope + ".json")
    proof.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Installed", len(outputs), "original native assets in", project)
    return result


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--scope", required=True, choices=("main", "main-dynamic", "all", "item-hint", "item-icons"))
    parser.add_argument("--project", type=Path, default=ROOT / "unity")
    args = parser.parse_args()
    install(args.scope, args.project)
