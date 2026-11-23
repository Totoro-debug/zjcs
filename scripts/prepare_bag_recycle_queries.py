"""Port original gift/key recycling queries and verified local read-only inputs."""
import hashlib
import json
from pathlib import Path
import re

import msgpack

from official_apk import Apk, ROOT
from official_manifest import read_asset_manifest
from official_source import OfficialSource
from official_tables import Constructors
from prepare_bag_item_queries import member


OFFICIAL = ROOT / 'unity/Assets/Game/Runtime/Official/BagRecycleQueries'
OFFLINE = ROOT / 'unity/Assets/Game/Runtime/Offline/BagRecycleQueries'
ASSETS = ROOT / 'unity/Assets/Game/Resources/Home/BagRecycleQueries'
RECEIPT = ROOT / 'assets/reference/bag-recycle-queries-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/bag_recycle_queries/BagRecycleQueryServices.cs'


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    inputs, outputs, methods = {}, {}, {}
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.exists() else {}

    def read(path):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    def write(path, raw):
        name = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(name):
            raise ValueError('Preserve another recycling query change: ' + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.exists() or path.read_bytes() != raw: path.write_bytes(raw)
        outputs[name] = sha(raw)

    def metadata(path, folder=False):
        kind = 'DefaultImporter' if folder else 'TextScriptImporter' if path.suffix == '.json' else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        text = 'fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:bag-recycle-queries:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest() + '\n'
        text += 'folderAsset: yes\n' if folder else ''
        write(Path(str(path) + '.meta'), (text + kind + ':\n  externalObjects: {}\n').encode())

    identity = json.loads(read(code.parent / 'code-manifest.json'))
    assembly = code / 'managed/decoded/Console.dll'
    if identity['manifest'] != source.version or sha(assembly.read_bytes()) != identity['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('Verify the actual selected original recycling assembly')
    inputs[assembly.relative_to(ROOT).as_posix()] = sha(assembly.read_bytes())
    original = read(code / 'common-source/Common/PlayerUtils.cs')
    bodies = []
    replacements = [('this Player player', 'IBagRecycleQueryPlayer player'), ('Parsers.Instance', 'Services.Tables'),
        ('NpcInfoParser npcInfoParser', 'IBagRecycleNpcTable npcInfoParser'), ('PlayerNpcManager npcManager', 'IBagRecycleNpcManager npcManager'),
        ('NpcFriendshipLevelInfoParser npcFriendshipLevelInfoParser', 'IBagRecycleNpcLevelTable npcFriendshipLevelInfoParser'),
        ('ItemTagInfoParser itemTagInfoParser', 'IBagRecycleItemTagTable itemTagInfoParser'),
        ('player.IsTagInAllRegionActived(config.Tag)', 'IsTagInAllRegionActived(player, config.Tag)')]
    for name in ('GetCanRecycleGift', 'GetCanRecycleDoorkey', 'IsTagInAllRegionActived'):
        raw = member(original, r'^\s*public static [^\n]+\b' + name + r'\(')
        body = raw.replace('public static ', 'public ', 1)
        for old, new in replacements: body = body.replace(old, new)
        body = '\n'.join(line.rstrip() for line in body.splitlines()).strip()
        if 'Parsers.' in body or 'this Player' in body: raise ValueError('Unbound recycling dependency: ' + name)
        methods[name] = dict(original_sha256=sha(raw.encode()), migrated_sha256=sha(body.encode()))
        bodies.append(body)
    header = '// Original PlayerUtils recycling queries; explicit query owners replace global services.\nusing System;\nusing System.Collections.Generic;\nusing Zjcs.Official.BagItemQueries;\nusing Zjcs.Official.BagTipDots;\nnamespace Zjcs.Official.BagRecycleQueries\n{\npublic sealed class BagRecycleQueryRules\n{\n    readonly IBagRecycleQueryServices Services;\n    public BagRecycleQueryRules(IBagRecycleQueryServices services) => Services = services ?? throw new ArgumentNullException(nameof(services));\n'
    for folder in (OFFICIAL, OFFLINE, ASSETS): metadata(folder, True)
    for name, raw in [('BagRecycleQueryRules.cs', (header + '\n\n'.join(bodies) + '\n}\n}\n').encode()), ('BagRecycleQueryServices.cs', read(TEMPLATE).encode())]:
        path = OFFICIAL / name; write(path, raw); metadata(path)
    definitions = [(OFFICIAL, ['Zjcs.Official.BagItemQueries', 'Zjcs.Official.BagTipDots']),
        (OFFLINE, ['Zjcs.Official.BagRecycleQueries', 'Zjcs.Official.BagItemQueries', 'Zjcs.Official.BagTipDots', 'Zjcs.Offline.Progression', 'Zjcs.Offline.BagItemQueries'])]
    for folder, refs in definitions:
        name = 'Zjcs.' + ('Official' if folder == OFFICIAL else 'Offline') + '.BagRecycleQueries'
        path = folder / (name + '.asmdef'); write(path, (json.dumps(dict(name=name, references=refs), indent=2) + '\n').encode()); metadata(path)
    for name in ('OfflineBagRecycleCatalog.cs', 'OfflineBagRecycleQueries.cs'):
        path = OFFLINE / name; read(path); outputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes()); metadata(path)

    constructors = Constructors(code)
    mapping = {name.rsplit('/', 1)[-1]: kind for kind, name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)', constructors.source('Tables'))}
    manifest = read_asset_manifest(source.manifest_bytes('DefaultPackage'))
    names = ('npc', 'npc_friendship_level', 'npc_gift', 'item_tag', 'map_world', 'map_region')
    selected = {}
    for name in (*names, 'world_stage_tag_map_info'):
        matches = [row for row in manifest if row['path'].lower().endswith('/' + name + '.g.bytes')]
        if len(matches) != 1: raise ValueError('Resolve the original recycling table: ' + name)
        selected[name] = matches[0]
    tables, configuration = {}, {}
    with Apk.current() as apk:
        for bundle in dict.fromkeys(row['bundle'] for row in selected.values()):
            container = {path.lower(): obj for path, obj in apk.container(bundle)}
            for name, row in selected.items():
                if row['bundle'] != bundle: continue
                obj = container[row['path'].lower()].deref(); raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
                configuration[name] = dict(source=apk.provenance(obj), asset=row['path'], payload_sha256=sha(raw), object_sha256=sha(obj.get_raw_data()))
                if name in names:
                    tables[name] = constructors.table(raw, mapping[name]); configuration[name]['constructor'] = mapping[name]
                else:
                    decoded = msgpack.unpackb(raw, raw=False, strict_map_key=False)
                    if not isinstance(decoded, list) or len(decoded) != 1 or not isinstance(decoded[0], dict): raise ValueError('Verify original WorldStageTagMapInfo formatter')
                    tables[name] = decoded[0]
    for name in constructors.sources:
        path = code / 'common-source/Common' / (name + '.cs')
        if not path.exists(): path = code / 'common-source/Common.LubanGen' / (name + '.cs')
        if path.exists(): read(path)
        elif name not in ('Vector2', 'Vector3', 'Pos2d'): raise ValueError('Verify original recycling table decoder: ' + name)
    for name in ('NpcInfoParser', 'NpcFriendshipLevelInfoParser', 'PlayerNpcManager', 'Player', 'PlayerItemManagerExt', 'WorldStageTagMapInfoParser', 'WorldStageTagEntityInfo', 'WorldStageTagMapInfo', 'MapWorldInfo'):
        read(code / 'common-source/Common' / (name + '.cs'))
    for name in ('WorldStageTagEntityInfoFormatter', 'WorldStageTagMapInfoFormatter'): read(code / 'common-source/MessagePack.Formatters.Common' / (name + '.cs'))
    world_tags = []
    for tag, entities in tables['world_stage_tag_map_info'].items():
        if entities is not None and any(not isinstance(row, list) or len(row) != 4 or any(type(v) != int for v in row) for row in entities):
            raise ValueError('Verify all four original world-tag entity fields')
        world_tags.append(dict(Tag=tag, Entities=None if entities is None else [dict(zip(('MapClassId', 'Region', 'LoEntityId', 'EntityClassId'), row)) for row in entities]))
    maps = [dict(ClassId=row['ClassId'], IsDefaultPublicMap=row['IsDefaultPublicMap'],
        Regions=[region['Region'] for region in tables['map_region'] if region['ClassId'] == row['ClassId']]) for row in tables['map_world']]
    if {entity['MapClassId'] for row in world_tags for entity in row['Entities'] or []} - {row['ClassId'] for row in maps}:
        raise ValueError('Bind the actual map-part type for a recycling tag')
    payload = dict(Npcs=[dict(Id=row['Id'], ExclusiveGiftIds=row['ExclusiveGiftIds']) for row in tables['npc']],
        FriendshipLevels=[dict(Id=row['Id'], Level=row['Level']) for row in tables['npc_friendship_level']],
        GiftIds=[row['ClassId'] for row in tables['npc_gift']], ItemTags=tables['item_tag'], WorldTags=world_tags, Maps=maps)
    path = ASSETS / 'catalog.json'; write(path, (json.dumps(payload, ensure_ascii=False, separators=(',', ':'), allow_nan=False) + '\n').encode()); metadata(path)
    for name in ('official_apk.py', 'official_source.py', 'official_manifest.py', 'official_tables.py', 'official_metadata.py', 'official_bundle_reader.py', 'prepare_bag_item_queries.py', 'prepare_home_entity_bindings.py'):
        read(ROOT / 'scripts' / name)
    read(Path(__file__))
    # Reuse the existing original item projection rather than a second inventory table/owner.
    item_receipt = ROOT / 'assets/reference/bag-item-query-bindings-source-manifest.json'; read(item_receipt)
    for name, expected in json.loads(item_receipt.read_bytes())['outputs'].items():
        path = ROOT / name
        if sha(path.read_bytes()) != expected: raise ValueError('The verified Bag item input differs: ' + name)
        read(path)
    validations = [ROOT / name for name in ('scripts/test_bag_recycle_queries.ps1', 'scripts/fixtures/bag_recycle_queries_validation.cs')]
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, source_selection_sha256=sha((ROOT/'official-source.json').read_bytes()),
        methods=methods, configuration=configuration, inputs=inputs, outputs=outputs,
        validation_inputs={path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in validations if path.exists()},
        adaptation='Original gift/key and tag-region queries retain branches, enumeration order and thresholds. Local read-only projections use current inventory, NPC friendship and visited-map defaults/public activation records.',
        scope='Shared query and local data owners only; recycle transactions, complete Bag graph host and native storage gameplay remain separate.'), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared original Bag recycling queries and local configuration')


if __name__ == '__main__':
    prepare()
