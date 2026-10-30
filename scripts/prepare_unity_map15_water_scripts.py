"""Migrate the exact 871 water and transparent-mask scripts to Unity."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import uuid


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / ".tools/source-packages/current/871_156598/official-code"
DESTINATION = ROOT / "unity/Assets/Game/Runtime/Official/World/NativeWater"
RECEIPT = ROOT / "assets/reference/official-map/map15-water-script-source-manifest.json"
ASSETS = {
    "Pond.cs": ("customwater-source/Pond.cs", "efa12caf542d835e937d1b9e4d87b4cdbed600998b55cfc6fdfde6627a046b12"),
    "WaterMaskGenerator.cs": ("customwater-source/WaterMaskGenerator.cs", "71f8dbc4ab1c56ee7f87da068862d737fe616b30fc841c275d22818f146a404f"),
    "WaterConfig.cs": ("customwater-source/WaterConfig.cs", "3c87a7e4daa6a1b50ed5f8b0daddc791a399980c48e1166a8d1f1796315b85e1"),
    "TransparentMask.cs": ("client-source/TransparentMask.cs", "006db31fc491dbd390843ca33e77ab9004054c428b1f961f4ecee83f400ac2b9"),
}
ORIGINAL_GUIDS = {
    "Pond.cs": "b57cc1d0897e99ce6b0d11567db9faa3",
    "WaterMaskGenerator.cs": "c5d55a7e5bde9e4ce182efab6fa4cb3d",
    "TransparentMask.cs": "4f0384e5dca07e4017a7b5e40d37882b",
}


def migrate(name: str, source: Path) -> bytes:
    text = source.read_text(encoding="utf-8")
    if name in {"Pond.cs", "WaterMaskGenerator.cs"}:
        marker = "using UnityEngine;\n"
        if marker not in text:
            raise ValueError("Official water source import block changed: " + name)
        text = text.replace(marker, marker + "using Zjcs.Official.World.NativeWater;\n", 1)
    return text.encode("utf-8")


def script_guid(name: str) -> str:
    return uuid.uuid5(uuid.NAMESPACE_URL, "zjcs-map15-official-water:" + name).hex


def build() -> None:
    generated = []
    for name, (relative, expected_hash) in ASSETS.items():
        source = SOURCE / relative
        raw = source.read_bytes()
        if hashlib.sha256(raw).hexdigest() != expected_hash:
            raise ValueError("Pinned 871 official water source hash mismatch: " + relative)
        target = DESTINATION / name
        content = migrate(name, source)
        target.write_bytes(content)
        meta = (
            "fileFormatVersion: 2\n"
            "guid: " + script_guid(name) + "\n"
            "MonoImporter:\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 2\n"
            "  defaultReferences: []\n"
            "  executionOrder: 0\n"
            "  icon: {instanceID: 0}\n"
            "  userData:\n"
            "  assetBundleName:\n"
            "  assetBundleVariant:\n"
        )
        Path(str(target) + ".meta").write_text(meta, encoding="utf-8", newline="\n")
        generated.append(dict(name=name, source=relative, source_sha256=expected_hash,
                              output_sha256=hashlib.sha256(content).hexdigest(),
                              original_script_guid=ORIGINAL_GUIDS.get(name),
                              output_path=target.relative_to(ROOT).as_posix(),
                              script_guid=script_guid(name)))
    receipt = dict(format=1, manifest="871_156598", assets=generated,
                   operation="Copy exact selected official sources after SHA256 verification. Add only NativeWater namespace imports to Pond and WaterMaskGenerator so they bind the existing migrated WaterMaskRenderer. Preserve original class fields and method bodies.")
    RECEIPT.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    for record in generated:
        print(record["name"], record["source_sha256"], record["output_sha256"], record["script_guid"])


if __name__ == "__main__":
    build()
