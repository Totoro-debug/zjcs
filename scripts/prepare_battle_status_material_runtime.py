"""Migrate the current original material effect and reference-counted restoration."""
import json
import re
import struct
from pathlib import Path
from hashlib import sha256
from Crypto.Hash import MD4

from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, identity, write
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

OUTPUT = ROOT/'unity/Assets/Game/Runtime/Official/BattleMaterialPlayables'
RECEIPT = ROOT/'assets/reference/battle-animation/status-material-runtime-source-manifest.json'
NAMES = ('MeshMaterialProperty', 'MyPlayableMaterialProperty',
         'PlayableMaterialPropertyProvider', 'PlayableMaterialPropertyStencilProvider')


def method_fingerprints(text):
    result = {}
    pattern = r'(?m)^\t((?:public|private|protected)[^\n;=]*\([^;\n]*\)(?: where [^\n]+)?)\n\t\{'
    for match in re.finditer(pattern, text):
        start = text.index('{', match.start()); end, depth = start + 1, 1
        while depth:
            depth += (text[end] == '{') - (text[end] == '}'); end += 1
        body = re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', text[start:end]))
        assert match[1] not in result
        result[match[1]] = sha256(body.encode()).hexdigest()
    return result


def adapt(original):
    text, diagnostics = logging_adapter(original)
    text = re.sub(r'(?m)^using (?:AppearanceSystem(?:\.[\w.]+)*|Common|GameFramework|Framework(?:\.[\w.]+)*);\n', '', text)
    text = text.replace('\t\tGLoggerErrorInterpolatedStringHandler message;\n', '')
    # The logging adapter removes handler temporaries, but these two original
    # methods also use that bool for their ordinary early-return branch.
    for name in ('HasFloat', 'HasColor'):
        anchor = '\tpublic bool '+name+'(string key)\n\t{'
        if anchor in text:
            text = text.replace(anchor, anchor+'\n\t\tbool enabled;', 1)
    text = text.replace('Vector2.op_Implicit(localBounds.size)', '(Vector2)localBounds.size')
    text = text.replace('Object.op_Implicit((Object)(object)mMaterial)', '(bool)mMaterial')
    text = re.sub(r'\.TryGetComponent<([^>]+)>\(ref (\w+)\)', r'.TryGetComponent<\1>(out \2)', text)
    text = re.sub(r'(\w+)\.Deconstruct\(ref (\w+), ref (\w+)\);', r'\2 = \1.Key; \3 = \1.Value;', text)
    assert 'Log.' not in text and 'GLogger' not in text and '.Deconstruct(' not in text
    # Keep attributes attached to the declaration inside the namespace.
    start = re.search(r'(?m)^(?:\[|public (?:abstract )?class)', text).start()
    return ('// Migrated current official application source; see status material runtime receipt.\n'
        'using Object = UnityEngine.Object;\nusing Zjcs.Official.StoryPlayables;\n'
        'using Zjcs.Official.ProfessionUI;\n'
        'using AppearanceRenderer = Zjcs.Official.CombatAppearance.AppearanceRenderer;\n'
        +text[:start]+'namespace Zjcs.Official.BattleMaterialPlayables\n{\n'+text[start:]+'\n}\n'), diagnostics


def build():
    manifest = json.loads((CODE.parent/'code-manifest.json').read_bytes())
    assembly = CODE/'managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    outputs, originals = [], []
    for name in NAMES:
        source = CODE/'client-source'/(name+'.cs')
        original = source.read_text(encoding='utf-8-sig')
        text, diagnostics = adapt(original)
        old_methods, new_methods = method_fingerprints(original), method_fingerprints(text)
        assert old_methods.keys() == new_methods.keys(), name
        if name != 'MeshMaterialProperty':
            assert old_methods == new_methods, name
        target = OUTPUT/(name+'.cs')
        write(target, text.encode()); runtime_meta(target)
        outputs += [target, Path(str(target)+'.meta')]
        originals.append(dict(original=identity(source), migrated=identity(target),
            original_mono_script_file_id=struct.unpack('<i', MD4.new(('s\0\0\0'+name).encode()).digest()[:4])[0],
            diagnostics_adapted=diagnostics,
            original_method_bodies=old_methods, adapted_method_bodies=new_methods,
            adapted_source_sha256=sha256(text.encode()).hexdigest()))
    asmdef = OUTPUT/'Zjcs.Official.BattleMaterialPlayables.asmdef'
    write(asmdef, (json.dumps(dict(name='Zjcs.Official.BattleMaterialPlayables', references=[
        'Zjcs.Official.StoryPlayables', 'Zjcs.Official.ProfessionUI']),indent=2)+'\n').encode())
    runtime_meta(asmdef); runtime_meta(OUTPUT)
    outputs += [asmdef, Path(str(asmdef)+'.meta'), Path(str(OUTPUT)+'.meta')]
    dependencies = [ROOT/path for path in (
        'unity/Assets/Game/Runtime/Official/CombatAppearance/AppearanceRenderer.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/AppearanceRendererLite.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/AppearanceRendererPartHandler.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/AppearanceSkeletonPartEntry.cs',
        'unity/Assets/Game/Runtime/Official/ProfessionUI/IAppearanceSkeletonPart.cs')]
    write(RECEIPT, (json.dumps(dict(manifest=manifest['manifest'],
        code_extraction=identity(CODE.parent/'code-manifest.json'), assembly=identity(assembly),
        originals=originals, generator=identity(Path(__file__)),
        validation=[identity(ROOT/path) for path in (
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.StatusMaterials.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs',
            'unity/Assets/Game/Editor/Combat/AnimatorPlayback/Zjcs.Editor.Combat.AnimatorPlayback.asmdef',
            'scripts/prepare_battle_status_material_assets.py', 'scripts/prepare_battle_status_orbit_assets.py')],
        outputs=[identity(p) for p in outputs], reused_dependencies=[identity(p) for p in dependencies],
        adaptation='Namespace imports bind the existing migrated original full/lite appearance and loaded part collection. Only diagnostics, ILSpy Vector2/Object operator syntax, TryGetComponent out arguments and KeyValuePair deconstruction syntax change. All four original classes, serialized fields, branches, curves, material selection order, per-property reference counts, provider callbacks and restore order are retained.',
        limits=['Formal battle actors, every native material graph and status lifecycle installation are separate integration work.']),
        ensure_ascii=False, indent=2)+'\n').encode())
    print('Original material application classes migrated:', len(originals))


if __name__ == '__main__':
    build()
