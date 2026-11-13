"""Bind selected bed XP metadata, original text and palette to production awards."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_home_mainline_task_data import read_texts


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare():
    native_path = ROOT / 'assets/reference/home-award-queue-native-source-manifest.json'
    native = json.loads(native_path.read_bytes())
    rules_path = ROOT / 'unity/Assets/Official/Home/home-rules-current.json'
    rules = json.loads(rules_path.read_bytes())
    if native['manifest'] != '884_156692' or rules['manifest'] != '884_156692':
        raise ValueError('Selected award version changed')
    item, = [row for row in rules['tables']['item'] if row['ClassId'] == 50]
    if item['ItemType'] != 50 or item['IconEffect'] or not item['CanOverlap']:
        raise ValueError('Bind the selected original specialized XP branch')
    keys = {'ui_award_list_tip', 'ui_award_list_append_tip', 'item_50_name',
            'ui_ItemIcon_count_key', 'ui_ItemIcon_count_overflow_key', 'ui_output_addition_equip_text'}
    texts, language = {}, {}
    with Apk.current() as apk:
        if apk.version != '884_156692':
            raise ValueError('Selected APK version changed')
        for name in ('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                     'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'):
            raw = apk.reader.read('WwiseBnk', name)
            texts.update(read_texts(raw.data, keys))
            language[name] = dict(source=dict(raw.provenance), payload_sha256=hashlib.sha256(raw.data).hexdigest())
    if texts.keys() != keys:
        raise ValueError('Original award texts missing: ' + repr(keys - texts.keys()))
    payload = dict(manifest='884_156692', item=item, global_colors=native['global_colors']['fields'],
                   texts=[dict(key=key, text=texts[key]) for key in sorted(keys)])
    output = ROOT / 'unity/Assets/Game/Resources/Home/home-award-queue-data.json'
    proof_path = ROOT / 'assets/reference/home-award-queue-data-source-manifest.json'
    data = (json.dumps(payload, ensure_ascii=False, indent=2) + '\n').encode('utf8')
    if output.exists() and output.read_bytes() != data:
        previous = json.loads(proof_path.read_bytes())
        if sha(output) != previous['outputs'][output.relative_to(ROOT).as_posix()]:
            raise ValueError('Preserve changed production award data')
    output.write_bytes(data)
    meta = Path(str(output) + '.meta')
    if not meta.exists():
        meta.write_text('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs:home-award-queue-data').hexdigest()
                        + '\nTextScriptImporter:\n  externalObjects: {}\n', 'utf8', newline='\n')
    proof = dict(manifest='884_156692', language=language,
                 inputs={p.relative_to(ROOT).as_posix(): sha(p) for p in (Path(__file__), native_path, rules_path)},
                 outputs={p.relative_to(ROOT).as_posix(): sha(p) for p in (output, meta)},
                 scope='Selected production bed XP receipt presentation; other reward metadata and icon mappings remain separate.')
    proof_path.write_text(json.dumps(proof, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Bound selected production bed award data')


if __name__ == '__main__':
    prepare()
