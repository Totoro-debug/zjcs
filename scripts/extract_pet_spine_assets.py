"""Extract pinned pet Spine skeletons, atlases, and textures from verified Unity bundles."""
import argparse
import hashlib
import io
import json
from pathlib import Path
import shutil
import sys

ROOT = Path(__file__).resolve().parents[1]
PET_MODELS = ROOT / "assets/official/pet-models.json"
PET_RUNTIME = ROOT / "unity/Assets/Game/Resources/PetGacha/pet-gacha-runtime.json"
INPUT_DIR = ROOT / "output/character-pet-spine-inputs"
OUTPUT_DIR = ROOT / "output/pet-gacha-model-extract"
ASSET_RIPPER_ROOTS = (
    ROOT / "output/pet-gacha-model-native-all/ExportedProject/Assets/AppearanceAssets/Pet",
    ROOT / "output/pet-gacha-model-native/ExportedProject/Assets/AppearanceAssets/Pet",
)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def sha256(path):
    return digest(path.read_bytes())


def unique_asset(destination, name, value):
    previous = destination.get(name)
    if previous is not None and previous != value:
        raise ValueError("Conflicting official pet Spine assets share a name: " + name)
    destination[name] = value


def skeleton_reference(root):
    found = []
    pending = [root]
    while pending:
        node = pending.pop()
        if not isinstance(node, dict):
            continue
        value = node.get("pet_skeleton")
        if isinstance(value, str):
            found.append(value)
        pending.extend(node.get("children", []))
    found = sorted(set(found))
    if len(found) != 1:
        raise ValueError("An official pet prefab must resolve to one Spine skeleton reference.")
    return found[0]


def find_page_texture(skeleton):
    candidates = [name for name in skeleton.get("textures", {})
                  if not name.endswith("_Low.png") and "placeholder" not in name.lower()]
    if len(candidates) != 1:
        raise ValueError("An official pet skeleton must resolve to one high-resolution atlas page.")
    return candidates[0]


def meta_templates():
    templates = {}
    patterns = {
        ".skel.bytes": "pet_*.skel.bytes.meta",
        ".atlas.txt": "pet_*.atlas.txt.meta",
        ".png": "pet_*.png.meta",
    }
    for extension, pattern in patterns.items():
        for root in ASSET_RIPPER_ROOTS:
            matches = sorted(root.glob("pet_*" + "/" + pattern.split("/", 1)[-1]))
            if matches:
                templates[extension] = matches[0]
                break
        if extension not in templates:
            raise FileNotFoundError("AssetRipper importer metadata is unavailable for " + extension)
    return templates


