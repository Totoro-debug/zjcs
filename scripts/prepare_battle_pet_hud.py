"""Recover GamePetSkillIcon's serialized references from the installed HUD source."""
import hashlib
import json
from pathlib import Path
import sys
import urllib.request
import io
import re

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from refresh_current_ui_graphics import object_path
from prepare_official_city import PanelSchemas
from UnityPy.classes.PPtr import PPtr
from official_tables import Constructors

FIELDS = ("mPetIcon", "mPetEnergyProgressImg", "mPetEnergyProgressText",
          "mPetSupportSkillIcon", "mPetSupportSkillQuality", "mNoEquipPetShowUI",
          "mPetSupportSkillGo", "mPetIconGo", "mTriggerSupportSkillAnim", "mDisabledGO")


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode())
    meta = path.with_name(path.name + ".meta")
    if not meta.exists():
        guid = hashlib.sha256(path.relative_to(ROOT).as_posix().encode()).hexdigest()[:32]
        meta.write_bytes(("fileFormatVersion: 2\nguid: " + guid + "\n").encode())


def visuals(apk, obj, component):
    # Follow the installed historical prefab's actual CAB reference. The APK
    # supplies the same referenced serialized file, rather than a name match.
    image = obj.assets_file.objects[component["mPetIcon"]["m_PathID"]]
    image_data = PanelSchemas().read(image)
    if image_data["m_PreserveAspect"] or image_data["m_Type"] != 0:
        raise ValueError("Review the original pet Image geometry")
    pointer = image_data["m_Sprite"]
    sprite = PPtr(m_FileID=pointer["m_FileID"], m_PathID=pointer["m_PathID"],
                  assetsfile=image.assets_file).deref()
    rules_path = ROOT / "unity/Assets/Official/Combat/rules.json"
    rules_receipt = rules_path.with_name("rules-source-manifest.json")
    raw = rules_path.read_bytes()
    if hashlib.sha256(raw).hexdigest() != json.loads(rules_receipt.read_bytes())["output_sha256"]:
        raise ValueError("Installed combat pet rules differ from their source hash")
    rules = json.loads(raw)
    sprites = {o.read().m_Name: o for o in sprite.assets_file.objects.values() if o.type.name == "Sprite"}
    support_image = obj.assets_file.objects[component["mPetSupportSkillIcon"]["m_PathID"]]
    support_data = PanelSchemas().read(support_image)
    if support_data["m_PreserveAspect"] or support_data["m_Type"] != 0:
        raise ValueError("Review the original support skill Image geometry")
    support_pointer = support_data["m_Sprite"]
    support_sprite = PPtr(m_FileID=support_pointer["m_FileID"], m_PathID=support_pointer["m_PathID"],
                         assetsfile=support_image.assets_file).deref()
    support_sprites = {o.read().m_Name: o for o in support_sprite.assets_file.objects.values() if o.type.name == "Sprite"}
    for bundle in apk.bundles:
        if "_image_itemicon_petskill_" in bundle["name"]:
            for original in apk.objects(bundle["name"]):
                if original.type.name == "Sprite":
                    name = original.read().m_Name
                    if name in support_sprites:
                        raise ValueError("Ambiguous original support skill Sprite: " + name)
                    support_sprites[name] = original
    jobs = []
    for evolution in rules["pet_evolution"]:
        name = evolution["PetIcon"].rsplit("/", 1)[-1]
        row = dict(classId=evolution["ClassId"], phase=evolution["EvolutionPhase"],
                   supportSkillId=evolution["SupportSkills"][0], resource="")
        jobs.append((row, name, sprites))
    items = {row["ClassId"]: row for row in rules["item"]}
    # The installed configuration remains historical. Verify its consumed fields
    # against the active Luban objects before binding the active native sprites.
    parser = Constructors(ROOT / ".tools/source-packages/current" / apk.version / "official-code")
    mapping = {name: cls for cls, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source("Tables"))}
    compared = []
    support_ids = {row["SupportSkills"][0] for row in rules["pet_evolution"] if row["EvolutionPhase"] == 1}
    for table in ("pet_evolution", "item", "skill_rank"):
        original = apk.asset("config_luban", "/" + table + ".g.bytes")
        payload = original.read().m_Script.encode("utf8", "surrogateescape")
        active = parser.table(payload, mapping[table])
        if table == "pet_evolution":
            selected = {(row["ClassId"], row["EvolutionPhase"]): row for row in active}
            for row in rules[table]:
                found = selected[(row["ClassId"], row["EvolutionPhase"])]
                if any(found[field] != row[field] for field in ("PetIcon", "SupportSkills")):
                    raise ValueError("Pet HUD configuration changed across recorded sources")
        elif table == "item":
            selected = {row["ClassId"]: row for row in active}
            if any(any(selected[item][field] != items[item][field] for field in ("RawIcon", "Quality")) for item in support_ids):
                raise ValueError("Pet support item display changed across recorded sources")
        else:
            selected = {row["Rank"]: row["Quality"] for row in active}
            if any(selected[row["Rank"]] != row["Quality"] for row in rules[table]):
                raise ValueError("Pet skill rank quality changed across recorded sources")
        compared.append(dict(table=table, source=apk.provenance(original), object_sha256=hashlib.sha256(original.get_raw_data()).hexdigest(),
                             payload_sha256=hashlib.sha256(payload).hexdigest()))
    for item_id in sorted({row["SupportSkills"][0] for row in rules["pet_evolution"] if row["EvolutionPhase"] == 1}):
        name = items[item_id]["RawIcon"].rsplit("/", 1)[-1]
        jobs.append((dict(itemId=item_id, resource=""), name, support_sprites))
    rows, inputs, outputs = [], [], []
    for row, name, originals in jobs:
        original = originals.get(name)
        if original is None:
            if name != "pet_1619":
                raise ValueError("Original pet portrait reference is absent: " + name)
            # Existing user decision is maintained in docs/features/pets.md.
            rows.append(row)
            continue
        data = original.read()
        atlas = data.m_SpriteAtlas.deref() if data.m_SpriteAtlas.path_id else None
        packed = dict(atlas.read().m_RenderDataMap)[data.m_RenderDataKey] if atlas else data.m_RD
        texture = packed.texture.deref()
        if (packed.settingsRaw >> 2) & 15 or packed.alphaTexture.path_id:
            raise ValueError("Review packed pet portrait rotation or split alpha")
        decoded = data.image
        png = io.BytesIO(); decoded.save(png, format="PNG", optimize=False)
        resource = "Combat/PetHud/Portraits/" + name
        target = ROOT / "unity/Assets/Official/Combat/PetHUD/Resources" / (resource + ".png")
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(png.getvalue())
        guid = hashlib.sha256(target.relative_to(ROOT).as_posix().encode()).hexdigest()[:32]
        target.with_name(target.name + ".meta").write_bytes(("fileFormatVersion: 2\nguid: " + guid + "\nTextureImporter:\n"
            "  serializedVersion: 12\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n"
            "  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n"
            "  isReadable: 0\n  textureType: 0\n  textureShape: 1\n  alphaIsTransparency: 0\n"
            "  maxTextureSize: 4096\n  textureCompression: 0\n  platformSettings:\n"
            "  - serializedVersion: 3\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 4096\n"
            "    textureFormat: 4\n    textureCompression: 0\n    overridden: 0\n").encode())
        folder = target.parent
        while folder != ROOT / "unity/Assets/Official/Combat":
            meta = folder.with_name(folder.name + ".meta")
            if not meta.exists():
                folder_guid = hashlib.sha256(folder.relative_to(ROOT).as_posix().encode()).hexdigest()[:32]
                meta.write_bytes(("fileFormatVersion: 2\nguid: " + folder_guid + "\nfolderAsset: yes\n").encode())
            folder = folder.parent
        rect = packed.textureRect
        def vector(x, y): return dict(x=x, y=y)
        row.update(resource=resource, sourceSize=vector(data.m_Rect.width, data.m_Rect.height),
            trimOffset=vector(packed.textureRectOffset.x, packed.textureRectOffset.y),
            trimSize=vector(rect.width, rect.height),
            uvMin=vector((rect.x-round(rect.x))/decoded.width, (rect.y-round(rect.y))/decoded.height),
            uvMax=vector((rect.x+rect.width-round(rect.x))/decoded.width,
                         (rect.y+rect.height-round(rect.y))/decoded.height))
        rows.append(row)
        for source_obj in (original, atlas, texture):
            if source_obj is None:
                continue
            inputs.append(dict(**apk.provenance(source_obj), object_sha256=hashlib.sha256(source_obj.get_raw_data()).hexdigest()))
        outputs.extend([identity(target), identity(target.with_name(target.name + ".meta"))])
    ranks = rules["skill_rank"]
    item_qualities = {r["ClassId"]: r["Quality"] for r in rules["item"]}
    defaults = [dict(itemId=item, rank=min(r["Rank"] for r in ranks if r["Quality"] == item_qualities[item]))
                for item in sorted({r["supportSkillId"] for r in rows if "classId" in r})]
    return dict(evolutions=[r for r in rows if "classId" in r], supportIcons=[r for r in rows if "itemId" in r],
                ranks=[dict(rank=r["Rank"], quality=r["Quality"]) for r in ranks],
                defaultRanks=defaults), dict(installed_rules=identity(rules_path), rules_receipt=identity(rules_receipt),
        original_image_field=dict(pointer=pointer, path_id=str(image.path_id),
            object_sha256=hashlib.sha256(image.get_raw_data()).hexdigest()),
        referenced_serialized_file=sprite.assets_file.name, active_configuration_comparison=compared,
        support_template_sprite=apk.provenance(support_sprite), inputs=inputs, outputs=outputs)


