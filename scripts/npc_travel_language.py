"""Read the original Chinese language entries required by NPC asset converters."""
import hashlib
from pathlib import Path
import struct
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / '.tools/character-art-python'))
import xxhash


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def language(keys):
    result, sources = {}, {}
    wanted = {xxhash.xxh64_intdigest(key.encode()): key for key in keys}
    for name in ['wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
                 'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile']:
        path = ROOT / '.tools/source-packages/WwiseBnk' / name
        raw = path.read_bytes()
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for index in range(count):
            key, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
            if key not in wanted:
                continue
            pos = start + offset
            size = struct.unpack_from('<H', raw, pos)[0]
            result[wanted[key]] = raw[pos + 2:pos + 2 + size].decode('utf8')
        sources[path.relative_to(ROOT).as_posix()] = digest(path)
    return result, sources
