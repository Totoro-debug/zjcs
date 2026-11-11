"""Restore ordered original summon role views and exact lifecycle graph closures."""
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/python-packages'))
sys.path.insert(0, str(ROOT / '.tools/character-art-python/Lib/site-packages'))
from UnityPy.classes import PPtr

from official_apk import Apk
from official_unity_schema import ScriptSchemas
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from prepare_battle_status_orbit_assets import build
from prepare_battle_movement_effects import SCRIPTS
from verify_battle_skill_graph_rules import verify_selected_components

BASE = ROOT / 'unity/Assets/Official/Combat/SummonRoleViews/Resources/Combat/SummonRoleViews'
DATA = BASE / 'role-views.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/summon-role-views-source-manifest.json'


def main():
    apk = Apk.current()
    if apk.version != '884_156692':
        raise ValueError('Review summon role views for the selected version')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    schemas = ScriptSchemas(code_root=code)
    entities = json.loads((ROOT / 'unity/Assets/Official/Combat/battle-rules.json').read_bytes())['entities']
    character_components = {str(entity): {'FightRoleCharacterComponent': entities[str(entity)]['FightRoleCharacterComponent']}
                            for entity in (3181, 3182, 3183)}
    selected_character_components = verify_selected_components(character_components)
    rows, sources, roots, originals = [], [], {}, {}
    try:
        for entity_id in (3181, 3182, 3183):
            bundle = f'defaultpackage_assets_prefabs_ecentity_fight_fightmonster_{entity_id}.bundle'
            found = []
            for obj in apk.objects(bundle):
                if obj.type.name != 'MonoBehaviour' or obj.read(check_read=False).m_Script.read().m_ClassName != 'ECViewDriver':
                    continue
                fields = schemas.read(obj)
                references = {row['rid']: row for row in fields['references']['RefIds']}
                for pointer in fields['mECViews']:
                    view = references.get(pointer['rid'])
                    if not view or view['type']['class'] != 'ECFightRoleCharacterView':
                        continue
                    data = view['data']
                    if data['mDelayVisibilityParts'] or data['mGenerateDelayTime'] != 0 or data['mGeneratePlayableNeedDelay']:
                        raise ValueError('Bind original delayed visibility before opening this role')
                    row = dict(entityId=entity_id, tryOnStart=bool(data['mTryDiePlayableOnStart']))
                    character = character_components[str(entity_id)]['FightRoleCharacterComponent']
                    row.update(DestroyOnDie=character['DestroyOnDie'], DestroyDelayTime=character['DestroyDelayTime'])
                    for field in ('mDiePlayable', 'mDieOnStartPlayable', 'mGeneratePlayable'):
                        value = data[field]
                        row[field] = ''
                        if not value['m_PathID']:
                            continue
                        target = PPtr(assetsfile=obj.assets_file, **value).deref()
                        name = target.read().m_Name
                        expected = 'playable_die_1' if field == 'mDiePlayable' else f'playable_{entity_id}_generate'
                        if field == 'mDieOnStartPlayable' or name != expected:
                            raise ValueError('Unexpected original summon lifecycle graph: ' + name)
                        path = ('Prefabs/Playables/' if field == 'mDiePlayable' else
                                'Prefabs/Playables/EC/Battle/Monster/') + name + '.prefab'
                        original = dict(source=apk.provenance(target),
                            object_sha256=hashlib.sha256(target.get_raw_data()).hexdigest())
                        if name in originals and originals[name] != original:
                            raise ValueError('Conflicting shared lifecycle graph identity')
                        roots[name], originals[name] = path, original
                        row[field] = 'Combat/SummonRoleViews/Graphs/' + path[:-7]
                    found.append(row)
                    sources.append(dict(entityId=entity_id, source=apk.provenance(obj),
                        object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                        driver_fields=fields, ordered_character_view=view))
            if len(found) != 1:
                raise ValueError('Expected one ordered original summon character view')
            rows.extend(found)
    finally:
        apk.archive.close()
    build(DEST=BASE / 'Graphs', DATA=BASE / 'graphs.json', RECEIPT=RECEIPT,
        ROOTS={}, graph_roots=roots, graph_originals=originals,
        family='SummonRoleViews', shader_prefix='SummonRoleViews_',
        MAPPED={**SCRIPTS, -822603460: ('StoryPlayables', 'ApplyBySource'),
                205640859: ('StoryAnimation', 'MyPlayableAnimator'),
                1574993081: ('BattleMaterialPlayables', 'MyPlayableMaterialProperty')},
        runtime_receipts=('scene-player-animator-source-manifest.json',), generator=Path(__file__),
        limits=['Exact 3181/3182/3183 ordered ECFightRoleCharacterView bindings and graph assets. '
                'Other EC views, action invisibility status 24008 and other summon models are outside this extraction.'])
    write_changed(DATA, json.dumps(dict(schema=2, manifest=apk.version, roles=rows), ensure_ascii=False, indent=2) + '\n')
    meta(DATA)
    proof = json.loads(RECEIPT.read_bytes())
    registry = ROOT / 'unity/Assets/Game/Editor/OfficialSparseShaderVariants.cs'
    registrations = dict(re.findall(r'\["([^"]+\.shader)"\]\s*=\s*(\d+)', registry.read_text('utf-8-sig')))
    shader_outputs = [row for row in proof['outputs'] if row['path'].endswith('.shader')]
    if len(shader_outputs) != len(proof['shaders']):
        raise ValueError('Bind each original summon shader to its program receipt')
    proof['shader_program_domains'] = []
    for output, shader in zip(shader_outputs, proof['shaders']):
        conversion = ROOT / shader['conversion']['path']
        if identity(conversion) != shader['conversion']:
            raise ValueError('Original summon shader program receipt differs')
        programs = json.loads(conversion.read_bytes())['programs']
        domains = {}
        for program in programs:
            key = (program['subshader'], program['pass_index'])
            domains.setdefault(key, set()).add(tuple(sorted(program['keywords'])))
        counts = {len(value) for value in domains.values()}
        path = output['path'].removeprefix('unity/')
        if len(counts) != 1 or registrations.get(path) != str(next(iter(counts))):
            raise ValueError('Register the exact original summon shader program domain before building: ' + path)
        proof['shader_program_domains'].append(dict(path=path, programs_per_pass=next(iter(counts)),
            original_passes=len(domains), original_conversion=shader['conversion']))
    proof.update(original_role_views=sources, decoder=identity(ROOT / 'scripts/official_unity_schema.py'),
        schema=identity(ROOT / 'scripts/unity_schema_types.cs'),
        official_role_view=identity(code / 'client-source/ECFightRoleCharacterView.cs'))
    archive = code.parent / 'client-decompilation-manifest.json'
    archived = {row['path']: row['sha256'] for project in json.loads(archive.read_bytes())['projects']
        for row in project['files']}
    original = code / 'client-source/ECFightRoleCharacterView.cs'
    if identity(original)['sha256'] != archived[original.relative_to(ROOT).as_posix()]:
        raise ValueError('Original role view differs from its pinned decompilation')
    proof['decompilation_receipt'] = identity(archive)
    proof['runtime_bindings'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleCharacterGenerationPlayback.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSummonRoleViewCatalog.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleLiveMonsterModelView.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleSceneView.cs')]
    proof['selected_character_components'] = selected_character_components
    proof['runtime_bindings'].append(identity(ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleCharacterDeathPlayback.cs'))
    proof['runtime_scope'] = ('Original generation and existing shared ECFightRoleCharacterView death methods use the original '
        'playable manager, Animator, MeshMaterialProperty and AppearanceRendererLite bindings. Visual retirement reads '
        'the selected original FightRoleCharacterComponent DestroyOnDie/DestroyDelayTime; no server entity removal is inferred.')
    proof['validation_inputs'] = [identity(ROOT / path) for path in (
        'unity/Assets/Game/Editor/OfficialSparseShaderVariants.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleSummonGenerationValidation.cs',
        'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.FirstPromotion.cs',
        'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleGraphCapabilityValidation.cs')]
    proof['outputs'].extend(identity(p) for p in (DATA, Path(str(DATA) + '.meta')))
    write_changed(RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Restored', len(rows), 'ordered summon role views and', len(roots), 'original lifecycle graphs')


if __name__ == '__main__':
    main()
