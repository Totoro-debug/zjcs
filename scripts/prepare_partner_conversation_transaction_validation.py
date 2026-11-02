"""Prepare original conversation/world Resources and real transaction service."""
from pathlib import Path
import shutil

from prepare_partner_conversation_world_validation import main as prepare_world
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    prepare_world()
    editor = validation_directory() / 'unity/Assets/Editor'
    (editor / 'PartnerConversationWorldValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_conversation_transaction_validation.cs',
        editor / 'PartnerConversationTransactionValidation.cs')
    print('Prepared actual original conversations/world and local reward transactions', flush=True)


if __name__ == '__main__': main()
