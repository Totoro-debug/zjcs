"""Prepare actual progression assemblies for conversation save Play Mode checks."""
from pathlib import Path
import shutil

from prepare_partner_conversation_binding_validation import main as prepare_binding
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    prepare_binding()
    work = validation_directory()
    editor = work / 'unity/Assets/Editor'
    (editor / 'PartnerConversationBindingValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_conversation_save_validation.cs',
        editor / 'PartnerConversationSaveValidation.cs')
    print('Prepared original conversation state and actual profile persistence', flush=True)


if __name__ == '__main__': main()
