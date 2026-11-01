"""Record the shared public/private route presenter and its unchanged native UI inputs."""
import hashlib
import json
from pathlib import Path

from official_source import OfficialSource

ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT/'assets/reference/dungeon-ui/route-presenter-source-manifest.json'


def record(path):
    path = ROOT/path
    raw = path.read_bytes()
    normalized = path.suffix in ('.cs', '.py')
    if normalized:
        raw = raw.replace(b'\r\n', b'\n')
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(raw).hexdigest(),
                hashLineEndings='LF' if normalized else 'raw')


def build():
    selected = OfficialSource()
    if selected.version != '884_156692':
        raise ValueError('Review selected official route code before reproducing')
    historical = ROOT/'assets/reference/dungeon-ui/prefab-binding-source-manifest.json'
    receipt = json.loads(historical.read_bytes())
    binding = receipt['bindings']
    if record(binding['path'])['sha256'] != binding['sha256']:
        raise ValueError('Reused original route UI bindings differ from their recorded bytes')
    data = json.loads((ROOT/binding['path']).read_bytes())
    code = '.tools/source-packages/current/884_156692/official-code/'
    prefabs = ('DailyDungeonBigMapDialog', 'DailyDungeonBigMapRoomItem', 'DailyDungeonGradeItem')
    output = dict(
        selected_manifest=selected.version,
        reused_native_ui_manifest=data['manifest'],
        historical_ui_receipt=record(historical.relative_to(ROOT)),
        ui_bindings=record(binding['path']),
        native_prefabs=[record('unity/Assets/Game/Resources/Dungeons/UI/'+name+'.prefab') for name in prefabs],
        official_code=[record(code+'client-source/'+name+'.cs') for name in
                       ('DailyDungeonBigMapDialog', 'DailyDungeonBigMapRoomItem', 'DailyDungeonRoomItem')]
                      + [record(code+'common-source/Common/DailyDungeonMapGroupInfoParser.cs')],
        reused_ports=[record('unity/Assets/Game/Runtime/Official/WorldUI/'+name+'.cs') for name in
                      ('WorldDailyDungeonRoute', 'WorldDailyDungeonRoomItem')],
        runtime=record('unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonRoutePresenter.cs'),
        shared_bindings=record('unity/Assets/Game/Runtime/Presentation/World/OfficialDungeonUiBindingRuntime.cs'),
        callers=[record('unity/Assets/Game/Runtime/Presentation/Home/HomeHudView.Dungeons.cs'),
                 record('unity/Assets/Game/Runtime/Presentation/World/Mainline/OfficialMainlineMapRuntime.Map12Buildings.cs')],
        validation=record('unity/Assets/Game/Editor/HomeBattleHostPlayModeAcceptance.cs'),
        map12_validation=record('unity/Assets/Game/Editor/OfficialMap12PlayModeAcceptance.BuildingRuntime.cs'),
        binding_path_validation=record('scripts/test_dungeon_ui_binding_paths.py'),
        generator=record(Path(__file__).resolve().relative_to(ROOT)),
        operation='Extract the already validated Map12 route presentation into one module: original cell pooling, '
                  'coordinates, normal/choose views, roads, grades, selection order and eligible room callbacks. '
                  'Home and Map12 retain their existing session, room result and exit decisions.',
        scope='No new art or audio payload. The original 871 native UI identity is preserved separately from '
              'the selected 884 code references. No active online matchmaking or route reward rule is added.')
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    OUTPUT.write_text(json.dumps(output, ensure_ascii=False, indent=2)+'\n', encoding='utf-8', newline='\n')
    print('Recorded shared route presenter; preserved native UI manifest:', data['manifest'])


if __name__ == '__main__':
    build()
