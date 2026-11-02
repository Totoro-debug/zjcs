"""Record actual current skill clips through original playable and native drawing."""
import json
from pathlib import Path
from prepare_battle_scene_appearance_runtime import ROOT, identity, meta, write_changed


def main():
    fixture = ROOT / 'unity/Assets/Game/Editor/Combat/AnimatorPlayback/OfficialBattleAnimatorPlaybackAcceptance.SceneSkillMotions.cs'
    meta(fixture)
    files = [fixture, fixture.with_name(fixture.name + '.meta')]
    for name in ('OfficialBattleAnimatorPlaybackAcceptance.cs',
                 'OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAnimator.cs',
                 'OfficialBattleAnimatorPlaybackAcceptance.ScenePlayerAppearance.cs'):
        path = fixture.parent / name
        files.extend((path, path.with_name(path.name + '.meta')))
    curve = ROOT / 'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeCurve.cs'
    files.extend((curve, curve.with_name(curve.name + '.meta')))
    dependencies = ('scene-motions', 'scene-bound-mix-next', 'scene-player-animator')
    playback = ROOT / 'unity/Assets/Game/Runtime/Presentation/Combat/OfficialBattleAnimatorPlayback.cs'
    original = ROOT / '.tools/source-packages/current/884_156692/official-code/client-source'
    application = []
    for name in ('MyPlayableOverrideAnimator', 'MyPlayableAnimator', 'AnimationController'):
        application.append(dict(original=identity(original / (name + '.cs')),
                                migrated=identity(ROOT / ('unity/Assets/Game/Runtime/Official/StoryAnimation/' + name + '.cs'))))
    proof = dict(manifest='884_156692', application=application, playback=identity(playback),
                 animator_runtime=identity(ROOT / 'assets/reference/battle-rules/animator-runtime-source-manifest.json'),
                 dependencies=[identity(ROOT / ('assets/reference/battle-animation/' + name + '-source-manifest.json')) for name in dependencies],
                 outputs=[identity(path) for path in files], generator=identity(Path(__file__).resolve()),
                 binding='Complete current sex/name clip lookup supplies actual original skill AnimationClips to the existing original override playable. Actual Animator state time reaches the common native pose/drawing interface. All current profession skill names are exercised; representative source skills render complete body/weapon camera frames, and actual MixNext reaches original animated slot alpha tracks. Constant linear source intervals retain their exact value, avoiding a reproduced Unity weighted-curve result of 1.00000012 for the original constant-one leg IK tracks in current 200/skill_14; source keys and IK range validation remain unchanged.',
                 limits=['The fixture uses an explicit graph-routing probe cue, not a migrated current skill prefab, skill selection rule, FX or audio implementation. It does not use hit/idle clips as substitute skills.',
                         'The common body solver remains explicitly before physics. Independent weapon motion still comes from a diagnostic producer; original Game part-animation ignore branches are preserved.',
                         'All original scene clips are catalogued. Real skill pose coverage is limited to current 100/skill_ and 200/skill_ motions; all other states, mixing domains, culling/update scheduling and production battle/Windows Player integration still require validation.',
                         'Original source skill alpha keys or null-attachment keys may hide the body at the sampled time. Empty body geometry is accepted only when original reached attachment keys explicitly hide every initially visible body slot and no active render attachment remains. Camera fitting/portrait checks do not establish official battle scale or HUD alignment.'])
    write_changed(ROOT / 'assets/reference/battle-animation/scene-skill-motions-source-manifest.json',
                  json.dumps(proof, ensure_ascii=False, indent=2) + '\n')
    print('Recorded actual current skill clip/original playable/native drawing bindings.')


if __name__ == '__main__':
    main()
