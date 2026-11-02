"""Exercise the saved-dialogue service with actual progression and original contracts."""
import json
from pathlib import Path
import shutil

from prepare_partner_conversation_binding_validation import main as prepare_binding
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    prepare_binding()
    project = validation_directory() / 'unity'
    editor = project / 'Assets/Editor'
    (editor / 'PartnerConversationBindingValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_dialogue_local_ui_validation.cs',
        editor / 'PartnerDialogueLocalUiValidation.cs')
    # Compile the installed service and original contract unchanged. The native
    # avatar/panel dependency closure is deliberately outside this state test.
    folder = project / 'Assets/DialogueService'
    folder.mkdir()
    for relative in ('Offline/PartnersUI/OfflinePartnerDialogueUiServices.cs',
                     'Official/PartnersUI/Roster/PartnerDialogueUiServices.cs',
                     'Official/WorldUI/MovementType.cs'):
        source = ROOT / 'unity/Assets/Game/Runtime' / relative
        for path in (source, Path(str(source) + '.meta')):
            shutil.copy2(path, folder / path.name)
    (folder / 'DialogueService.asmdef').write_text(json.dumps(dict(
        name='PartnerDialogueServiceValidation', references=['Zjcs.Offline.Progression', 'Zjcs.Official.Partners'])),
        'utf8', newline='\n')
    definition = editor / 'Validation.asmdef'
    value = json.loads(definition.read_bytes())
    value['references'].append('PartnerDialogueServiceValidation')
    definition.write_text(json.dumps(value), 'utf8', newline='\n')
    print('Prepared real saved-dialogue service and original UI contract', flush=True)


if __name__ == '__main__': main()
