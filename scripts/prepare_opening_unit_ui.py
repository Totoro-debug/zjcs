"""Recover the opening battle's faction colors, head bindings and native UI settings."""
import hashlib
import io
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_character_creation_opening import json_bytes, write_with_meta, REFERENCE, PAYLOAD, prefab_walk, decode_text
from UnityPy.classes.PPtr import PPtr


def main():
    apk, schemas = Apk(), ScriptSchemas()
    assets = [row for name in apk.archive.namelist()
              if name.startswith("assets/yoo/") and name.endswith("_854_156383.v3.bytes")
              for row in read_asset_manifest(apk.archive.read(name))]
    sources, outputs = [], []

    def record(obj):
        sources.append(dict(apk.provenance(obj), sha256=hashlib.sha256(obj.get_raw_data()).hexdigest()))
        return obj

    def asset(suffix, expected_type=None):
        matches = [row for row in assets if row["path"].lower().endswith(suffix.lower())]
        if not matches:
            matches = [row for row in assets if row["address"].lower().endswith(suffix.removesuffix(".png").lower())]
        if len(matches) != 1:
            raise ValueError("Ambiguous official UI asset: " + suffix + ": " + str(len(matches)))
        row = matches[0]
        return record(next(ptr.deref() for path, ptr in apk.container(row["bundle"])
                           if path.lower() == row["path"].lower()
                           and (expected_type is None or ptr.type.name == expected_type)))

    def sprite(address):
        obj = asset("/" + address.removesuffix(".png") + ".png")
        value = obj.read()
        buffer = io.BytesIO()
        value.image.save(buffer, format="PNG")
        file = address.rsplit("/", 1)[-1] + ".png"
        outputs.append(write_with_meta(PAYLOAD / "UnitUI" / file, buffer.getvalue()))
        return file

    config = asset("/GamePlayManagerConfig.asset", "MonoBehaviour")
    cfg = schemas.read(config)
    factions = []
    for index, data in enumerate(cfg["FactionEffectDatas"]):
        factions.append(dict(index=index, name="Faction" + str(index), hpColor=list(data["HpColor"].values()),
            hpAddColor=list(data["HpAddColor"].values()), hpSubColor=list(data["HpSubColor"].values()),
            roundBackground=sprite(data["FactionRoundImgPath"])))
    npc_obj = record(apk.asset("config_luban", "/npc.g.bytes"))
    npcs = Constructors().table(npc_obj.read().m_Script.encode("utf-8", "surrogateescape"), "TbNpcInfo")
    heads = []
    for class_id in (8401, 8402):
        npc = next(row for row in npcs if row["Id"] == class_id)
        bundle = f"defaultpackage_assets_prefabs_ecentity_fight_fightnpc_{npc['FightEntityId']}.bundle"
        root = next(p.read() for path, p in apk.container(bundle)
                    if p.type.name == "GameObject" and path.endswith(f"/{npc['FightEntityId']}.prefab"))
        node, rect = next((node, rect) for node, rect in prefab_walk(root) if node.m_Name == "Name")
        text_obj = next(c.component.deref() for c in node.m_Component if c.component.type.name == "MonoBehaviour"
                        and c.component.deref().read(check_read=False).m_Script.read().m_ClassName == "TextMeshPro")
        layout = decode_text(record(text_obj).get_raw_data(), "Name")
        transform = rect
        position, scale = [0, 0, 0], [1, 1, 1]
        while transform:
            p = [getattr(transform.m_LocalPosition, axis) for axis in "xyz"]
            if hasattr(transform, "m_AnchoredPosition"):
                p[:2] = [transform.m_AnchoredPosition.x, transform.m_AnchoredPosition.y]
            s = [getattr(transform.m_LocalScale, axis) for axis in "xyz"]
            position = [p[i] + position[i] * s[i] for i in range(3)]
            scale = [s[i] * scale[i] for i in range(3)]
            transform = record(transform.m_Father.deref()).read() if transform.m_Father.path_id else None
        layout.update(position=position, scale=scale, size=[rect.m_SizeDelta.x, rect.m_SizeDelta.y])
        heads.append(dict(classId=class_id, file=sprite(npc["Icon"]), nameKey=npc["NameKey"], nameLayout=layout))
    for class_id in (3281, 3282, 3283, 3481, 3803):
        heads.append(dict(classId=class_id, file=sprite("ItemIcon/GameHeadIcon/Monster/G_" + str(class_id))))
    bundle = "prefabs_ui_fight_fightmainpanel_extmodel_fightmainpanelrecordfight"
    round_ui = next(record(obj) for obj in apk.objects(bundle) if obj.type.name == "MonoBehaviour"
                    and obj.read(check_read=False).m_Script.read().m_ClassName == "GameRoundUI")
    round_settings = schemas.read(round_ui)
    paths = {}
    for key in ("IconSlot", "IconBack"):
        transform = PPtr(**round_settings[key], assetsfile=round_ui.assets_file).read()
        names = []
        while transform:
            names.insert(0, transform.m_GameObject.read().m_Name)
            transform = transform.m_Father.read() if transform.m_Father.path_id else None
        paths[key] = "/" + "/".join(names)
    from npc_travel_language import language
    names, language_sources = language([head["nameKey"] for head in heads if "nameKey" in head])
    for head in heads:
        if "nameKey" in head:
            head["name"] = names[head["nameKey"]]
    ui = dict(manifest="854_156383", factions=factions, heads=heads,
              timeline=dict(paths, minDist=round_settings["MinDist"], moveDuration=round_settings["IconMoveDuration"]))
    outputs.append(write_with_meta(PAYLOAD / "unit-ui.json", json_bytes(ui)))
    receipt = json.loads(REFERENCE.read_text(encoding="utf-8"))
    receipt["unit_ui"] = dict(inputs=sources, language_sources=language_sources,
        generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    official_code = ROOT / ".tools/source-packages/official-code/client-source"
    source_names = ("GameRoundUI", "GameRoundCharacterItem", "CharacterDamageUI", "ElementConfrontationUI",
                    "DamageSequenceImage", "AttachmentFollower", "Spine.Extension/CustomRenderExistingMesh", "MyPlayableOverrideAnimator")
    receipt["battle_presentation_adapters"] = dict(
        official_code=[dict(path=(official_code / (name + ".cs")).relative_to(ROOT).as_posix(),
            sha256=hashlib.sha256((official_code / (name + ".cs")).read_bytes()).hexdigest()) for name in source_names],
        operation="Native prefab bindings, bitmap fonts and GLES shader programs adapted to Windows; "
                  "unit-bar SpriteRenderer world units converted to UGUI sliced border units",
        unit_bar_source_sha256=hashlib.sha256((ROOT / "unity/Assets/Official/Combat/HUD/unit-bars.json").read_bytes()).hexdigest())
    generated = []
    for folder in (ROOT / "unity/Assets/Official/Opening/Fighters", ROOT / "unity/Assets/Official/Opening/Damage"):
        generated.extend(path for path in folder.rglob("*") if path.is_file())
    generated.extend((ROOT / "unity/Assets/Game/Resources/Opening").glob("OpeningBoss*.shader"))
    generated.append(ROOT / "unity/Assets/Game/Resources/Opening/OpeningDamageBitmap.shader")
    generated.extend(ROOT / ("unity/Assets/Official/Combat/HUD/Sprites/" + name + ".png.meta")
        for name in ("ImgLoad_HpBar-f915a5bb0c", "ImgBg_HpBar-8bcbf4b323"))
    receipt["battle_presentation_adapters"]["unity_outputs"] = [dict(path=path.relative_to(ROOT).as_posix(),
        sha256=hashlib.sha256(path.read_bytes()).hexdigest()) for path in sorted(generated)]
    paths = {row["path"] for row in outputs}
    receipt["outputs"] = [row for row in receipt["outputs"] if row["path"] not in paths] + outputs
    REFERENCE.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")
    print(json.dumps(ui, ensure_ascii=True))


if __name__ == "__main__":
    main()
