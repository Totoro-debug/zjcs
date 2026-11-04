"""Recover current partner skill selection and original Animator cue graphs.

Only generated configuration and game application components are decoded.
Non-Animator effects remain recorded source nodes, not replacement effects.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re

from current_component_schemas import CurrentSchemas
from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_tables import Constructors
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed
from UnityPy.classes import PPtr


def build(work):
    work = work.resolve()
    if work.parent != ROOT / 'output/validation' or not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    apk, schemas = Apk.current(), CurrentSchemas()
    if apk.version != '884_156692':
        raise ValueError('Review the selected partner source version')
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
               re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    table_names = ('npc_level_detail', 'npc_profession_detail', 'skill')
    tables, table_proof, objects = {}, {}, []
    inventory = ROOT / 'unity/Assets/Official/Combat/NativePartners/catalog.json'
    npc_ids = sorted(row['npcId'] for row in json.loads(inventory.read_bytes())['fighters'])
    try:
        for obj in apk.objects('defaultpackage_assets_config_luban.bundle'):
            if obj.type.name != 'TextAsset':
                continue
            name = obj.read().m_Name.rsplit('/', 1)[-1].removesuffix('.g')
            if name not in table_names:
                continue
            if name in tables:
                raise ValueError('Ambiguous table: ' + name)
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            tables[name] = constructors.table(raw, mapping[name])
            table_proof[name] = dict(source=apk.provenance(obj),
                object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(),
                payload_sha256=hashlib.sha256(raw).hexdigest(), constructor=mapping[name])
        if set(tables) != set(table_names):
            raise ValueError('Missing current partner battle table')
        professions = {row['ClassId']: row for row in tables['npc_profession_detail']}
        skills = {row['ClassId']: row for row in tables['skill']}
        selected = []
        skill_ids = set()
        for npc_id in npc_ids:
            levels = [row for row in tables['npc_level_detail'] if row['ClassId'] == npc_id]
            if not levels:
                raise ValueError('Missing partner level rows: ' + str(npc_id))
            profession_ids = sorted({row['ProfessionDetailId'] for row in levels})
            active_ids = sorted({key for pid in profession_ids for key in professions[pid]['ActiveSkills']})
            active = [skills[key] for key in active_ids]
            for row in active:
                skill_ids.add(row['EcEntityId'])
                skill_ids.update(row['DirectionEcEntityId'].values())
                skill_ids.update(row['RandomSkillEntityId'])
            selected.append(dict(npc_id=npc_id, levels=levels,
                professions=[professions[key] for key in profession_ids], skills=active))

        assets = read_asset_manifest(apk.source.manifest_bytes('DefaultPackage', apk.archive))

        def component(go, name):
            matches = [ref.component.deref() for ref in go.read().m_Component
                       if ref.component.deref().type.name == 'MonoBehaviour'
                       and ref.component.deref().read(check_read=False).m_Script.read().m_ClassName == name]
            if len(matches) != 1:
                raise ValueError(f'Expected one {name} on {go.path_id}, got {len(matches)}')
            return matches[0]

        def pointer(owner, value):
            return PPtr(**value, assetsfile=owner.assets_file).deref() if value['m_PathID'] else None

        def read(obj):
            fields = schemas.read(obj)
            objects.append(dict(source=apk.provenance(obj),
                object_sha256=hashlib.sha256(obj.get_raw_data()).hexdigest(), fields=fields))
            return fields

        def walk(obj, time, cues, omitted):
            cls = obj.read(check_read=False).m_Script.read().m_ClassName
            fields = read(obj)
            node = dict(type=cls, time=time, source=apk.provenance(obj))
            if cls not in ('MyPlayableSequence', 'MyPlayableAnimator'):
                omitted.append(node)
                return node
            if fields['mySpeed'] != 1 or fields['myFadeInDuration'] != 0 or fields['myFadeOutDuration'] != 0:
                raise ValueError('Animator graph requires original speed/fade composition: ' + cls)
            if any(fields[name] for name in ('myTriggerList', 'myFadeoutTrigerList', 'myEndTrigerList')):
                raise ValueError('Animator graph has event-triggered children: ' + cls)
            if cls == 'MyPlayableSequence':
                node['children'] = []
                for row in fields['myPlayables']:
                    if row['myTime'] < 0:
                        raise ValueError('Negative original sequence time')
                    node['children'].append(walk(pointer(obj, row['myPlayable']), time + row['myTime'], cues, omitted))
            else:
                cue = dict(kind=0, time=time, speed=fields['mySpeed'], animation='', clip_resource='',
                    origin_clip_name='', trigger_param=fields['myTriggerParam'],
                    pre_state_name_allow_list=fields['PreStateNameAllowList'],
                    my_bool_param=fields['myBoolParam'], my_bool_value=bool(fields['myBoolValue']),
                    my_float_param=fields['myFloatParam'], my_float_value=fields['myFloatValue'],
                    int_param=fields['IntParam'], int_value=fields['IntValue'], my_duration=fields['myDuration'],
                    layer_name=fields['LayerName'], layer_weight=fields['LayerWeight'],
                    end_resume_param=bool(fields['myEndResumeParam']))
                cues.append(cue)
                node['cue_index'] = len(cues) - 1
            return node

        records = []
        for key in sorted(skill_ids - {0}):
            candidates = [row for row in assets if row['path'].endswith('/' + str(key) + '.prefab')]
            if len(candidates) != 1:
                raise ValueError(f'Ambiguous/missing original skill {key}: {len(candidates)}')
            owner = component(apk.asset(candidates[0]['bundle'], '/' + str(key) + '.prefab'), 'MyFightSkill')
            fields = read(owner)
            variants = []
            for field in ('myPlayablePrefab', 'UpPlayablePrefab', 'DownPlayablePrefab', 'LeftPlayablePrefab', 'RightPlayablePrefab'):
                root = pointer(owner, fields[field])
                if not root:
                    continue
                cues, omitted = [], []
                graph = walk(component(root, 'MyPlayableSequence'), 0., cues, omitted)
                variants.append(dict(field=field, playables=cues, graph=graph, non_animator_nodes=omitted))
            records.append(dict(skill_entity_id=key, variants=variants))
            print(key, [(v['field'], len(v['playables']), len(v['non_animator_nodes'])) for v in variants], flush=True)
    finally:
        apk.archive.close()
    payload = dict(manifest=apk.version, partners=selected, skills=records)
    target = work / 'partner-native-playables.json'
    write_changed(target, json.dumps(payload, ensure_ascii=False, separators=(',', ':')) + '\n')
    sources = []
    for name, source in constructors.sources.items():
        if not source:
            continue
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.is_file():
            path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if path.is_file():
            sources.append(identity(path))
    proof = dict(manifest=apk.version, selection=identity(inventory), tables=table_proof,
        generated_sources=sources, component_schemas=schemas.records, objects=objects, output=identity(target),
        application_sources=[identity(code / 'client-source' / (name + '.cs')) for name in
            ('MyFightSkill', 'MyPlayableAnimator')],
        sequence_source=identity(code / 'game-framework-source/GameFramework/MyPlayableSequence.cs'),
        generator=identity(Path(__file__).resolve()),
        limits=['Animator cues only; recorded non-Animator nodes are not implemented by this catalog.',
                'Sequence child order, delays and directional branches are retained; sequence completion uses the original child lifecycle, not a manufactured duration.'])
    write_changed(work / 'partner-native-playables-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Selected', len(selected), 'partners and', len(records), 'current skills', flush=True)
    return payload, proof


def install(work, proof):
    target = ROOT / 'unity/Assets/Game/Resources/Combat/partner-native-playables.json'
    receipt = ROOT / 'assets/reference/partner-native-playables-source-manifest.json'
    raw = (work / 'partner-native-playables.json').read_bytes()
    roster = ROOT / 'unity/Assets/Official/Partners/partner-rules.json'
    rules = json.loads(roster.read_bytes())
    if rules['manifest'] != proof['manifest']:
        raise ValueError('Partner roster and skill versions differ')
    npcs = {row['Id']: row for row in rules['tables']['npc']}
    inventory = json.loads((ROOT / proof['selection']['path']).read_bytes())
    if inventory['manifest'] != proof['manifest'] or any(
            npcs[row['npcId']]['FightEntityId'] != row['entityId'] for row in inventory['fighters']):
        raise ValueError('Current partner battle entity selection differs')
    proof['current_roster'] = identity(roster)
    if target.exists() and target.read_bytes() != raw and (not receipt.is_file() or hashlib.sha256(target.read_bytes()).hexdigest()
                            != json.loads(receipt.read_bytes())['output']['sha256']):
        raise ValueError('Preserve the existing partner playable payload until its provenance matches')
    target.write_bytes(raw)
    validation = ROOT / 'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativePlayableAcceptance.cs'
    for path in (target, validation):
        metadata = Path(str(path) + '.meta')
        if not metadata.exists():
            meta(path)
        elif not re.search(r'(?m)^guid: [0-9a-f]{32}$', metadata.read_text('utf8')):
            raise ValueError('Invalid new partner playable metadata: ' + str(metadata))
        # This generator owns only these newly introduced import files.
        write_changed(metadata, '\n'.join(line.rstrip() for line in metadata.read_text('utf8').splitlines()) + '\n')
    proof['output'] = identity(target)
    proof['outputs'] = [identity(path) for path in (Path(str(target) + '.meta'), validation, Path(str(validation) + '.meta'))]
    folder = validation.parent
    definition = folder / 'Zjcs.Editor.PartnerBattlePlayables.asmdef'
    for path in (folder, definition):
        metadata = Path(str(path) + '.meta')
        if not metadata.exists():
            meta(path)
        write_changed(metadata, '\n'.join(line.rstrip() for line in metadata.read_text('utf8').splitlines()) + '\n')
    proof['outputs'].extend(identity(path) for path in (Path(str(folder) + '.meta'), definition, Path(str(definition) + '.meta')))
    proof['validation_factory'] = identity(ROOT / 'scripts/prepare_partner_native_playables_validation.py')
    code = ROOT / '.tools/source-packages/current' / proof['manifest']
    proof['decoding_inputs'] = [identity(path) for path in
        [ROOT / 'scripts' / name for name in ('official_tables.py', 'official_metadata.py', 'official_unity_schema.py',
                                             'current_component_schemas.py', 'official_apk.py', 'official_manifest.py')]
        + [code / 'code-manifest.json', code / 'official-code/global-metadata.dat']
        + [code / 'native-conversion/changed-inputs/Managed' / (name + '.dll')
           for name in ('Assembly-CSharp', 'GameFramework.Runtime')]]
    proof['original_playback_binding'] = [identity(ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat' / name)
        for name in ('OfficialBattleAnimatorPlayback.cs', 'OfficialBattleAnimationTimeline.cs')]
    write_changed(receipt, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', type=Path, required=True)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    _, receipt = build(args.workspace)
    if args.install:
        install(args.workspace, receipt)
