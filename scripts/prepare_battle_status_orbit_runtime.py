"""Migrate the original stack-driven status orbit without changing its pool rules."""
import json
import re
from pathlib import Path
from hashlib import sha256

from official_apk import ROOT
from prepare_battle_status_playable_runtime import CODE, OUTPUT, identity, write
from prepare_battle_status_view_runtime import source, wrap, methods
from prepare_unity_story_playable_runtime import logging_adapter
from prepare_unity_story_sound_runtime import runtime_meta

RECEIPT = ROOT / 'assets/reference/battle-animation/status-orbit-runtime-source-manifest.json'


def build():
    original = source('MyPlayableOrbitMovement')
    text, diagnostics = logging_adapter(original)
    assert diagnostics == 1
    text = text.replace('\t\t\t\tIServiceProvider container = ((Component)(object)this).GetSceneContainer();\n', '')
    old = 'ServiceProviderExtensions.Instantiate(container, orbiter, ((Component)this).transform, false)'
    new = 'StoryPlayableInstantiation.Instantiate(((Component)this).transform, orbiter, ((Component)this).transform, false)'
    assert text.count(old) == 1
    text = text.replace(old, new)
    anchor = '\tpublic void SetOrbitLevel(int orbitLevel)'
    assert text.count(anchor) == 1
    text = text.replace(anchor, '''	// Local pool adaptation: a parent sequence can reset children before their
	// deferred stop is processed. Return live orbiters while their old pool index
	// is valid, then restore selection caches for an unchanged-stack replay.
	public override void ResetData()
	{
		if (mOrbiters.Count != 0) StopImpl();
		mOrbitLevel = -1;
		mOrbitInfoIndex = -1;
		mAngleBetweenOrbiters = 0f;
		mTarget = null;
		base.ResetData();
	}

''' + anchor, 1)
    text = wrap(text).replace('using Zjcs.Official.StoryPlayables;',
                             'using Zjcs.Official.StoryPlayables;\nusing Zjcs.Official.StoryAttachment;')
    assert 'GetSceneContainer' not in text and 'ServiceProviderExtensions' not in text
    target = OUTPUT / 'MyPlayableOrbitMovement.cs'
    write(target, text.encode())
    runtime_meta(target)
    fixture = ROOT / 'unity/Assets/Game/Editor/BattleStatusPlayables/OfficialBattleStatusPlayableAcceptance.Orbits.cs'
    runtime_meta(fixture)
    normalize = lambda body: re.sub(r'\s+', '', re.sub(r'//[^\n]*', '', body))
    old_methods, new_methods = methods(original), methods(text)
    fingerprints = {}
    for key, body in old_methods.items():
        if key == 'InitOrbiterPool()':
            body = body.replace('IServiceProvider container = ((Component)(object)this).GetSceneContainer();', '')
            body = body.replace(old, new)
        elif key.startswith('SetOrbitLevel('):
            body, count = logging_adapter(body)
            assert count == 1
        assert normalize(body) == normalize(new_methods[key]), key
        fingerprints[key] = sha256(normalize(body).encode()).hexdigest()
    manifest = json.loads((CODE.parent / 'code-manifest.json').read_bytes())
    assembly = CODE / 'managed/decoded/Assembly-CSharp.dll'
    assert manifest['manifest'] == '884_156692'
    assert identity(assembly)['sha256'] == manifest['assemblies'][assembly.name]['decoded_sha256']
    write(RECEIPT, (json.dumps(dict(manifest='884_156692',
        code_extraction=identity(CODE.parent / 'code-manifest.json'), assembly=identity(assembly),
        original=identity(CODE / 'client-source/MyPlayableOrbitMovement.cs'),
        original_mono_script_file_id=-413707684,
        verified_methods_after_scene_instantiation_and_diagnostics_adaptation=fingerprints,
        offline_pool_reset='Extra ResetData override returns live orbiters through the original StopImpl before clearing private selection caches. Parent-sequence reset can precede deferred child stop. This prevents unchanged-stack reuse from retaining an empty old range and releases original pool objects before discarding their pool index; original 19 methods remain otherwise unchanged.',
        generator=identity(Path(__file__)), outputs=[identity(target), identity(Path(str(target)+'.meta'))],
        validation=[identity(path) for path in (fixture,
            fixture.with_name('OfficialBattleStatusPlayableAcceptance.cs'),
            ROOT / 'scripts/prepare_battle_status_playable_validation.py',
            ROOT / 'scripts/prepare_battle_status_orbit_assets.py')],
        reused_dependencies=[identity(ROOT / path) for path in (
            'assets/reference/battle-animation/status-view-runtime-source-manifest.json',
            'unity/Assets/Game/Runtime/Official/StoryPlayableManager/StoryPlayableInstantiation.cs',
            'unity/Assets/Game/Runtime/Official/StoryAttachment/AttachPosCfg.cs',
            'scripts/prepare_battle_status_view_runtime.py', 'scripts/prepare_unity_story_playable_runtime.py',
            'scripts/prepare_unity_story_sound_runtime.py')],
        adaptation='Original orbit fields, first inclusive level range, shared/distinct orbiter pools, position/LookAt, signed playable time and stop behavior retained. Scene DI instantiation uses the existing explicit scoped factory; framework diagnostics use Unity logging.',
        limits=['Original native orbit graphs and actual status/actor binding require separate asset and runtime integration.']),
        ensure_ascii=False, indent=2) + '\n').encode())
    print('Original status orbit migrated:', len(fingerprints), 'verified methods.')


if __name__ == '__main__':
    build()
