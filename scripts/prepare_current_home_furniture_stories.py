"""Recover the selected home furniture's original story and dialogue data."""
import argparse
import hashlib
import json
from pathlib import Path

from UnityPy.classes.PPtr import PPtr

from official_apk import Apk, ROOT
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_map16_story_source import decode_localization
from prepare_unity_map13_sources import encode, meta
from validation_workspace import validation_directory


STORIES = {
    'UnlockCommunication': 'defaultpackage_assets_scriptableconfig_story_map11story_basicguide.bundle',
    'SenSenHomeStore': 'defaultpackage_assets_scriptableconfig_story_map11story_sensenstory.bundle',
}
OUTPUT = ROOT / 'unity/Assets/Official/Home/Source/furniture-unlock-stories.json'
RECEIPT = ROOT / 'assets/reference/home-furniture-unlock-story-source-manifest.json'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def without_source_versions(value):
    if isinstance(value, dict):
        return {key: without_source_versions(item) for key, item in value.items()
                if key not in ('manifest', 'source_manifest', 'apk_manifest', 'resource_version')}
    if isinstance(value, list):
        return [without_source_versions(item) for item in value]
    return value


def decode_graph(apk, schemas, name, bundle):
    graph, = [obj for obj in apk.objects(bundle) if obj.type.name == 'MonoBehaviour'
        and obj.read(check_read=False).m_Name == name
        and obj.read(check_read=False).m_Script.read().m_ClassName == 'StoryGraph']
    definition = schemas.read(graph)
    nodes, by_path, proofs = {}, {}, []
    for raw_pointer in definition['nodes']:
        obj = PPtr(**raw_pointer, assetsfile=graph.assets_file).deref()
        data = schemas.read(obj)
        node_id = str(data['NodeId'])
        if node_id in nodes or str(obj.path_id) in by_path:
            raise ValueError('Duplicate original story node: ' + name + '/' + node_id)
        by_path[str(obj.path_id)] = node_id
        data['_type'] = obj.read(check_read=False).m_Script.read().m_ClassName
        nodes[node_id] = data
        proofs.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
                           node_id=int(node_id), node_type=data['_type']))
    for data in nodes.values():
        ports = {}
        for port in data.pop('ports', {}).get('values', []):
            connections = []
            for link in port['connections']:
                target = str(link['node']['m_PathID'])
                if target == '0':
                    continue
                if target not in by_path:
                    raise ValueError('Story port targets an external node: ' + name + '/' + target)
                connections.append(dict(node=by_path[target], port=link['fieldName']))
            if port['_fieldName'] in ports:
                raise ValueError('Duplicate original story port: ' + name)
            ports[port['_fieldName']] = dict(direction=port['_direction'],
                flow=port.get('_flowConnectionType', 0), connections=connections)
        data['ports'] = ports
        for field in ('WrappingInputNode', 'WrappingOutputNode', 'GoTo'):
            if field in data:
                target = str(data[field]['m_PathID'])
                if target != '0' and target not in by_path:
                    raise ValueError('Story node references an external node: ' + name + '/' + target)
                data[field] = by_path.get(target, '')
        if data.get('Subgraph', {}).get('m_PathID'):
            raise ValueError('Recover original subgraph before using story: ' + name)
        for field in ('m_GameObject', 'm_Script', 'm_Enabled', 'graph', 'position', 'Mono', 'IsMonoShow'):
            data.pop(field, None)
    return (dict(nodes=nodes, properties={key: value for key, value in definition.items() if key != 'nodes'}),
            dict(source=apk.provenance(graph), object_sha256=sha(graph.get_raw_data()), nodes=proofs))


