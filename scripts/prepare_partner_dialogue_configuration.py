"""Decode selected original NPC conversation nodes and record their format closure."""
import argparse
import hashlib
import json
from pathlib import Path
import re

from official_apk import Apk, ROOT
from official_tables import Constructors
from validation_workspace import validation_directory


def sha(raw): return hashlib.sha256(raw).hexdigest()


def metadata(path, importer):
    return ('fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs-partner-conversation:' +
        path.relative_to(ROOT).as_posix()).encode()).hexdigest() + '\n' + importer + ':\n  externalObjects: {}\n').encode()


def build(work):
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required')
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in
        re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    candidates = [obj for obj in apk.objects('defaultpackage_assets_config_luban.bundle')
        if obj.type.name == 'TextAsset' and obj.read().m_Name.rsplit('/', 1)[-1] == 'npc_dialogue.g']
    obj, = candidates
    raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
    rows = constructors.table(raw, mapping['npc_dialogue'])
    if not rows or len({(row['ClassId'], row['Node']) for row in rows}) != len(rows):
        raise ValueError('Missing or duplicate original dialogue nodes')
    nodes = {(row['ClassId'], row['Node']): row for row in rows}
    for row in rows:
        for node in row['NextNodes'] or []:
            if (row['ClassId'], node) not in nodes: raise ValueError('Missing next node')
        for class_id in row['NextIds'] or []:
            if (class_id, 1) not in nodes: raise ValueError('Missing next conversation')
    sources = {}
    for name, text in constructors.sources.items():
        if not text: continue
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.is_file() or name in ('Pos2d', 'Vector2', 'Vector3'):
            path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if path.read_bytes() != (ROOT / '.tools/source-packages/official-code' / path.relative_to(code)).read_bytes():
            raise ValueError('Selected dialogue source differs: ' + name)
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    for path in (code / 'global-metadata.dat', ROOT / 'scripts/official_tables.py', ROOT / 'scripts/official_metadata.py'):
        sources[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    output = (json.dumps(dict(manifest=apk.version, nodes=rows), ensure_ascii=False,
        separators=(',', ':'), allow_nan=False) + '\n').encode('utf8')
    receipt = dict(manifest=apk.version, table=dict(source=apk.provenance(obj),
        object_sha256=sha(obj.get_raw_data()), payload_sha256=sha(raw), bytes=len(raw),
        constructor=mapping['npc_dialogue'], rows=len(rows)), source_sha256=sources,
        output_sha256=sha(output), generator_sha256=sha(Path(__file__).read_bytes()))
    (work / 'partner-dialogue-rules.json').write_bytes(output)
    (work / 'partner-dialogue-rules-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Decoded original dialogue nodes:', len(rows), 'classes:', len({row['ClassId'] for row in rows}), flush=True)
    return receipt


def install(work, receipt):
    if sha((work / 'partner-dialogue-rules.json').read_bytes()) != receipt['output_sha256']:
        raise ValueError('Staged original conversation payload changed')
    if receipt['generator_sha256'] != sha(Path(__file__).read_bytes()):
        raise ValueError('Rebuild conversation projection with the current generator')
    rules = ROOT / 'unity/Assets/Official/Partners/partner-dialogue-rules.json'
    script = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression/OfficialPartnerConversationAssetCatalog.cs'
    adapter = script.with_name('OfficialPartnerConversationCatalog.cs')
    catalog = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerConversationAssetCatalog.asset'
    rules_meta = metadata(rules, 'TextScriptImporter')
    # The running main Editor may have assigned identities to these new code
    # files already. Preserve those actual identities in the explicit binding.
    script_meta_path = Path(str(script) + '.meta')
    adapter_meta_path = Path(str(adapter) + '.meta')
    script_meta = script_meta_path.read_bytes() if script_meta_path.exists() else metadata(script, 'MonoImporter')
    adapter_meta = adapter_meta_path.read_bytes() if adapter_meta_path.exists() else metadata(adapter, 'MonoImporter')
    guid = lambda raw: re.search(rb'guid: ([0-9a-f]{32})', raw)[1].decode()
    body = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
        '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        '  m_Script: {fileID: 11500000, guid: ' + guid(script_meta) + ', type: 3}\n'
        '  m_Name: OfficialPartnerConversationAssetCatalog\n  m_EditorClassIdentifier:\n'
        '  rules: {fileID: 4900000, guid: ' + guid(rules_meta) + ', type: 3}\n').encode()
    planned = {rules: (work / 'partner-dialogue-rules.json').read_bytes(),
        Path(str(rules) + '.meta'): rules_meta, script_meta_path: script_meta,
        adapter_meta_path: adapter_meta, catalog: body,
        Path(str(catalog) + '.meta'): metadata(catalog, 'NativeFormatImporter')}
    for path, raw in planned.items():
        if path.exists() and path.read_bytes() != raw: raise ValueError('Preserve changed conversation binding: ' + str(path))
        path.write_bytes(raw)
    receipt['installed_output_sha256'] = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes())
        for p in (*planned, script, adapter)}
    receipt['validation_source_sha256'] = {p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in (
        ROOT / 'scripts/fixtures/partner_conversation_rules_harness.cs',
        ROOT / 'scripts/fixtures/partner_conversation_binding_validation.cs',
        ROOT / 'scripts/test_partner_conversation_rules.py',
        ROOT / 'scripts/prepare_partner_conversation_binding_validation.py')}
    (ROOT / 'assets/reference/partner-dialogue-rules-source-manifest.json').write_text(
        json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original conversation table and explicit Resources binding', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    work = validation_directory()
    receipt = build(work)
    if parser.parse_args().install: install(work, receipt)
