"""Port original conversation creation and node-state mutations without server policy."""
import argparse
import hashlib
import json
from pathlib import Path

from official_apk import ROOT
from prepare_partner_gift_configuration import method
from validation_workspace import validation_directory


def sha(raw): return hashlib.sha256(raw).hexdigest()


def build(work, install=False):
    if not (work / '.zjcs-validation.json').is_file(): raise ValueError('Owned workspace required')
    version = json.loads((ROOT / 'official-source.json').read_bytes())['active']
    inputs, methods = {}, {}

    def source(name):
        relative = 'common-source/Common/' + name + '.cs'
        path = ROOT / '.tools/source-packages/official-code' / relative
        raw = path.read_bytes()
        if raw != (ROOT / '.tools/source-packages/current' / version / 'official-code' / relative).read_bytes():
            raise ValueError('Selected conversation implementation differs: ' + name)
        inputs[path.relative_to(ROOT).as_posix()] = sha(raw)
        return raw.decode('utf-8-sig').replace('\r\n', '\n')

    def selected(name, signature):
        body = method(source(name), signature)
        methods[name + '.' + signature] = sha(body.encode())
        return body

    template = ROOT / 'scripts/templates/partner_dialogue/PartnerConversationRules.cs'
    inputs[template.relative_to(ROOT).as_posix()] = sha(template.read_bytes())
    text = template.read_text('utf8')
    for original, marker, target in [('DialogueStatus', 'STATUS', 'PartnerConversationStatus'),
        ('DialogueType', 'TYPE', 'PartnerConversationType')]:
        enum = source(original).split('namespace Common;\n', 1)[1].strip().replace(original, target)
        text = text.replace('/*' + marker + '*/', enum)
    create = selected('PlayerNpcDialogueDataWrap',
        'public static PlayerNpcDialogueDataWrap Create(long playerId, long npcId, int dialogClassId, int nodeId)')
    create = create.replace('int nodeId)', 'int nodeId, long dialogueId, DateTime dateTime)')
    create = create.replace('CreateNew()', 'new PartnerConversationRecord()')
    create = create.replace('(long)SnowFlakeIdGenerator.NextID()', 'dialogueId')
    create = create.replace('TimeManager.Instance.GetDateTime()', 'dateTime')
    advance = selected('PlayerNpcDialogueDataWrap', 'public void SetCurrentNodeAndUpdateStatus(int nodeId, Player player)')
    advance = advance.replace('Player player', 'IPartnerConversationPlayer player')
    advance = advance.replace('NpcDialogueInfoParser npcDialogueInfoParser = Parsers.Instance.NpcDialogueInfoParser;',
        'IPartnerConversationConfiguration npcDialogueInfoParser = player.DialogueConfiguration;')
    advance = advance.replace('TimeManager.Instance.GetDateTime()', 'player.Now')
    for marker, body in [('CREATE', create), ('ADVANCE', advance)]:
        body = body.replace('PlayerNpcDialogueDataWrap', 'PartnerConversationRecord')
        body = body.replace('DialogueStatus.', 'PartnerConversationStatus.').replace('DialogueStatus dialogueStatus', 'PartnerConversationStatus dialogueStatus')
        body = body.replace('NpcDialogueInfo npcDialogueInfo', 'PartnerConversationNode npcDialogueInfo')
        text = text.replace('/*' + marker + '*/', body)
    if any(token in text for token in ('/*', 'Parsers.', 'TimeManager.', 'SnowFlake', 'CreateNew')):
        raise ValueError('Unadapted original conversation boundary')
    output = (text.rstrip() + '\n').expandtabs(4).encode('utf8')
    target = ROOT / 'unity/Assets/Game/Runtime/Official/Partners/PartnerConversationRules.cs'
    meta = ('fileFormatVersion: 2\nguid: ' + hashlib.md5(b'zjcs-partner-conversation-rules').hexdigest() + '\n').encode()
    receipt = dict(manifest=version, input_sha256=inputs, original_method_sha256=methods,
        generator_sha256=sha(Path(__file__).read_bytes()),
        output_sha256={target.relative_to(ROOT).as_posix(): sha(output),
            target.relative_to(ROOT).as_posix() + '.meta': sha(meta)},
        adaptations=['Database identity and clock are supplied by the local transaction owner.',
            'Player/configuration and condition evaluation use explicit interfaces.',
            'Retain the original existing-status branch and update notification order.',
            'Conversation triggering, daily scheduling and rewards are not supplied by these two original methods.'])
    (work / target.name).write_bytes(output)
    proof = work / 'partner-conversation-rules-source-manifest.json'
    proof.write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    if install:
        previous_path = ROOT / 'assets/reference' / proof.name
        previous = json.loads(previous_path.read_bytes()) if previous_path.exists() else {}
        for path, raw in ((target, output), (Path(str(target) + '.meta'), meta)):
            if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get('output_sha256', {}).get(path.relative_to(ROOT).as_posix()):
                raise ValueError('Preserve changed conversation output: ' + str(path))
            path.write_bytes(raw)
        (ROOT / 'assets/reference' / proof.name).write_bytes(proof.read_bytes())
    print('Prepared original conversation creation and node-state rules', flush=True)
    return receipt


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--install', action='store_true')
    build(validation_directory(), parser.parse_args().install)
