"""Migrate the original status stack transitions and attached effect priorities."""
import json
import re
import struct
from pathlib import Path
from hashlib import sha256
from Crypto.Hash import MD4

from official_apk import ROOT
from prepare_unity_story_sound_runtime import runtime_meta

CODE = ROOT / '.tools/source-packages/current/884_156692/official-code'
OUTPUT = ROOT / 'unity/Assets/Game/Runtime/Official/BattleStatusPlayables'
RECEIPT = ROOT / 'assets/reference/battle-animation/status-playable-runtime-source-manifest.json'
NAMESPACE = 'Zjcs.Official.BattleStatusPlayables'
METHODS = {
    'MyPlayableStackPlay': ('MyPlayableStackPlay', 'Play', 'Stop'),
    'StackCountPlayableMap': ('Match',),
    'ApplyEffectByPriority': ('PlayImpl', 'StopImpl', 'ProcessEffect', 'ProcessOtherPausePlayable',
        'OnInterruptedByHigherPriority', 'ResumeOnOtherHigherPriorityStop', 'PausePlayable'),
}


def method_body(text, name):
    match = re.search(r'(?m)^\s*(?:public|private|protected)[^\n;=]*\b' + name
                      + r'\s*\([^;\n]*\)\s*\{', text)
    assert match, name
    start = text.index('{', match.start())
    depth, end = 1, start + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=sha256(path.read_bytes()).hexdigest())


def write(path, raw):
    path.parent.mkdir(parents=True, exist_ok=True)
    if not path.exists() or path.read_bytes() != raw:
        path.write_bytes(raw)


def build():
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assert manifest['manifest'] == '884_156692'
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    outputs, sources = [], []
    for name, methods in METHODS.items():
        source = CODE / 'client-source' / (name + '.cs')
        original = source.read_text(encoding='utf-8-sig')
        text = re.sub(r'(?m)^using (?:GameFramework|Framework(?:\.[\w.]+)?);\n', '', original)
        # Scene injection replaces only the DI attribute/visibility. The original
        # target identity, priority group, interruptions and resume scan stay intact.
        text = text.replace('[Inject]\n\tprivate IMyPlayableCenter MyPlayableCenter',
                            'public IMyPlayableCenter MyPlayableCenter')
        start = re.search(r'(?m)^(?:\[Serializable\]|public class )', text).start()
        text = ('// Migrated current official application source; see status playable runtime receipt.\n'
            'using Object = UnityEngine.Object;\n'
            'using Zjcs.Official.StoryPlayables;\n'
            'using Zjcs.Official.StoryPlayableManager;\n'
            'using Zjcs.Official.StoryAttachment;\n'
            + text[:start] + 'namespace ' + NAMESPACE + '\n{\n' + text[start:] + '\n}\n')
        equivalence = {}
        for method in methods:
            bodies = [re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', method_body(value, method)))
                      for value in (original, text)]
            assert bodies[0] == bodies[1], (name, method)
            equivalence[method] = sha256(bodies[0].encode()).hexdigest()
        target = OUTPUT / (name + '.cs')
        write(target, text.encode())
        runtime_meta(target)
        outputs += [target, Path(str(target) + '.meta')]
        sources.append(dict(original=identity(source), migrated=identity(target),
                            unchanged_method_bodies=equivalence))
        if name == 'ApplyEffectByPriority':
            file_id = struct.unpack('<i', MD4.new(('s\0\0\0' + name).encode()).digest()[:4])[0]
            assert file_id == 1552490721
            sources[-1]['original_mono_script_file_id'] = file_id
    asmdef = OUTPUT / (NAMESPACE + '.asmdef')
    write(asmdef, (json.dumps(dict(name=NAMESPACE, rootNamespace=NAMESPACE, references=[
        'Zjcs.Official.StoryPlayables', 'Zjcs.Official.StoryPlayableManager',
        'Zjcs.Official.StoryAttachment'], autoReferenced=True), indent=2) + '\n').encode())
    runtime_meta(asmdef)
    runtime_meta(OUTPUT)
    outputs += [asmdef, Path(str(asmdef) + '.meta'), Path(str(OUTPUT) + '.meta')]
    validation = [ROOT / path for path in (
        'unity/Assets/Game/Editor/BattleStatusPlayables/OfficialBattleStatusPlayableAcceptance.cs',
        'unity/Assets/Game/Editor/BattleStatusPlayables/Zjcs.Editor.BattleStatusPlayables.asmdef',
        'scripts/prepare_battle_status_playable_validation.py',
        'scripts/prepare_battle_movement_effect_validation.py')]
    dependencies = [ROOT / path for path in (
        'assets/reference/official-map/map14-story-playable-runtime-source-manifest.json',
        'assets/reference/official-map/map14-story-playable-manager-source-manifest.json',
        'assets/reference/official-map/map14-story-attachment-transform-runtime-source-manifest.json',
        'assets/reference/battle-animation/ground-effects-assets-source-manifest.json',
        'unity/Assets/Game/Runtime/Official/StoryPlayables/MyPlayable.cs',
        'unity/Assets/Game/Runtime/Official/StoryPlayables/MyPlayableCenter.cs',
        'unity/Assets/Game/Runtime/Official/StoryPlayables/ApplyEnableOnPlay.cs',
        'unity/Assets/Game/Runtime/Official/StoryPlayableManager/MyPlayableManager.cs',
        'unity/Assets/Game/Runtime/Official/StoryAttachment/AttachPosCfg.cs')]
    write(RECEIPT, (json.dumps(dict(manifest='884_156692',
        code_extraction=identity(CODE.parent / 'code-manifest.json'), assemblies=[identity(assembly)],
        sources=sources, generator=identity(Path(__file__)), outputs=[identity(p) for p in outputs],
        conversion_helpers=[identity(ROOT / 'scripts/prepare_unity_story_sound_runtime.py')],
        validation=[identity(p) for p in validation], reused_dependencies=[identity(p) for p in dependencies],
        adaptation='Original application source. Namespace/import/Object aliases and explicit scene-center injection only; all stack matching, transition ordering, stop behavior, attachment-target priority partitioning, interruption and resume method bodies remain identical.',
        limits=['Status entity lifecycle, complete native status graph assets and actor bindings are separate required migration work.']),
        ensure_ascii=False, indent=2) + '\n').encode())
    print('Original status stack transitions, literal stack ranges and attached effect priorities migrated; method bodies verified.')


if __name__ == '__main__':
    build()
