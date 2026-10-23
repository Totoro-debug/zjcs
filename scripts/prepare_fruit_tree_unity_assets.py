"""Import the official fruit tree ParticleSystems and exact material closure."""
from __future__ import annotations

import argparse
import hashlib
import json
import re
import uuid
from collections import deque
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
EXPORT_ROOT = ROOT / "output" / "fruit-tree-emitter-native-decompile-20261023" / "ExportedProject" / "Assets"
SOURCE_PREFAB = Path("Prefabs/Home/Dynamic/Furniture/FruitTree/FruitTree.prefab")
OUTPUT_ROOT = ROOT / "unity" / "Assets" / "Official" / "Home" / "Resources" / "FruitTree"
OUTPUT_PREFAB = OUTPUT_ROOT / "FruitTreeParticles.prefab"
PROJECT_ASSETS = ROOT / "unity" / "Assets"
MANIFEST = ROOT / "assets" / "reference" / "fruit-tree" / "source-manifest.json"
EXPECTED_PARTICLE_SYSTEMS = 23
EXPECTED_RENDERERS = 24
SOURCE_BUNDLE = "defaultpackage_assets_prefabs_home_dynamic_furniture_fruittree.bundle"
SOURCE_BUNDLE_MD5 = "e1aa24ba2b413ea677ef3f58a05a9625"
DOC_PATTERN = re.compile(r"(?m)(?=^--- !u!)")
DOC_HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)", re.M)
GUID_PATTERN = re.compile(r"guid: ([0-9a-f]{32})")
COMPONENT_PATTERN = re.compile(r"(?m)^  - component: \{fileID: (-?\d+)\}\r?\n")
SCRIPT_EXTENSIONS = {".cs"}
TEXT_ASSET_EXTENSIONS = {
    ".prefab", ".asset", ".mat", ".anim", ".controller", ".overrideController",
    ".shader", ".cginc", ".hlsl", ".compute", ".renderTexture",
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def guid_from_meta(path: Path) -> str | None:
    match = re.search(r"(?m)^guid: ([0-9a-f]{32})$", path.read_text(encoding="utf-8"))
    return match.group(1) if match else None


def parse_documents(text: str):
    sections = DOC_PATTERN.split(text)
    header = sections[0]
    docs = {}
    for section in sections[1:]:
        match = DOC_HEADER.match(section)
        if match:
            docs[match.group(2)] = [int(match.group(1)), section]
    return header, docs


def property_id(text: str, key: str) -> str | None:
    match = re.search(rf"(?m)^  {re.escape(key)}: \{{fileID: (-?\d+)\}}", text)
    return match.group(1) if match else None


def game_object_name(document: str) -> str:
    match = re.search(r"(?m)^  m_Name: (.*)$", document)
    return match.group(1) if match else ""


def rewrite_children(document: str, child_ids: list[str]) -> str:
    match = re.search(r"(?ms)^  m_Children:.*?(?=^  m_Father:)", document)
    if not match:
        raise ValueError("Fruit tree Transform has no m_Children field")
    body = "  m_Children: []\n" if not child_ids else "  m_Children:\n" + "".join(
        f"  - {{fileID: {child_id}}}\n" for child_id in child_ids
    )
    return document[:match.start()] + body + document[match.end():]


def retain_particle_hierarchy(source: Path) -> tuple[bytes, dict]:
    original = source.read_text(encoding="utf-8")
    header, docs = parse_documents(original)
    game_objects = {file_id: value for file_id, value in docs.items() if value[0] == 1}
    transform_to_object = {}
    object_to_transform = {}
    object_components = {}
    for file_id, (_, document) in game_objects.items():
        component_ids = [match.group(1) for match in COMPONENT_PATTERN.finditer(document)]
        object_components[file_id] = component_ids
        for component_id in component_ids:
            component = docs.get(component_id)
            if component and component[0] in (4, 224):
                object_to_transform[file_id] = component_id
                transform_to_object[component_id] = file_id

    parent_objects = {}
    child_transforms = {}
    particle_objects = set()
    particle_systems = 0
    particle_renderers = 0
    for file_id, component_ids in object_components.items():
        classes = {docs[component_id][0] for component_id in component_ids if component_id in docs}
        particle_systems += int(198 in classes)
        particle_renderers += int(199 in classes)
        if 198 in classes or 199 in classes:
            particle_objects.add(file_id)
    if particle_systems != EXPECTED_PARTICLE_SYSTEMS or particle_renderers != EXPECTED_RENDERERS:
        raise ValueError(
            f"Expected {EXPECTED_PARTICLE_SYSTEMS} ParticleSystems and {EXPECTED_RENDERERS} renderers; "
            f"found {particle_systems} and {particle_renderers}"
        )

    for object_id, transform_id in object_to_transform.items():
        transform = docs[transform_id][1]
        father = property_id(transform, "m_Father")
        parent_object = transform_to_object.get(father) if father and father != "0" else None
        if parent_object:
            parent_objects[object_id] = parent_object
        children_match = re.search(r"(?ms)^  m_Children:.*?(?=^  m_Father:)", transform)
        child_transforms[object_id] = (
            re.findall(r"(?m)^  - \{fileID: (-?\d+)\}", children_match.group(0))
            if children_match else []
        )

    retained = set(particle_objects)
    for object_id in tuple(particle_objects):
        while object_id in parent_objects:
            object_id = parent_objects[object_id]
            retained.add(object_id)
    roots = [object_id for object_id in retained if object_id not in parent_objects]
    if len(roots) != 1 or game_object_name(game_objects[roots[0]][1]) != "FruitTree":
        raise ValueError("FruitTree particle hierarchy does not have a single FruitTree root")

    retained_components = set()
    for object_id in retained:
        retained_components.update(
            component_id for component_id in object_components[object_id]
            if component_id in docs and docs[component_id][0] in (4, 224, 198, 199)
        )

    output_docs = []
    for file_id, (class_id, document) in docs.items():
        if class_id == 1:
            if file_id not in retained:
                continue
            document = re.sub(r"(?m)^(  m_IsActive: )0$", r"\g<1>1", document)
            document = COMPONENT_PATTERN.sub(
                lambda match: match.group(0) if match.group(1) in retained_components else "",
                document,
            )
        elif class_id in (4, 224):
            object_id = property_id(document, "m_GameObject")
            if object_id not in retained:
                continue
            children = [child_id for child_id in child_transforms.get(object_id, [])
                if transform_to_object.get(child_id) in retained]
            document = rewrite_children(document, children)
        elif class_id in (198, 199):
            object_id = property_id(document, "m_GameObject")
            if object_id not in retained:
                continue
            retained_components.add(file_id)
        else:
            continue
        output_docs.append(document)

    output = (header + "".join(output_docs)).encode("utf-8")
    return output, {
        "particle_system_count": particle_systems,
        "particle_renderer_count": particle_renderers,
        "retained_game_object_count": len(retained),
    }


def build_guid_index(export_root: Path) -> dict[str, Path]:
    result = {}
    for meta in export_root.rglob("*.meta"):
        guid = guid_from_meta(meta)
        asset = meta.with_suffix("")
        if not guid or not asset.is_file():
            continue
        existing = result.get(guid)
        if existing and existing != asset:
            raise ValueError(f"Duplicate exported GUID {guid}: {existing} and {asset}")
        result[guid] = asset
    return result


def build_project_guid_index(excluded_root: Path) -> dict[str, Path]:
    result = {}
    for meta in PROJECT_ASSETS.rglob("*.meta"):
        if excluded_root in meta.parents:
            continue
        guid = guid_from_meta(meta)
        if not guid:
            continue
        asset = meta.with_suffix("")
        if not asset.is_file():
            continue
        existing = result.get(guid)
        if existing and existing != asset:
            raise ValueError(f"Duplicate project GUID {guid}: {existing} and {asset}")
        result[guid] = asset
    return result


def text_dependencies(path: Path, payload: bytes | None = None) -> tuple[set[str], set[str]]:
    if path.suffix not in TEXT_ASSET_EXTENSIONS:
        return set(), set()
    text = payload.decode("utf-8", errors="replace") if payload is not None else path.read_text(
        encoding="utf-8", errors="replace"
    )
    guids = set(GUID_PATTERN.findall(text))
    includes = set()
    for match in re.finditer(r"(?m)#include\s*[\"<]([^\">]+)[\">]", text):
        includes.add(match.group(1))
    return guids, includes


def find_include(source: Path, include: str, export_root: Path, filename_index: dict[str, list[Path]]) -> Path | None:
    if include.startswith(("Unity", "Packages/", "com.unity.")):
        return None
    candidates = [source.parent / include, export_root / include]
    for candidate in candidates:
        if candidate.is_file() and export_root in candidate.parents:
            return candidate
    matches = filename_index.get(Path(include).name.casefold(), [])
    matches = [path for path in matches if export_root in path.parents]
    if len(matches) == 1:
        return matches[0]
    if len(matches) > 1:
        raise ValueError(f"Shader include is ambiguous: {include} from {source}")
    raise ValueError(f"Shader include is missing from the AssetRipper export: {include} from {source}")


def relative_target(source: Path, export_root: Path) -> Path:
    return OUTPUT_ROOT / "Dependencies" / source.relative_to(export_root)


def import_assets(export_root: Path) -> dict:
    source_prefab = export_root / SOURCE_PREFAB
    if not source_prefab.is_file():
        raise FileNotFoundError(f"AssetRipper prefab is missing: {source_prefab}")
    OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    exported_guids = build_guid_index(export_root)
    project_guids = build_project_guid_index(OUTPUT_ROOT)
    prefab_bytes, prefab_info = retain_particle_hierarchy(source_prefab)
    source_meta = source_prefab.with_name(source_prefab.name + ".meta")
    if not source_meta.is_file() or not guid_from_meta(source_meta):
        raise ValueError("AssetRipper prefab metadata is missing or has no stable GUID")

    staged: dict[Path, tuple[Path, bytes | None]] = {OUTPUT_PREFAB: (source_prefab, prefab_bytes)}
    guids, includes = text_dependencies(source_prefab, prefab_bytes)
    pending_guids = deque(guids)
    visited_guids = set()
    source_files = [path for path in export_root.rglob("*") if path.is_file()]
    filename_index: dict[str, list[Path]] = {}
    for path in source_files:
        if path.suffix != ".meta":
            filename_index.setdefault(path.name.casefold(), []).append(path)
    include_queue = deque((source_prefab, include) for include in includes)
    copied_by_guid: dict[str, Path] = {}
    external_references = []

    while pending_guids or include_queue:
        while pending_guids:
            guid = pending_guids.popleft()
            if guid in visited_guids:
                continue
            visited_guids.add(guid)
            source = exported_guids.get(guid)
            if source is None:
                existing = project_guids.get(guid)
                if existing is not None:
                    external_references.append({"guid": guid, "project_path": existing.relative_to(ROOT).as_posix()})
                    continue
                if set(guid) == {"0"}:
                    continue
                raise ValueError(f"Official fruit tree dependency GUID is unavailable: {guid}")
            if source.suffix in SCRIPT_EXTENSIONS or "Scripts" in source.parts:
                raise ValueError(f"Unexpected official script reference in particle assets: {source}")
            target = relative_target(source, export_root)
            staged[target] = (source, None)
            copied_by_guid[guid] = target
            source_meta_path = source.with_name(source.name + ".meta")
            if not source_meta_path.is_file():
                raise ValueError(f"Official fruit tree dependency has no .meta: {source}")
            staged[target.with_name(target.name + ".meta")] = (source_meta_path, None)
            child_guids, child_includes = text_dependencies(source)
            pending_guids.extend(child_guids - visited_guids)
            include_queue.extend((source, include) for include in child_includes)

        while include_queue:
            parent, include = include_queue.popleft()
            include_path = find_include(parent, include, export_root, filename_index)
            if include_path is None:
                continue
            target = relative_target(include_path, export_root)
            staged[target] = (include_path, None)
            include_meta = include_path.with_name(include_path.name + ".meta")
            if include_meta.is_file():
                staged[target.with_name(target.name + ".meta")] = (include_meta, None)
            _, nested_includes = text_dependencies(include_path)
            include_queue.extend((include_path, nested) for nested in nested_includes)

    source_prefab_guid = guid_from_meta(source_meta)
    staged[OUTPUT_PREFAB.with_name(OUTPUT_PREFAB.name + ".meta")] = (source_meta, None)
    for target, (source, payload) in staged.items():
        if target.suffix == ".meta":
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(source.read_bytes())
            continue
        meta_source = staged.get(target.with_name(target.name + ".meta"), (None, None))[0]
        guid = guid_from_meta(meta_source) if meta_source else None
        if guid and guid in project_guids and project_guids[guid] != target:
            current = project_guids[guid]
            if current.read_bytes() != (payload if payload is not None else source.read_bytes()):
                raise ValueError(f"Fruit tree asset GUID conflicts with project asset: {guid} ({current})")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(payload if payload is not None else source.read_bytes())

    folders = set()
    for target in staged:
        folder = target.parent
        while folder != PROJECT_ASSETS and PROJECT_ASSETS in folder.parents:
            folders.add(folder)
            folder = folder.parent
    for folder in folders:
        folder_meta = folder.with_name(folder.name + ".meta")
        if folder_meta.is_file():
            continue
        folder_guid = uuid.uuid5(uuid.NAMESPACE_URL, "zjcs:" + folder.relative_to(PROJECT_ASSETS).as_posix()).hex
        folder_meta.write_text(
            "fileFormatVersion: 2\n"
            f"guid: {folder_guid}\n"
            "folderAsset: yes\n"
            "DefaultImporter:\n"
            "  externalObjects: {}\n"
            "  userData:\n"
            "  assetBundleName:\n"
            "  assetBundleVariant:\n",
            encoding="utf-8",
        )

    output_records = []
    for target, (source, payload) in staged.items():
        if target.suffix == ".meta":
            continue
        output_records.append({
            "source": source.relative_to(export_root).as_posix(),
            "output": target.relative_to(ROOT).as_posix(),
            "source_sha256": sha256(source),
            "output_sha256": hashlib.sha256(payload if payload is not None else source.read_bytes()).hexdigest(),
            "guid": guid_from_meta(target.with_name(target.name + ".meta")),
        })

    source_manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))
    source_manifest["unity_native_assets"] = {
        "source_manifest": "854_156383",
        "source_bundle": SOURCE_BUNDLE,
        "source_bundle_md5": SOURCE_BUNDLE_MD5,
        "asset_ripper_version": "1.3.14",
        "shader_export_mode": "Decompile",
        "source_prefab": SOURCE_PREFAB.as_posix(),
        "source_prefab_sha256": sha256(source_prefab),
        "output_prefab": OUTPUT_PREFAB.relative_to(ROOT).as_posix(),
        "output_prefab_sha256": hashlib.sha256(prefab_bytes).hexdigest(),
        **prefab_info,
        "resource_key": "FruitTree/FruitTreeParticles",
        "retained_dependency_count": len(output_records) - 1,
        "external_project_references": external_references,
        "assets": output_records,
        "operation": "Preserved official ParticleSystem, ParticleSystemRenderer, Transform hierarchy, material, shader, texture, and mesh data; removed non-particle scene objects and client MonoBehaviours for Unity offline runtime binding.",
    }
    MANIFEST.write_text(json.dumps(source_manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return source_manifest["unity_native_assets"]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--export-root", type=Path, default=EXPORT_ROOT)
    args = parser.parse_args()
    result = import_assets(args.export_root.resolve())
    print(
        "Imported fruit tree native particles: "
        f"{result['particle_system_count']} systems, {result['particle_renderer_count']} renderers, "
        f"{result['retained_dependency_count']} dependencies, output "
        f"{result['output_prefab_sha256']}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
