"""Prepare actual Resources/profile bindings for original daily-limit checks."""
from pathlib import Path
import json
import shutil

from prepare_partner_conversation_binding_validation import main as prepare_binding
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    prepare_binding()
    editor = validation_directory() / 'unity/Assets/Editor'
    (editor / 'PartnerConversationBindingValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_conversation_day_limit_validation.cs',
        editor / 'PartnerConversationDayLimitValidation.cs')
    for source, target in (
        ('partner_gift_transaction_validation.cs', 'PartnerGiftTransactionValidation.cs'),
        ('partner_roster_transaction_validation.cs', 'PartnerRosterTransactionValidation.cs')):
        shutil.copy2(ROOT / 'scripts/fixtures' / source, editor / target)
    definition = editor / 'Validation.asmdef'
    value = json.loads(definition.read_bytes())
    value['references'].append('Zjcs.Offline.Equipment')
    definition.write_text(json.dumps(value), 'utf8', newline='\n')
    print('Prepared original day-limit rules with actual Resources and disk profile', flush=True)


if __name__ == '__main__': main()
