"""Migrate original custom playable targeting without changing selection order."""
import json
import re
from pathlib import Path

from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, identity, write, method_body
from prepare_battle_status_material_runtime import method_fingerprints
from prepare_unity_story_sound_runtime import runtime_meta

RECEIPT = ROOT/'assets/reference/battle-animation/status-custom-runtime-source-manifest.json'


def build():
    manifest = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly = CODE/'managed/decoded/GameFramework.Runtime.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    source = CODE/'game-framework-source/GameFramework/ApplyByCustom.cs'
    original = source.read_text(encoding='utf-8-sig')
    assert original.count('namespace GameFramework;') == 1
    text = '// Migrated current official framework source; see status custom runtime receipt.\n'+original.replace(
        'namespace GameFramework;', 'namespace Zjcs.Official.StoryPlayables\n{')+'\n}\n'
    before, after = method_fingerprints(original), method_fingerprints(text)
    assert len(before) == 3 and before == after
    target = ROOT/'unity/Assets/Game/Runtime/Official/StoryPlayables/ApplyByCustom.cs'
    write(target, text.encode()); runtime_meta(target)
    dependencies = []
    normalize = lambda text: re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', text))
    for name, methods in (
        ('MyPlayableOnInit', ('Awake', 'OnEnable', 'OnDisable', 'OnDestroy', 'OnPlayableInit', 'OnPlayablePlay')),
        ('MyPlayableApplyUtils', ('SetPlayableApplyComponent',)),
        ('MyPlayableApplyAttribute', ('GetApplyComponent',))):
        src = CODE/'game-framework-source/GameFramework'/(name+'.cs')
        dst = target.parent/(name+'.cs')
        for method in methods:
            assert normalize(method_body(src.read_text(encoding='utf-8-sig'), method)) == normalize(
                method_body(dst.read_text(encoding='utf-8-sig'), method)), (name, method)
        dependencies.append(dict(original=identity(src), migrated=identity(dst), unchanged_methods=methods))
    native_root = ROOT/'.tools/source-packages/current/884_156692/native-conversion'
    native_receipt = native_root/'native-export-all-source-manifest.json'
    export = json.loads(native_receipt.read_bytes())
    assert export['manifest'] == manifest['manifest']
    hashes = {row['path']: row['sha256'] for row in export['outputs']}
    uses = []
    for entity, names in ((58220, (58222, 58223, 58224)), (58230, (58233,)), (58250, (58252, 58253, 58254))):
        for name in names:
            relative = f'ExportedProject/Assets/Prefabs/EcEntity/Fight/FightSkill/{entity}/playable_{name}_Mastercast.prefab'
            path = native_root/'native-export-all'/relative
            record = identity(path)
            assert record['sha256'] == hashes[relative]
            assert 'm_Script: {fileID: 795837747,' in path.read_text(encoding='utf-8-sig')
            uses.append(record)
    write(RECEIPT, (json.dumps(dict(manifest=manifest['manifest'], code_extraction=identity(CODE.parent/'code-manifest.json'),
        assembly=identity(assembly), original=identity(source), original_mono_script_file_id=795837747,
        original_method_bodies=before, adapted_method_bodies=after,
        outputs=[identity(target), identity(Path(str(target)+'.meta'))], generator=identity(Path(__file__)),
        reused_dependencies=dependencies,
        native_export_receipt=identity(native_receipt), native_use_sites=uses,
        validation=identity(ROOT/'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.StatusMaterials.cs'),
        adaptation='Namespace and C# 9 namespace braces only. Preserve CustomApplyObj, ApplyTargetOfNullAttr, Init and Play selection and binding side effects.',
        limits=['The seven original master-cast graphs still require their original skeleton animation and track dependencies. Actual native material graphs are used as modified fixture templates only.']),
        ensure_ascii=False, indent=2)+'\n').encode())
    print('Original custom target binding: three unchanged methods and eight unchanged dependency methods.')


if __name__ == '__main__':
    build()
