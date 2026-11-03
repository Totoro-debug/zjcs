"""Prepare original conversation/world Resources and real transaction service."""
from pathlib import Path
import shutil
import hashlib
import json

from prepare_partner_conversation_world_validation import main as prepare_world
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    prepare_world()
    work = validation_directory()
    project = work / 'unity'
    receipt = json.loads((ROOT / 'assets/reference/partner-conversation-transactions-source-manifest.json').read_bytes())
    relative = 'unity/Assets/Game/Runtime/Offline/Progression/OfflinePartnerConversationService.cs'
    source = ROOT / relative
    if hashlib.sha256(source.read_bytes()).hexdigest() != receipt['installed_output_sha256'][relative]:
        raise ValueError('Conversation transaction source receipt differs: ' + relative)
    target = project / 'Assets/Offline/Progression/OfflinePartnerConversationService.cs'
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, target)
    shutil.copy2(Path(str(source) + '.meta'), Path(str(target) + '.meta'))

    editor = project / 'Assets/Editor'
    (editor / 'PartnerConversationWorldValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_conversation_transaction_validation.cs',
        editor / 'PartnerConversationTransactionValidation.cs')
    shutil.copy2(ROOT / 'scripts/fixtures/partner_local_actor_identity_validation.cs',
        editor / 'PartnerLocalActorIdentityValidation.cs')
    print('Prepared actual original conversations/world and local reward transactions', flush=True)


if __name__ == '__main__': main()
