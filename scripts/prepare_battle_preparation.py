"""Extract current official preparation inputs, preserving APK/CDN object provenance."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys
import uuid

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/python-packages'))
import UnityPy  # Load the complete cached package before legacy tool paths are added.
from official_apk import Apk
from official_unity_schema import ScriptSchemas
from official_tables import Constructors

DEST = ROOT / 'unity/Assets/Official/Combat/Preparation'
OUTPUT = DEST / 'preparation.json'
RECEIPT = ROOT / 'assets/reference/battle-rules/preparation-source-manifest.json'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def save(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf8')


def metadata(path, folder=False):
    sidecar = Path(str(path) + '.meta')
    if sidecar.exists():
        return
    relative = path.relative_to(ROOT / 'unity').as_posix()
    guid = uuid.uuid5(uuid.NAMESPACE_URL, 'zjcs:official-preparation:' + relative).hex
    sidecar.write_text('fileFormatVersion: 2\nguid: ' + guid + '\n'
                      + ('folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n'
                         if folder else ''), encoding='utf8')


def build():
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    code_manifest = code.parent / 'code-manifest.json'
    proof = json.loads(code_manifest.read_bytes())
    if proof['manifest'] != apk.version:
        raise ValueError('Code manifest is not the selected official version')
    assemblies = []
    for name in ('Assembly-CSharp.dll', 'Console.dll'):
        path = code / 'managed/decoded' / name
        digest = sha(path.read_bytes())
        if digest != proof['assemblies'][name]['decoded_sha256']:
            raise ValueError('Decoded official assembly hash mismatch: ' + name)
        assemblies.append(dict(path=path.relative_to(ROOT).as_posix(), sha256=digest,
                               source=proof['assemblies'][name]))
    schemas = ScriptSchemas(code_root=code)
    inputs = {}

    def record(obj):
        identity = obj.assets_file.name + ':' + str(obj.path_id)
        inputs[identity] = dict(apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                                type=obj.type.name)
        return obj

    def component(bundle, classname):
        matches = []
        for obj in apk.objects(bundle):
            if obj.type.name != 'MonoBehaviour':
                continue
            raw = obj.read(check_read=False)
            if raw.m_Script.path_id and raw.m_Script.read().m_ClassName == classname:
                matches.append(obj)
        if len(matches) != 1:
            raise ValueError(f'Expected one official {classname}: {len(matches)}')
        obj = record(matches[0])
        return obj, schemas.read(obj)

    constructors = Constructors(code_root=code)
    def table(path, typename):
        obj = record(apk.asset('config_luban', path))
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        return constructors.table(raw, typename)

    stages = table('Assets/Config/Luban/stage.g.bytes', 'TbStageInfo')
    stage_types = table('Assets/Config/Luban/stage_type.g.bytes', 'TbStageTypeInfo')
    ranges = [dict(stageId=row['Id'], type=row['Type'],
                   width=row['TeamChangePosRange']['X'], height=row['TeamChangePosRange']['Y'])
              for row in stages]
    offsets = [dict(type=row['StageType'], x=row['EntryOffsetOfTeam']['X'], y=row['EntryOffsetOfTeam']['Y'])
               for row in stage_types]

    drag_obj, drag = component('prefabs_ecentity_fight_fightcharacter_3100', 'PrepareBattlePlayerDrag')
    character_objects = apk.objects('prefabs_ecentity_fight_fightcharacter_3100')
    collider_obj = record(next(o for o in character_objects if o.type.name == 'BoxCollider'))
    drag_root = drag_obj.read(check_read=False).m_GameObject.path_id
    current = collider_obj.read().m_GameObject.read().m_Component[0].component.deref()
    collider_transforms = []
    while current.read().m_GameObject.path_id != drag_root:
        record(current)
        collider_transforms.insert(0, dict(name=current.read().m_GameObject.read().m_Name,
                                           transform=current.read_typetree()))
        if not current.read().m_Father.path_id:
            raise ValueError('Original pointer collider is not below the preparation controller')
        current = current.read().m_Father.deref()
    record(current)
    objects = apk.objects('prefabs_ui_fight_changebattleposarea')
    renderer_obj = record(next(o for o in objects if o.type.name == 'SpriteRenderer'))
    renderer = renderer_obj.read()
    sprite_obj = record(renderer.m_Sprite.deref())
    sprite = sprite_obj.read()
    # Preserve decoded original sprite dimensions, border, pivot and texture settings.
    if sprite.m_SpriteAtlas.path_id:
        atlas = record(sprite.m_SpriteAtlas.deref()).read()
        packed = next(v for k, v in atlas.m_RenderDataMap if k == sprite.m_RenderDataKey)
        texture = record(packed.texture.deref()).read()
        if packed.alphaTexture.path_id:
            record(packed.alphaTexture.deref())
    else:
        texture = record(sprite.m_RD.texture.deref()).read()
    material_obj = record(renderer.m_Materials[0].deref())
    material = material_obj.read()
    shader = record(material.m_Shader.deref()).read()
    if shader.m_ParsedForm.m_Name != 'Sprites/Default':
        raise ValueError('Restore the original preparation shader before exporting this version')
    animation_obj = record(next(o for o in objects if o.type.name == 'Animation'))
    animation = animation_obj.read()
    clip_obj = record(animation.m_Animation.deref())
    clip = clip_obj.read_typetree()
    if not clip['m_Legacy'] or any(clip[k] for k in ('m_RotationCurves', 'm_CompressedRotationCurves',
            'm_EulerCurves', 'm_PositionCurves', 'm_ScaleCurves', 'm_PPtrCurves', 'm_Events')):
        raise ValueError('Additional native preparation animation channels require conversion')
    for curve in clip['m_FloatCurves']:
        if curve['classID'] != 212 or curve['attribute'] != 'm_Color.a' or curve['path']:
            raise ValueError('Unexpected original preparation animation binding')
    root_obj = record(next(o for o in objects if o.type.name == 'GameObject'))
    transform_obj = record(next(o for o in objects if o.type.name == 'Transform'))
    transform = transform_obj.read_typetree()
    if transform['m_Children']:
        raise ValueError('Preparation prefab now has children requiring native restoration')
    DEST.mkdir(parents=True, exist_ok=True)
    image_path = DEST / (sprite.m_Name + '.png')
    image = sprite.image
    if image.size != (int(sprite.m_Rect.width), int(sprite.m_Rect.height)):
        raise ValueError('Original preparation sprite needs trim restoration')
    image.save(image_path)
    data = dict(manifest=apk.version, drag=drag, ranges=ranges, offsets=offsets,
        pointer=dict(collider=collider_obj.read_typetree(), transforms=collider_transforms),
        area=dict(name=root_obj.read().m_Name, transform=transform,
                  renderer=renderer_obj.read_typetree(), material=material_obj.read_typetree(),
                  animation=animation_obj.read_typetree(), clip=clip,
                  sprite=dict(path=image_path.relative_to(ROOT / 'unity').as_posix(),
                    pivot=[sprite.m_Pivot.x, sprite.m_Pivot.y],
                    border=[getattr(sprite.m_Border, k) for k in 'xyzw'], ppu=sprite.m_PixelsToUnits,
                    filter=texture.m_TextureSettings.m_FilterMode,
                    wrapU=texture.m_TextureSettings.m_WrapU, wrapV=texture.m_TextureSettings.m_WrapV)))
    save(OUTPUT, data)
    metadata(DEST, folder=True)
    for path in (OUTPUT, image_path):
        metadata(path)
    sources = ['client-source/' + name + '.cs' for name in (
        'PrepareBattlePlayerDrag', 'PrepareBattlePlayerDragBase', 'ChangeRangeTool',
        'CommonChangeRangeInfoTool', 'StartGameBtn', 'GameTeamReadyCountDownUI',
        'NormalPlaybackStrategy')]
    sources += ['common-source/Common/' + name + '.cs' for name in (
        'GameCharacterType', 'StageInfo', 'FightHitSummonComponent', 'FightResultFailedGuardComponent',
        'FightResultFailedPlayerDuelComponent', 'FightProcessStepMsg')]
    save(RECEIPT, dict(manifest=apk.version, assemblies=assemblies,
        code_manifest=dict(path=code_manifest.relative_to(ROOT).as_posix(), sha256=sha(code_manifest.read_bytes())),
        sources=[dict(path=(code / p).relative_to(ROOT).as_posix(), sha256=sha((code / p).read_bytes())) for p in sources],
        inputs=list(inputs.values()), outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes()))
            for p in (OUTPUT, image_path, Path(str(OUTPUT) + '.meta'), Path(str(image_path) + '.meta'))],
        generator=dict(path=Path(__file__).relative_to(ROOT).as_posix(), sha256=sha(Path(__file__).read_bytes())),
        operation='Verify decoded current assemblies; reuse recorded decompilation. Resolve prefab references APK-first, then verified selected CDN cache; decode original sprite and preserve native renderer, material, legacy animation and drag fields. No replacement artwork or gameplay rules.'))
    print(f'Official preparation {apk.version}: {len(inputs)} verified native objects; sprite {image.size}; drag '
          f'{drag["DownKeepTime"]}/{drag["DragThreshold"]}')


def record_native():
    proof = json.loads(RECEIPT.read_bytes())
    if proof['manifest'] != Apk.current().version:
        raise ValueError('Preparation receipt is not the selected version')
    native = ROOT / 'unity/Assets/Game/Resources/Combat/Preparation'
    required = ['ChangeBattlePosArea.prefab', 'ChangeBattlePosArea_Loop.anim',
                'Sprites-Default.mat', 'PrepareBattlePlayerPointer.prefab',
                'OfficialBattlePreparationCatalog.asset']
    paths = [native / name for name in required]
    paths += [Path(str(p) + '.meta') for p in paths]
    paths += [OUTPUT, Path(str(OUTPUT) + '.meta'),
              DEST / 'ImgChangeBattlePosArea.png', DEST / 'ImgChangeBattlePosArea.png.meta',
              DEST / 'ImgChangeBattlePosArea.asset', DEST / 'ImgChangeBattlePosArea.asset.meta']
    proof['outputs'] = [dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes())) for p in paths]
    proof['generator']['sha256'] = sha(Path(__file__).read_bytes())
    proof['native_converter'] = dict(path='unity/Assets/Game/Editor/Combat/OfficialBattlePreparationAssetTools.cs',
        sha256=sha((ROOT / 'unity/Assets/Game/Editor/Combat/OfficialBattlePreparationAssetTools.cs').read_bytes()))
    proof['binding'] = 'OfficialBattlePreparationCatalog.Resources -> Source + AreaPrefab + PlayerPointerPrefab; area -> native sprite/material/legacy AnimationClip. OfficialBattleRuntimeController -> OfficialBattlePreparationView -> native collider raycast and explicit local position acceptance.'
    save(RECEIPT, proof)
    print('Native preparation outputs and explicit bindings recorded with SHA256.')


if __name__ == '__main__':
    if '--record-native' in sys.argv:
        record_native()
    else:
        build()
