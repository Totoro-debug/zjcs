"""Bind selected tip_dot rows to the three recovered original partner graphs."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_tables import Constructors
from prepare_partner_input_dialog import metadata
from prepare_current_partner_native import documents
from validation_workspace import validation_directory

GRAPHS = {
    'DiningTable': 'unity/Assets/Official/Partners/UI/Panel/Graph/65c863354b905727.asset',
    'NpcDetailItem': 'unity/Assets/Official/Partners/UI/Card/Graph/f89cc69ce5f93418.asset',
    'NpcProfileItem': 'unity/Assets/Official/Partners/UI/Card/Graph/cf8b4773fedab97d.asset',
}


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def guid(path): return re.search(r'(?m)^guid: ([a-f0-9]{32})', path.read_text('utf8'))[1]


def main():
    work = validation_directory()
    proof_path = work / 'npc-shared-inputs.json'
    proof = json.loads(proof_path.read_bytes())
    if proof['manifest'] != '884_156692': raise ValueError('Review selected table extraction')
    table = work / 'star-god-unity/Assets/NpcSharedConfiguration/Resources/PartnerNpcShared/tip_dot.g.bytes'
    source = proof['tables']['tip_dot.g']
    if sha(table) != source['payload_sha256']: raise ValueError('Cached selected APK table differs')
    constructors = Constructors(ROOT / '.tools/source-packages/current/884_156692/official-code')
    rows = constructors.table(table.read_bytes(), 'TbTipDotInfo')
    if len({row['GraphName'] for row in rows}) != len(rows): raise ValueError('Duplicate original graph definitions')
    native_path = ROOT / 'assets/reference/partner-panel-native-source-manifest.json'
    native = json.loads(native_path.read_bytes())
    if native['manifest'] != proof['manifest']: raise ValueError('Review graph resource version')
    graph_sources, graph_metadata = {}, {}
    for name, relative in GRAPHS.items():
        path = ROOT / relative
        if native['installed_output_sha256'][relative] != sha(path): raise ValueError('Original graph output changed: ' + name)
        value = documents(path.read_text('utf8'))[11400000]['MonoBehaviour']
        row, = [row for row in rows if row['GraphName'] == name]
        if value['m_Name'] != name:
            raise ValueError('Graph/table identity differs: ' + name)
        graph_metadata[name] = dict(serialized_type=value['Type'], serialized_scope=value['Scope'], configuration=row)
        graph_sources[relative] = sha(path)
        graph_sources[relative + '.meta'] = sha(Path(str(path) + '.meta'))
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/PartnersUI/OfficialPartnerTipDotAssetCatalog.cs'
    script_meta = metadata(script)
    output = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerTipDotAssetCatalog.asset'
    lines = ['%YAML 1.1', '%TAG !u! tag:unity3d.com,2011:', '--- !u!114 &11400000', 'MonoBehaviour:',
        '  m_ObjectHideFlags: 0', '  m_CorrespondingSourceObject: {fileID: 0}', '  m_PrefabInstance: {fileID: 0}',
        '  m_PrefabAsset: {fileID: 0}', '  m_GameObject: {fileID: 0}', '  m_Enabled: 1', '  m_EditorHideFlags: 0',
        '  m_Script: {fileID: 11500000, guid: ' + guid(script_meta) + ', type: 3}',
        '  m_Name: OfficialPartnerTipDotAssetCatalog', "  m_EditorClassIdentifier: ''", '  definitions:']
    for row in rows:
        lines += ['  - GraphName: ' + json.dumps(row['GraphName']), '    GraphPath: ' + json.dumps(row['GraphPath']),
            '    GraphType: ' + str(row['GraphType']), '    GraphScope: ' + str(row['GraphScope'])]
        lines.append('    Graph: ' + ('{fileID: 11400000, guid: ' + guid(Path(str(ROOT / GRAPHS[row['GraphName']]) + '.meta')) + ', type: 2}'
            if row['GraphName'] in GRAPHS else '{fileID: 0}'))
    output.write_text('\n'.join(lines) + '\n', 'utf8', newline='\n')
    output_meta = metadata(output, 'NativeFormatImporter')
    for path in (script_meta, output_meta):
        path.write_text('\n'.join(line.rstrip() for line in path.read_text('utf8').splitlines()) + '\n', 'utf8', newline='\n')
    inputs = {native_path.relative_to(ROOT).as_posix(): sha(native_path)}
    inputs.update({(constructors.source_root / (name + '.cs')).relative_to(ROOT).as_posix():
        sha(constructors.source_root / (name + '.cs')) for name in constructors.sources})
    result = dict(manifest=proof['manifest'], table=dict(name='tip_dot.g.bytes', **source), inputs=inputs,
        reused_original_graphs=graph_sources, original_graph_metadata=graph_metadata,
        outputs={path.relative_to(ROOT).as_posix(): sha(path) for path in (script, script_meta, output, output_meta)},
        generator_sha256=sha(Path(__file__)), rows=len(rows), bound_graphs=list(GRAPHS),
        validation_helpers={path: sha(ROOT / path) for path in ('scripts/fixtures/partner_tipdot_catalog_validation.cs',
            'scripts/prepare_partner_tipdot_catalog_validation.py')},
        operation=['Reuse the verified selected APK table extraction cache; decode the entire table with selected official Luban constructors.',
            'Preserve all graph names, paths, type/scope ordinals and table order. Explicitly reference only the three recovered original partner graphs.',
            'Verify root names. Preserve serialized graph Type/Scope separately from TipDotInfo: original runtime consumers use the table even where graph authoring metadata differs.',
            'Graphs outside this module retain configuration identity and fail explicitly if requested without a native binding.',
            'Do not rename, clone, modify, approximate or prune the original graph assets or nodes. No full asset snapshot.'])
    target = ROOT / 'assets/reference/partner-tipdot-catalog-source-manifest.json'
    target.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Bound', len(GRAPHS), 'actual original partner graphs with', len(rows), 'selected table definitions')


if __name__ == '__main__': main()
