"""Bind the two original stele Sprites to verified shared Windows payloads."""
import json
import hashlib
from pathlib import Path
import re

from official_apk import Apk, ROOT
import UnityPy
import yaml
from UnityPy.classes import PPtr
from prepare_unity_map12_stele_entity_inputs import BUNDLE, OUTPUT, RECEIPT as INPUT_RECEIPT
from prepare_unity_map13_sources import encode, sha, source_path, write

SHARED = ROOT / "assets/reference/official-map/map13-native-entity-sprites-source-manifest.json"
CATALOG = ROOT / "unity/Assets/Game/Resources/Map12/Native/stele-sprite-catalog.asset"
RECEIPT = ROOT / "assets/reference/official-map/map12-native-stele-sprites-source-manifest.json"
RUNTIME = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13EntityAssets"
GUID = re.compile(r"(?m)^guid: ([a-f0-9]{32})$")


def record(path):
    return dict(path=source_path(path), sha256=sha(path.read_bytes()))


def guid(path):
    match = GUID.search(Path(str(path) + ".meta").read_text(encoding="utf8"))
    if match is None:
        raise ValueError("Original sprite dependency has no explicit GUID")
    return match[1]


def key(obj):
    return obj.assets_file.name.casefold() + ":" + str(obj.path_id)


