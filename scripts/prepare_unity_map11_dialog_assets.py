"""Recover every portrait and expression referenced by the pinned Map11 stories."""
import json
import re

import prepare_unity_map10_dialog_assets as shared
from official_apk import Apk
from official_unity_schema import ScriptSchemas


ROOT = shared.ROOT
OUTPUT = ROOT / "unity/Assets/Official/World/Map/11/Resources/Map11"
MANIFEST = ROOT / "assets/reference/official-map/map11-dialogue-source-manifest.json"


def build():
    runtime_path = OUTPUT / "stories.json"
    runtime_raw = runtime_path.read_bytes()
    runtime = json.loads(runtime_raw)
    source_manifest_path = ROOT / "assets/reference/official-map/map11-runtime-source-manifest.json"
    source_manifest = json.loads(source_manifest_path.read_text(encoding="utf-8"))
    if runtime["manifest"] != shared.VERSION or runtime["mapClassId"] != 11 \
            or source_manifest["outputs"]["stories.json"]["sha256"] != shared.sha256(runtime_raw):
        raise ValueError("Map11 story portraits require the verified fixed-version story closure.")
    previous = {}
    if MANIFEST.exists():
        old_raw = MANIFEST.read_bytes()
        old = json.loads(old_raw)
        for row in old["outputs"]:
            path = ROOT / row["path"]
            if shared.sha256(path.read_bytes()) != row["sha256"]:
                raise ValueError("Changed Map11 dialogue output: " + str(path))
            previous[path.resolve()] = row["sha256"]
        previous[MANIFEST.resolve()] = shared.sha256(old_raw)
    apk, schemas = Apk(shared.VERSION), ScriptSchemas()
    entry = next(name for name in apk.archive.namelist()
                 if "PackageManifest_DefaultPackage_" in name and name.endswith(".bytes"))
    package_raw = apk.archive.read(entry)
    guid_paths = {match[2].decode(): match[1].decode() for match in re.finditer(
        rb"(Assets/[^\x00-\x1f]+?)\x20\x00([a-f0-9]{32})", package_raw)}

    def sprite(guid, output_name):
        path = guid_paths[guid]
        asset_key = "defaultpackage_" + path.replace("/", "_").lower()
        bundles = sorted((bundle for bundle in apk.index
                          if asset_key.startswith(bundle.removesuffix(".bundle"))), key=len, reverse=True)
        matches = [(asset, pointer.deref()) for bundle in bundles for asset, pointer in apk.container(bundle)
                   if asset.lower() == path.lower() and pointer.type.name == "Sprite"]
        if len(matches) != 1:
            raise ValueError("Unresolved pinned dialogue sprite GUID: " + guid + " / " + path)
        info, source = shared.extract_sprite(apk, 0, "", output_name, previous, matches[0],
            OUTPUT / "Dialogue", "Map11")
        source["guid"] = guid
        return info, source

    roles = {row["RoleId"]: row for row in runtime["dialogRoles"]}
    faces = {}
    for story in runtime["stories"] + runtime["subgraphs"]:
        for group in story["dialogueGroups"]:
            for line in group["Lines"]:
                role = roles.get(line["RoleId"])
                if role and role["PortraitPath"]:
                    faces.setdefault(role["PortraitPath"], set()).update(
                        [line["RoleFaceType"]] if line["RoleFaceType"] else [])
    layouts, sources, outputs = [], {}, []
    for path, expressions in sorted(faces.items()):
        key = path.removeprefix("UI/Portrait/Npc_Portrait_")
        layout, source, files = shared.extract_portrait(apk, schemas, key, expressions, previous, sprite)
        layouts.append(layout)
        sources[path] = source
        outputs.extend(files)
    style, style_source = shared.extract_speaker_style(apk, schemas)
    settings_object = apk.asset("scriptableconfig_modules_story", "/StorySettings.asset")
    story_settings = schemas.read(settings_object)
    map10 = json.loads((ROOT / "unity/Assets/Official/World/Map/10/Resources/Map10/dialogue-portraits.json")
                      .read_text(encoding="utf-8"))
    payload = shared.json_bytes({"manifest": shared.VERSION, "mapClassId": 11,
        "sourceSha256": shared.sha256(runtime_raw), "speakerStyle": style,
        "voiceOverIds": story_settings["mVoiceOverIds"],
        "portraits": layouts, "icons": map10["icons"]})
    target = OUTPUT / "dialogue-portraits.json"
    shared.write_generated(target, payload, previous)
    shared.write_meta(target, "TextScriptImporter")
    result = {"manifest": shared.VERSION, "mapClassId": 11,
        "inputs": {"stories": {"path": runtime_path.relative_to(ROOT).as_posix(), "sha256": shared.sha256(runtime_raw)},
                   "storyManifestSha256": shared.sha256(source_manifest_path.read_bytes()),
                   "packageManifest": {"apkEntry": entry, "sha256": shared.sha256(package_raw)},
                   "portraits": sources, "talkDialogPrefab": style_source,
                   "storySettings": dict(apk.provenance(settings_object), fields=story_settings)},
        "outputs": [{"path": row["path"], "sha256": row["sha256"]} for row in outputs]
                   + [{"path": target.relative_to(ROOT).as_posix(), "sha256": shared.sha256(payload)}]}
    shared.write_generated(MANIFEST, shared.json_bytes(result), previous)
    shared.write_meta(OUTPUT / "Dialogue", "DefaultImporter")
    print("Map11 dialogue:", len(layouts), "original portrait prefabs,", len(outputs), "sprites.")
    return result


if __name__ == "__main__":
    build()
