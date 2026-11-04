"""Bind original FightRolePetEntityComponent timeline heads, independently of pet HUD icons."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_unity_battle_monster_portraits import asset_yaml
from prepare_battle_performance_camera import EXPORT
from inspect_battle_skill_graph_bindings import EXPORT_RECEIPT


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    apk = Apk.current()
    rules = ROOT / "unity/Assets/Game/Resources/Combat/pet-battle-rules.json"
    ids = sorted({r["FightEntityClassId"] for r in json.loads(rules.read_bytes())["tables"]["pet_evolution"]
                  if r["EvolutionPhase"] >= 2 and r["EntitySkill"] > 0})
    name = "defaultpackage_assets_image_itemicon_gameheadicon_pet.bundle"
    objects = {o.read().m_Name: o for o in apk.objects(name) if o.type.name == "Sprite"}
    outputs, inputs, bindings = [], [], []
    verified = {r["path"]: r["sha256"] for r in json.loads(EXPORT_RECEIPT.read_bytes())["outputs"]}
    def exported(path):
        raw = path.read_bytes()
        if hashlib.sha256(raw).hexdigest() != verified[path.relative_to(EXPORT.parents[1]).as_posix()]:
            raise ValueError("Original native export differs: " + str(path))
        inputs.append(identity(path))
        return raw
    folder = ROOT / "unity/Assets/Official/Combat/PetPortraits"
    folder.mkdir(parents=True, exist_ok=True)
    for entity_id in ids:
        original = objects["G_" + str(entity_id)]
        sprite = original.read()
        if (sprite.m_SpriteAtlas.path_id or sprite.m_Rect.width != 192 or sprite.m_Rect.height != 192
                or sprite.m_Pivot.x != .5 or sprite.m_Pivot.y != .5 or sprite.m_PixelsToUnits != 100):
            raise ValueError("Review original pet timeline sprite geometry")
        source = EXPORT / "Image/ItemIcon/GameHeadIcon/Pet" / (sprite.m_Name + ".asset")
        source_png = source.with_suffix(".png")
        png = exported(source_png)
        target = folder / source.name
        texture = folder / source_png.name
        guid = hashlib.sha256(target.relative_to(ROOT).as_posix().encode()).hexdigest()[:32]
        texture_guid = hashlib.sha256(texture.relative_to(ROOT).as_posix().encode()).hexdigest()[:32]
        native = exported(source).decode("utf-8-sig")
        old_texture_guid = re.search(r'texture: \{fileID: 2800000, guid: ([a-f0-9]{32}), type: 3\}', native)[1]
        native = native.replace(old_texture_guid, texture_guid)
        exported(source_png.with_name(source_png.name + ".meta"))
        texture_meta = ("fileFormatVersion: 2\nguid: " + texture_guid + "\nTextureImporter:\n"
            "  serializedVersion: 12\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n"
            "  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    mipBias: 0\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n"
            "  isReadable: 0\n  textureType: 0\n  textureShape: 1\n  alphaIsTransparency: 0\n"
            "  maxTextureSize: 4096\n  textureCompression: 0\n  platformSettings:\n"
            "  - serializedVersion: 3\n    buildTarget: DefaultTexturePlatform\n    maxTextureSize: 4096\n"
            "    textureFormat: 4\n    textureCompression: 0\n    overridden: 0\n")
        for path, raw in ((target, native.encode()), (texture, png),
                          (texture.with_name(texture.name + ".meta"), texture_meta.encode()),
                          (target.with_name(target.name + ".meta"), ("fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 21300000\n").encode())):
            prior = path.read_bytes() if path.exists() else raw
            if path.suffix != ".png":
                prior = prior.replace(b"\r\n", b"\n")
            if path.suffix == ".meta":
                expected_guid = re.search(rb"(?m)^guid: ([a-f0-9]{32})", raw)[1]
                if re.search(rb"(?m)^guid: ([a-f0-9]{32})", prior)[1] != expected_guid:
                    raise ValueError("Preserve changed pet portrait GUID: " + str(path))
            elif prior != raw:
                raise ValueError("Preserve changed pet portrait output: " + str(path))
            path.write_bytes(raw)
            outputs.append(identity(path))
        bindings.append(dict(iconId=entity_id, guid=guid))
        inputs.append(dict(**apk.provenance(original), object_sha256=hashlib.sha256(original.get_raw_data()).hexdigest()))
    folder_meta = folder.with_name(folder.name + ".meta")
    if not folder_meta.exists():
        folder_meta.write_bytes(("fileFormatVersion: 2\nguid: " + hashlib.sha256(folder.relative_to(ROOT).as_posix().encode()).hexdigest()[:32] + "\nfolderAsset: yes\n").encode())
    outputs.append(identity(folder_meta))
    script = ROOT / "unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterPortraitCatalog.cs"
    script_guid = next(line.split(": ")[1] for line in script.with_name(script.name + ".meta").read_text().splitlines() if line.startswith("guid: "))
    asset = ROOT / "unity/Assets/Game/Resources/Combat/OfficialBattlePetPortraitCatalog.asset"
    native_catalog = asset_yaml(script_guid, bindings).replace(b"m_Name: OfficialBattleMonsterPortraitCatalog", b"m_Name: OfficialBattlePetPortraitCatalog")
    native_catalog = re.sub(rb'(sprite: \{fileID: 21300000, guid: [a-f0-9]{32}, type:) 3', rb'\1 2', native_catalog)
    native_catalog = native_catalog.replace(b"m_EditorClassIdentifier: \n", b"m_EditorClassIdentifier:\n")
    asset.write_bytes(native_catalog)
    meta = asset.with_name(asset.name + ".meta")
    if not meta.exists():
        meta.write_bytes(("fileFormatVersion: 2\nguid: " + hashlib.sha256(asset.relative_to(ROOT).as_posix().encode()).hexdigest()[:32] + "\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n").encode())
    outputs.extend([identity(asset), identity(meta)])
    receipt = dict(manifest=apk.version, generator=identity(Path(__file__).resolve()), rules=identity(rules),
        application_sources=[identity(ROOT / path) for path in (
            ".tools/source-packages/official-code/client-source/GameRoundCharacterItem.cs",
            ".tools/source-packages/official-code/client-source/GameRoundCharacterItemScene.cs",
            ".tools/source-packages/official-code/common-source/Common/LanguageUtils.cs")],
        inputs=inputs, outputs=outputs, entity_ids=ids,
        operation="GameRoundCharacterItem pet branch uses LanguageUtils.GetPetIcon(PetEntityClassId), ItemIcon/GameHeadIcon/Pet/G_ID; retain verified native exported Sprite meshes/UVs and decoded source textures, explicitly bind a separate catalog of the existing shared catalog type.")
    target = ROOT / "assets/reference/battle-hud/pet-portraits-source-manifest.json"
    target.write_bytes((json.dumps(receipt, ensure_ascii=False, indent=2) + "\n").encode())
    apk.archive.close()
    print("Original pet timeline portraits bound:", len(bindings))


if __name__ == "__main__":
    build()
