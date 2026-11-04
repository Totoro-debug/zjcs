"""Verify the native monster graph fixture's real loadouts against selected sources."""
import hashlib
import argparse
import json
from pathlib import Path
import re
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / ".tools/python-packages"))
from official_apk import Apk, ROOT
from official_tables import Constructors
from current_component_schemas import CurrentSchemas
from UnityPy.classes import PPtr


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def verify(entity_ids=None):
    selection = ROOT / "unity/Assets/Game/Resources/Combat/monster-animation-controllers.json"
    catalog = json.loads(selection.read_bytes())
    available = {row["entityId"] for row in catalog["models"]}
    ids = available if entity_ids is None else set(entity_ids)
    if not ids or not ids <= available: raise ValueError("Selected native monster controller bindings are absent")
    selected_models = sorted((row for row in catalog["models"] if row["entityId"] in ids), key=lambda row: row["entityId"])
    battle = ROOT / "unity/Assets/Official/Combat/battle-rules.json"
    rules = ROOT / "unity/Assets/Official/Combat/rules.json"
    groups = {row["MonsterId"]: row["ActiveSkills"] for row in json.loads(battle.read_bytes())["tables"]["monster_group"]
              if row["MonsterId"] in ids and row["GroupNo"] == 0}
    wanted = {item for items in groups.values() for item in items}
    skills = {row["ClassId"]: row["EcEntityId"] for row in json.loads(rules.read_bytes())["skill"] if row["ClassId"] in wanted}
    apk = Apk.current()
    if apk.version != "884_156692":
        raise ValueError("Review the selected monster skill source version")
    code = ROOT / ".tools/source-packages/current" / apk.version / "official-code"
    parser = Constructors(code)
    mapping = {name: cls for cls, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source("Tables"))}
    originals = {}
    schemas = CurrentSchemas()
    original_bone_gaps = []
    try:
        for name in ("monster_group", "skill"):
            obj = apk.asset("config_luban", "/" + name + ".g.bytes")
            raw = obj.read().m_Script.encode("utf8", "surrogateescape")
            decoded = parser.table(raw, mapping[name])
            if name == "monster_group":
                rows = [row for row in decoded if row["MonsterId"] in ids and row["GroupNo"] == 0]
                if {row["MonsterId"]: row["ActiveSkills"] for row in rows} != groups:
                    raise ValueError("Runtime monster loadouts differ from the selected official group-zero table")
            else:
                rows = [row for row in decoded if row["ClassId"] in wanted]
                if {row["ClassId"]: row["EcEntityId"] for row in rows} != skills:
                    raise ValueError("Runtime skill item/entity bindings differ from the selected official table")
                if any(row["RandomSkillEntityId"] or row["DirectionEcEntityId"] for row in rows):
                    raise ValueError("The fixture must explicitly cover configured skill variants")
            originals[name] = dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                payload_sha256=sha(raw), rows=rows)
        # Record source-name closure independently of the converted prefabs.
        # This does not infer aliases or call an unresolved reference usable.
        for monster, item_ids in sorted(groups.items()):
            if not item_ids: continue
            objects = apk.objects(f"prefabs_ecentity_fight_fightmonster_{monster}")
            agent, = [obj for obj in objects if obj.type.name == "MonoBehaviour"
                and obj.read(check_read=False).m_Script.read().m_ClassName == "AttachAgent"]
            registry = schemas.read(agent)
            # AttachAgent.Init skips serialized null values. A name alone is
            # therefore not evidence of a usable original registration.
            names = {name for name, pointer in zip(registry["myBoneNameList"], registry["myBoneList"])
                     if pointer["m_PathID"]}
            for item in item_ids:
                skill = skills[item]
                objects = apk.objects(f"prefabs_ecentity_fight_fightskill_{skill}")
                owner, = [obj for obj in objects if obj.type.name == "MonoBehaviour"
                    and obj.read(check_read=False).m_Script.read().m_ClassName == "MyFightSkill"
                    and obj.read(check_read=False).m_GameObject.read().m_Name == str(skill)]
                fields = schemas.read(owner)
                pointer = fields["RightPlayablePrefab"]
                if not pointer["m_PathID"]: pointer = fields["myPlayablePrefab"]
                if not pointer["m_PathID"]: raise ValueError(f"The fixture's original right/default graph is missing: {skill}")
                graph = PPtr(**pointer, assetsfile=owner.assets_file).deref()
                pending = [graph]
                components = []
                while pending:
                    go = pending.pop()
                    for entry in go.read().m_Component:
                        obj = entry.component.deref()
                        if obj.type.name in ("Transform", "RectTransform"):
                            pending.extend(child.read().m_GameObject.deref() for child in obj.read().m_Children)
                        elif obj.type.name == "MonoBehaviour": components.append(obj)
                for obj in components:
                    if obj.type.name != "MonoBehaviour" or obj.read(check_read=False).m_Script.read().m_ClassName != "ApplyByBone": continue
                    fields = schemas.read(obj)
                    if fields["AttachAgentGoType"] == 1 and fields["BoneName"] not in names:
                        original_bone_gaps.append(dict(monster=monster, item=item, skill=skill, bone=fields["BoneName"],
                            reason="null-registration" if fields["BoneName"] in registry["myBoneNameList"] else "unregistered-name",
                            apply=dict(source=apk.provenance(obj), fields=fields, sha256=sha(obj.get_raw_data())),
                            registry=dict(source=apk.provenance(agent), fields=registry, sha256=sha(agent.get_raw_data()))))
    finally:
        apk.archive.close()
    paths = []
    for name in parser.sources:
        path = code / "common-source/Common" / (name + ".cs")
        if not path.exists():
            path = code / "common-source/Common.LubanGen" / (name + ".cs")
        if path.exists(): paths.append(path)
    fixture = ROOT / "unity/Assets/Game/Editor/ProfessionUI/OfficialBattleMonsterSceneValidation.ConfiguredGraphs.cs"
    result = dict(manifest=apk.version, selection=dict(path=selection.relative_to(ROOT).as_posix(), entity_ids=sorted(ids),
        models_sha256=sha(json.dumps(selected_models, sort_keys=True, separators=(",", ":")).encode())),
        originals=originals, original_bone_gaps=original_bone_gaps, schemas=schemas.records,
        constructors={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in paths},
        runtime_inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (battle, rules)},
        fixture=dict(path=fixture.relative_to(ROOT).as_posix(), sha256=sha(fixture.read_bytes())),
        generator_sha256=sha(Path(__file__).read_bytes()),
        counts=dict(models=len(ids), active_models=sum(bool(items) for items in groups.values()), casts=sum(map(len, groups.values()))),
        operation="APK-first decode of selected original group-zero loadouts and item/entity bindings; compare only these bindings with legacy runtime catalogs. No numeric/version substitution. The view fixture does not test target selection, damage settlement or natural skill completion.")
    target = ROOT / "assets/reference/battle-animation/configured-monster-graphs-source-manifest.json"
    target.write_bytes(json.dumps(result, ensure_ascii=False, separators=(",", ":")).encode("utf8"))
    print("Original configured monster graph bindings verified:", result["counts"])
    print("Original source bone names without a non-null registry entry:",
        [(row["monster"], row["skill"], row["bone"]) for row in original_bone_gaps])


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--entities", help="Optional comma-separated subset of installed original monster entity IDs")
    args = parser.parse_args()
    verify([int(value) for value in args.entities.split(",")] if args.entities else None)
