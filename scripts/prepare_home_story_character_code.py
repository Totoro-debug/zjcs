"""Stage original dialogue character switching and animation controllers."""
import hashlib
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_home_story_option_code import replace, sha, strip_log
from validation_workspace import validation_directory

VERSION = '884_156692'
SOURCE = ROOT / '.tools/source-packages/current' / VERSION / 'official-code/client-source'
NAMES = ('TalkDialogCharacter', 'TalkDialogCharacterController', 'TalkDialogContentSwitch',
         'ETalkPortraitStatusType')


def port(name, raw):
    text = raw.decode('utf-8-sig').replace('\r\n', '\n')
    text = re.sub(r'^using (?:System|System\.Linq|UnityEngine|Framework(?:\.[\w.]+)?|R3);\n', '', text, flags=re.M)
    text = re.sub(r'\s*//IL_[^\n]*', '', text)
    text, count = strip_log(text, name)
    if count != (1 if name == 'TalkDialogCharacter' else 0):
        raise ValueError('Review original dialogue character logging branches: ' + name)
    if name == 'TalkDialogCharacter':
        text = replace(text, '\tprivate DisposableBag mDisposableBag;\n\n\t[Inject]\n\tprivate IResourceLoader ResourceLoader { get; set; }',
            '\tprivate readonly List<IHomeHudAssetHandle<GameObject>> mPortraitHandles = new List<IHomeHudAssetHandle<GameObject>>();\n'
            '\tpublic IStoryPortraitServices Services { get; set; }')
        text = replace(text, '\t\tmDisposableBag = default;', '\t\tmPortraitHandles.Clear();')
        text = replace(text, '\t\tmDisposableBag.Dispose();',
            '\t\tforeach (var handle in mPortraitHandles) handle.Dispose();\n\t\tmPortraitHandles.Clear();')
        text = replace(text, 'StoryUtils.TimeLerp(', 'StoryPortraitTime.TimeLerp(')
        text = replace(text, 'Disposable.AddTo<AssetOperationHandle<GameObject>>(ResourceLoader.LoadPrefab(path), ref mDisposableBag).Result',
            'LoadPortrait(path)')
        text = replace(text, 'UIUtils.Instantiate(result, (Transform)(object)mCharacterOffsetAdaptRect)',
            'InstantiatePortrait(result, (Transform)(object)mCharacterOffsetAdaptRect)')
        text = replace(text, '\t\t\tcomponent.SetDirection(mTalkPortraitDirectionType);',
            '\t\t\tcomponent.RegionProvider = Services.Region;\n\t\t\tcomponent.SetDirection(mTalkPortraitDirectionType);')
        insertion = ('\tprivate GameObject LoadPortrait(string path)\n\t{\n'
            '\t\tvar handle = Services.LoadPortrait(path);\n\t\tmPortraitHandles.Add(handle);\n'
            '\t\treturn handle.Result;\n\t}\n\n'
            '\tprivate static GameObject InstantiatePortrait(GameObject prefab, Transform parent)\n\t{\n'
            '\t\tGameObject value = Object.Instantiate(prefab, parent, false);\n'
            '\t\tvalue.transform.localPosition = Vector3.zero;\n\t\tvalue.transform.localRotation = Quaternion.identity;\n'
            '\t\tvalue.transform.localScale = Vector3.one;\n\t\tvalue.SetActive(true);\n\t\treturn value;\n\t}\n\n')
        text = replace(text, '\tprivate PortraitFace CreatePortrait(string path)', insertion + '\tprivate PortraitFace CreatePortrait(string path)')
    if name == 'TalkDialogCharacterController':
        text = replace(text, '\t[Inject]\n\tprivate IScriptableObjectConfig<StorySettings> StorySettings { get; set; }',
            '\tpublic IStoryPortraitServices Services { get; set; }')
        text = replace(text, 'StorySettings.Value.VoiceOverIds.Contains(roleId)', 'Services.IsVoiceOver(roleId)')
        text = replace(text, '\tpublic void SetPortrait(int roleId, string portraitPath, string face, Action onSwitchPortraitFinish)\n\t{',
            '\tpublic void SetPortrait(int roleId, string portraitPath, string face, Action onSwitchPortraitFinish)\n\t{\n'
            '\t\tmLeftCharacter.Services = Services;\n\t\tmRightCharacter.Services = Services;')
    for token in ('GLogger', '[Inject]', 'ResourceLoader', 'DisposableBag', 'AssetOperationHandle',
                  'StorySettings.', 'StoryUtils.', 'UIUtils.'):
        if token in text: raise ValueError('Unbound original dialogue character dependency: ' + name + '/' + token)
    header = ('// Adapted from the selected original client; see home-story-character source manifest.\n'
        'namespace Zjcs.Official.HomeStoryCharacter\n{\nusing System;\nusing System.Collections.Generic;\n'
        'using UnityEngine;\nusing Zjcs.Official.HomeHudUI;\nusing Zjcs.Official.HomeStoryPortrait;\n'
        'using Object = UnityEngine.Object;\n')
    return '\n'.join(line.rstrip() for line in (header + text + '\n}\n').splitlines()) + '\n'


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('An owned validation workspace is required.')
    folder = work / 'home-story-character-code'
    folder.mkdir(exist_ok=True)
    inputs, outputs = {}, {}
    for name in NAMES:
        source = SOURCE / (name + '.cs')
        raw = source.read_bytes()
        inputs[source.relative_to(ROOT).as_posix()] = sha(raw)
        target = folder / source.name
        target.write_text(port(name, raw), 'utf8', newline='\n')
        key = hashlib.md5(('zjcs:home-story-character:' + name).encode()).hexdigest()
        Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n'
            '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',
            'utf8', newline='\n')
        for path in (target, Path(str(target) + '.meta')): outputs[path.name] = sha(path.read_bytes())
    template = ROOT / 'scripts/templates/home_story_character_services.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    for name in ('StoryUtils', 'UIUtils', 'EUIInstantiateFlag'):
        path = SOURCE / (name + '.cs')
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
    target = folder / 'StoryPortraitServices.cs'
    target.write_bytes(template.read_bytes())
    key = hashlib.md5(b'zjcs:home-story-character:StoryPortraitServices').hexdigest()
    Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nMonoImporter:\n'
        '  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',
        'utf8', newline='\n')
    for path in (target, Path(str(target) + '.meta')): outputs[path.name] = sha(path.read_bytes())
    target = folder / 'Zjcs.Official.HomeStoryCharacter.asmdef'
    target.write_text(json.dumps(dict(name='Zjcs.Official.HomeStoryCharacter', references=[
        'Launch', 'UniTask', 'Zjcs.Official.HomeStoryPortrait', 'Zjcs.Official.HomeHudUI', 'Unity.ugui']),
        indent=2) + '\n', 'utf8', newline='\n')
    key = hashlib.md5(b'zjcs:home-story-character:asmdef').hexdigest()
    Path(str(target) + '.meta').write_text('fileFormatVersion: 2\nguid: ' + key + '\nAssemblyDefinitionImporter:\n'
        '  externalObjects: {}\n', 'utf8', newline='\n')
    for path in (target, Path(str(target) + '.meta')): outputs[path.name] = sha(path.read_bytes())
    (work / 'home-story-character-code-source-manifest.json').write_text(json.dumps(dict(
        manifest=VERSION, inputs=inputs, outputs=outputs, generator_sha256=sha(Path(__file__).read_bytes()),
        scope='Original portrait switching, animation and time interpolation; dialogue hierarchy remains to be bound.'),
        ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original dialogue character controllers:', len(NAMES), flush=True)


if __name__ == '__main__':
    main()
