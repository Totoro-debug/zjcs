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
    text = source.read_text('utf8').replace('using Common;\n', 'using Awaiter = Cysharp.Threading.Tasks.UniTask.Awaiter;\n')
    old = 'public class MapRegionLimitStatusRefreshForRankUnlockTime : IMapRegionLimitStatusRefreshProvider, IDisposable\n{'
    assert old in text
    text = text.replace(old, old + '\n\tpublic IMap14LabelServices Services { get; set; }', 1)
    text = text.replace('.GetMapRegionUnlockTime(MapPlanesRankInfo.CreateFromPlanes())', '.GetMapRegionUnlockTime()')
    text = text.replace('TimeManager.Instance.GetDateTime()', 'mapRegionLimitStatusRefreshForRankUnlockTime.Services.Now')
    text = '// Original selected time refresh state machine; only explicit local service binding changes.\nnamespace Zjcs.Official.World.NativeMap14Labels\n{\n' + text + '\n}\n'
    target = ROOT / 'unity/Assets/Game/Runtime/Official/World/NativeMap14Labels/MapRegionLimitStatusRefreshForRankUnlockTime.cs'
    target.write_text(text, 'utf8', newline='\n')
    module = ROOT / 'unity/Assets/Game/Runtime/Offline/World/MapRegionTime'
    for path in [target, module, *module.iterdir()]:
        if path.suffix != '.meta':
            meta(path, folder=path.is_dir())
    print('Bound original timed-region refresh state machine and local module metadata')


if __name__ == '__main__':
    build()
