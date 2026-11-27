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


def language(keys, source=None):
    result, sources = {}, {}
    wanted = {xxhash.xxh64_intdigest(key.encode()): key for key in keys}
    names = ['wwisebnk_assets_launch_config_binary_language_zh_cn_text_g_bytes.rawfile',
             'wwisebnk_assets_config_binary_language_zh_cn_text_g_bytes.rawfile']
    if source is not None:
        from official_bundle_reader import OfficialBundleReader
        with OfficialBundleReader(source) as reader:
            payloads = [(name, reader.read('WwiseBnk', name)) for name in names]
        inputs = [(name, item.data, dict(item.provenance)) for name, item in payloads]
    else:
        inputs = [(str(ROOT / '.tools/source-packages/WwiseBnk' / name),
                   (ROOT / '.tools/source-packages/WwiseBnk' / name).read_bytes(), None) for name in names]
    for name, raw, provenance in inputs:
        count = struct.unpack_from('<i', raw)[0]
        start = 4 + count * 12
        for index in range(count):
            key, offset = struct.unpack_from('<Qi', raw, 4 + index * 12)
            if key not in wanted:
                continue
            pos = start + offset
            size = struct.unpack_from('<H', raw, pos)[0]
            result[wanted[key]] = raw[pos + 2:pos + 2 + size].decode('utf8')
        if provenance is None:
            sources[Path(name).relative_to(ROOT).as_posix()] = hashlib.sha256(raw).hexdigest()
        else:
            sources[name] = dict(provenance, sha256=hashlib.sha256(raw).hexdigest())
    return result, sources
