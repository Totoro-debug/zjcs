"""Verify the official profession service-loaded prefabs and achievement reward sprites."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from map13_native_schemas import NativeSchemas
from prepare_unity_profession_native_inputs import CACHE, ROOT_BUNDLES
from prepare_unity_profession_native_bindings import guid
from UnityPy.classes import PPtr
from prepare_unity_profession_reward_sprites import build as reward_sprite
from official_tables import Constructors


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha(path))


def build(unity_project=None):
    apk = Apk.current()
    if apk.version != "871_156598":
        raise ValueError("Profession dynamic source version differs")
    code = ROOT / ".tools/source-packages/current/871_156598/official-code"
    schemas = NativeSchemas(code, unity_project=unity_project)
    config = apk.asset("defaultpackage_assets_scriptableconfig_modules_itemicon.bundle", "/ItemIconManagerConfig.asset")
    tree = schemas.default.tree("Assembly-CSharp", "ItemIconManagerConfig")
    fields = config.read_typetree(nodes=tree)
    prefab = PPtr(**fields["DefPrefab"], assetsfile=config.assets_file).deref()
    roots = json.loads((CACHE / "typed/inputs/source-manifest.json").read_bytes())
    if roots["manifest"] != apk.version:
        raise ValueError("Profession dynamic input version differs")
    icon_seed = next(row for row in roots["dynamic_prefab_seeds"] if row["path"].endswith("/ItemIconDefault.prefab"))
    if str(prefab.path_id) != icon_seed["source"]["path_id"] or hashlib.sha256(prefab.get_raw_data()).hexdigest() != icon_seed["payload_sha256"]:
        raise ValueError("Default reward prefab does not match the native component source")
    rules_path = ROOT / "assets/official/rules.json"
    rules = json.loads(rules_path.read_bytes())
    constructors = Constructors(code)
    table_types = {match[2]: match[1] for match in re.finditer(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source("Tables"))}
    current, table_inputs = {}, []
    for name in ("achievement", "item", "rule_tip"):
        table = apk.asset("config_luban", "/" + name + ".g.bytes")
        raw = table.read().m_Script.encode("utf-8", "surrogateescape")
        current[name] = json.loads(json.dumps(constructors.table(raw, table_types[name]), ensure_ascii=False))
        table_inputs.append(dict(name=name, table_type=table_types[name], source=apk.provenance(table),
            payload_sha256=hashlib.sha256(table.get_raw_data()).hexdigest(), sha256=hashlib.sha256(raw).hexdigest(),
            consumed_bytes=len(raw), rows=len(current[name])))
    achievements = [row for row in current["achievement"] if row["Group"] == 10]
    if achievements != [row for row in rules["achievement"] if row["Group"] == 10]:
        raise ValueError("Packaged profession achievement rows differ from the selected current official table")
    if len(achievements) != 10 or {row["AchievementType"] for row in achievements} != {56}:
        raise ValueError("Inspect the official profession achievement reward scope")
    rewards = sorted({int(key) for row in achievements for key in row["Reward"]})
    exported = CACHE / "typed/export/ExportedProject/Assets"
    resources = []
    native_assets, reward_proofs = [], []
    for seed in roots["dynamic_prefab_seeds"]:
        path = CACHE / "typed/restored" / seed["path"]
        if not path.exists():
            path = exported / seed["path"]
        resources.append(dict(kind="prefab", native_relative_path=seed["path"], source=seed["source"],
            payload_sha256=seed["payload_sha256"], export_sha256=sha(path), export_guid=guid(path)))
    sprite_bundles = [name for name in ROOT_BUNDLES if "_image_itemicon_" in name]
    sprites = {}
    for bundle in sprite_bundles:
        for name, ptr in apk.container(bundle):
            obj = ptr.deref()
            if obj.type.name == "Sprite":
                sprites.setdefault(Path(name).stem, []).append((name, obj))
    for item_id in rewards:
        item = next(row for row in current["item"] if row["ClassId"] == item_id)
        if item != next(row for row in rules["item"] if row["ClassId"] == item_id):
            raise ValueError("Packaged profession reward item differs from current official table")
        if item_id in fields["IdList"] or item["ItemType"] in fields["TypeList"]:
            raise ValueError("Reward icon uses an override; follow its original prefab: " + str(item_id))
        matches = sprites.get(Path(item["RawIcon"]).name, [])
        if len(matches) != 1:
            raise ValueError("Reward sprite source is missing or ambiguous: " + item["RawIcon"])
        name, obj = matches[0]
        relative = name.removeprefix("Assets/")
        path = exported / relative
        meta = path.with_name(path.name + ".meta")
        if "  spriteMode: 1" not in meta.read_text(encoding="utf-8"):
            raise ValueError("Inspect the reward Sprite subasset fileID before binding")
        recovered = reward_sprite(apk, obj)
        recovered["item_id"] = item_id
        recovered["expected"]["path"] = "Assets/ProfessionDynamicResourcesProbe/" + recovered["sprite"]["native_relative_path"]
        recovered["expected"]["texture_path"] = "Assets/ProfessionDynamicResourcesProbe/" + recovered["texture"]["native_relative_path"]
        reward_proofs.append(recovered)
        native_assets.extend([recovered["sprite"], recovered["texture"]])
        resources.append(dict(kind="reward_sprite", item_id=item_id, item_type=item["ItemType"],
            quality=item["Quality"], logical_address=item["RawIcon"], icon_prefab=icon_seed["path"],
            native_relative_path=recovered["sprite"]["native_relative_path"], source=apk.provenance(obj),
            payload_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
            export_sha256=recovered["sprite"]["sha256"], export_guid=recovered["sprite"]["guid"],
            original_converter_output=dict(path=relative, sha256=sha(path), guid=guid(path), meta_sha256=sha(meta)),
            sprite_file_id=21300000))
    effect_proof_path = ROOT / "assets/reference/official-map/map13-text-effects-code-source-manifest.json"
    effect = json.loads(effect_proof_path.read_bytes())
    if effect["manifest"] != apk.version:
        raise ValueError("Native TMP effect source version differs")
    for row in effect["outputs"]:
        path = ROOT / row["path"]
        if sha(path) != row["sha256"] or sha(path.with_name(path.name + ".meta")) != row["meta_sha256"]:
            raise ValueError("Original TMPEffect port changed: " + str(path))
    rule_rows = [row for row in current["rule_tip"] if row["RuleKey"] == "ProfessionLevelInfo"]
    profession_data = ROOT / "assets/official/professions.json"
    packaged_rule = json.loads(profession_data.read_bytes())["level_rules"]
    orders = sorted(row["Order"] for row in rule_rows if row["Order"] != 0)
    if (len([row for row in rule_rows if row["Order"] == 0]) != 1 or
            packaged_rule["title_key"] != "rule_tip_ProfessionLevelInfo_0" or
            [row["key"] for row in packaged_rule["rows"]] != ["rule_tip_ProfessionLevelInfo_" + str(order) for order in orders]):
        raise ValueError("Original profession rule-tip row selection differs")
    constructor_sources = []
    for name in sorted(constructors.sources):
        path = code / "common-source/Common" / (name + ".cs")
        if not path.exists() or name in ("Pos2d", "Vector2", "Vector3"):
            path = code / "common-source/Common.LubanGen" / (name + ".cs")
        if constructors.sources[name]:
            constructor_sources.append(record(path))
    result = dict(format=1, manifest=apk.version, resources=resources, native_assets=native_assets, reward_sprite_proofs=reward_proofs,
        texture_decoder=record(ROOT / "scripts/prepare_unity_map13_native_scene.py"),
        reward_converter=record(ROOT / "scripts/prepare_unity_profession_reward_sprites.py"),
        current_tables=dict(inputs=table_inputs, constructor_sources=constructor_sources,
            interpreter=record(ROOT / "scripts/official_tables.py"), scope="All three tables fully decoded; only the group-10 achievement rows, its four item definitions and ProfessionLevelInfo rule-tip key selection are compared with the existing packaged projections."),
        rule_tip=dict(rows=rule_rows, packaged_projection=record(profession_data)),
        rules=record(rules_path), achievement_ids=[row["Id"] for row in achievements],
        schema_reference_inputs=[record(path) if path.is_relative_to(ROOT) else dict(path=str(path), sha256=sha(path)) for path in schemas.reference_inputs],
        icon_manager_config=dict(source=apk.provenance(config), payload_sha256=hashlib.sha256(config.get_raw_data()).hexdigest(),
            fields=fields, default_prefab=apk.provenance(prefab)),
        official_sources=[record(code / name) for name in (
            "client-source/ItemIconManager.cs", "client-source/ItemIconManagerConfig.cs", "client-source/IconConfigPool.cs",
            "client-source/ItemIcon.cs", "client-source/ItemProgress.cs", "client-source/RuleDialog.cs", "client-source/TipInfo.cs",
            "client-source/ProfessionOverviewPanel.cs", "common-source/Common/RuleTipInfoParser.cs")],
        reused_text_effect=record(effect_proof_path),
        reused_components=[dict(official=record(code / "client-source" / (name + ".cs")),
            runtime=record(ROOT / "unity/Assets/Game/Runtime/Official/WorldUI" / ("World" + name + ".cs")))
            for name in ("BgDialogElement", "ItemProgress")],
        validation=record(ROOT / "unity/Assets/Game/Editor/ProfessionUI/ProfessionRewardSpriteValidation.cs"),
        probe_generator=record(ROOT / "scripts/prepare_unity_profession_reward_probe.py"), generator=record(Path(__file__)),
        input_proof=record(ROOT / "assets/reference/profession-ui/native-input-source-manifest.json"),
        operation="Follow original ItemIconManager ID override then IconConfigPool type override/default branches; resolve all group-10 achievement rewards through their item RawIcon and original APK Sprite/container identities. Preserve original native prefabs and original Sprite importer references. Reuse the native-code-derived TMPEffect port only after checking its source version and every output hash.",
        limitations=["This source proof and explicit native dependency closure do not instantiate the profession UI.",
            "RuleDialog text data, ItemIcon runtime providers, portrait/animation, window/input and save integration remain required.",
            "Restored native Sprite geometry/UVs/texture flags require actual Unity import validation before runtime installation."])
    dest = ROOT / "assets/reference/profession-ui/dynamic-resources-source-manifest.json"
    dest.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Verified original profession dynamic resources:", len(resources), "resources;", len(rewards), "reward items")
    return result


if __name__ == "__main__":
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--unity-project", type=Path, help="Unity project providing actual compiled TMP/UGUI metadata")
    build(parser.parse_args().unity_project)
