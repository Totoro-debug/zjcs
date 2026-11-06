"""Isolate the actual partner actor and migrated original Animator playables."""
import argparse
import json
import shutil

from official_apk import ROOT
from prepare_partner_native_battle_validation import main as prepare_actor
from validation_assemblies import stage_runtime_modules
from validation_workspace import validation_directory


def main(extend_existing=False):
    if not extend_existing:
        prepare_actor()
    project = validation_directory() / 'native-battle-unity'
    if not (project.parent / '.zjcs-validation.json').is_file() or not (project / 'Assets/Editor/Validation.asmdef').is_file():
        raise ValueError('Use the owned original partner actor fixture')
    runtime = ROOT / 'unity/Assets/Game/Runtime'
    pending = ['Zjcs.Combat.Official', 'Zjcs.Official.StoryAnimation', 'Zjcs.Official.StoryPlayables']
    selected = stage_runtime_modules(
        project, runtime, pending,
        allowed_external={'Unity.Newtonsoft.Json', 'Unity.RenderPipelines.Universal.Runtime'})
    bridge = project / 'Assets/PartnerAnimatorPlayback'
    bridge.mkdir(exist_ok=True)
    for name in ('OfficialBattleAnimatorPlayback.cs', 'OfficialBattleAnimationTimeline.cs'):
        shutil.copy2(runtime / 'Presentation/Combat' / name, bridge)
    (bridge / 'PartnerAnimatorPlayback.asmdef').write_text(json.dumps(dict(name='PartnerAnimatorPlayback',
        references=['Zjcs.Combat.Official', 'Zjcs.Official.StoryAnimation', 'Zjcs.Official.StoryPlayables'])), 'utf8')
    editor = project / 'Assets/Editor'
    shutil.copy2(ROOT / 'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativePlayableAcceptance.cs', editor)
    definition = editor / 'Validation.asmdef'
    data = json.loads(definition.read_bytes())
    data['references'] = list(dict.fromkeys(data['references'] + ['PartnerAnimatorPlayback', 'Zjcs.Official.StoryAnimation', 'Zjcs.Official.StoryPlayables']))
    definition.write_text(json.dumps(data), 'utf8')
    resources = project / 'Assets/Resources'
    resources.mkdir(exist_ok=True)
    shutil.copy2(validation_directory() / 'partner-native-playables.json', resources)
    print('Prepared actual partner Animator playable validation:', project)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--extend-existing', action='store_true')
    main(parser.parse_args().extend_existing)