def build(work):
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    code_manifest = code.parent / 'code-manifest.json'
    if json.loads(code_manifest.read_bytes()).get('manifest') != apk.version:
        raise ValueError('Story code manifest differs from the selected source')
    constructors = Constructors(code)
    schemas = ScriptSchemas(code_root=code)
    table_proofs = []

    def table(name, constructor, package='config_luban'):
        obj = apk.asset(package, '/' + name + '.g.bytes')
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        table_proofs.append(dict(source=apk.provenance(obj), table=name, constructor=constructor,
            object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw), bytes=len(raw)))
        return constructors.table(raw, constructor)

    names = set(STORIES)
    story_rows = [row for row in table('story', 'TbStoryInfo') if row.get('StoryName') in names]
    if {row['StoryName'] for row in story_rows} != names or len(story_rows) != len(names):
        raise ValueError('Selected furniture story rows are incomplete or ambiguous')
    graphs, graph_proofs = {}, {}
    for name, bundle in STORIES.items():
        graphs[name], graph_proofs[name] = decode_graph(apk, schemas, name, bundle)
    dialogs = [row for row in table('dialog', 'TbDialogInfo') if row.get('StoryName') in names]
    options = [row for row in table('story_option_dialog', 'TbStoryOptionInfo') if row.get('StoryName') in names]
    role_ids = {row['RoleId'] for row in dialogs if row['RoleId'] > 0}
    roles = [row for row in table('Dialog/dialog_role', 'TbDialogRoleInfo', 'config_luban_dialog')
             if row['RoleId'] in role_ids]
    if {row['RoleId'] for row in roles} != role_ids:
        raise ValueError('A selected story dialogue role is missing')
    dialogues = {}
    for row in dialogs:
        dialogues.setdefault(row['StoryName'], {}).setdefault(row['Id'], []).append(row)
    for groups in dialogues.values():
        for rows in groups.values():
            rows.sort(key=lambda row: row['Order'])
    for name, graph in graphs.items():
        for node in graph['nodes'].values():
            if node['_type'] in ('DialogNode', 'OptionDialogNode'):
                if node['DialogId'] not in dialogues.get(name, {}):
                    raise ValueError('Story node has no dialogue rows: ' + name + '/' + str(node['NodeId']))
                if node['_type'] == 'OptionDialogNode':
                    available = {row['Id'] for row in options if row['StoryName'] == name}
                    if not set(node['Options']).issubset(available):
                        raise ValueError('Story node has missing option rows: ' + name)
    keys = {f"dialog_{story}_{row['Id']}_{row['Order']}" for story, groups in dialogues.items()
        for rows in groups.values() for row in rows}
    keys.update(f"story_{row['StoryName']}_{row['Id']}_option" for row in options)
    role_keys = {f"dialog_role_{role_id}_name" for role_id in role_ids}
    # The original TalkDialog checks ContainsKey and hides the name plate when
    # a role name is absent, so these lookups must remain optional.
    localization, language_proofs = decode_localization(apk, keys | role_keys, keys)
    server_records = {}
    for path, ptr in apk.container('config_story'):
        name = path.rsplit('/', 1)[-1].removesuffix('.json')
        if not path.endswith('.json') or name not in names:
            continue
        obj = ptr.deref()
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        server_records[name] = json.loads(raw)
        table_proofs.append(dict(source=apk.provenance(obj), asset=path,
            object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw), bytes=len(raw)))
    if set(server_records) != names:
        raise ValueError('Selected client-exported story records are incomplete')
    payload = encode(dict(storyRows=story_rows, graphs=graphs,
        dialogues=dialogues, dialogRoles=roles, storyOptions=options,
        localization=localization, serverRecords=server_records))
    (work / 'furniture-unlock-stories.json').write_bytes(payload)
    sources = {}
    for name, text in constructors.sources.items():
        if not text:
            continue
        source = code / 'common-source/Common' / (name + '.cs')
        if not source.is_file() or name in ('Pos2d', 'Vector2', 'Vector3'):
            source = code / 'common-source/Common.LubanGen' / (name + '.cs')
        sources[source.relative_to(ROOT).as_posix()] = sha(source.read_bytes())
    for path in (Path(__file__), ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_unity_schema.py',
                 ROOT / 'scripts/prepare_map16_story_source.py', code / 'global-metadata.dat',
                 code / 'client-source/FurnitureBase.cs', code / 'client-source/DialogNode.cs',
                 code / 'client-source/OptionDialogNode.cs', code / 'client-source/OptionTalkDialogPanel.cs',
                 code / 'client-source/TalkDialog.cs', code / 'common-source/Common/DialogInfoParser.cs', code_manifest,
                 code / 'common-source/Common/LanguageUtils.cs'):
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    node_types = {node['_type'] for graph in graphs.values() for node in graph['nodes'].values()}
    for node_type in sorted(node_types | {'StoryGraph'}):
        path = code / 'client-source' / (node_type + '.cs')
        if not path.is_file():
            raise FileNotFoundError('Original story class is missing: ' + node_type)
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    proof = dict(manifest=apk.version, sources=sources, tables=without_source_versions(table_proofs), graphs=without_source_versions(graph_proofs),
        language=without_source_versions(language_proofs), schema_records=schemas.records,
        output=dict(path=OUTPUT.relative_to(ROOT).as_posix(), sha256=sha(payload), bytes=len(payload)),
        counts=dict(graphs=len(graphs), nodes=sum(len(g['nodes']) for g in graphs.values()),
            dialogues=len(dialogs), roles=len(roles), options=len(options), localization=len(localization)),
        scope='Original graph and text source only; scene execution and native dialogue UI require separate integration.')
    (work / 'furniture-unlock-stories-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Recovered original furniture stories:', proof['counts'], flush=True)


def install(work):
    proof = json.loads((work / 'furniture-unlock-stories-manifest.json').read_bytes())
    payload = (work / 'furniture-unlock-stories.json').read_bytes()
    if sha(payload) != proof['output']['sha256']:
        raise ValueError('Staged story source changed')
    for relative, expected in proof['sources'].items():
        if sha((ROOT / relative).read_bytes()) != expected:
            raise ValueError('Story source changed: ' + relative)
    if OUTPUT.exists():
        if not RECEIPT.is_file():
            raise ValueError('Refusing to replace an untracked furniture story source')
        previous = json.loads(RECEIPT.read_bytes())
        if sha(OUTPUT.read_bytes()) != previous['output']['sha256']:
            raise ValueError('Existing furniture story source differs from its receipt')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    meta(OUTPUT.parent, folder=True)
    OUTPUT.write_bytes(payload)
    meta(OUTPUT)
    proof['output']['meta_sha256'] = sha(Path(str(OUTPUT) + '.meta').read_bytes())
    proof['output']['folder_meta_sha256'] = sha(Path(str(OUTPUT.parent) + '.meta').read_bytes())
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    (install if args.install else build)(validation_directory())
