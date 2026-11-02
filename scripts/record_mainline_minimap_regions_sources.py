"""Record original regional minimap code, current assets and their common runtime seam."""
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_sources import encode, write


def record(path):
    return dict(path=path.relative_to(ROOT).as_posix(),
        sha256=hashlib.sha256(path.read_bytes().replace(b'\r\n', b'\n')).hexdigest())


def build():
    historical_path = ROOT / 'assets/reference/official-map/map13-minimap-region-manager-code-source-manifest.json'
    historical = json.loads(historical_path.read_bytes())
    original = []
    for name in ('MapMeCharacterUIBase', 'MiniMapPrivateRegionMaskManager'):
        before, = [row for row in historical['inputs'] if row['path'].endswith('/' + name + '.cs')]
        current = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source' / (name + '.cs')
        if hashlib.sha256(current.read_bytes()).hexdigest() != before['sha256']:
            raise ValueError('Selected original minimap code differs: ' + name)
        original.append(dict(selected=dict(path=current.relative_to(ROOT).as_posix(), sha256=before['sha256']), historical=before))
        installed, = [row for row in historical['outputs'] if row['path'].endswith('/' + name + '.cs')]
        if hashlib.sha256((ROOT / installed['path']).read_bytes()).hexdigest() != installed['sha256']:
            raise ValueError('The already migrated original region manager changed: ' + name)
    resources = []
    for map_id in (13, 16):
        receipt = ROOT / f'assets/reference/official-map/map{map_id}-current-minimap-regions-source-manifest.json'
        data = json.loads(receipt.read_bytes())
        if data['manifest'] != '884_156692' or data['mapClassId'] != map_id:
            raise ValueError('Selected original minimap resource identity differs')
        for relative, expected in data['outputs'].items():
            if hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() != expected:
                raise ValueError('Installed native minimap output differs: ' + relative)
        resources.append(record(receipt))
    preview = ROOT / '.tools/source-packages/current/884_156692/official-code/framework-source/PreviewTool.cs'
    if hashlib.sha256(preview.read_bytes()).hexdigest() != '66b138f5f124629eb239d4fe9cd3e7b92a5bdd5e3bf691cedc24f6f002982611':
        raise ValueError('Compare the selected original empty PreviewTool before reusing WorldPreviewTool')
    schema = ROOT / '.tools/source-packages/current/884_156692/native-conversion/changed-inputs/Managed/UnityEngine.TilemapModule.dll'
    module = ROOT / 'unity/Assets/Game/Runtime/Presentation/World/MainlineMiniMapRegions'
    fixture = ROOT / 'unity/Assets/Game/Editor/MainlineNativeGround'
    paths = [*sorted(path for path in module.iterdir() if path.is_file()), Path(str(module) + '.meta'),
        *sorted(path for path in fixture.iterdir() if path.is_file())]
    paths += [ROOT / relative for relative in ('scripts/prepare_mainline_minimap_regions.py',
        'scripts/prepare_mainline_map15_native_ground.py', 'scripts/prepare_mainline_ground_validation.py',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldPreviewTool.cs',
        'unity/Assets/Game/Runtime/Official/WorldUI/WorldPreviewTool.cs.meta')]
    proof = dict(format=1, manifest='884_156692', original_code_equivalence=original,
        original_empty_preview_tool=record(preview),
        tile_schema_assembly=dict(path=schema.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(schema.read_bytes()).hexdigest()),
        tile_schema_tools=[record(ROOT / 'scripts' / name) for name in ('current_component_schemas.py', 'official_unity_schema.py')],
        historical_migration=record(historical_path), resource_receipts=resources,
        operation='Bind the original Ground subtree, Grid, region slots and all region Tilemaps. Preserve original region manager initialization, status events, public/private Z, reverse visibility traversal, inactive cache reuse and ten-second retirement. Inject local state/controller/view-range and original GUID asset leases before activation. Original view-range notifications remain explicit so periodic refresh cannot reset retirement. Each map supplies its original asset catalog; original root MiniMap and other UI/marker/mask managers are not replaced or claimed complete.',
        outputs=[record(path) for path in paths], hashing='Repository text outputs use UTF-8 LF; native asset receipts retain raw bytes.',
        tool=record(Path(__file__)))
    write(ROOT / 'assets/reference/official-map/mainline-minimap-regions-source-manifest.json', encode(proof))
    print('Recorded shared original regional minimap:', len(paths), 'code/metadata outputs')


if __name__ == '__main__':
    build()
