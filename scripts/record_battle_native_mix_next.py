"""Record independent MixNext channels and actual original Animator validation.

Only original game data and public enum constants are inspected. No animation
SDK implementation, DLL methods or npm runtime are read or copied.
"""
import json
from pathlib import Path
from official_metadata import Metadata
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed


def main():
    hierarchy = ROOT / 'unity/Assets/Official/Combat/SceneCharacterSources/PlayerModel_Game-source.json'
    prefab = json.loads(hierarchy.read_bytes())
    component = next(component for node in prefab['nodes'] for component in node['components']
                     if component['type'] == 'Spine.Unity.SkeletonMecanim')
    translator = component['fields']['translator']
    assert translator == dict(autoReset=1, useCustomMixMode=1, layerMixModes=[1], layerBlendModes=[1])
    metadata = ROOT / '.tools/source-packages/current/884_156692/official-code/global-metadata.dat'
    parsed = Metadata(metadata.read_bytes())
    constants = {}
    for row in parsed.types:
        if row['enum'] and row['name'] in ('MixMode', 'MixBlend'):
            values = {field['name']: parsed.default_int(field['index'])
                      for field in row['fields'] if field['name'] != 'value__'}
            constants[row['name']] = values
    assert constants['MixMode']['MixNext'] == 1 and constants['MixBlend']['First'] == 1
    names = ['ProfessionNativeAnimatorPose', 'ProfessionNativeAnimationChannels', 'ProfessionNativeBonePose',
             'ProfessionNativeSlotPose', 'ProfessionNativeAppearanceSkeleton', 'ProfessionNativeConstraintAnimation',
             'ProfessionNativeConstraintPose']
    files = [ROOT / ('unity/Assets/Game/Runtime/Offline/ProfessionUI/' + name + '.cs') for name in names]
    fixture = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneMixNext.cs'
    meta(fixture)
    files.extend((fixture, fixture.with_name(fixture.name + '.meta')))
    proof = dict(manifest='884_156692', inputs=[identity(hierarchy), identity(metadata), identity(ROOT / 'unity/Assets/Game/Editor/Zjcs.Editor.asmdef')],
                 dependencies=[identity(ROOT / ('assets/reference/battle-animation/' + name + '-source-manifest.json'))
                               for name in ('scene-core', 'game-animators', 'native-animator-pose', 'scene-mecanim-parts')],
                 translator=translator, public_enum_constants=constants,
                 outputs=[identity(path) for path in files], generator=identity(Path(__file__).resolve()),
                 documentation=['https://en.esotericsoftware.com/spine-unity-main-components-4.2#Animation-Blending-Control',
                                'https://esotericsoftware.com/spine-api-reference-4.2#MixBlend',
                                'https://en.esotericsoftware.com/forum/d/16410-about-negative-bone-scale-web-player'],
                 binding='Explicit two-motion Animator MixNext snapshot and solver; retain each original motion clock and actual incoming clip weight. Reset the outgoing local pose, replace only reached incoming keyed properties, switch discrete keys, mix reflected scale magnitudes using the incoming sign, then solve mixed IK/transform constraints once. No solved world-matrix interpolation or independent clock.',
                 limits=['EvaluateBeforePhysics and EvaluateMixNextBeforePhysics remain explicit pre-physics stages, with the existing geometric solver restrictions. Full physical motion is not implemented by these methods.',
                         'Only actual single-layer, single-outgoing/single-incoming complementary clip weights are accepted. Blend trees, other mix modes/layers and interrupted-transition parity remain unverified.',
                         'The actual current eight-character fixture verifies idle/hit local channels, attachment/draw order, clocks, weights, repeatability and rendered body pixels. Animated colors/deforms/inheritance and additional constraint branches require further original-input validation.',
                         'The scene drawing endpoint still explicitly consumes the unmixed path; transition notifications, other native poses and production battle/Windows Player integration remain pending.',
                         'Previous receipts retain their historical extraction/implementation identities. This receipt records the current common channel and solver extensions without changing original payloads.'])
    write_changed(ROOT / 'assets/reference/battle-animation/native-mix-next-source-manifest.json', json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recorded original prefab MixNext configuration, public enum constants and independent channel/Animator bindings.')


if __name__ == '__main__':
    main()