def build():
    shared = json.loads(SHARED.read_bytes())
    receipt = json.loads(INPUT_RECEIPT.read_bytes())
    source = json.loads(OUTPUT.read_bytes())
    apk = Apk.current()
    if any(value != "871_156598" for value in
           (shared["manifest"], receipt["manifest"], source["manifest"], apk.version)):
        raise ValueError("Original stele Sprite version differs")
    for entry in receipt["outputs"]:
        if sha((ROOT / entry["path"]).read_bytes()) != entry["sha256"]:
            raise ValueError("Original stele component projection differs")
    guarded = [Path(__file__).resolve(), SHARED, INPUT_RECEIPT, OUTPUT]
    guarded.extend(RUNTIME / (name + ".cs") for name in (
        "NativeMap13EntitySpriteCatalog", "NativeMap13EntitySpriteLibrary", "NativeMap13EntitySpriteBinding"))
    guarded.extend(Path(str(path) + ".meta") for path in list(guarded) if path.suffix == ".cs")
    guarded.extend(ROOT / "scripts" / name for name in (
        "official_apk.py", "prepare_unity_map13_sources.py", "prepare_unity_map12_stele_entity_inputs.py"))
    before = {path: sha(path.read_bytes()) for path in guarded}
    originals = {str(obj.path_id): obj for obj in apk.objects(BUNDLE)}
    renderers, sprites, packages = [], {}, {}
    for obj in source["objects"]:
        if obj["type"] != "SpriteRenderer":
            continue
        renderer = originals[obj["path_id"]]
        if sha(renderer.get_raw_data()) != obj["payload_sha256"]:
            raise ValueError("Original stele SpriteRenderer identity differs")
        sprite = PPtr(**obj["fields"]["m_Sprite"], assetsfile=renderer.assets_file).deref()
        identity = key(sprite)
        candidates = [(package, entry) for package in shared["packages"] for entry in package["native_objects"]
                      if entry["identity"] == identity]
        if len(candidates) != 1:
            raise ValueError("Exact original stele Sprite is not retained in a verified Windows package")
        package, original_record = candidates[0]
        if original_record["original_payload_sha256"] != sha(sprite.get_raw_data()):
            raise ValueError("Shared stele Sprite original bytes differ")
        package_path = ROOT / package["output"]["path"]
        raw = package_path.read_bytes()
        if sha(raw) != package["output"]["sha256"]:
            raise ValueError("Shared Windows sprite package bytes differ")
        native = UnityPy.load(raw)
        selected, = [value for value in native.objects if key(value) == identity]
        if (int(selected.assets_file.target_platform) != 19
                or not selected.assets_file._enable_type_tree
                or selected.assets_file.unity_version != "2022.3.57f1"
                or selected.get_raw_data() != sprite.get_raw_data()
                or selected.read_typetree(check_read=True) != sprite.read_typetree(check_read=True)):
            raise ValueError("Shared native Sprite changed source fields, bytes or platform")
        alias = "__zjcs/map13/native/" + sha(identity.encode())[:24]
        bundle, = [value.read(check_read=True) for value in native.objects if value.type.name == "AssetBundle"]
        entries = [entry for name, entry in bundle.m_Container if name == alias]
        if len(entries) != 1 or entries[0].asset.m_PathID != sprite.path_id or entries[0].asset.m_FileID:
            raise ValueError("Shared native Sprite alias resolves to another object")
        # Retain the shared package as-is. Its source receipt proves all source
        # mip decoding and sampler fields, not only a preview/cropped image.
        packages[package["key"]] = package
        sprites[identity] = dict(key=identity, package=package["key"], assetName=alias,
            originalName=sprite.read(check_read=True).m_Name,
            original_payload_sha256=sha(sprite.get_raw_data()), source=apk.provenance(sprite))
        renderers.append(dict(sourceKey=key(renderer), spriteKey=identity, kind="SpriteRenderer"))
    if len(renderers) != 2 or len(sprites) != 2 or len(packages) != 1:
        raise ValueError("Original stele Sprite coverage differs")
    fields = dict(m_ObjectHideFlags=0, m_CorrespondingSourceObject=dict(fileID=0),
        m_PrefabInstance=dict(fileID=0), m_PrefabAsset=dict(fileID=0), m_GameObject=dict(fileID=0),
        m_Enabled=1, m_EditorHideFlags=0,
        m_Script=dict(fileID=11500000, guid=guid(RUNTIME / "NativeMap13EntitySpriteCatalog.cs"), type=3),
        m_Name=CATALOG.stem, m_EditorClassIdentifier="", manifest=apk.version,
        packages=[dict(key=package["key"], sha256=package["output"]["sha256"],
            data=dict(fileID=4900000, guid=guid(ROOT / package["output"]["path"]), type=3))
            for package in packages.values()],
        sprites=[{name: value[name] for name in ("key", "package", "assetName", "originalName")}
            for value in sprites.values()], renderers=renderers)
    raw = ("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\n"
           + yaml.safe_dump(dict(MonoBehaviour=fields), allow_unicode=True, sort_keys=False)).encode()
    write(CATALOG, raw)
    catalog_meta = CATALOG.with_name(CATALOG.name + ".meta")
    identity = source_path(CATALOG).removeprefix("unity/")
    meta_raw = ("fileFormatVersion: 2\nguid: " + hashlib.md5(("zjcs-official:" + identity).encode()).hexdigest()
        + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n"
          "  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    if catalog_meta.exists() and catalog_meta.read_bytes().replace(b"\r\n", b"\n") != meta_raw:
        raise ValueError("Refusing to replace unrelated stele sprite catalog metadata")
    write(catalog_meta, meta_raw)
    for path, expected in before.items():
        if sha(path.read_bytes()) != expected:
            raise ValueError("Original sprite binding input changed during extraction")
    write(RECEIPT, encode(dict(format=1, manifest=apk.version, mapClassId=12,
        inputs=[record(path) for path in guarded], renderer_bindings=renderers,
        sprites=list(sprites.values()), shared_packages=[dict(key=p["key"], source=p["source"],
            output=record(ROOT / p["output"]["path"]),
            meta=record(Path(str(ROOT / p["output"]["path"]) + ".meta")),
            textures=p["textures"]) for p in packages.values()],
        outputs=[record(CATALOG), record(catalog_meta)],
        resource="Map12/Native/stele-sprite-catalog",
        operation="Resolve both original Map12 SpriteRenderer PPtrs from the selected APK-first entity source. Match exact CAB/path IDs and original payload SHA-256 against the shared selected-version Windows package. Verify retained Sprite bytes and every decoded field against the original, native target 19, explicit type trees, unchanged source Unity version, exact lookup aliases and package bytes. Reuse the existing verified mip-complete texture conversion without duplicating or cropping images. Scope renderer bindings to the two original Map12 identities.",
        limitations=["Explicit Sprite library only. Native entity prefab binding, lifecycle, Unity import, actual rendering, pointer interaction and Windows Player validation remain required. Automatic runtime acceptance is paused."])))
    print("Original Map12 stele Sprites bound:", len(renderers), "renderers,", len(sprites), "unchanged Sprites,", len(packages), "shared Windows package", flush=True)


if __name__ == "__main__":
    build()
