"""Restore the exact death graphs referenced by current original player views."""
import hashlib
import json
from pathlib import Path

from UnityPy.classes import PPtr
from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_orbit_assets import build


RECEIPT = ROOT / 'assets/reference/battle-animation/character-death-assets-source-manifest.json'
VIEWS = ROOT / 'unity/Assets/Game/Resources/Combat/character-death-views.json'


def main():
    apk = Apk.current()
    schemas = CurrentSchemas()
    roles, source_views = [], []
    names = {'mDiePlayable': 'normal', 'mDieOnStartPlayable': 'on_start'}
    expected = {'normal': 'playable_die', 'on_start': 'playable_die_onStart'}
    roots = {}
    try:
        assert apk.version == '884_156692'
        for entity_id in (3100, 3102, 3991):
            bundle = f'defaultpackage_assets_prefabs_ecentity_fight_fightcharacter_{entity_id}.bundle'
            for component in apk.objects(bundle):
                if component.type.name != 'MonoBehaviour':
                    continue
                script = component.read(check_read=False).m_Script.read()
                if script.m_ClassName != 'ECViewDriver':
                    continue
                fields = schemas.read(component)
                for reference in fields.get('references', {}).get('RefIds', []):
                    if reference['type']['class'] != 'ECFightRoleCharacterView':
                        continue
                    data = reference['data']
                    row = dict(entity_id=entity_id, try_on_start=bool(data['mTryDiePlayableOnStart']))
                    for field, key in names.items():
                        pointer = data[field]
                        if not pointer['m_PathID']:
                            row[field] = ''
                            continue
                        target = PPtr(**pointer, assetsfile=component.assets_file).deref()
                        assert target.read().m_Name == expected[key]
                        path = f'Prefabs/Playables/{expected[key]}.prefab'
                        if key in roots:
                            assert roots[key] == path
                        roots[key] = path
                        row[field] = key
                    roles.append(row)
                    source_views.append(dict(entity_id=entity_id, source=apk.provenance(component),
                        object_sha256=hashlib.sha256(component.get_raw_data()).hexdigest(),
                        driver_fields=fields, character_view_reference=reference))
        assert {row['entity_id'] for row in roles} == {3100, 3102, 3991}
        assert set(roots) == set(expected)
    finally:
        apk.archive.close()

    build(DEST=ROOT / 'unity/Assets/Official/Combat/CharacterDeath/Resources/Combat/CharacterDeath',
        DATA=ROOT / 'unity/Assets/Game/Resources/Combat/character-death-graphs.json',
        RECEIPT=RECEIPT, ROOTS={}, graph_roots=roots, family='CharacterDeath', shader_prefix='CharacterDeath_',
        MAPPED={-253978515: ('StoryPlayables', 'MyPlayableSequence'),
            -822603460: ('StoryPlayables', 'ApplyBySource'),
            205640859: ('StoryAnimation', 'MyPlayableAnimator')},
        runtime_receipts=('scene-player-animator-source-manifest.json',), generator=Path(__file__),
        limits=['Original death graphs and role-view bindings only. Full player prefab, native player pose '
            'consumer and shared battle death lifecycle/Windows Player integration require separate validation.'])
    write_changed(VIEWS, json.dumps(dict(manifest='884_156692', roles=roles), ensure_ascii=False, indent=2) + '\n')
    meta(VIEWS)
    proof = json.loads(RECEIPT.read_bytes())
    proof['original_role_views'] = source_views
    proof['schemas'] = schemas.records
    proof['outputs'].extend(identity(p) for p in (VIEWS, Path(str(VIEWS) + '.meta')))
    base = ROOT / '.tools/source-packages/current/884_156692'
    code = base / 'official-code'
    archive = base / 'client-decompilation-manifest.json'
    archived = {row['path']: row['sha256'] for project in json.loads(archive.read_bytes())['projects']
        for row in project['files']}
    sources = [code / 'client-source' / name for name in (
        'ECFightRoleCharacterView.cs', 'AnimationController.cs', 'MyPlayableAnimator.cs')]
    for path in sources:
        assert identity(path)['sha256'] == archived[path.relative_to(ROOT).as_posix()]
    runtime = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat'
    ports = [runtime / (name + '.cs') for name in (
        'OfficialBattleCharacterDeathCatalog', 'OfficialBattleCharacterDeathPlayback')]
    ports.append(ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleCharacterDeathValidation.cs')
    proof['death_method_port'] = dict(archive_receipt=identity(archive),
        official_sources=[identity(p) for p in sources],
        outputs=[identity(p) for p in ports] + [identity(Path(str(p) + '.meta')) for p in ports],
        operation='Port only ECFightRoleCharacterView death methods, retaining initialization gate, '
            'try-on-start, normal/on-start nullable prefab selection, original manager Play source/target, '
            'force-resume-all-pauses, stop-then-replay and Stop handle cleanup. The exact bound entity '
            'root/current death value replace global MyEntity lookup and component subscription. '
            'This is not a port of the complete role view or production scene integration.')
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Restored exact normal/on-start death sequences and all three original character role bindings.')


if __name__ == '__main__':
    main()
