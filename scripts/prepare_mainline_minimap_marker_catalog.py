"""Install selected official marker queries; reuse the original UI display parser."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from official_tables import Constructors


MAPS = (10, 11, 12, 13, 14, 16)
CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
PREFIX = 'UI/Map/MiniMap/MiniMapMark/'


def digest(raw):
    return hashlib.sha256(raw).hexdigest()


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=digest(path.read_bytes()))


def method(source, declaration):
    start = source.index(declaration)
    start = source.index('{', start)
    depth, end = 1, start + 1
    while depth:
        depth += (source[end] == '{') - (source[end] == '}')
        end += 1
    return source[start:end]


def build():
    receipt_path = ROOT / 'assets/reference/official-map/mainline-current-minimap-markers-source-manifest.json'
    markers = json.loads(receipt_path.read_bytes())
    if markers['manifest'] != '884_156692' or {row['mapClassId'] for row in markers['maps']} != set(MAPS):
        raise ValueError('Review the installed native marker selection')
    original = CODE / 'common-source/Common/EntityMapUIDisplayInfoParser.cs'
    installed = ROOT / 'unity/Assets/Game/Runtime/Official/World/Map13Entities/EntityMapUIDisplayInfoParser.cs'
    declaration = 'public EntityMapUIDisplayInfo Get('
    if method(original.read_text('utf8'), declaration) != method(installed.read_text('utf8'), declaration):
        raise ValueError('Restore the selected original UI-display lookup before binding its data')
    original_load = method(original.read_text('utf8'), 'public override void PostParse()')
    # The migrated parser accepts the exact rows instead of a global GConfig
    # parser. No selector, prefix, dictionary insertion or ordering changes.
    original_load = original_load.replace('\n\t\t((ParserBase)this).PostParse();', '')
    if original_load != method(installed.read_text('utf8'), 'public void Load('):
        raise ValueError('Original UI-display initialization differs beyond explicit data injection')

    apk = Apk.current()
    try:
        if apk.version != '884_156692':
            raise ValueError('Review the selected marker configuration version')
        code_manifest = CODE.parent / 'code-manifest.json'
        code_identity = json.loads(code_manifest.read_bytes())
        if digest((CODE / 'global-metadata.dat').read_bytes()) != code_identity['metadata_sha256']:
            raise ValueError('Official constructor metadata differs from its selected extraction')
        constructors = Constructors(CODE)
        inputs = []

        def table(name, constructor):
            obj = apk.asset('config_luban_map', '/' + name + '.g.bytes')
            raw = obj.read().m_Script.encode('utf8', 'surrogateescape')
            inputs.append(dict(source=apk.provenance(obj), payload_sha256=digest(raw), constructor=constructor))
            return constructors.table(raw, constructor)

        extensions = table('entity_extend_parameter', 'TbEntityExtendParameterInfo')
        displays = table('entity_map_ui_display', 'TbEntityMapUIDisplayInfo')
        maps = table('map', 'TbMapInfo')
        selected_maps = []
        for selection in markers['maps']:
            info, = [row for row in maps if row['ClassId'] == selection['mapClassId']]
            if info['MapGroupId'] != selection['groupId']:
                raise ValueError('Native marker catalog and official map group differ')
            selected_maps.append(dict(MapClassId=info['ClassId'], MapGroupId=info['MapGroupId'],
                EntityClasses=selection['entityClasses']))
        classes = {value for row in selected_maps for value in row['EntityClasses']}
        extension_fields = ('EntityClassId', 'CompleteState', 'DialogIconPrefab', 'MiniMapMark',
            'MiniMapMarkHideInEnd', 'MiniMapMarkShowInPublic', 'MiniMapMarkShowInOffline', 'InteractionUnique')
        selected_extensions = [{key: row[key] for key in extension_fields}
            for row in extensions if row['EntityClassId'] in classes]
        selected_displays = [row for row in displays if any(row['EntityClassId'] in item['EntityClasses']
            and row['GroupId'] in (0, item['MapGroupId']) and row['MapClassId'] in (0, item['MapClassId'])
            for item in selected_maps)]
        names = {row['MiniMapMark'] for row in selected_extensions + selected_displays} - {''}
        if names != {name for row in markers['maps'] for name in row['markers']}:
            raise ValueError('Original runtime selector data and installed native prefab closure differ')
        payload = dict(Manifest=apk.version, Maps=selected_maps, Extensions=selected_extensions, Displays=selected_displays)
        destination = ROOT / 'unity/Assets/Game/Resources/MainlineMiniMap/current-marker-data.json'
        destination.parent.mkdir(parents=True, exist_ok=True)
        folder_meta = Path(str(destination.parent) + '.meta')
        if not folder_meta.is_file():
            folder_meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:mainline-minimap-resources').hexdigest()
                + '\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n', 'utf8')
        destination.write_text(json.dumps(payload, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        meta = Path(str(destination) + '.meta')
        if not meta.is_file():
            meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:mainline-minimap-marker-data:884').hexdigest() + '\n', 'utf8')
        sources = [original, CODE / 'common-source/Common/EntityExtendParameterInfoParser.cs',
            CODE / 'common-source/MapUtils.cs', installed, code_manifest, Path(__file__), ROOT / 'scripts/official_tables.py']
        for name in constructors.sources:
            sources.extend(path for folder in ('Common', 'Common.LubanGen')
                if (path := CODE / 'common-source' / folder / (name + '.cs')).is_file())
        proof = dict(format=1, manifest=apk.version, tables=inputs,
            inputs=[record(path) for path in dict.fromkeys(sources)], native_markers=record(receipt_path),
            outputs=[record(destination), record(meta), record(folder_meta)],
            integration=[record(ROOT / path) for path in (
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapMarkerCatalog.cs',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/MainlineMiniMapMarkerCatalog.cs.meta',
                'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap/Zjcs.Offline.World.MainlineMiniMap.asmdef',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMap13MiniMapMarkSupportsAcceptance.cs',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMap13MiniMapMarkSupportsAcceptance.Catalog.cs',
                'unity/Assets/Game/Editor/Map13MiniMap/OfficialMap13MiniMapMarkSupportsAcceptance.Catalog.cs.meta',
                'scripts/prepare_mainline_minimap_offline_validation.py',
                'scripts/test_mainline_minimap_native_bindings.py')],
            counts=dict(maps=len(selected_maps), extensions=len(selected_extensions), displays=len(selected_displays), markers=len(names)),
            operation='Project only the original marker-consumed extension fields, preserve complete matching UI-display rows and their serialized order. The installed original Get and injected Load bodies match the selected original code; extension marker prefix and UI override fallback follow MapUtils. No flattened selector, map-group substitution, server rule or marker state is inferred.')
        output = ROOT / 'assets/reference/official-map/mainline-minimap-marker-catalog-source-manifest.json'
        output.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
        print('Installed shared original marker queries:', proof['counts'])
    finally:
        apk.archive.close()


if __name__ == '__main__':
    build()
