"""Bind the original bed output's dynamic text keys to selected APK-first language."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_home_mainline_task_data import read_texts, text_meta


def build():
    source = ROOT / 'unity/Assets/Official/Home/UI/HangUpWindows/home-hangup-window-texts.json'
    keys = {row['key'] for row in json.loads(source.read_bytes())['texts']}
    keys.add('hang_up_1_base_output')
    texts, language = {}, {}
    with Apk.current() as apk:
        assert apk.version == '884_156692'
        for name in ('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                     'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'):
            verified = apk.reader.read('WwiseBnk', name)
            texts.update(read_texts(verified.data, keys))
            language[name] = dict(source=dict(verified.provenance), payload_sha256=hashlib.sha256(verified.data).hexdigest())
    if keys != texts.keys():
        raise ValueError('Original selected bed text is missing: ' + repr(keys - texts.keys()))
    target = ROOT / 'unity/Assets/Game/Resources/Home/home-bed-output-texts.json'
    target.write_text(json.dumps(dict(manifest='884_156692',
        texts=[dict(key=key, text=texts[key]) for key in sorted(texts)]), ensure_ascii=False, indent=2) + '\n',
        'utf8', newline='\n')
    text_meta(target)
    names = [source, Path(__file__), ROOT / 'scripts/prepare_home_mainline_task_data.py',
        ROOT / 'scripts/official_apk.py', ROOT / 'scripts/official_bundle_reader.py',
        ROOT / '.tools/source-packages/current/884_156692/official-code/common-source/Common/LanguageUtils.cs']
    proof = dict(manifest='884_156692', language=language,
        inputs={p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in names},
        outputs={p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in (target, Path(str(target) + '.meta'))},
        dynamic_key='Original LanguageUtils.GetHangUpBaseOutput(1): hang_up_1_base_output')
    (ROOT / 'assets/reference/home-bed-output-texts-source-manifest.json').write_text(
        json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Selected bed output text:', texts['hang_up_1_base_output'])


if __name__ == '__main__':
    build()
