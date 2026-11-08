"""Recover the original casters of the three embedded monster skill graphs.

Keep the installed monster inventory and its exact existing bindings. Read only
original asset data; reuse the existing full-binary and Windows asset converters.
"""
import argparse
import hashlib
import json
import re
import io
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from current_component_schemas import CurrentSchemas
from prepare_battle_live_monsters import build as extract, entity_prefab_family
from prepare_battle_live_monster_assets import build as install
from prepare_battle_monster_animator_bindings import build as animator_bindings
from prepare_battle_monster_animation_controllers import build as application_controllers
from prepare_battle_monster_attachments import main as attachments
from validation_workspace import validation_directory
from prepare_unity_battle_monster_portraits import sprite_meta, asset_yaml


SELECTED = (3331, 3481, 3620)
RECEIPT = ROOT / 'assets/reference/battle-animation/embedded-source-actors-source-manifest.json'


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def verified_loadouts():
    battle = ROOT / 'unity/Assets/Official/Combat/battle-rules.json'
    rules = ROOT / 'unity/Assets/Official/Combat/rules.json'
    groups = {row['MonsterId']: row['ActiveSkills'] for row in json.loads(battle.read_bytes())['tables']['monster_group']
        if row['MonsterId'] in SELECTED and row['GroupNo'] == 0}
    wanted = {item for values in groups.values() for item in values}
    skills = {row['ClassId']: row['EcEntityId'] for row in json.loads(rules.read_bytes())['skill'] if row['ClassId'] in wanted}
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    parser = Constructors(code)
    mapping = {name: cls for cls, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', parser.source('Tables'))}
    originals = {}
    try:
        if apk.version != '884_156692':
            raise ValueError('Review the original source actor table version')
        for name in ('monster_group', 'skill'):
            obj = apk.asset('config_luban', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf-8', 'surrogateescape')
            decoded = parser.table(raw, mapping[name])
            if name == 'monster_group':
                rows = [row for row in decoded if row['MonsterId'] in SELECTED and row['GroupNo'] == 0]
                if {row['MonsterId']: row['ActiveSkills'] for row in rows} != groups or set(groups) != set(SELECTED):
                    raise ValueError('The original source actor skill loadouts differ from the installed runtime')
            else:
                rows = [row for row in decoded if row['ClassId'] in wanted]
                if {row['ClassId']: row['EcEntityId'] for row in rows} != skills or any(
                        row['RandomSkillEntityId'] or row['DirectionEcEntityId'] for row in rows):
                    raise ValueError('Explicitly recover differing original source actor skill entity bindings')
            originals[name] = dict(source=apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                payload_sha256=hashlib.sha256(raw).hexdigest(), rows=rows)
    finally:
        apk.archive.close()
    constructors = []
    for name in sorted(parser.sources):
        candidates = [code / folder / (name + '.cs') for folder in ('common-source/Common', 'common-source/Common.LubanGen')]
        constructors.extend(identity(path) for path in candidates if path.is_file())
    return dict(originals=originals, constructors=constructors, runtime_inputs=[identity(battle), identity(rules)],
        operation='Compare original group-zero skill item/entity bindings only. Do not substitute numerical rules from another input version.')


def verified_unused_18160_branch():
    graph_receipt = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
    graph = json.loads(graph_receipt.read_bytes())
    original, = [row for row in graph['originals'] if row['id'] == '18160:myPlayablePrefab']
    apk, schemas = Apk.current(), CurrentSchemas()
    records = []
    try:
        root, = [obj for obj in apk.objects(original['source']['bundle'])
            if obj.type.name == 'GameObject' and obj.path_id == int(original['source']['path_id'])]
        if hashlib.sha256(root.get_raw_data()).hexdigest() != original['original_game_object_sha256']:
            raise ValueError('The original 18160 graph root changed')
        def visit(go, path=''):
            components = [ref.component.deref() for ref in go.read().m_Component]
            transform, = [obj for obj in components if obj.type.name == 'Transform']
            for obj in [go, transform] + [obj for obj in components if obj.type.name == 'MonoBehaviour']:
                fields = schemas.read(obj) if obj.type.name == 'MonoBehaviour' else obj.read_typetree()
                records.append(dict(path=path, type=obj.type.name,
                    class_name=obj.read(check_read=False).m_Script.read().m_ClassName if obj.type.name == 'MonoBehaviour' else '',
                    source=apk.provenance(obj), object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), fields=fields))
            for child in transform.read().m_Children:
                child_go = child.read().m_GameObject.deref()
                visit(child_go, path + '/' + child_go.read().m_Name if path else child_go.read().m_Name)
        visit(root)
        sequence, = [row for row in records if row['path'] == '' and row['class_name'] == 'MyPlayableSequence']
        unused, = [row for row in records if row['path'] == 'effect (1)' and row['class_name'] == 'MyPlayableFollowBone']
        skeleton, = [row for row in records if row['path'] == 'effect (1)/Spine GameObject (monster_3481)' and row['type'] == 'GameObject']
        scheduled = [entry['myPlayable']['m_PathID'] for entry in sequence['fields']['myPlayables']]
        if int(unused['source']['path_id']) in scheduled or skeleton['fields']['m_IsActive']:
            raise ValueError('Review the original 18160 dormant branch before changing its visibility checks')
        for row in records:
            if row['class_name'].startswith('MyPlayable') and any(row['fields'].get(key) for key in
                    ('myTriggerList', 'myFadeoutTrigerList', 'myEndTrigerList')):
                raise ValueError('Review additional original 18160 trigger links')
        selected = [row for row in records if row['path'] in ('', 'effect', 'effect (1)',
            'effect (1)/Spine GameObject (monster_3481)')]
        return dict(graph_manifest=identity(graph_receipt), original_records=selected,
            operation='18160 schedules the particle effect, not effect (1). Preserve the unused skeleton inactive; do not force-enable it for validation.')
    finally:
        apk.archive.close()


def recover_portrait():
    apk = Apk.current()
    try:
        obj, = [obj for obj in apk.objects('defaultpackage_assets_image_itemicon_gameheadicon_monster_map4countries_noatlas.bundle')
            if obj.type.name == 'Sprite' and obj.peek_name() == 'G_3620']
        sprite = obj.read()
        origin = apk.provenance(obj)
        legacy_receipt = ROOT / 'assets/reference/battle-hud/monster-portraits-source-manifest.json'
        legacy_bundle, = [row for row in json.loads(legacy_receipt.read_bytes())['bundles']
            if row['bundle'] == origin['bundle']]
        if legacy_bundle['bundle_sha256'] != origin['raw_sha256']:
            raise ValueError('Review differing portrait bundle versions before extending the original icon catalog')
        if (sprite.m_PixelsToUnits != 100 or sprite.m_Pivot.x != .5 or sprite.m_Pivot.y != .5
                or sprite.m_Extrude != 1 or sprite.m_IsPolygon
                or any(getattr(sprite.m_Border, axis) != 0 for axis in 'xyzw')):
            raise ValueError('Recover additional original portrait importer branches')
        texture = sprite.m_RD.texture.deref()
        stream = io.BytesIO(); sprite.image.save(stream, format='PNG')
        image = ROOT / 'unity/Assets/Official/Combat/EmbeddedSourcePortraits/G_3620.png'
        catalog = ROOT / 'unity/Assets/Game/Resources/Combat/EmbeddedSourceMonsterPortraitCatalog.asset'
        guid = lambda path: hashlib.md5(('zjcs-official:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest()
        script = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterPortraitCatalog.cs'
        script_guid = re.search(r'^guid: (\w+)', Path(str(script) + '.meta').read_text(), re.M).group(1)
        asset = asset_yaml(script_guid, [dict(iconId=3620, guid=guid(image))]).replace(
            b'm_Name: OfficialBattleMonsterPortraitCatalog', b'm_Name: EmbeddedSourceMonsterPortraitCatalog')
        # Decode the original ASTC pixels once; do not add Windows block compression
        # or importer alpha bleeding to the recovered sprite. Retain original sampling.
        original_texture = texture.read()
        settings = original_texture.m_TextureSettings
        if (original_texture.m_MipCount != 1 or original_texture.m_ColorSpace != 1
                or settings.m_FilterMode != 1 or settings.m_Aniso != 1 or settings.m_MipBias != 0
                or any(getattr(settings, name) != 1 for name in ('m_WrapU', 'm_WrapV', 'm_WrapW'))):
            raise ValueError('Recover differing original portrait texture sampling')
        metadata = sprite_meta(image, guid(image), guid(image)[:32]).replace(
            b'  alphaIsTransparency: 1\n', b'  alphaIsTransparency: 0\n').replace(
            b'  platformSettings: []\n',
            b'  platformSettings:\n  - serializedVersion: 3\n    buildTarget: Standalone\n'
            b'    maxTextureSize: 2048\n    resizeAlgorithm: 0\n    textureFormat: 4\n'
            b'    textureCompression: 0\n    compressionQuality: 100\n    crunchedCompression: 0\n'
            b'    allowsAlphaSplitting: 0\n    overridden: 1\n    androidETC2FallbackOverride: 0\n'
            b'    forceMaximumCompressionQuality_BC6H_BC7: 0\n')
        outputs = {image: stream.getvalue(), Path(str(image) + '.meta'): metadata,
            catalog: asset,
            Path(str(catalog) + '.meta'): ('fileFormatVersion: 2\nguid: ' + guid(catalog) + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode(),
            Path(str(image.parent) + '.meta'): ('fileFormatVersion: 2\nguid: ' + guid(image.parent) + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n').encode()}
        previous = json.loads(RECEIPT.read_bytes()).get('portrait', {}).get('outputs', {}) if RECEIPT.exists() else {}
        for path, raw in outputs.items():
            if path.exists() and path.read_bytes() != raw and previous.get(path.relative_to(ROOT).as_posix()) != identity(path)['sha256']:
                raise ValueError('Preserve changed original portrait output: ' + str(path))
            path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(raw)
        return dict(source=origin, legacy_receipt=identity(legacy_receipt),
            identical_legacy_bundle_sha256=legacy_bundle['bundle_sha256'],
            sprite_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
            texture=apk.provenance(texture), texture_sha256=hashlib.sha256(texture.get_raw_data()).hexdigest(),
            texture_sampling=dict(mip_count=original_texture.m_MipCount, color_space=original_texture.m_ColorSpace,
                filter_mode=settings.m_FilterMode, aniso=settings.m_Aniso, mip_bias=settings.m_MipBias,
                wrap_u=settings.m_WrapU, wrap_v=settings.m_WrapV, wrap_w=settings.m_WrapW),
            windows_conversion='Original decoded RGBA pixels; Standalone RGBA32, no additional block compression or alpha bleeding.',
            pixels_per_unit=sprite.m_PixelsToUnits, pivot=[sprite.m_Pivot.x, sprite.m_Pivot.y],
            rect={axis: getattr(sprite.m_Rect, axis) for axis in ('x', 'y', 'width', 'height')},
            outputs={path.relative_to(ROOT).as_posix(): identity(path)['sha256'] for path in outputs},
            operation='Original APK-first G_3620 sprite. Separate supplemental catalog preserves every existing portrait binding.')
    finally:
        apk.archive.close()


def main(reuse_extracted=False):
    work = validation_directory()
    base = ROOT / 'unity/Assets/Official/Combat/NativeMonsters'
    manifest = ROOT / 'assets/reference/battle-animation/live-monsters-source-manifest.json'
    previous = json.loads(manifest.read_bytes())
    inventory = base / 'inventory.json'
    if previous['manifest'] != '884_156692' or identity(inventory)['sha256'] != previous['outputs'][inventory.relative_to(ROOT).as_posix()]:
        raise ValueError('Preserve the existing original monster inventory until its identity matches')
    before = json.loads(inventory.read_bytes())
    entities = sorted({row['entityId'] for row in before['models']} | set(SELECTED))

    # The existing native texture is a verified serializer template. Every actual
    # texture field and every original mip is replaced by the shared converter.
    template_source = base / next(iter(previous['textures'].values()))['file']
    template_identity = identity(template_source)
    if template_identity['sha256'] != previous['outputs'][template_identity['path']]:
        raise ValueError('The verified Windows texture serializer template changed')
    template = template_source.read_text(encoding='utf-8')
    if '--- !u!28 &2800000\n' not in template:
        raise ValueError('Unexpected native texture serializer header')
    template = template.replace('--- !u!28 &2800000\n', '--- !u!28 &1\n', 1)
    (work / 'monster-texture-template.asset').write_text(template, encoding='utf-8', newline='\n')

    if not reuse_extracted:
        apk = Apk.current()
        try:
            groups = dict(fightmonster=[], fightpet=[])
            for entity in entities:
                groups[entity_prefab_family(apk, entity)].append(entity)
        finally:
            apk.archive.close()
        extract(groups['fightmonster'], groups['fightpet'])
    extracted = json.loads((work / 'live-monsters/catalog.json').read_bytes())
    if sorted(row['entityId'] for row in extracted['models']) != entities:
        raise ValueError('The extracted inventory must preserve every installed original model')
    install()
    animator_bindings()
    application_controllers()
    attachments()
    loadouts = verified_loadouts()

    receipts = [manifest,
        ROOT / 'assets/reference/battle-animation/monster-animator-bindings-source-manifest.json',
        ROOT / 'assets/reference/battle-animation/monster-animation-controllers-source-manifest.json',
        ROOT / 'assets/reference/battle-animation/monster-attachments-source-manifest.json',
        ROOT / 'assets/reference/battle-animation/monster-additional-shadows-source-manifest.json']
    proof = dict(manifest='884_156692', selected_entities=list(SELECTED),
        installed_entities=entities, generator=identity(Path(__file__).resolve()),
        serializer_template=dict(source=template_identity,
            normalized_header='2800000 -> 1', sha256=hashlib.sha256(template.encode('utf-8')).hexdigest()),
        dependent_manifests=[identity(path) for path in receipts],
        original_loadout_bindings=loadouts,
        original_18160_branch=verified_unused_18160_branch(),
        portrait=recover_portrait(),
        implementation=[identity(ROOT / path) for path in (
            'unity/Assets/Game/Editor/ProfessionUI/OfficialBattleMonsterDataValidation.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleEmbeddedMonsterValidation.cs',
            'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleMonsterPortraitCatalog.cs',
            'unity/Assets/Game/Runtime/Presentation/Home/SkillSummonPlayerValidation.cs')],
        operation='APK-first full original source caster skeletons, textures, controllers, attachment registries and shadows. '
            'Preserve installed models; no server rules or skill loadouts are changed.')
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', encoding='utf-8', newline='\n')
    print('Original embedded source actors installed:', SELECTED, '; retained inventory:', len(entities))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--reuse-extracted', action='store_true', help='Verify and reuse this owned workspace\'s complete extraction')
    main(parser.parse_args().reuse_extracted)
