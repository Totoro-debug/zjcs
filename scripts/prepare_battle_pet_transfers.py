"""Recover the original managed-reference pet transfer views and embedded graphs."""
import json
from pathlib import Path

from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_battle_pet_followers import identity_object
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_orbit_assets import build as build_graphs, SCRIPTS

DATA = ROOT / 'unity/Assets/Game/Resources/Combat/pet-transfers.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/pet-transfers-source-manifest.json'
CODE = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'


def object_path(obj):
    transform = obj.read().m_Component[0].component.read()
    parts = [obj.read().m_Name]
    while transform.m_Father.path_id:
        transform = transform.m_Father.read()
        parts.insert(0, transform.m_GameObject.read().m_Name)
    return '/'.join(parts[1:])


def build():
    inventory = ROOT / 'unity/Assets/Official/Combat/NativeMonsters/inventory.json'
    models = json.loads(inventory.read_bytes())['models']
    schemas, apk = CurrentSchemas(), Apk.current()
    rows, originals, graphs, adults, graph_sources = [], [], {}, [], {}
    try:
        for model in models:
            entity = model['entityId']
            if not 9200 < entity < 9400: continue
            bundle = f'prefabs_ecentity_fight_fightpet_{entity}'
            for component in apk.objects(bundle):
                if component.type.name != 'MonoBehaviour': continue
                cls = component.read(check_read=False).m_Script.read().m_ClassName
                if cls == 'MyFightRolePetEntity':
                    fields = schemas.read(component)
                    assert fields['mTranferPlayable']['m_PathID'] == 0
                    adults.append(dict(entityId=entity, transferPlayable=None, original=identity_object(apk, component)))
                if cls != 'ECViewDriver': continue
                fields = schemas.read(component)
                for reference in fields.get('references', {}).get('RefIds', []):
                    if reference['type']['class'] != 'ECFightRolePetTransferView': continue
                    view = reference['data']
                    def deref(value): return PPtr(**value, assetsfile=component.assets_file).deref()
                    hide, show = deref(view['mHidePlayable']), deref(view['mShowPlayable'])
                    assert hide.path_id == show.path_id and hide.read().m_Name.startswith('playable_fight_pethide'), (entity, hide.read().m_Name, show.read().m_Name)
                    graph_id = hide.read().m_Name
                    graphs[graph_id] = (f'GameObject/{graph_id}.prefab' if graph_id == 'playable_fight_pethide'
                        else f'Prefabs/Playables/EC/Battle/Monster/{graph_id}.prefab')
                    graph_sources.setdefault(graph_id, identity_object(apk, hide))
                    rows.append(dict(entityId=entity, transferTime=view['mTransferTime'],
                        graphGroups=[object_path(deref(value)) for value in view['mGraphGroups']],
                        hidePlayable=graph_id, showPlayable=graph_id))
                    originals.append(dict(entityId=entity, driver=identity_object(apk, component),
                        managed_reference=reference, graph=identity_object(apk, hide)))
    finally:
        apk.archive.close()
    assert len(rows) == 21 and len(adults) == 19
    mapped = dict(SCRIPTS)
    mapped[1640720687] = ('StorySounds', 'MyPlayableSound')
    build_graphs(DEST=ROOT / 'unity/Assets/Official/Combat/PetTransfers/Resources/Combat/PetTransfers',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/pet-transfer-graphs.json', RECEIPT=RECEIPT,
        ROOTS={}, graph_roots=graphs, family='PetTransfers', shader_prefix='PetTransfer_', MAPPED=mapped,
        generator=Path(__file__), graph_originals=graph_sources,
        reference_scripts={-210918009: dict(name='WwiseEventReference',
            native_asset='Wwise/ScriptableObjects/Event/7FAF45CE-17E9-4F3D-93C0-234CED34F8B2.asset',
            runtime='unity/Assets/Game/Runtime/Official/ProfessionUI/WwiseEventReference.cs',
            receipt='assets/reference/profession-ui/audio-source-manifest.json')},
        operation='Convert the complete original hide/show graphs referenced by the managed pet transfer view, preserving every node, component and original local fileID. Resolve native-export standalone GameObject prefabs back to their actual source bundle and object.',
        limits=['Original transfer presentation only. No new entityization energy, summoning or support timing rules.'])
    write_changed(DATA, json.dumps(dict(manifest='884_156692', models=rows), indent=2) + '\n')
    meta(DATA)
    proof = json.loads(RECEIPT.read_bytes())
    proof.update(original_views=originals, adult_transfer_bindings=adults, component_schemas=schemas.records,
        original_view_code=identity(CODE / 'ECFightRolePetTransferView.cs'),
        original_adult_code=identity(CODE / 'MyFightRolePetEntity.cs'), selection=identity(inventory))
    proof['runtime_bindings'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Official/BattleSkillPlayables/ECFightRolePetTransferView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePetTransferCatalog.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattlePetFollowerView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveMonsterModelView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.Pets.cs')]
    proof['validation_sources'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Editor/OfficialBattlePetFollowerAcceptance.cs',
        'unity/Assets/Game/Editor/OfficialBattlePetHostAcceptance.cs')]
    proof['outputs'].extend(identity(p) for p in (DATA, Path(str(DATA) + '.meta')))
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recovered', len(rows), 'child transfer views;', len(adults), 'original null adult transfer graphs')


if __name__ == '__main__': build()

