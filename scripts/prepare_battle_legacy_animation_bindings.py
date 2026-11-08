"""Give native Animation consumers a Legacy copy while retaining the Animator clip."""
import json
import re
from pathlib import Path
from hashlib import sha256

from official_apk import Apk, ROOT
from prepare_battle_scene_appearance_runtime import identity, meta, write_changed

DEST = ROOT / 'unity/Assets/Official/Combat/SkillGraphRuntime/Resources/Combat/SkillGraphs'
EXPORT_ROOT = ROOT / '.tools/source-packages/current/884_156692/native-conversion/native-export-all'
EXPORT = EXPORT_ROOT / 'ExportedProject/Assets'
GRAPH_RECEIPT = ROOT / 'assets/reference/battle-animation/skill-native-graph-assets-source-manifest.json'
RECEIPT = ROOT / 'assets/reference/battle-animation/legacy-animation-bindings-source-manifest.json'
NAME = 'Eff_Monster_3511_Skill02_3'
PREFABS = tuple('Prefabs/EcEntity/Fight/FightSkill/63110/' + name + '.prefab'
                for name in ('playable63110', 'playable63110up', 'playable63110down'))


def digest(raw):
    return sha256(raw).hexdigest()


def prepare():
    proof = json.loads(GRAPH_RECEIPT.read_text(encoding='utf-8'))
    export_receipt = EXPORT_ROOT.parent / 'native-export-all-source-manifest.json'
    exported = json.loads(export_receipt.read_text(encoding='utf-8'))
    if proof['manifest'] != '884_156692' or exported['manifest'] != proof['manifest']:
        raise ValueError('Review the active native animation input version.')
    verified = {row['path']: row['sha256'] for row in exported['outputs']}
    records = {row['path']: row for row in proof['outputs']}
    inputs = []

    def source(relative):
        path = EXPORT / relative
        if digest(path.read_bytes()) != verified[path.relative_to(EXPORT_ROOT).as_posix()]:
            raise ValueError('Original animation binding differs from its export: ' + relative)
        inputs.append(identity(path))
        return path.read_text(encoding='utf-8-sig')

    def current(path):
        row = records[path.relative_to(ROOT).as_posix()]
        if digest(path.read_bytes()) != row['sha256']:
            raise ValueError('Refusing to overwrite changed native animation output: ' + str(path))
        return path.read_text(encoding='utf-8-sig')

    relative = 'AnimationClip/' + NAME + '.anim'
    original = source(relative)
    if original.count('  m_Legacy: 0\n') != 1:
        raise ValueError('Review original dual Animation/Animator clip mode.')
    original_guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', source(relative + '.meta'))[1]
    clip = DEST / relative
    text = current(clip)
    clip_guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', current(Path(str(clip) + '.meta')))[1]
    if text != original:
        raise ValueError('Review original animation curves before making a Legacy copy.')
    apk = Apk.current()
    try:
        if apk.version != proof['manifest']:
            raise ValueError('Native animation inputs differ from the selected official version.')
        bundle = 'defaultpackage_assets_prefabs_ecentity_fight_fightskill_63110.bundle'
        clips = [obj for obj in apk.objects(bundle)
                 if obj.type.name == 'AnimationClip' and obj.peek_name() == NAME]
        if len(clips) != 1 or clips[0].read().m_Legacy:
            raise ValueError('Review the official bundled clip mode.')
        native = dict(source=apk.provenance(clips[0]), object_sha256=digest(clips[0].get_raw_data()),
                      name=NAME, legacy=False)
    finally:
        apk.archive.close()
    clone = clip.with_name(clip.stem + '.legacy.anim')
    converted = text.replace('  m_Legacy: 0\n', '  m_Legacy: 1\n')
    if clone.exists() and clone.read_text(encoding='utf-8-sig') != converted:
        raise ValueError('Refusing to overwrite a changed Legacy animation copy.')
    write_changed(clone, converted)
    meta(clone)
    clone_meta = Path(str(clone) + '.meta')
    clone_guid = re.search(r'(?m)^guid: ([a-f0-9]{32})$', clone_meta.read_text(encoding='utf-8-sig'))[1]
    outputs = [clone, clone_meta]
    bindings = []
    block = re.compile(r'(?ms)^--- !u!111 &\d+\nAnimation:\n.*?(?=^--- !u!|\Z)')
    for relative in PREFABS:
        raw_source = source(relative)
        source(Path(relative).as_posix() + '.meta')
        original_blocks = [row for row in block.findall(raw_source) if original_guid in row]
        if len(original_blocks) != 1:
            raise ValueError('Expected one original Animation consumer in ' + relative)
        path = DEST / relative
        before = current(path)
        replacements = 0

        def replace(match):
            nonlocal replacements
            body = match[0]
            if clip_guid in body or clone_guid in body:
                replacements += 1
                return body.replace('guid: ' + clip_guid, 'guid: ' + clone_guid)
            return body

        after = block.sub(replace, before)
        if replacements != 1:
            raise ValueError('Expected one converted Animation consumer in ' + relative)
        write_changed(path, after)
        records[path.relative_to(ROOT).as_posix()] = identity(path)
        outputs.append(path)
        bindings.append(dict(prefab=path.relative_to(ROOT).as_posix(),
                             original_animation_component=original_blocks[0].splitlines()[0],
                             animator_clip_guid=clip_guid, animation_clip_guid=clone_guid))
    for path in outputs[:2]:
        records[path.relative_to(ROOT).as_posix()] = identity(path)
    old_paths = [row['path'] for row in proof['outputs']]
    old_path_set = set(old_paths)
    proof['outputs'] = [records[key] for key in old_paths]
    proof['outputs'].extend(records[key] for key in records if key not in old_path_set)
    proof['generator'] = identity(ROOT / 'scripts/prepare_battle_skill_graph_assets.py')
    proof['legacy_animation_binding_adapter'] = identity(Path(__file__))
    write_changed(GRAPH_RECEIPT, json.dumps(proof, ensure_ascii=False, indent=2) + '\n')

    write_changed(RECEIPT, json.dumps(dict(manifest=proof['manifest'], original_clip=native,
        native_export_receipt=identity(export_receipt), native_inputs=inputs,
        generator=identity(Path(__file__)), outputs=[identity(path) for path in outputs], bindings=bindings,
        operation='Preserve every original curve, event and clip setting. Copy the dual-use non-Legacy clip with only m_Legacy=1 for the three original Animation consumers. Animator controllers retain the original non-Legacy clip.'),
        ensure_ascii=False, indent=2) + '\n')
    print('Restored original Animation bindings:', len(bindings), 'Legacy copy:', clone.name)


if __name__ == '__main__':
    prepare()
