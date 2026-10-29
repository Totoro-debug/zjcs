"""Bind the pinned APK's native TMP font without sharing mutable map atlases."""
import hashlib
import json
import re
import subprocess
import uuid
from pathlib import Path

from official_apk import ROOT


def digest(data):
    return hashlib.sha256(data).hexdigest()


def restore(reader):
    dest = ROOT / "unity/Assets/Official/Skills/NativeText"
    dest.mkdir(parents=True, exist_ok=True)
    result = {}
    for name, receipt_name in (("font", "map13-native-text-font-source-manifest.json"),
                               ("shader", "map13-text-shader-source-manifest.json")):
        receipt_path = ROOT / "assets/reference/official-map" / receipt_name
        receipt = json.loads(receipt_path.read_text(encoding="utf-8"))
        inputs = []
        for row in receipt["inputs"]:
            obj = reader.object(row["source"]["bundle"], row["source"]["path_id"])
            assert digest(obj.get_raw_data()) == row["object_sha256"], row["source"]
            inputs.append({"source": reader.apk.provenance(obj), "object_sha256": row["object_sha256"]})
        outputs = []
        if name == "shader":
            for row in receipt["outputs"]:
                assert digest((ROOT / row["path"]).read_bytes()) == row["sha256"], row["path"]
                outputs.append(row)
        else:
            # Unity's TMP build processor clears dynamic font atlases in shared assets.
            # Recover the original Alpha8 bytes rather than accepting that mutation.
            atlas_row = receipt["inputs"][2]
            atlas = reader.object(atlas_row["source"]["bundle"], atlas_row["source"]["path_id"]).read()
            pixels = atlas.get_image_data()
            assert digest(pixels) == receipt["atlas"]["sha256"]
            atlas_record = next(row for row in receipt["outputs"] if row["path"].endswith("Atlas.texture.asset"))
            atlas_bytes = None
            revisions = subprocess.check_output(
                ["git", "log", "--all", "--format=%H", "--", atlas_record["path"]],
                cwd=ROOT, text=True).splitlines()
            for revision in revisions:
                candidate = subprocess.check_output(
                    ["git", "show", revision + ":" + atlas_record["path"]], cwd=ROOT)
                if digest(candidate) == atlas_record["sha256"]:
                    atlas_bytes = candidate
                    break
            assert atlas_bytes is not None, atlas_record["path"]
            serialized_pixels = bytes.fromhex(re.search(rb"(?m)^  _typelessdata: ([0-9a-f]+)$", atlas_bytes)[1].decode())
            assert serialized_pixels == pixels
            redirects = {}
            payloads = []
            for row in receipt["outputs"]:
                source = ROOT / row["path"]
                data = atlas_bytes if source.name.endswith("Atlas.texture.asset") else source.read_bytes()
                assert digest(data) == row["sha256"], row["path"]
                metadata = source.with_name(source.name + ".meta").read_text(encoding="utf-8")
                assert digest(metadata.encode()) == row["meta_sha256"]
                old_guid = re.search(r"(?m)^guid: (\w+)$", metadata)[1]
                guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-official-skills/native-text/" + source.name).hex
                redirects[old_guid] = guid
                payloads.append((dest / source.name, data, metadata, guid))
            for path, data, metadata, guid in payloads:
                if path.suffix != ".ttf":
                    text = data.decode("utf-8")
                    for before, after in redirects.items():
                        text = text.replace(before, after)
                    if path.name == "FZY4K-Dynamic.asset":
                        text, count = re.subn(r"(?m)^  m_ClearDynamicDataOnBuild: 1$", "  m_ClearDynamicDataOnBuild: 0", text)
                        assert count == 1
                    data = text.encode("utf-8")
                path.write_bytes(data)
                path.with_name(path.name + ".meta").write_text(
                    re.sub(r"(?m)^guid: \w+$", "guid: " + guid, metadata), encoding="utf-8")
                outputs.append({"path": path.relative_to(ROOT).as_posix(), "sha256": digest(data),
                                "meta_sha256": digest(path.with_name(path.name + ".meta").read_bytes())})
            folder_guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-official-skills/native-text").hex
            dest.with_name(dest.name + ".meta").write_text(
                "fileFormatVersion: 2\nguid: " + folder_guid + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n", encoding="utf-8")
        result[name] = {"conversion_reference": receipt_path.relative_to(ROOT).as_posix(),
                        "inputs": inputs, "outputs": outputs}
    result["adaptation"] = "Separate native font/material/atlas instances for skills; preserve the original initial atlas during Windows builds (m_ClearDynamicDataOnBuild 1 -> 0). Clone font, material and atlas at runtime before adding glyphs. Original glyph tables, Alpha8 pixels, font bytes and custom shader are unchanged."
    return result
