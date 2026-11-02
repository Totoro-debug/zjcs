"""Prepare actual progression assemblies and original conversation Resources."""
import hashlib
import json
from pathlib import Path
import shutil

from prepare_partner_gift_validation import ROOT, prepare
from validation_workspace import validation_directory


def main():
    work = validation_directory()
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('Owned workspace required')
    project = work / 'unity'
    if not project.exists(): prepare(work)
    editor = project / 'Assets/Editor'
    for name in ('PartnerGiftTransactionValidation.cs', 'PartnerRosterTransactionValidation.cs'):
        (editor / name).unlink(missing_ok=True)
    fixture = ROOT / 'scripts/fixtures/partner_conversation_binding_validation.cs'
    shutil.copy2(fixture, editor / 'PartnerConversationBindingValidation.cs')
    receipt = json.loads((ROOT / 'assets/reference/partner-dialogue-rules-source-manifest.json').read_bytes())
    for relative, expected in receipt['installed_output_sha256'].items():
        source = ROOT / relative
        if hashlib.sha256(source.read_bytes()).hexdigest() != expected: raise ValueError('Changed conversation binding: ' + relative)
        if relative.endswith(('.cs', '.cs.meta')):
            target = project / 'Assets' / source.relative_to(ROOT / 'unity/Assets/Game/Runtime')
        else: target = project / source.relative_to(ROOT / 'unity')
        target.parent.mkdir(parents=True, exist_ok=True); shutil.copy2(source, target)
    print('Prepared actual conversation Resources and progression assemblies', flush=True)


if __name__ == '__main__': main()