def extract(input_dir, output_dir):
    try:
        import UnityPy
    except ImportError:
        sys.path.insert(0, str(ROOT / ".tools/character-art-python"))
        import UnityPy

    stage_manifest_path = input_dir / "source-manifest.json"
    if not stage_manifest_path.is_file():
        raise FileNotFoundError("The verified Unity bundle input manifest is missing.")
    stage_manifest = json.loads(stage_manifest_path.read_text(encoding="utf-8"))
    if stage_manifest.get("manifest") != "854_156383":
        raise ValueError("Pet Spine source inputs must use manifest 854_156383.")

    pet_models = json.loads(PET_MODELS.read_text(encoding="utf-8"))
    runtime = json.loads(PET_RUNTIME.read_text(encoding="utf-8"))
    profiles = runtime.get("CharacterPetProfiles", [])
    if runtime.get("Manifest") != "854_156383" or len(profiles) != 21:
        raise ValueError("The official character pet directory must contain all 21 pinned models.")

    requested = set(stage_manifest.get("requested_bundles", []))
    requested_skeletons = set()
    requested_textures = set()
    for profile in profiles:
        model_id = int(profile["UIModel"])
        model = pet_models["models"].get(str(model_id))
        if model is None:
            raise KeyError("The official pet model index is missing: " + str(model_id))
        source = model.get("source", {})
        if source.get("manifest") != "854_156383" or source.get("bundle") not in requested:
            raise ValueError("The official pet prefab was not staged: " + str(model_id))
        reference = skeleton_reference(model["root"])
        skeleton = pet_models["skeletons"].get(reference)
        if skeleton is None:
            raise KeyError("The official pet skeleton index is missing: " + str(model_id))
        texture_file = find_page_texture(skeleton)
        source_stem = Path(texture_file).stem
        requested_skeletons.add(source_stem + ".skel")
        requested_skeletons.add(source_stem + ".atlas")
        requested_textures.add(source_stem)

    environment = UnityPy.load(str(input_dir))
    text_assets = {}
    for asset in environment.objects:
        if asset.type.name != "TextAsset":
            continue
        data = asset.read()
        if data.m_Name not in requested_skeletons:
            continue
        script = data.m_Script
        if isinstance(script, str):
            script = script.encode("utf-8", "surrogateescape")
        if not isinstance(script, bytes):
            raise TypeError("A pinned pet TextAsset has an unsupported byte representation: " + data.m_Name)
        unique_asset(text_assets, data.m_Name, script)

    textures = {}
    for asset in environment.objects:
        if asset.type.name != "Texture2D":
            continue
        data = asset.read()
        if data.m_Name not in requested_textures:
            continue
        image = data.image
        encoded = io.BytesIO()
        image.save(encoded, format="PNG")
        value = (image.size, encoded.getvalue())
        previous = textures.get(data.m_Name)
        if previous is not None and previous[0] != value[0]:
            raise ValueError("Conflicting official pet textures share a name: " + data.m_Name)
        unique_asset(textures, data.m_Name, value)

    missing_assets = requested_skeletons.difference(text_assets) | requested_textures.difference(textures)
    if missing_assets:
        raise FileNotFoundError("Official pet Spine assets are missing from the staged CAB closure: "
                                + ", ".join(sorted(missing_assets)))

    templates = meta_templates()
    output_assets = output_dir / "ExportedProject/Assets/AppearanceAssets/Pet"
    if output_dir.exists():
        raise FileExistsError("Refusing to overwrite a pet model extraction directory: " + str(output_dir))

    serialized_sources = {row["serialized_file"]: row["source"]
                          for row in stage_manifest.get("serialized_files", [])}
    model_records = []
    for profile in sorted(profiles, key=lambda value: value["ClassId"]):
        model_id = int(profile["UIModel"])
        model = pet_models["models"][str(model_id)]
        reference = skeleton_reference(model["root"])
        skeleton = pet_models["skeletons"].get(reference)
        if skeleton is None:
            raise KeyError("The official pet skeleton index is missing: " + str(model_id))
        texture_file = find_page_texture(skeleton)
        source_stem = Path(texture_file).stem
        skeleton_name = source_stem + ".skel"
        atlas_name = source_stem + ".atlas"
        if skeleton_name not in text_assets or atlas_name not in text_assets:
            raise FileNotFoundError("The referenced official pet Spine pair is unavailable: " + source_stem)
        if source_stem not in textures:
            raise FileNotFoundError("The referenced official pet Spine texture is unavailable: " + source_stem)

        atlas_bytes = text_assets[atlas_name]
        page, separator, rest = atlas_bytes.partition(b"\n")
        if page.rstrip(b"\r").decode("utf-8", "strict") != texture_file or not separator:
            raise ValueError("The official pet atlas page does not match its referenced texture: " + source_stem)

        target_stem = "pet_" + str(model_id)
        target_dir = output_assets / target_stem
        target_dir.mkdir(parents=True)
        output_files = {
            target_stem + ".skel.bytes": text_assets[skeleton_name],
            target_stem + ".atlas.txt": (target_stem + ".png").encode("ascii") + separator + rest,
            target_stem + ".png": textures[source_stem][1],
        }
        records = []
        for filename, contents in output_files.items():
            path = target_dir / filename
            path.write_bytes(contents)
            extension = ".skel.bytes" if filename.endswith(".skel.bytes") \
                else ".atlas.txt" if filename.endswith(".atlas.txt") else ".png"
            shutil.copyfile(templates[extension], Path(str(path) + ".meta"))
            records.append({"file": filename, "bytes": len(contents), "sha256": digest(contents)})

        serialized_name = reference.split(":", 1)[0]
        skeleton_source = serialized_sources.get(serialized_name)
        if not skeleton_source:
            raise ValueError("The official pet skeleton source is not in the staged CAB closure: " + str(model_id))
        model_records.append({
            "pet_class_id": int(profile["ClassId"]), "ui_model": model_id,
            "skeleton_asset": source_stem + ".skel", "texture_asset": texture_file,
            "skeleton_reference": reference, "skeleton_source_bundle": skeleton_source,
            "source_skeleton_sha256": digest(text_assets[skeleton_name]),
            "source_atlas_sha256": digest(atlas_bytes),
            "source_texture_sha256": digest(textures[source_stem][1]),
            "files": records,
        })

    output_dir.mkdir(parents=True, exist_ok=True)
    manifest = {
        "manifest": "854_156383",
        "generator": "scripts/extract_pet_spine_assets.py; UnityPy 1.25.4",
        "inputs": {
            "assets/official/pet-models.json": sha256(PET_MODELS),
            "unity/Assets/Game/Resources/PetGacha/pet-gacha-runtime.json": sha256(PET_RUNTIME),
            "output/character-pet-spine-inputs/source-manifest.json": sha256(stage_manifest_path),
        },
        "models": model_records,
    }
    manifest_path = output_dir / "source-manifest.json"
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Extracted", len(model_records), "official pet Spine models from the verified CAB closure.")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input-dir", type=Path, default=INPUT_DIR)
    parser.add_argument("--output-dir", type=Path, default=OUTPUT_DIR)
    args = parser.parse_args()
    input_dir = args.input_dir.resolve()
    output_dir = args.output_dir.resolve()
    if not input_dir.is_relative_to((ROOT / "output").resolve()):
        raise ValueError("Pet Spine inputs must stay under repository output/.")
    if not output_dir.is_relative_to((ROOT / "output").resolve()):
        raise ValueError("Pet Spine extraction must stay under repository output/.")
    extract(input_dir, output_dir)


if __name__ == "__main__":
    main()
