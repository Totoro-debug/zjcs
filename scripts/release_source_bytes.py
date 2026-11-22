"""Recover release text bytes only when they exactly match their source receipts."""
import argparse
import hashlib
import json
import os
from pathlib import Path


def restore(path, expected_hash, expected_size=None):
    raw = path.read_bytes()

    def matches(value):
        return ((expected_size is None or len(value) == expected_size)
                and hashlib.sha256(value).hexdigest() == expected_hash)

    if matches(raw):
        return False
    lf = raw.replace(b'\r\n', b'\n')
    for candidate in (lf, lf.replace(b'\n', b'\r\n')):
        if matches(candidate):
            path.write_bytes(candidate)
            return True
    raise ValueError('Source bytes cannot be recovered to the verified receipt: ' + str(path))


def prepare(source):
    source = Path(source).resolve()
    project = source / 'unity'
    expected = []

    def add(relative, digest, length=None):
        path = project / relative
        if not path.resolve().is_relative_to(project):
            raise ValueError('Release receipt refers outside the Unity project: ' + relative)
        expected.append((path, digest, length))

    skills = json.loads((project / 'Assets/Official/Skills/source-manifest.json').read_bytes())
    gradient, = [row for row in skills['files']
                 if row['path'] == 'Assets/Official/Skills/skill-quality-gradient.json']
    add(gradient['path'], gradient['sha256'], gradient['bytes'])
    equipment = json.loads((project / 'Assets/Official/Equipment/Runtime/source-manifest.json').read_bytes())['output']
    add('Assets/Official/Equipment/Runtime/equipment-runtime-rules.json', equipment['sha256'], equipment['bytes'])
    ui = json.loads((source / 'assets/reference/skill-ui-native-data-source-manifest.json').read_bytes())
    for row in ui['inputs']:
        add(row['path'], row['sha256'], row['bytes'])
        add(row['path'] + '.meta', row['meta_sha256'])
    changed = sum(restore(*record) for record in expected)
    receipt = source.parent / 'committed-source-snapshot.json'
    if receipt.exists():
        state = json.loads(receipt.read_bytes())
        for path, _, _ in expected:
            name = path.relative_to(source).as_posix()
            record = state['files'][name]
            stat = path.stat()
            record.update(size=stat.st_size, mtime_ns=stat.st_mtime_ns)
        temporary = receipt.with_suffix('.json.tmp')
        temporary.write_text(json.dumps(state, separators=(',', ':')) + '\n', 'utf8')
        os.replace(temporary, receipt)
    print('Verified release source bytes; restored text inputs:', changed, flush=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True)
    prepare(parser.parse_args().source)
