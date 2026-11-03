"""Bind the original timed region refresh provider to the local map clock port."""
import json
from pathlib import Path

from official_apk import ROOT
from prepare_unity_map13_sources import sha
from prepare_unity_map14_sources import meta


def build():
    source = ROOT / '.tools/source-packages/official-code/client-source/MapRegionLimitStatusRefreshForRankUnlockTime.cs'
    receipt = json.loads((ROOT / '.tools/source-packages/prepared-code-source-manifest.json').read_bytes())
    expected = {row['path']: row['sha256'] for row in receipt['outputs']}
    if receipt['manifest'] != '884_156692' or expected.get(source.relative_to(ROOT).as_posix()) != sha(source.read_bytes()):
        raise ValueError('Selected original time refresh source differs from prepared receipt')
    for label_id in (13, 14):
        text = source.read_text('utf8').replace('using Common;\n', 'using Awaiter = Cysharp.Threading.Tasks.UniTask.Awaiter;\n')
        old = 'public class MapRegionLimitStatusRefreshForRankUnlockTime : IMapRegionLimitStatusRefreshProvider, IDisposable\n{'
        assert old in text
        text = text.replace(old, old + f'\n\tpublic IMap{label_id}LabelServices Services {{ get; set; }}', 1)
        text = text.replace('.GetMapRegionUnlockTime(MapPlanesRankInfo.CreateFromPlanes())', '.GetMapRegionUnlockTime()')
        text = text.replace('TimeManager.Instance.GetDateTime()', 'mapRegionLimitStatusRefreshForRankUnlockTime.Services.Now')
        text = f'// Original selected time refresh state machine; only explicit local service binding changes.\nnamespace Zjcs.Official.World.NativeMap{label_id}Labels\n{{\n' + text + '\n}\n'
        target = ROOT / f'unity/Assets/Game/Runtime/Official/World/NativeMap{label_id}Labels/MapRegionLimitStatusRefreshForRankUnlockTime.cs'
        target.write_text(text, 'utf8', newline='\n')
        meta(target)
    helper = ROOT / '.tools/source-packages/official-code/client-source/MapRegionLimitStatusRefreshHelper.cs'
    if expected.get(helper.relative_to(ROOT).as_posix()) != sha(helper.read_bytes()):
        raise ValueError('Selected original time helper differs from prepared receipt')
    text = helper.read_text('utf8').replace('using Common;\n', '').replace('using Framework;\n', '')
    declaration = 'public class MapRegionLimitStatusRefreshHelper\n{'
    assert declaration in text
    text = text.replace(declaration, declaration + '\n\tpublic IMap13LabelServices Services { get; set; }\n', 1)
    text = text.replace('MeManager.MainPlayer', 'Services.Player').replace(', MapPlanesRankInfo.CreateFromPlanes()', '')
    for provider in ('MapRegionLimitStatusRefreshForRankUnlockTime', 'MapRegionLimitStatusRefreshProviderForLevel'):
        text = text.replace('new ' + provider + '()', 'new ' + provider + ' { Services = Services }')
    prefix = '// Migrated official Map13 world-label call path; see source receipt.\nnamespace Zjcs.Official.World.NativeMap13Labels\n{\nusing Object = UnityEngine.Object;\nusing Zjcs.Official.World.NativeLabelUI;\nusing Zjcs.Official.World.NativeLabelUI.Framework;\n'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeMap13Labels/MapRegionLimitStatusRefreshHelper.cs'
    target.write_text(prefix + text + '\n}\n', 'utf8', newline='\n')
    module = ROOT / 'unity/Assets/Game/Runtime/Offline/World/MapRegionTime'
    for path in [module, *module.iterdir()]:
        if path.suffix != '.meta':
            meta(path, folder=path.is_dir())
    print('Bound original timed-region refresh state machine and local module metadata')


if __name__ == '__main__':
    build()
