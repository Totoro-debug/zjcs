import hashlib
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "scripts"))
from official_apk import Apk
from official_unity_schema import ScriptSchemas


def sha256(value):
    return hashlib.sha256(value).hexdigest()


def main():
    manifest_path = ROOT / "assets/reference/skill-quality-text/source-manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest["manifest"] != "854_156383":
        raise SystemExit("Skill quality source must remain pinned to official manifest 854_156383")
    apk = Apk()
    schemas = ScriptSchemas()

    def read(entry):
        if apk.bundle_records[entry["bundle"]]["hash"].lower() != entry["bundle_hash"].lower():
            raise ValueError("Bundle hash mismatch: " + entry["bundle"])
        obj = next(item for item in apk.objects(entry["bundle"])
                   if str(item.path_id) == entry["path_id"])
        if sha256(obj.get_raw_data()).lower() != entry["sha256"].lower():
            raise ValueError("Serialized source object hash mismatch: " + entry["path_id"])
        value = schemas.read(obj)
        if apk.provenance(obj)["bundle_hash"].lower() != entry["bundle_hash"].lower():
            raise ValueError("Object provenance mismatch: " + entry["path_id"])
        return value

    palette = read(manifest["palette"])
    normal = next(item for item in palette["QualityColorConfig"]["TextColorLists"]
                  if item["ThemeColorType"] == 0)
    color = normal["TextColors"][7]
    gradient = color["GradientColor"]
    colors = [{"color": {key: gradient[f"key{i}"][key] for key in "rgba"},
               "time": gradient[f"ctime{i}"] / 65535.0}
              for i in range(gradient["m_NumColorKeys"])]
    alphas = [{"alpha": gradient[f"key{i}"]["a"],
               "time": gradient[f"atime{i}"] / 65535.0}
              for i in range(gradient["m_NumAlphaKeys"])]
    components = []
    for entry in manifest["components"]:
        if entry["script"] != "TMPColorGradient":
            continue
        value = read(entry)
        components.append({"path": entry["path"], "angle": value["Angle"]})
    output = {"quality": 7, "theme": 0, "mode": gradient["m_Mode"],
              "colorSpace": gradient["m_ColorSpace"], "colors": colors,
              "alphas": alphas, "components": components,
              "normalColors": [entry["Color"] for entry in normal["TextColors"]]}
    path = ROOT / "unity/Assets/Official/Skills/skill-quality-gradient.json"
    path.write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    manifest["outputs"][path.relative_to(ROOT).as_posix()] = sha256(path.read_bytes())
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    payload_receipt_path = path.parent / "source-manifest.json"
    payload_receipt = json.loads(payload_receipt_path.read_text(encoding="utf-8"))
    record = next(record for record in payload_receipt["files"]
                  if record["path"] == path.relative_to(ROOT / "unity").as_posix())
    record.update(bytes=path.stat().st_size, sha256=sha256(path.read_bytes()))
    payload_receipt_path.write_text(json.dumps(payload_receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(sha256(path.read_bytes()), path.relative_to(ROOT))


if __name__ == "__main__":
    main()
