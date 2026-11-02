"""Decode selected map facts needed by the original conversation conditions."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_map_codec import unpack
from official_tables import Constructors
from validation_workspace import validation_directory


def sha(raw): return hashlib.sha256(raw).hexdigest()


def build(work, install=False):
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('Owned workspace required')
    apk = Apk.current()
    code = ROOT / '.tools/source-packages/current' / apk.version / 'official-code'
    constructors = Constructors(code)
    source_hashes, objects = {}, []

    def source(relative):
        path = code / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/official-code' / relative).read_bytes():
            raise ValueError('Selected map source differs: ' + relative)
        source_hashes[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig')

    def payload(bundle, suffix):
        obj = apk.asset(bundle, suffix)
        raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
        objects.append(dict(source=apk.provenance(obj), object_sha256=sha(obj.get_raw_data()),
            payload_sha256=sha(raw), bytes=len(raw), asset_suffix=suffix))
        return raw

    partner = json.loads((ROOT / 'unity/Assets/Official/Partners/partner-rules.json').read_bytes())['tables']
    nodes = json.loads((ROOT / 'unity/Assets/Official/Partners/partner-dialogue-rules.json').read_bytes())['nodes']
    rows = {row['ClassId']: row['Condition'] for row in partner['conditions']}
    needed, visited, goddess = set(), set(), set()

    def visit(node):
        if node is None: return
        kind = node['_type']
        if kind in ('WorldMapProgressCondition', 'MapRegionGoddessCondition', 'MapRegionActivedCondition'):
            needed.add(node['MapClassId'])
        if kind == 'WorldMapProgressCondition' and node['Region'] < 0:
            raise ValueError('Review whole-map conversation progress binding')
        if kind == 'MapRegionGoddessCondition': goddess.add((node['MapClassId'], node['Region']))
        for identity in ([node['Id']] if kind == 'LogicalNotCondition' else node.get('Ids') or []):
            if identity not in visited: visited.add(identity); visit(rows[identity])

    for row in nodes: visit(row['UnlockCondition']); visit(row['ChainSequenceCondition'])
    if needed != {11, 12, 13, 14, 17, 19, 21, 23} or goddess != {(11, 56), (17, 0)}:
        raise ValueError('Review selected conversation map closure')
    extension_rows = constructors.table(payload('config_luban_map', '/entity_extend_parameter.g.bytes'), 'TbEntityExtendParameterInfo')
    extensions = {row['EntityClassId']: row for row in extension_rows}
    enum = source('common-source/Common/MapEntityWorldStageType.cs')
    if 'Goddess = 202' not in enum: raise ValueError('Original goddess enum differs')
    for relative in ('common-source/Common/MapInfoWrap.cs',
        'common-source/MessagePack.Formatters.Common/MapInfoWrapFormatter.cs',
        'common-source/Common/MapWorldLoEntityInfoWrap.cs',
        'common-source/MessagePack.Formatters.Common/MapWorldLoEntityInfoWrapFormatter.cs',
        'common-source/Common/EntityPositionInfo.cs',
        'common-source/MessagePack.Formatters.Common/EntityPositionInfoFormatter.cs',
        'common-source/Common/SpawnEntityLiteInfo.cs',
        'common-source/MessagePack.Formatters.Common/SpawnEntityLiteInfoFormatter.cs',
        'common-source/Common/MapRegionEntityInfoManager.cs', 'common-source/Common/FormulaHandler.cs'):
        source(relative)
    maps = []
    for identity in sorted(needed):
        data = unpack(payload('config_mapinfowrap', f'/mapinfowrap_{identity}.g.bytes'))
        if len(data) != 5 or data[0] != identity: raise ValueError('Original map wrapper schema differs')
        gods = {}
        for region, entities in data[1].items():
            for entity in entities:
                if len(entity) != 7 or len(entity[4]) != 5: raise ValueError('Original entity wrapper schema differs')
                spawn = entity[5]
                if spawn is not None and extensions[spawn[0]]['WorldStageType'] == 202:
                    gods.setdefault(entity[4][4], []).append(entity[0])
        references = []
        for map_id, region in sorted(goddess):
            if map_id != identity: continue
            values = gods.get(region, [])
            if len(values) > 1: raise ValueError('Region-only offline activation is ambiguous: ' + str((identity, region)))
            references.append(dict(region=region, entity=values[-1] if values else 0))
        maps.append(dict(id=identity, regions=[dict(region=region, maximum=value) for region, value in data[4].items()],
            goddesses=references))
    for name, text in constructors.sources.items():
        if text:
            path = code / 'common-source/Common' / (name + '.cs')
            if not path.is_file(): path = code / 'common-source/Common.LubanGen' / (name + '.cs')
            source(path.relative_to(code).as_posix())
    for relative in ('scripts/official_map_codec.py', 'scripts/official_tables.py', 'scripts/official_metadata.py',
        'unity/Assets/Official/Partners/partner-rules.json', 'unity/Assets/Official/Partners/partner-dialogue-rules.json',
        'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineRegionsModule.Regions.cs'):
        source_hashes[relative] = sha((ROOT / relative).read_bytes())
    output = (json.dumps(dict(manifest=apk.version, maps=maps), ensure_ascii=False, separators=(',', ':')) + '\n').encode()
    target = ROOT / 'unity/Assets/Official/Partners/partner-conversation-world.json'
    json_guid = hashlib.md5(b'zjcs-partner-conversation-world').hexdigest()
    meta = ('fileFormatVersion: 2\nguid: ' + json_guid + '\nTextScriptImporter:\n  externalObjects: {}\n').encode()
    planned = [(target, output), (Path(str(target) + '.meta'), meta)]
    script_guid = None
    for name in ('OfficialPartnerConversationWorldAssetCatalog', 'OfflinePartnerConversationWorld'):
        path = ROOT / 'unity/Assets/Game/Runtime/Offline/Progression' / (name + '.cs')
        identity = hashlib.md5(('zjcs-conversation-world:' + name).encode()).hexdigest()
        if name == 'OfficialPartnerConversationWorldAssetCatalog': script_guid = identity
        importer = Path(str(path) + '.meta')
        raw = importer.read_bytes() if importer.exists() else ('fileFormatVersion: 2\nguid: ' + identity + '\n').encode()
        if ('guid: ' + identity).encode() not in raw: raise ValueError('Changed world adapter script identity')
        planned.extend([(path, path.read_bytes()), (importer, raw)])
    binding = ROOT / 'unity/Assets/Game/Resources/Partners/OfficialPartnerConversationWorldAssetCatalog.asset'
    yaml = ('%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
        '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
        '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
        '  m_Script: {fileID: 11500000, guid: ' + script_guid + ', type: 3}\n'
        '  m_Name: OfficialPartnerConversationWorldAssetCatalog\n  m_EditorClassIdentifier:\n'
        '  rules: {fileID: 4900000, guid: ' + json_guid + ', type: 3}\n').encode()
    binding_meta = ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs-conversation-world-catalog-binding').hexdigest()
        + '\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n').encode()
    planned.extend([(binding, yaml), (Path(str(binding) + '.meta'), binding_meta)])
    receipt = dict(manifest=apk.version, inputs=objects, source_sha256=source_hashes,
        generator_sha256=sha(Path(__file__).read_bytes()),
        installed_output_sha256={path.relative_to(ROOT).as_posix(): sha(raw) for path, raw in planned},
        validation_source_sha256={relative: sha((ROOT / relative).read_bytes()) for relative in (
            'scripts/prepare_partner_conversation_world_validation.py',
            'scripts/fixtures/partner_conversation_world_validation.cs')},
        operation=['Decode original five-field map wrappers with verified selected formatters.',
            'Restore original goddess identity lookup through actual entity extension rows and position regions.',
            'Keep ID zero when no goddess exists; require unique goddess when region-only save activation is used.',
            'Consume the already calculated fixed-point progress saved by the actual mainline region module; never normalize it twice.'])
    proof = work / 'partner-conversation-world-source-manifest.json'
    proof.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    (work / target.name).write_bytes(output)
    if install:
        previous_path = ROOT / 'assets/reference' / proof.name
        previous = json.loads(previous_path.read_bytes()) if previous_path.exists() else {}
        for path, raw in planned:
            if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get('installed_output_sha256', {}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve changed conversation world output: ' + str(path))
            path.write_bytes(raw)
        previous_path.write_bytes(proof.read_bytes())
    print('Decoded actual conversation world:', len(maps), 'maps;', sum(len(row['regions']) for row in maps), 'regions;',
        [dict(map=row['id'], goddesses=row['goddesses']) for row in maps if row['goddesses']], flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    build(validation_directory(), parser.parse_args().install)
