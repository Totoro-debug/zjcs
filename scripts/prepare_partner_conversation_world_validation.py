"""Prepare actual world bindings, profile state and conversation condition checks."""
import hashlib
import json
from pathlib import Path
import shutil

from prepare_partner_conversation_binding_validation import main as prepare_binding
from validation_workspace import validation_directory

ROOT = Path(__file__).resolve().parents[1]


def main():
    prepare_binding()
    work = validation_directory()
    project = work / 'unity'
    editor = project / 'Assets/Editor'
    (editor / 'PartnerConversationBindingValidation.cs').unlink()
    shutil.copy2(ROOT / 'scripts/fixtures/partner_conversation_world_validation.cs',
        editor / 'PartnerConversationWorldValidation.cs')
    receipt = json.loads((ROOT / 'assets/reference/partner-conversation-world-source-manifest.json').read_bytes())
    for relative, expected in receipt['installed_output_sha256'].items():
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Changed world binding: ' + relative)
        target = project / ('Assets/' + source.relative_to(ROOT / 'unity/Assets/Game/Runtime').as_posix()
            if relative.endswith(('.cs', '.cs.meta')) else source.relative_to(ROOT / 'unity').as_posix())
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    print('Prepared actual conversation world Resources and profile modules', flush=True)


if __name__ == '__main__': main()
