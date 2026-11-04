"""Bind the recovered original marker windows to a local Player resource catalog."""
import hashlib
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "assets/reference/official-map/map13-native-minimap-mark-dialogs-source-manifest.json"
DEST = ROOT / "unity/Assets/Game/Resources/MainlineMiniMap/mark-dialog-assets.asset"
RECEIPT = ROOT / "assets/reference/official-map/mainline-minimap-mark-dialog-catalog-source-manifest.json"
NATIVE = "unity/Assets/Official/World/Map/13/NativeMiniMapMarkDialogs/"


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path, raw=None):
    return {"path": path.relative_to(ROOT).as_posix(), "sha256": sha(path.read_bytes() if raw is None else raw)}


def verified_native(path, expected):
    raw = path.read_bytes()
    if sha(raw) == expected:
        return raw
    # Git may have checked a generated Unity YAML asset out as CRLF. Preserve
    # that working file and verify the exact already recorded committed bytes.
    committed = subprocess.check_output(["git", "show", "HEAD:" + path.relative_to(ROOT).as_posix()], cwd=ROOT)
    if sha(committed) != expected or raw.replace(b"\r\n", b"\n") != committed.replace(b"\r\n", b"\n"):
        raise ValueError("Original native dialog payload differs: " + str(path))
    return committed


def build():
    source = json.loads(SOURCE.read_bytes())
    if source.get("manifest") != "871_156598" or len(source.get("roots", [])) != 3:
        raise ValueError("The separately recorded original three-dialog source is required")
    bindings, inputs = [], []
    for entry in source["outputs"]:
        path = ROOT / entry["path"]
        if not path.as_posix().startswith((ROOT / NATIVE).as_posix()):
            raise ValueError("Foreign native dialog output")
        relative = path.relative_to(ROOT / NATIVE).as_posix()
        if path.suffix == ".prefab":
            if relative not in source["roots"]:
                raise ValueError("An unexpected original dialog prefab was included")
            key = relative.removeprefix("Prefabs/")
            raw = verified_native(path, entry["sha256"])
            match = re.search(rb"(?m)^--- !u!1 &(\d+)\r?$", raw)
        elif path.suffix == ".asset" and relative.startswith("Image/") and not relative.endswith(".texture.asset"):
            key = relative.removeprefix("Image/")
            raw = verified_native(path, entry["sha256"])
            match = re.search(rb"(?m)^--- !u!213 &(\d+)\r?$", raw)
        else:
            continue
        if match is None:
            raise ValueError("The original native object ID is missing: " + relative)
        metadata = Path(str(path) + ".meta")
        guid = re.search(r"(?m)^guid: ([a-f0-9]{32})$", metadata.read_text(encoding="utf8"))[1]
        expected_guid = hashlib.md5(("zjcs-map13-minimap-mark-dialogs:" + relative).encode()).hexdigest()
        if guid != expected_guid:
            raise ValueError("The original native asset GUID differs: " + relative)
        bindings.append((key, int(match[1]), guid))
        inputs += [record(path, raw), dict(record(metadata, metadata.read_bytes().replace(b"\r\n", b"\n")), hashLineEndings="LF")]
    if len(bindings) != 20 or sum(key.endswith(".prefab") for key, _, _ in bindings) != 3:
        raise ValueError("Expected all three original marker dialogs and 17 native sprites")
    blur_source = ROOT / "assets/reference/partner-friendship-window-source-manifest.json"
    blur = json.loads(blur_source.read_bytes())
    if blur["manifest"] != "884_156692":
        raise ValueError("The separately restored current original blur shader is required")
    blur_root = ROOT / "unity/Assets/Official/Partners/UI/FriendshipWindow"
    for name in ("PartnerScreenBlur.mat", "PartnerScreenBlur.mat.meta", "PartnerScreenBlur.shader", "PartnerScreenBlur.shader.meta"):
        path = blur_root / name
        inputs.append(record(path, verified_native(path, blur["outputs"][name])))
    material_guid = re.search(r"(?m)^guid: ([a-f0-9]{32})$", (blur_root / "PartnerScreenBlur.mat.meta").read_text("utf8"))[1]
    bindings.append(("CustomRenderTexture/ScreenBlur", 2100000, material_guid))
    script = ROOT / "unity/Assets/Game/Runtime/Offline/World/NativeMap13/NativeMap13AssetCatalog.cs"
    script_guid = re.search(r"(?m)^guid: ([a-f0-9]{32})$", Path(str(script) + ".meta").read_text(encoding="utf8"))[1]
    lines = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
             "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
             "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
             "  m_Script: {fileID: 11500000, guid: " + script_guid + ", type: 3}", "  m_Name: MainlineMiniMapMarkDialogs",
             "  m_EditorClassIdentifier: ''", "  Bindings:"]
    for key, file_id, guid in sorted(bindings):
        lines += ["  - OfficialPath: " + key, "    Asset: {fileID: " + str(file_id) + ", guid: " + guid + ", type: " + ("3" if key.endswith(".prefab") else "2") + "}"]
    raw = ("\n".join(lines) + "\n").encode()
    if DEST.exists() and DEST.read_bytes() != raw:
        previous = json.loads(RECEIPT.read_bytes())
        if sha(DEST.read_bytes()) != previous["output"]["sha256"]:
            raise ValueError("Refusing to overwrite a changed local dialog catalog")
    DEST.write_bytes(raw)
    metadata = Path(str(DEST) + ".meta")
    guid = hashlib.md5(("zjcs-mainline-minimap-dialog-catalog:" + DEST.relative_to(ROOT).as_posix()).encode()).hexdigest()
    meta = ("fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n").encode()
    if metadata.exists() and metadata.read_bytes() != meta:
        raise ValueError("Refusing to overwrite unrelated dialog catalog import metadata")
    metadata.write_bytes(meta)
    code = [script, Path(str(script) + ".meta"),
            ROOT / "unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapMarkerCatalog.cs",
            ROOT / "unity/Assets/Game/Editor/Map13MiniMap/OfficialMap13MiniMapMarkDialogsAcceptance.cs"]
    result = {"format": 1, "native_manifest": source["manifest"], "native_source_receipt": dict(record(SOURCE, SOURCE.read_bytes().replace(b"\r\n", b"\n")), hashLineEndings="LF"),
              "operation": "Bind complete already restored original marker dialog prefabs and sprites by their original UI addresses, plus the separately restored current 884 ScreenBlur material by its actual GUID. No prefab, texture, shader, material, animation or layout payload is rewritten. Native 871 provenance remains separate from selected 884 marker configuration and blur conversion.",
              "inputs": inputs + [dict(record(path, path.read_bytes().replace(b"\r\n", b"\n")), hashLineEndings="LF") for path in code],
              "blur_source_receipt": dict(record(blur_source, blur_source.read_bytes().replace(b"\r\n", b"\n")), hashLineEndings="LF"),
              "output": dict(record(DEST), prefabCount=3, spriteCount=17, materialCount=1, resource="MainlineMiniMap/mark-dialog-assets"),
              "metadata": record(metadata), "tool": record(Path(__file__)),
              "limitations": ["This resource catalog does not install the formal popup host or minimap state adapter."]}
    RECEIPT.write_bytes((json.dumps(result, ensure_ascii=False, indent=2) + "\n").encode())
    print("Bound original minimap marker dialogs:", len(bindings), "native assets")


if __name__ == "__main__":
    build()
