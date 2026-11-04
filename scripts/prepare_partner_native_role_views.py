"""Bind original partner EC role views to their exact local lifecycle graphs.

Reads application fields and asset data only; no animation SDK methods are used.
"""
import hashlib
import json
from pathlib import Path

from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_orbit_assets import build


BASE = ROOT / 'unity/Assets/Official/Combat/NativePartnerAssets/Resources/Combat/NativePartnerAssets'
VIEWS = BASE / 'role-views.json'
RECEIPT = ROOT / 'assets/reference/partner-native-role-views-source-manifest.json'


def main():
    inventory = ROOT / 'unity/Assets/Official/Combat/NativePartners/catalog.json'
    selected = json.loads(inventory.read_bytes())
    apk, schemas = Apk.current(), CurrentSchemas()
    if apk.version != selected['manifest'] or apk.version != '884_156692':
        raise ValueError('Review the original partner role view version')
    original_deaths = ROOT / 'assets/reference/battle-animation/character-death-assets-source-manifest.json'
    death_proof = json.loads(original_deaths.read_bytes())
    expected = {row['id']: row['source'] for row in death_proof['originals']}
    assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))
    records, sources = [], []

    def walk(go, found):
        children = []
        for ref in go.read().m_Component:
            obj = ref.component.deref()
            if obj.type.name in ('Transform', 'RectTransform'):
                children = obj.read().m_Children
            if obj.type.name != 'MonoBehaviour' or obj.read(check_read=False).m_Script.read().m_ClassName != 'ECViewDriver':
                continue
            fields = schemas.read(obj)
            for view in fields.get('references', {}).get('RefIds', []):
                if view['type']['class'] != 'ECFightRoleCharacterView':
                    continue
                data = view['data']
                if data['mDelayVisibilityParts']:
                    raise ValueError('Bind the original delayed visibility parts before opening this partner')
                row = dict(tryOnStart=bool(data['mTryDiePlayableOnStart']),
                    generateDelayTime=data['mGenerateDelayTime'],
                    generateNeedsDelay=bool(data['mGeneratePlayableNeedDelay']))
                for field, name in (('mDiePlayable', 'normal'), ('mDieOnStartPlayable', 'on_start'),
                                    ('mGeneratePlayable', 'generate')):
                    value = data[field]
                    row[field] = ''
                    if not value['m_PathID']:
                        continue
                    target = PPtr(**value, assetsfile=obj.assets_file).deref()
                    source = apk.provenance(target)
                    if name != 'generate':
                        if any(source[key] != expected[name][key] for key in ('bundle', 'path_id', 'raw_sha256')):
                            raise ValueError('Original partner death graph differs from the packaged graph')
                        row[field] = 'Combat/CharacterDeath/Prefabs/Playables/' + target.read().m_Name
                    else:
                        if target.read().m_Name != 'playable_10608_generate':
                            raise ValueError('Review the original partner generation graph')
                        row[field] = 'Combat/NativePartnerAssets/RoleGraphs/Prefabs/Playables/EC/Battle/Monster/playable_10608_generate'
                    sources.append(dict(field=field, source=source,
                        object_sha256=hashlib.sha256(target.get_raw_data()).hexdigest()))
                sources.append(dict(source=apk.provenance(obj),
                    object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), driver_fields=fields))
                found.append(row)
        for child in children:
            walk(child.read().m_GameObject.deref(), found)

    try:
        for partner in selected['fighters']:
            entity = partner['entityId']
            candidates = [row for row in assets if row['path'].endswith('/' + str(entity) + '.prefab')]
            if len(candidates) != 1:
                raise ValueError('Ambiguous original partner entity: ' + str(entity))
            found = []
            walk(apk.asset(candidates[0]['bundle'], '/' + str(entity) + '.prefab'), found)
            if len(found) != 1:
                raise ValueError('Expected one original partner character view')
            records.append(dict(npcId=partner['npcId'], entityId=entity, **found[0]))
    finally:
        apk.archive.close()
    build(DEST=BASE / 'RoleGraphs', DATA=BASE / 'role-graphs.json', RECEIPT=RECEIPT,
        ROOTS={}, graph_roots={'generate': 'Prefabs/Playables/EC/Battle/Monster/playable_10608_generate.prefab'},
        family='PartnerLifecycle', shader_prefix='PartnerLifecycle_',
        MAPPED={-822603460: ('StoryPlayables', 'ApplyBySource'),
                205640859: ('StoryAnimation', 'MyPlayableAnimator')},
        runtime_receipts=('scene-player-animator-source-manifest.json',), generator=Path(__file__),
        limits=['Original partner character view lifecycle bindings. Other EC views are not recreated by this module.'])
    write_changed(VIEWS, json.dumps(dict(manifest=apk.version, partners=records),
        ensure_ascii=False, separators=(',', ':')) + '\n')
    meta(VIEWS)
    proof = json.loads(RECEIPT.read_bytes())
    proof.update(partner_selection=identity(inventory), original_partner_views=sources,
        component_schemas=schemas.records, shared_death_graphs=identity(original_deaths))
    proof['runtime_bindings'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePartnerNativeModelView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimatorPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleCharacterDeathPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleShadowView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleRuntimeController.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSkillGraphPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleModelCatalog.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneBindings.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleUnitWorldHudView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialCombatAssetCatalog.cs',
        'unity/Assets/Game/Runtime/Offline/ProfessionUI/PartnerNativeBattleAttachments.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightBattleState.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightOfficialCombatCatalog.Movement.cs',
        'unity/Assets/Game/Runtime/Official/Combat/FightOfficialNpcAssistanceCatalog.cs')]
    proof['validation_inputs'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeRoleSceneProbe.cs',
        'scripts/prepare_partner_native_role_scene_validation.py',
        'scripts/prepare_partner_native_full_skill_graph_validation.py',
        'scripts/fixtures/map12_stele_npc_harness.cs')]
    proof['outputs'].extend(identity(path) for path in (VIEWS, Path(str(VIEWS) + '.meta')))
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Bound', len(records), 'original partner character views')


if __name__ == '__main__':
    main()
