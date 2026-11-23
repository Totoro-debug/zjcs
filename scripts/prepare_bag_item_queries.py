"""Port the complete original Bag item-dot eligibility/count query closure."""
import hashlib
import json
from pathlib import Path
import re

from official_apk import ROOT
from official_source import OfficialSource
from prepare_home_entity_bindings import block_end


DEST = ROOT / 'unity/Assets/Game/Runtime/Official/BagItemQueries'
RECEIPT = ROOT / 'assets/reference/bag-item-queries-source-manifest.json'
TEMPLATE = ROOT / 'scripts/templates/bag_item_queries/BagItemQueryServices.cs'
METHODS = ('ShowBagCanUseItemRedDot', 'GetCanUseBlueDotCount', 'CanMergeEquipPiece',
    'CanMergeItemPiece', 'CanMergePetPiece', 'CanMergeRandomTreasurePiece',
    'CanUsePeriodCardItem', 'CanUseCookingFormula', 'CanUseEquipReplaceItem', 'GetItemRedDotType')


def sha(raw):
    return hashlib.sha256(raw).hexdigest()


def member(text, pattern):
    match = re.search(pattern, text, re.M)
    if not match:
        raise ValueError('Verify the selected original query member: ' + pattern)
    start = match.start()
    end = block_end(text, text.index('{', match.end())) + 1
    return text[start:end]


def prepare():
    source = OfficialSource()
    code = ROOT / '.tools/source-packages/current' / source.version / 'official-code'
    code_receipt = code.parent / 'code-manifest.json'
    identity = json.loads(code_receipt.read_bytes())
    if identity['manifest'] != source.version:
        raise ValueError('The selected query assembly/source input differs')
    inputs = {code_receipt.relative_to(ROOT).as_posix(): sha(code_receipt.read_bytes())}
    assembly = code / 'managed/decoded/Console.dll'
    if sha(assembly.read_bytes()) != identity['assemblies']['Console.dll']['decoded_sha256']:
        raise ValueError('The selected common rules assembly differs')
    inputs[assembly.relative_to(ROOT).as_posix()] = sha(assembly.read_bytes())
    previous = json.loads(RECEIPT.read_bytes())['outputs'] if RECEIPT.is_file() else {}
    outputs = {}

    def write(path, raw):
        name = path.relative_to(ROOT).as_posix()
        if path.exists() and path.read_bytes() != raw and sha(path.read_bytes()) != previous.get(name):
            raise ValueError('Preserve another Bag query change: ' + name)
        path.parent.mkdir(parents=True, exist_ok=True)
        if not path.is_file() or path.read_bytes() != raw:
            path.write_bytes(raw)
        outputs[name] = sha(raw)

    def metadata(path, folder=False):
        kind = 'DefaultImporter' if folder else 'AssemblyDefinitionImporter' if path.suffix == '.asmdef' else 'MonoImporter'
        text = 'fileFormatVersion: 2\nguid: ' + hashlib.md5(('zjcs:bag-item-queries:' + path.relative_to(ROOT).as_posix()).encode()).hexdigest() + '\n'
        text += 'folderAsset: yes\n' if folder else ''
        write(Path(str(path) + '.meta'), (text + kind + ':\n  externalObjects: {}\n').encode())

    def read(path):
        inputs[path.relative_to(ROOT).as_posix()] = sha(path.read_bytes())
        return path.read_text('utf-8-sig').replace('\r\n', '\n')

    original = read(code / 'common-source/Common/Player.cs')
    methods, method_proofs = [], {}
    for name in METHODS:
        raw = member(original, r'^\s*public [^\n]+\b' + name + r'\(')
        body = raw
        for old, new in (
            ('Parsers.GameSettings', 'Services.Settings'), ('Parsers.Instance', 'Services.Tables'),
            ('this.HasItem(', 'Services.Player.HasItem('), ('this.FindItemsByType(', 'Services.Player.FindItemsByType('),
            ('PetManager.HasPet(', 'Services.Player.HasPet('), ('PayItemManager.TryGetBuyCount(', 'Services.Player.TryGetBuyCount('),
            ('TimeManager.Instance.GetDateTime()', 'Services.GetDateTime()'), ('config.IsLimit(this)', 'Services.IsLimit(config)')):
            body = body.replace(old, new)
        if any(token in body for token in ('Parsers.', 'TimeManager', 'PetManager', 'PayItemManager', 'this.HasItem(', 'config.IsLimit(this)')):
            raise ValueError('Unbound query dependency: ' + name)
        body = '\n'.join(line.rstrip() for line in body.splitlines()).strip()
        methods.append(body)
        method_proofs[name] = dict(original_sha256=sha(raw.encode()), migrated_sha256=sha(body.encode()))
    header = '// Original Bag item eligibility/count queries; only explicit owner boundaries differ.\nusing System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing Zjcs.Official.BagTipDots;\nnamespace Zjcs.Official.BagItemQueries\n{\npublic sealed class BagItemQueryRules\n{\n    readonly IBagItemQueryServices Services;\n    public BagItemQueryRules(IBagItemQueryServices services) => Services = services ?? throw new ArgumentNullException(nameof(services));\n'
    payload = header + '\n\n'.join(methods) + '\n}\n}\n'
    metadata(DEST, True)
    for name, raw in [('BagItemQueryRules.cs', payload.encode()), ('BagItemQueryServices.cs', read(TEMPLATE).encode())]:
        path = DEST / name; write(path, raw); metadata(path)
    for name in ('ItemType', 'RedDotType'):
        origin = member(read(code / 'common-source/Common' / (name + '.cs')), r'^public enum ' + name + r'\b')
        installed = member(read(ROOT / 'unity/Assets/Game/Runtime/Official/BagTipDots' / (name + '.cs')), r'^public enum ' + name + r'\b')
        if re.sub(r'\s+', '', origin) != re.sub(r'\s+', '', installed):
            raise ValueError('Verify the complete shared original enum before reusing it: ' + name)
    pay = member(read(code / 'common-source/Common/PayType.cs'), r'^public enum PayType\b')
    pay_path = DEST / 'PayType.cs'
    write(pay_path, ('// Selected original payment type identities.\nusing System.ComponentModel;\nnamespace Zjcs.Official.BagItemQueries\n{\n' + pay + '\n}\n').encode()); metadata(pay_path)
    definition = DEST / 'Zjcs.Official.BagItemQueries.asmdef'
    write(definition, (json.dumps(dict(name='Zjcs.Official.BagItemQueries', references=['Zjcs.Official.BagTipDots']), indent=2) + '\n').encode()); metadata(definition)
    inputs[Path(__file__).relative_to(ROOT).as_posix()] = sha(Path(__file__).read_bytes())
    validations = [ROOT / path for path in ('scripts/fixtures/bag_item_queries_validation.cs', 'scripts/test_bag_item_queries.ps1')]
    RECEIPT.write_text(json.dumps(dict(manifest=source.version, inputs=inputs, outputs=outputs, methods=method_proofs,
        validation_inputs={p.relative_to(ROOT).as_posix(): sha(p.read_bytes()) for p in validations if p.is_file()},
        adaptation='Complete original Player item-dot query closure. Read-only projections and explicit table/player/time/cooking owners replace global services; branch order, thresholds, counts and failure branches remain original.',
        scope='Shared query component. Local inventory owner binding, storage composition and Player gameplay require separate verification.'), ensure_ascii=False, indent=2) + '\n', 'utf8', newline='\n')
    print('Prepared complete original Bag item queries:', len(METHODS))


if __name__ == '__main__':
    prepare()
