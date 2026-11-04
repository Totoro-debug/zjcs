"""Prepare actual native partner actors with original application attachment bindings."""
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
    shutil.copy2(ROOT / 'unity/Assets/Game/Editor/PartnerBattlePlayables/OfficialPartnerNativeAttachmentAcceptance.cs', editor)
    definition = editor / 'Validation.asmdef'
    data = json.loads(definition.read_bytes())
    data['references'] += ['Zjcs.Official.StoryAttachment', 'Zjcs.Official.BattleSpinePlayables']
    definition.write_text(json.dumps(data), 'utf8')


if __name__ == '__main__':
    main()
