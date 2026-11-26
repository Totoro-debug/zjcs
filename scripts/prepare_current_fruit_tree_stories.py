"""Recover original fruit-tree unlock graphs, dialogue tables and client exports."""
import argparse
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_current_home_furniture_stories import decode_graph, sha, without_source_versions
from prepare_map16_story_source import decode_localization
from prepare_unity_map13_sources import encode, meta
from validation_workspace import validation_directory


NAMES = {'FruitTreeUnlock1', 'FruitTreeUnlock2'}
BUNDLE = 'defaultpackage_assets_scriptableconfig_story_fruittreeguide.bundle'
OUTPUT = ROOT / 'unity/Assets/Official/Home/Source/fruit-tree-unlock-stories.json'
RECEIPT = ROOT / 'assets/reference/home-fruit-tree-unlock-story-source-manifest.json'


def build(work):
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    apk = Apk.current()
    try:
        code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
        code_manifest = code.parent / 'code-manifest.json'
        if json.loads(code_manifest.read_bytes())['manifest'] != apk.version:
            raise ValueError('Selected source and decompiled story code differ')
        constructors = Constructors(code)
        schemas = ScriptSchemas(code_root=code)
        proofs = []

        def table(name, constructor, package='config_luban'):
            obj = apk.asset(package, '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            proofs.append(dict(source=apk.provenance(obj), table=name,
                object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw), bytes=len(raw)))
            return constructors.table(raw, constructor)

        stories = [row for row in table('story', 'TbStoryInfo') if row['StoryName'] in NAMES]
        if len(stories) != len(NAMES) or {row['StoryName'] for row in stories} != NAMES:
            raise ValueError('Original fruit-tree story registrations are incomplete')
        graphs, graph_proofs = {}, {}
        for name in sorted(NAMES):
            graphs[name], graph_proofs[name] = decode_graph(apk, schemas, name, BUNDLE)
        dialogs = [row for row in table('dialog', 'TbDialogInfo') if row['StoryName'] in NAMES]
        options = [row for row in table('story_option_dialog', 'TbStoryOptionInfo') if row['StoryName'] in NAMES]
        role_ids = {row['RoleId'] for row in dialogs if row['RoleId'] > 0}
        roles = [row for row in table('Dialog/dialog_role', 'TbDialogRoleInfo', 'config_luban_dialog')
                 if row['RoleId'] in role_ids]
        if {row['RoleId'] for row in roles} != role_ids:
            raise ValueError('A fruit-tree dialogue role is missing')
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
                        raise ValueError('Original graph references a missing dialogue: ' + name)
                    if node['_type'] == 'OptionDialogNode' and not set(node['Options']).issubset(
                            {row['Id'] for row in options if row['StoryName'] == name}):
                        raise ValueError('Original graph references missing options: ' + name)
        keys = {f"dialog_{name}_{row['Id']}_{row['Order']}" for name, groups in dialogues.items()
                for rows in groups.values() for row in rows}
        keys.update(f"story_{row['StoryName']}_{row['Id']}_option" for row in options)
        localization, language_proofs = decode_localization(apk,
            keys | {f'dialog_role_{role}_name' for role in role_ids}, keys)
        server_records = {}
        for path, pointer in apk.container('config_story'):
            name = path.rsplit('/', 1)[-1].removesuffix('.json')
            if not path.endswith('.json') or name not in NAMES:
                continue
            obj = pointer.deref()
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            server_records[name] = json.loads(raw)
            proofs.append(dict(source=apk.provenance(obj), asset=path,
                object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw), bytes=len(raw)))
        if set(server_records) != NAMES:
            raise ValueError('Client-exported fruit-tree story records are incomplete')
        payload = encode(dict(storyRows=stories, graphs=graphs, dialogues=dialogues,
            dialogRoles=roles, storyOptions=options, localization=localization, serverRecords=server_records))
        sources = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (
            Path(__file__), ROOT / 'scripts/prepare_current_home_furniture_stories.py',
            ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_unity_schema.py',
            ROOT / 'scripts/prepare_map16_story_source.py', code_manifest, code / 'global-metadata.dat')}
        for name, text in constructors.sources.items():
            if not text:
                continue
            path = code / 'common-source/Common' / (name + '.cs')
            if not path.is_file() or name in ('Pos2d', 'Vector2', 'Vector3'):
                path = code / 'common-source/Common.LubanGen' / (name + '.cs')
            sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        types = {node['_type'] for graph in graphs.values() for node in graph['nodes'].values()}
        for name in sorted(types | {'StoryGraph', 'HomeFruitTree', 'HomeFruitTreeProduceFruit', 'FurnitureBase'}):
            path = code / 'client-source' / (name + '.cs')
            sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        proof = dict(manifest=apk.version, sources=sources, tables=without_source_versions(proofs),
            graphs=without_source_versions(graph_proofs), language=without_source_versions(language_proofs),
            schema_records=schemas.records, output=dict(path=OUTPUT.relative_to(ROOT).as_posix(),
                sha256=sha(payload), bytes=len(payload)),
            scope='Original source only; native unlock-story execution is not yet connected.')
        (work / OUTPUT.name).write_bytes(payload)
        (work / RECEIPT.name).write_bytes(encode(proof))
        print('Recovered fruit-tree unlock stories:', len(graphs), 'graphs,',
              sum(len(graph['nodes']) for graph in graphs.values()), 'nodes,', len(dialogs), 'dialogue rows')
    finally:
        apk.reader.close()


def install(work):
    proof = json.loads((work / RECEIPT.name).read_bytes())
    payload = (work / OUTPUT.name).read_bytes()
    if sha(payload) != proof['output']['sha256']:
        raise ValueError('Staged fruit-tree source changed')
    for name, digest in proof['sources'].items():
        if sha((ROOT / name).read_bytes()) != digest:
            raise ValueError('Original source changed: ' + name)
    if OUTPUT.exists() and (not RECEIPT.is_file() or
            sha(OUTPUT.read_bytes()) != json.loads(RECEIPT.read_bytes())['output']['sha256']):
        raise ValueError('Existing fruit-tree source differs from its receipt')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_bytes(payload)
    meta(OUTPUT)
    proof['output']['meta_sha256'] = sha(Path(str(OUTPUT) + '.meta').read_bytes())
    RECEIPT.write_bytes(encode(proof))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    args = parser.parse_args()
    (install if args.install else build)(validation_directory())
