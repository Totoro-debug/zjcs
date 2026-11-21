"""Verify current original glossary wording used by a shared combat rule."""
import argparse
import hashlib
import json
import re
import struct
from pathlib import Path

import xxhash
from official_apk import Apk, ROOT


def identity(path):
    return dict(path=path.relative_to(ROOT).as_posix(), sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def main():
    parser = argparse.ArgumentParser(__doc__)
    parser.add_argument('--entry', type=int, required=True)
    parser.add_argument('--action-name', required=True)
    parser.add_argument('--write', action='store_true')
    args = parser.parse_args()
    keys = {xxhash.xxh64_intdigest(key.encode()): key for key in
            (f'entry_{args.entry}_name', f'entry_{args.entry}_desc')}
    with Apk.current() as apk:
        source = apk.reader.read('WwiseBnk', 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile')
        raw, texts = source.data, {}
        count, = struct.unpack_from('<i', raw)
        start = 4 + count * 12
        for index in range(count):
            key, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
            if key in keys:
                size, = struct.unpack_from('<H', raw, start + offset)
                texts[keys[key]] = raw[start+offset+2:start+offset+2+size].decode('utf8')
        assert len(texts) == len(keys), 'Missing current original entry wording'
        action = ROOT / '.tools/source-packages/current' / apk.version / 'official-code/common-source/Common/ActionType.cs'
        value = re.search(r'\b' + re.escape(args.action_name) + r'\s*=\s*(\d+)', action.read_text('utf-8-sig'))
        assert value, 'Missing original action type'
        result = dict(manifest=apk.version, entry_id=args.entry, texts=texts, action_name=args.action_name,
                      action_type=int(value[1]), action_source=identity(action), language_source=dict(source.provenance),
                      language_sha256=hashlib.sha256(raw).hexdigest(), generator=identity(Path(__file__)))
    path = ROOT / f'assets/reference/battle-rules/entry-{args.entry}-source-manifest.json'
    if args.write:
        path.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', 'utf8')
    else:
        assert json.loads(path.read_bytes()) == result, 'Current original entry source changed'
    print(json.dumps(dict(entry=args.entry, texts=texts, action_type=result['action_type']), ensure_ascii=True))


if __name__ == '__main__':
    main()