def build():
    panels = ROOT / "unity/Assets/Official/Combat/HUD/panels.json"
    runtime = ROOT / "unity/Assets/Official/Combat/HUD/panels-runtime.json"
    source = json.loads(panels.read_bytes())["prefabs"]["main"]["source"]
    if source["manifest"] != "854_156383":
        raise ValueError("Review the installed HUD's original identity")
    apk = Apk()
    filename = source["bundle_hash"] + ".bundle"
    packaged = "assets/yoo/DefaultPackage/" + filename
    cache = ROOT / ".tools/source-packages/cloud/854_156383/DefaultPackage" / filename
    if packaged in apk.available:
        raw = apk.archive.read(packaged)
    elif cache.exists():
        raw = cache.read_bytes()
    else:
        request = urllib.request.Request(source["url"], headers={"User-Agent": "Mozilla/5.0"})
        with urllib.request.urlopen(request, timeout=45) as response:
            raw = response.read()
    if (len(raw) != source["bundle_size"] or hashlib.md5(raw).hexdigest() != source["bundle_hash"]
            or hashlib.sha256(raw).hexdigest() != source["bundle_sha256"]):
        raise ValueError("Original HUD bundle differs from its installed source receipt")
    if not cache.exists():
        cache.parent.mkdir(parents=True, exist_ok=True)
        cache.write_bytes(raw)
    apk.env.load_file(raw)
    schemas = ScriptSchemas()
    trees = next(row for row in json.loads(runtime.read_bytes())["prefabs"] if row["key"] == "main")
    paths = {row["path"] for row in trees["nodes"]}
    candidates = []
    for obj in list(apk.env.objects):
        if obj.type.name != "MonoBehaviour":
            continue
        basic = obj.read(check_read=False)
        if basic.m_Script.path_id and basic.m_Script.read().m_ClassName == "GamePetSkillIcon":
            candidates.append(obj)
    if len(candidates) != 1:
        raise ValueError("Expected the single original GamePetSkillIcon")
    obj = candidates[0]
    component = schemas.read(obj)
    references, inputs = {}, []
    for name in FIELDS:
        ref = component[name]
        if ref["m_FileID"] or not ref["m_PathID"]:
            raise ValueError("Original pet widget reference is not local: " + name)
        target = obj.assets_file.objects[ref["m_PathID"]]
        go = target if target.type.name == "GameObject" else target.read(check_read=False).m_GameObject.deref()
        path, root_id = object_path(go)
        if path not in paths or root_id != source["path_id"]:
            raise ValueError("Pet widget reference escaped the installed original HUD: " + path)
        references[name] = path
        inputs.append(dict(field=name, path_id=str(target.path_id),
                           sha256=hashlib.sha256(target.get_raw_data()).hexdigest()))
    output = ROOT / "unity/Assets/Game/Resources/Combat/battle-pet-hud.json"
    config, visual_proof = visuals(apk, obj, component)
    animation = obj.assets_file.objects[component["mTriggerSupportSkillAnim"]["m_PathID"]].read()
    clip = animation.m_Animations[0].deref()
    original_clip = clip.read_typetree()
    if original_clip["m_Name"] != "FightMainPanel_GamePetSkillIconItem_Discharge" or not original_clip["m_Legacy"]:
        raise ValueError("Unexpected original pet discharge clip")
    rotations = [dict(path=row["path"], keys=row["curve"]["m_Curve"]) for row in original_clip["m_RotationCurves"]]
    installed = [value for node in trees["nodes"] for value in node.get("animations", [])
                 if value["name"] == original_clip["m_Name"] and value["owner"] == references["mTriggerSupportSkillAnim"]]
    if len(installed) != 1:
        raise ValueError("Original discharge scalar animation is not uniquely installed")
    scalar = {}
    owner_path = references["mTriggerSupportSkillAnim"]
    for row in original_clip["m_FloatCurves"]:
        scalar[(owner_path + "/" + row["path"], row["attribute"])] = row["curve"]["m_Curve"]
    for row in original_clip["m_ScaleCurves"]:
        for axis in "xyz":
            scalar[(owner_path + "/" + row["path"], "scale." + axis)] = [dict(time=k["time"], value=k["value"][axis],
                inSlope=k["inSlope"][axis], outSlope=k["outSlope"][axis], inWeight=k["inWeight"][axis], outWeight=k["outWeight"][axis], weightedMode=k["weightedMode"]) for k in row["curve"]["m_Curve"]]
    for track in installed[0]["tracks"]:
        keys = scalar.pop((track["path"], track["property"]))
        expected = [dict(time=k["time"], value=k["value"], in_tangent=k["inSlope"], out_tangent=k["outSlope"]) for k in keys]
        if track["keys"] != expected or any(k["weightedMode"] != 0 for k in keys):
            raise ValueError("Installed pet discharge scalar curve differs from the original")
    extra_curves = [dict(path=path[len(owner_path) + 1:], property=prop.replace("scale.", "localScale."), keys=keys)
                    for (path, prop), keys in scalar.items()]
    write(output, dict(manifest=source["manifest"], references=references, rotations=rotations, extraCurves=extra_curves, **config))
    write(output.with_name("battle-pet-hud-source-manifest.json"),
          dict(manifest=source["manifest"], output_sha256=identity(output)["sha256"]))
    code = ROOT / ".tools/source-packages/official-code/client-source/GamePetSkillIcon.cs"
    current = ROOT / ".tools/source-packages/current/884_156692/official-code/client-source/GamePetSkillIcon.cs"
    if code.read_bytes() != current.read_bytes():
        raise ValueError("Review the different original pet widget implementations before adapting them")
    proof = dict(manifest=source["manifest"], generator=identity(Path(__file__).resolve()),
        installed_panels=identity(panels), installed_runtime=identity(runtime), original_bundle=source,
        apk_checked=dict(path=str(apk.source.apk.relative_to(ROOT)), packaged=packaged in apk.available),
        original_component=dict(path_id=str(obj.path_id), sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()),
        references=inputs, visuals=visual_proof,
        discharge_clip=dict(source=apk.provenance(clip), object_sha256=hashlib.sha256(clip.get_raw_data()).hexdigest(),
            operation="Compare every installed scalar curve to the original; restore quaternion channels as a native legacy clip and omitted scale Z on the existing scalar clock."),
        application_sources=[identity(code), identity(current),
            identity(ROOT / '.tools/source-packages/official-code/client-source/FightSkillListDataEntityProvider.cs'),
            identity(ROOT / '.tools/source-packages/official-code/client-source/FightSkillListDataProvider.cs'),
            identity(ROOT / '.tools/source-packages/official-code/common-source/Common/FormulaHandler.cs'),
            identity(ROOT / '.tools/source-packages/official-code/framework-firstpass-source/Framework/CultureNumberFormatterBase.cs'),
            identity(ROOT / '.tools/source-packages/official-code/framework-firstpass-source/Framework/NumberFormatHelper.cs')],
        runtime_sources=[identity(ROOT / path) for path in (
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudAnimationPlayer.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillEntityPlayback.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePetHudView.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleHudView.Pet.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillQualityCatalog.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs',
            'unity/Assets/Game/Editor/OfficialBattlePetHudAcceptance.cs',
            'unity/Assets/Game/Editor/OfficialBattlePetHostAcceptance.cs')],
        outputs=[identity(output), identity(output.with_name("battle-pet-hud-source-manifest.json"))],
        operation="Restore original GamePetSkillIcon field references without changing the HUD hierarchy or layout.")
    receipt = ROOT / "assets/reference/battle-hud/pet-widget-source-manifest.json"
    receipt.parent.mkdir(parents=True, exist_ok=True)
    receipt.write_bytes((json.dumps(proof, ensure_ascii=False, indent=2) + "\n").encode())
    print("Original pet HUD references recovered:", len(references))


if __name__ == "__main__":
    build()
