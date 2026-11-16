"""Extract original story dialogue and camera settings from the selected APK."""
import argparse
import hashlib
import json
import os
from pathlib import Path

from official_apk import Apk, ROOT
from official_unity_schema import ScriptSchemas
from install_home_story_options import metadata
from prepare_unity_map13_sources import meta
from validation_workspace import validation_directory

VERSION = '884_156692'
OUTPUT = ROOT / 'unity/Assets/Game/Resources/Home/story-settings.json'
RECEIPT = ROOT / 'assets/reference/home-story-settings-source-manifest.json'
FIELDS = ('mDialogVoiceStopEventKey', 'mVoiceOverIds', 'TalkBubblePlayablePath',
    'IsTalkNeedCameraMove', 'IsDialogNodeNeedCameraMove', 'TalkCameraMoveXCurve', 'TalkCameraMoveYCurve',
    'TalkCameraMoveDuration', 'TalkCameraViewOffset', 'GetEntityPosOffset')


def sha(raw): return hashlib.sha256(raw).hexdigest()


def build(work):
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('An owned validation workspace is required.')
    code = ROOT / '.tools/source-packages/current' / VERSION / 'official-code'
    schemas = ScriptSchemas(code_root=code)
    with Apk.current() as apk:
        if apk.version != VERSION: raise ValueError('Selected official version changed.')
        obj = apk.asset('scriptableconfig_modules_story', '/StorySettings.asset')
        fields = schemas.read(obj)
        selected = {name: fields[name] for name in FIELDS}
        payload = (json.dumps(dict(manifest=VERSION, settings=selected), ensure_ascii=False, indent=2) + '\n').encode('utf8')
        inputs = {}
        for path in (Path(__file__), ROOT / 'scripts/official_apk.py', ROOT / 'scripts/official_unity_schema.py',
                     code / 'common-source/StorySettings.cs', code / 'client-source/StoryUtils.cs',
                     code / 'client-source/TalkDialog.cs', code / 'client-source/DialogNode.cs',
                     code / 'common-source/DialogInfoUtils.cs'):
            inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        proof = dict(manifest=VERSION, inputs=inputs, object=dict(apk.provenance(obj),
            object_sha256=sha(obj.get_raw_data())), schema_records=schemas.records,
            output=dict(path=OUTPUT.relative_to(ROOT).as_posix(), sha256=sha(payload)),
            scope='Original dialogue voice, narrator and camera settings; execution requires the story host.')
    (work / 'home-story-settings.json').write_bytes(payload)
    (work / 'home-story-settings-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Staged original story settings:', len(selected), 'fields', flush=True)


def install(work):
    proof = json.loads((work / 'home-story-settings-source-manifest.json').read_bytes())
    payload = (work / 'home-story-settings.json').read_bytes()
    if sha(payload) != proof['output']['sha256']: raise ValueError('Staged story settings changed.')
    for path, expected in proof['inputs'].items():
        if sha((ROOT / path).read_bytes()) != expected: raise ValueError('Changed story setting input: ' + path)
    if OUTPUT.exists():
        if not RECEIPT.is_file(): raise ValueError('Preserve existing untracked story settings.')
        previous = json.loads(RECEIPT.read_bytes())
        if sha(OUTPUT.read_bytes()) != previous['output']['sha256']:
            raise ValueError('Existing story settings differ from their receipt.')
    OUTPUT.write_bytes(payload)
    meta(OUTPUT)
    proof['output']['meta_sha256'] = sha(Path(str(OUTPUT) + '.meta').read_bytes())
    runtime = ROOT / 'unity/Assets/Game/Runtime/Offline/HomeStory/HomeStorySettings.cs'
    runtime_meta = metadata(runtime, 'MonoImporter')
    proof['runtime_binding'] = {path.relative_to(ROOT).as_posix(): sha(path.read_bytes()) for path in (runtime, runtime_meta)}
    RECEIPT.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Installed original story settings.', flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    parser.add_argument('--dotnet-dir', type=Path)
    args = parser.parse_args()
    if args.dotnet_dir:
        if not (args.dotnet_dir / 'dotnet.exe').is_file(): raise ValueError('The provided .NET runtime is missing dotnet.exe.')
        os.environ['PATH'] = str(args.dotnet_dir) + os.pathsep + os.environ.get('PATH', '')
    (install if args.install else build)(validation_directory())
