"""Record original region time branches, the confirmed local date source and binding changes."""
import json
from pathlib import Path
import subprocess

from official_apk import ROOT
from prepare_mainline_map15_configurations import record, sha
from prepare_unity_map13_sources import encode, write

BASELINE = 'c0bcf8d89a28e77f8d70f39f0d09a786f1aa20b3'


def build():
    prepared_path = ROOT / '.tools/source-packages/prepared-code-source-manifest.json'
    prepared = json.loads(prepared_path.read_bytes())
    expected = {row['path']: row['sha256'] for row in prepared['outputs']}
    inputs = []
    for relative in ('common-source/Common/MapRegionInfo.cs', 'common-source/MapUtils.cs',
            'client-source/MapRegionLimitStatusRefreshHelper.cs',
            'client-source/MapRegionLimitStatusRefreshForRankUnlockTime.cs',
            'client-source/ShowRemainTimeInfo.cs',
            'framework-firstpass-source/Framework/CultureTimeSpanArg.cs'):
        path = ROOT / '.tools/source-packages/official-code' / relative
        if prepared['manifest'] != '884_156692' or expected.get(path.relative_to(ROOT).as_posix()) != sha(path.read_bytes()):
            raise ValueError('Selected original timed-region source differs: ' + relative)
        inputs.append(record(path))
    formatter = 'unity/Assets/Game/Runtime/Official/World/NativeLabelUI/Framework/CultureTimeSpanArg.cs'
    original = subprocess.check_output(['git', 'show', BASELINE + ':' + formatter], cwd=ROOT).decode('utf8').replace('\r\n', '\n')
    current = (ROOT / formatter).read_text('utf8')
    language_path = ROOT / 'assets/reference/official-map/map13-language-code-source-manifest.json'
    language = json.loads(language_path.read_bytes())
    previous_formatter = next(row for row in language['inputs'] if row['path'].endswith('/Framework/CultureTimeSpanArg.cs'))
    if inputs[-1]['sha256'] != previous_formatter['sha256']:
        raise ValueError('Selected countdown formatter differs from the migrated original source')
    restored = current.replace('    private readonly ICultureTextFormatter text;\n    private ICultureTextFormatter Text => text ?? Culture.Text;\n', '')
    restored = restored.replace('ETimeSpanFormatType format = ETimeSpanFormatType.Normal,\n        ICultureTextFormatter text = null)', 'ETimeSpanFormatType format = ETimeSpanFormatType.Normal)')
    restored = restored.replace('        this.text = text;\n', '').replace('Text.GetText', 'Culture.Text.GetText')
    if restored != original:
        raise ValueError('Original countdown formatting changed beyond explicit text service injection')
    module = ROOT / 'unity/Assets/Game/Runtime/Offline/World/MapRegionTime'
    outputs = [*sorted(p for p in module.iterdir() if p.is_file()), Path(str(module) + '.meta')]
    paths = [formatter,
        'unity/Assets/Game/Runtime/Offline/Progression/OfflineCharacterProgressionService.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/MapRegionLimitStatusRefreshForRankUnlockTime.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/MapRegionLimitStatusRefreshForRankUnlockTime.cs.meta',
        'unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/MapRegionLimitStatusRefreshHelper.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/Map14LabelServices.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/Zjcs.Official.World.NativeMap14Labels.asmdef',
        'unity/Assets/Game/Runtime/Presentation/World/MainlineNativeGround/MainlineWorldLabels.cs',
        'unity/Assets/Game/Runtime/Presentation/World/MainlineNativeGround/MainlineNativeGround.cs',
        'unity/Assets/Game/Runtime/Presentation/World/MainlineNativeGround/Zjcs.Presentation.World.MainlineNativeGround.asmdef',
        'unity/Assets/Game/Runtime/Presentation/World/NativeMap14/Zjcs.Presentation.World.NativeMap14.asmdef',
        'unity/Assets/Game/Runtime/Presentation/World/Zjcs.Presentation.World.asmdef',
        'scripts/prepare_mainline_region_time.py', 'scripts/prepare_mainline_ground_validation.py',
        'scripts/prepare_mainline_map15_world_view.py']
    paths += ['scripts/prepare_unity_map13_region_components.py',
        'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels/MapRegionInfo.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels/MapRegionLimitStatusRefreshHelper.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels/ShowRemainTimeInfo.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels/MapRegionLimitStatusRefreshForRankUnlockTime.cs',
        'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels/MapRegionLimitStatusRefreshForRankUnlockTime.cs.meta',
        'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels/Zjcs.Official.World.NativeMap13Labels.asmdef',
        'unity/Assets/Game/Editor/Map13MiniMap/Zjcs.Editor.Map13.MiniMap.asmdef']
    paths += [p.relative_to(ROOT).as_posix() for p in
        sorted((ROOT / 'unity/Assets/Game/Runtime/Offline/World/MainlineMiniMap').iterdir()) if p.is_file()]
    paths += [p.relative_to(ROOT).as_posix() for p in
        sorted((ROOT / 'unity/Assets/Game/Editor/Map13MiniMap').glob('OfficialMainlineMiniMapRootAcceptance*.cs*'))]
    fixture = ROOT / 'unity/Assets/Game/Editor/MainlineNativeGround'
    outputs += [ROOT / p for p in paths] + sorted(p for p in fixture.iterdir() if p.is_file())
    proof = dict(format=1, manifest=prepared['manifest'], prepared=record(prepared_path), original_code=inputs,
        original_language_receipt=record(language_path),
        formatter_source_equivalence=dict(selected=inputs[-1], historical=previous_formatter,
            scope='Only this original source file matches byte for byte; the enclosing framework assemblies differ.'),
        formatter_baseline=dict(commit=BASELINE, path=formatter, sha256=sha(original.encode()),
            verification='Reversing only the optional text-service injection reproduces the installed original formatter byte for byte after LF normalization.'),
        definition=record(ROOT / 'unity/Assets/Official/World/Map/16/map16.json'),
        minimap_label_definition=record(ROOT / 'unity/Assets/Game/Resources/Map16/Native/region-label-data.json'),
        minimap_label_definition_receipt=record(ROOT / 'assets/reference/official-map/map16-world-view-source-manifest.json'),
        confirmed_policy='鲸背岛使用角色位阶与本地时间：首次达到位阶4后按各区原14至32天偏移计时；超过位阶4立即通过。保留等级、绳索剧情及其他条件。不应用服务器合服加速。',
        operation='Preserve original rank-none/lower/higher/missing-date branches, AddDays offset, inclusive time boundary and time-before-level. Restore the original UniTask refresh state machine with explicit clock/date services and the missing nested Awaiter qualification. Inject countdown localization per instance without changing format calculations. Region callbacks belong to each label instance. Existing rank-up records are retained as the first acquisition date.',
        outputs=[dict(path=p.relative_to(ROOT).as_posix(), sha256=sha(p.read_bytes().replace(b'\r\n', b'\n'))) for p in outputs],
        hashing='Installed text outputs use UTF-8 LF; original input records retain raw bytes.', tool=record(Path(__file__)))
    write(ROOT / 'assets/reference/official-map/mainline-region-time-source-manifest.json', encode(proof))
    print('Recorded original timed-region bindings:', len(outputs), 'outputs')


if __name__ == '__main__':
    build()
