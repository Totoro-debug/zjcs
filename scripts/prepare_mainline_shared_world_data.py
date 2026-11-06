"""Bind selected original EC records to the configurable shared map loader."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_mainline_map_definition import MainlineMapSource
from prepare_unity_map14_shared_ec_runtime import meta as original_meta

MAPS = (10, 11, 12, 13, 14, 15, 16)
DESTINATION = ROOT / 'unity/Assets/Official/World/Shared/SharedWorldDataCurrent'
RESOURCES = DESTINATION / 'Resources/MainlineSharedWorld'


def meta(path):
    existing = Path(str(path) + '.meta')
    if existing.exists():
        # The main editor may discover this new payload folder while extraction
        # runs. Retain its assigned identity; all loading uses explicit paths.
        if len(re.findall(rb'(?m)^guid: [a-f0-9]{32}\r?$', existing.read_bytes())) != 1:
            raise ValueError('Invalid retained asset metadata: ' + str(existing))
        raw = existing.read_bytes()
        normalized = b'\n'.join(line.rstrip(b' \t\r') for line in raw.split(b'\n'))
        if normalized != raw:
            existing.write_bytes(normalized)
        return
    original_meta(path)


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def build():
    receipt = ROOT / 'assets/reference/official-map/mainline-shared-world-data-source-manifest.json'
    if receipt.exists():
        for output in json.loads(receipt.read_bytes())['outputs']:
            path = ROOT / output['path']
            if not path.is_file() or record(path)['sha256'] != output['sha256']:
                raise ValueError('Preserve changed generated shared map input: ' + str(path))
    source = MainlineMapSource(14)
    if source.apk.version != '884_156692':
        raise ValueError('Review the selected original shared map version')
    try:
        definitions = {mid: ROOT / f'unity/Assets/Official/World/Map/{mid}/map{mid}.json' for mid in MAPS}
        required = set()
        for path in definitions.values():
            data = json.loads(path.read_bytes())
            required.update(row['EntityId'] for row in data['Entities'])
            required.update((data['Navigation']['CharacterId'], data['Navigation']['MemberId']))
        source.templates(required)
        addresses = {row['asset'] for row in source.template_sources.values()}
        objects = dict(source.apk.container('config_ec'))
        shared = []
        outputs = []

        def payload(obj, original):
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            digest = hashlib.sha256(raw).hexdigest()
            relative = Path(original.removeprefix('Config/'))
            target = RESOURCES / 'Payload' / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(raw)
            meta(target)
            outputs.extend((record(target), record(Path(str(target) + '.meta'))))
            return dict(OriginalPath=original, Resource='MainlineSharedWorld/Payload/' + relative.as_posix().removesuffix('.bytes'),
                        Sha256=digest, source=source.apk.provenance(obj), path=target.relative_to(ROOT).as_posix())

        for address in sorted(addresses):
            shared.append(payload(objects[address].deref(), address.removeprefix('Assets/')))
        container = payload(source.apk.asset('config_luban_ec', '/EC/container.g.bytes'), 'Config/Luban/EC/container.g.bytes')
        binding = RESOURCES / 'container-binding.json'
        binding.write_text(json.dumps(dict(Resource=container['Resource'], Sha256=container['Sha256']), indent=2) + '\n', encoding='utf8', newline='\n')
        meta(binding)
        outputs.extend((record(binding), record(Path(str(binding) + '.meta'))))
        old = ROOT / 'unity/Assets/Official/World/Map/14/SharedSerialization/Resources/Map14/SharedSerialization/catalog.json'
        lookup = json.loads(old.read_bytes())
        for mid in MAPS:
            entries = list(shared)
            for bundle, suffix, original in (
                ('config_ec', f'/map_{mid}.g.bytes', f'Config/EC/map_{mid}.g.bytes'),
                ('config_ec', f'/MapRoot_{mid}.g.bytes', f'Config/EC/MapRoot_{mid}.g.bytes'),
                ('config_ec_maprootentitylite', f'/{mid}.g.bytes', f'Config/EC/MapRootEntityLite/{mid}.g.bytes'),
                ('config_mapinfowrap', f'/mapinfowrap_{mid}.g.bytes', f'Config/MapInfoWrap/mapinfowrap_{mid}.g.bytes')):
                entries.append(payload(source.apk.asset(bundle, suffix), original))
            target = RESOURCES / str(mid) / 'catalog.json'
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_text(json.dumps(dict(MapClassId=mid, Entries=entries, LookupResource=lookup['LookupResource'],
                LookupSha256=lookup['LookupSha256']), ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
            meta(target)
            outputs.extend((record(target), record(Path(str(target) + '.meta'))))
        for folder in sorted((p for p in DESTINATION.rglob('*') if p.is_dir()), reverse=True):
            meta(folder)
            outputs.append(record(Path(str(folder) + '.meta')))
        meta(DESTINATION)
        outputs.append(record(Path(str(DESTINATION) + '.meta')))
        integration = [ROOT / path for path in (
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/MainlineSharedWorld.cs',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/MainlineSharedWorld.cs.meta',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/MainlineSharedEntityCreation.cs',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/MainlineSharedEntityCreation.cs.meta',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedLogging.cs',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedLogging.cs.meta',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedData.cs',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedAssemblies.cs',
            'unity/Assets/Game/Runtime/Offline/World/NativeMap14SharedEntities/NativeMap14SharedRoot.cs',
            'unity/Assets/Game/Runtime/Presentation/World/NativeMap14/OfficialMap14NativeWorldView.cs',
            'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.NativeMap14.cs',
            'unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Persistence.cs',
            'unity/Assets/Game/Runtime/Presentation/World/Zjcs.Presentation.World.asmdef',
            'unity/Assets/Game/Editor/Map14/WorldHost/OfficialMap14NativeHostAcceptance.cs',
            'unity/Assets/Game/Editor/Map13Mainline/OfficialMap13MainlinePlayAcceptance.cs',
            'unity/Assets/Game/Editor/Map13Mainline/Zjcs.Editor.Map13Mainline.asmdef',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.SharedMaps.cs',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.SharedMaps.cs.meta',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.FreshEntities.cs',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.FreshEntities.cs.meta',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Logging.cs',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Logging.cs.meta',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Injection.cs',
            'unity/Assets/Game/Editor/Map13MiniMap/OfficialMainlineMiniMapRootAcceptance.Injection.cs.meta')]
        state_sources = ROOT / ".tools/source-packages/current" / source.apk.version / "official-code/common-source/Common"
        receipt.write_text(json.dumps(dict(manifest=source.apk.version, mapClassIds=list(MAPS),
            operation='Retain exact selected EC map/root/common template and MapInfoWrap/lite bytes. Configure the existing original shared parser and container by map identity; no initial server entity state or spawning policy is supplied.',
            inputs=[record(path) for path in definitions.values()], templates=source.template_sources, container_table=container,
            formatter_lookup=record(old), original_code=source.checked_code()
                + [record(state_sources / (name + ".cs")) for name in ("WorldStageComponent", "GridTransformComponent", "MapLoEntityIdManager")],
            tools=[record(path) for path in (Path(__file__), ROOT / 'scripts/prepare_mainline_map_definition.py',
                ROOT / 'scripts/official_ec_codec.py', ROOT / 'scripts/official_map_codec.py')], outputs=outputs,
            integration=[record(path) for path in integration],
            limitations=['Historical original Console IL and formatter selection remain separately recorded.',
                'Runtime parsing, native startup, formal host restoration and Player verification are separate checks.']),
            ensure_ascii=False, indent=2) + '\n', encoding='utf8', newline='\n')
        print(f'Bound {len(MAPS)} original maps and {len(shared)} shared EC files ({len(required)} template/action identities).', flush=True)
    finally:
        source.apk.archive.close()


if __name__ == '__main__':
    build()
