"""Stage the selected original portrait component with explicit region services."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_home_story_option_code import replace, sha
from validation_workspace import validation_directory

VERSION = '884_156692'
SOURCE = ROOT / '.tools/source-packages/current' / VERSION / 'official-code/client-source'
NAMES = ('PortraitFace', 'PortraitFaceData', 'PortraitFaceRegionData',
         'PortraitFaceMainRegionData', 'PortratFaceOffsetData', 'ETalkPortraitDirectionType')


def port(name, raw):
    text = raw.decode('utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'^using (?:System|Common|UnityEngine);\n', '', text, flags=re.M)
    text = re.sub(r'\s*//IL_[^\n]*', '', text)
    if name == 'PortraitFace':
        text = replace(text, 'public class PortraitFace : MonoBehaviour\n{',
            'public class PortraitFace : MonoBehaviour\n{\n\tpublic Func<RegionCodeEnum> RegionProvider { get; set; }')
        text = replace(text, 'ServerInfoManager.Instance.RegionCode', 'RegionProvider()')
        text = replace(text, 'data.GetFaceSpriteProxy(mDirectionType)',
            'data.GetFaceSpriteProxy(mDirectionType, RegionProvider())')
    if name == 'PortraitFaceData':
        text = replace(text, 'region ?? ServerInfoManager.Instance.RegionCode',
            'region ?? throw new InvalidOperationException("Portrait region is required.")')
    if 'ServerInfoManager' in text:
        raise ValueError('Unbound official region service in ' + name)
    header = ('// Adapted from the selected original client; see home-story-portrait source manifest.\n'
        'namespace Zjcs.Official.HomeStoryPortrait\n{\n'
        'using System;\nusing Common;\nusing UnityEngine;\nusing Zjcs.Official.HomeHudUI;\n'
        'using Object = UnityEngine.Object;\n')
    return '\n'.join(line.rstrip() for line in (header + text + '\n}\n').splitlines()) + '\n'


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file():
        raise ValueError('An owned validation workspace is required.')
    folder = work / 'home-story-portrait-code'
    folder.mkdir(exist_ok=True)
    inputs, outputs = {}, {}
    for name in NAMES:
        source = SOURCE / (name + '.cs')
        raw = source.read_bytes()
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        target = folder / source.name
        target.write_text(port(name, raw), 'utf8', newline='\n')
        key = hashlib.md5(('zjcs:home-story-portrait:' + name).encode()).hexdigest()
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n'
            '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',
            'utf8', newline='\n')
        for path in (target, Path(str(target) + '.meta')):
            outputs[path.name] = sha(path.read_bytes())
    target = folder / 'Zjcs.Official.HomeStoryPortrait.asmdef'
    target.write_text(json.dumps(dict(name='Zjcs.Official.HomeStoryPortrait', references=[
        'Launch', 'Zjcs.Official.HomeHudUI', 'Zjcs.Official.ProfessionUI', 'Unity.ugui']), indent=2) + '\n',
        'utf8', newline='\n')
    key = hashlib.md5(b'zjcs:home-story-portrait:asmdef').hexdigest()
    Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nAssemblyDefinitionImporter:\n'
        '  externalObjects: {}\n', 'utf8', newline='\n')
    for path in (target, Path(str(target) + '.meta')):
        outputs[path.name] = sha(path.read_bytes())
    (work / 'home-story-portrait-code-source-manifest.json').write_text(json.dumps(dict(
        manifest=VERSION, inputs=inputs, outputs=outputs, generator_sha256=sha(Path(__file__).read_bytes()),
        scope='Original portrait and face components with explicit local region; native prefabs remain to be bound.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original story portrait components:', len(NAMES), flush=True)


if __name__ == '__main__':
    main()
