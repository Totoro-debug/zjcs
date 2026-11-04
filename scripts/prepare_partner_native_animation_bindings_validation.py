"""Isolate original entity-node AnimationController and its original playable consumers."""
import json
import shutil

from official_apk import ROOT
from prepare_partner_native_playables_validation import main as prepare_playables
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    shutil.copy2(ROOT / 'unity/Assets/Game/Resources/Combat/partner-native-playables.json', work)
    prepare_playables()
    editor = work / 'native-battle-unity/Assets/Editor'
    shutil.copy2(ROOT / 'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeAnimationBindingAcceptance.cs', editor)
    definition = editor / 'Validation.asmdef'
    data = json.loads(definition.read_bytes())
    data['references'] += ['Zjcs.Official.StoryAttachment']
    definition.write_text(json.dumps(data), 'utf8')


if __name__ == '__main__':
    main()
