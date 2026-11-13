"""Stage the selected original queue in the owned graphical Unity fixture."""
import hashlib
import json
from pathlib import Path

from official_apk import Apk, ROOT
from prepare_home_mainline_task_data import read_texts
from validation_workspace import validation_directory, require_temporary_unity_project


def prepare():
    work = validation_directory()
    project = require_temporary_unity_project(work / 'unity')
    proof_path = ROOT / 'assets/reference/home-award-queue-native-source-manifest.json'
    proof = json.loads(proof_path.read_bytes())
    for relative, expected in proof['installed_output_sha256'].items():
        if hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() != expected:
            raise ValueError('Installed award asset changed: ' + relative)
    for row in proof['reused']:
        if hashlib.sha256((ROOT / row['path']).read_bytes()).hexdigest() != row['sha256']:
            raise ValueError('Shared award dependency changed: ' + row['path'])
    runtime_proof = json.loads((ROOT / 'assets/reference/home-award-queue-runtime-source-manifest.json').read_bytes())
    for relative, expected in runtime_proof['outputs'].items():
        if hashlib.sha256((ROOT / relative).read_bytes()).hexdigest() != expected:
            raise ValueError('Original award runtime changed: ' + relative)
    paths = set(proof['installed_output_sha256'])
    for row in proof['reused']:
        paths.update((row['path'], row['path'] + '.meta'))
    for row in proof['shared_textures']:
        paths.update((row['canonical']['path'], row['canonical']['path'] + '.meta'))
    runtime = ROOT / 'unity/Assets/Game/Runtime/Official/HomeAwardUI'
    paths.update(p.relative_to(ROOT).as_posix() for p in runtime.rglob('*') if p.is_file())
    paths.add(runtime.relative_to(ROOT).as_posix() + '.meta')
    for relative in sorted(paths):
        source = ROOT / relative
        target = work / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
    keys = {'ui_award_list_tip', 'ui_award_list_append_tip', 'item_50_name',
        'ui_ItemIcon_count_key', 'ui_ItemIcon_count_overflow_key', 'ui_output_addition_equip_text'}
    texts, language = {}, {}
    with Apk.current() as apk:
        if apk.version != '884_156692': raise ValueError('Selected version changed')
        for name in ('wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                     'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile'):
            verified = apk.reader.read('WwiseBnk', name)
            texts.update(read_texts(verified.data, keys))
            language[name] = dict(source=dict(verified.provenance), payload_sha256=hashlib.sha256(verified.data).hexdigest())
    if texts.keys() != keys: raise ValueError('Selected award text missing: ' + repr(keys - texts.keys()))
    rules_path = ROOT / 'unity/Assets/Official/Home/home-rules-current.json'
    rules = json.loads(rules_path.read_bytes())
    if rules['manifest'] != '884_156692': raise ValueError('Selected item table changed')
    item, = [row for row in rules['tables']['item'] if row['ClassId'] == 50]
    data = dict(item=item, global_colors=proof['global_colors']['fields'],
        texts=[dict(key=key, text=texts[key]) for key in sorted(keys)])
    data_path = project / 'Assets/HomeAwardQueueValidation/Resources/home-award-queue-fixture.json'
    data_path.parent.mkdir(parents=True, exist_ok=True)
    data_path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    fixture = ROOT / 'scripts/fixtures/home_award_queue_validation.cs'
    (data_path.parent.parent / 'HomeAwardQueueValidation.cs').write_bytes(fixture.read_bytes())
    references = ['Unity.ugui', 'Unity.TextMeshPro', 'Zjcs.Official.HomeAwardUI',
        'Zjcs.Official.ProfessionUI', 'Zjcs.Official.WorldUI', 'Zjcs.Official.World.NativeLabelUI',
        'Zjcs.Official.HomeHangUpWindowsUI', 'Zjcs.Offline.HomeHudUI',
        'Zjcs.Offline.PartnersUI', 'Zjcs.Offline.ProfessionUI', 'Coffee.UIParticle']
    (data_path.parent.parent / 'Zjcs.HomeAwardQueueValidation.asmdef').write_text(json.dumps(dict(
        name='Zjcs.HomeAwardQueueValidation', references=references, includePlatforms=['Editor']), indent=2) + '\n', 'utf8')
    result = dict(manifest='884_156692', native_manifest=proof_path.relative_to(ROOT).as_posix(),
        native_sha256=hashlib.sha256(proof_path.read_bytes()).hexdigest(), language=language,
        runtime_source_sha256=hashlib.sha256((ROOT / 'assets/reference/home-award-queue-runtime-source-manifest.json').read_bytes()).hexdigest(),
        inputs={p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in (Path(__file__), fixture, rules_path)},
        bindings={p: hashlib.sha256((ROOT / p).read_bytes()).hexdigest() for p in sorted(paths)},
        validation_base_commit=json.loads((work / 'award-queue-base-commit.json').read_bytes())['commit']
            if (work / 'award-queue-base-commit.json').exists() else '62d5197181a605fed844607adece4ab209c2cdc6',
        selected_xp_icon_source='assets/reference/home-hangup-windows-native-source-manifest.json',
        selected_xp_icon_source_sha256=hashlib.sha256((ROOT / 'assets/reference/home-hangup-windows-native-source-manifest.json').read_bytes()).hexdigest(),
        scope='Graphical original queue with selected XP item only; general reward catalog and production collection binding remain incomplete.')
    (ROOT / 'assets/reference/home-award-queue-validation-source-manifest.json').write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared original award graphical fixture:', len(paths), 'bound files')


if __name__ == '__main__': prepare()
