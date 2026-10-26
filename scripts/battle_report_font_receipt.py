"""Keep committed TMP font payloads distinct from mutable Editor glyph caches."""
import hashlib
from pathlib import Path
import re
import subprocess
import sys


def font_payload_record(path, root):
    raw = path.read_bytes()
    if b'\n  m_GlyphTable:' not in raw or b'\n  m_AtlasWidth:' not in raw:
        return None
    relative = path.relative_to(root).as_posix()
    result = subprocess.run(['git', 'show', 'HEAD:' + relative], cwd=root, stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    if result.returncode:
        # New font assets are recorded from their actual generated payload before their first commit.
        return dict(sha256=hashlib.sha256(raw).hexdigest(), bytes=len(raw))
    committed = result.stdout
    record = dict(sha256=hashlib.sha256(committed).hexdigest(), bytes=len(committed))
    if raw.replace(b'\r\n', b'\n') == committed:
        return record
    for ancestor in [root, *root.parents]:
        dependency = ancestor / '.tools/character-art-python'
        if dependency.is_dir():
            sys.path.insert(0, str(dependency))
            break
    import yaml

    def parse(value):
        text = re.sub(r'(?m)^%.*\n', '', value.decode('utf8'))
        text = re.sub(r'(?m)^--- !u!\d+ &(-?\d+)', r'---\nfile_id: \1', text)
        text = re.sub(r'(?m)^(\s*_typelessdata:) ([0-9a-fA-F]+)$', r'\1 "\2"', text)
        def normalize(item):
            if item is None:
                return ''  # Unity emits either empty or blank string YAML fields on save.
            if isinstance(item, dict):
                return {key: normalize(child) for key, child in item.items()}
            if isinstance(item, list):
                return [normalize(child) for child in item]
            return item
        return {item['file_id']: normalize({key: value for key, value in item.items() if key != 'file_id'})
                for item in yaml.safe_load_all(text)}

    original, current = parse(committed), parse(raw)
    if set(original) != set(current):
        raise ValueError('TMP native object identities changed: ' + relative)
    differences = []
    for identity, document in original.items():
        if set(document) != set(current[identity]):
            raise ValueError('TMP native object types changed: ' + relative)
        kind = next(iter(document))
        before, after = document[kind], current[identity][kind]
        if set(before) != set(after):
            raise ValueError('TMP serialized fields changed: ' + relative)
        allowed = {'_typelessdata'} if kind == 'Texture2D' else {
            'm_GlyphTable', 'm_CharacterTable', 'm_UsedGlyphRects', 'm_FreeGlyphRects'} if kind == 'MonoBehaviour' else set()
        for key in before:
            if before[key] == after[key]:
                continue
            if key not in allowed:
                raise ValueError('TMP source parameter changed outside its dynamic cache: ' + relative + ':' + key)
            differences.append(kind + '.' + key)
        for key, index in [('m_GlyphTable', 'm_Index'), ('m_CharacterTable', 'm_Unicode')]:
            if key in before:
                lookup = {item[index]: item for item in after[key]}
                if not all(lookup.get(item[index]) == item for item in before[key]):
                    raise ValueError('A seeded TMP glyph/character changed: ' + relative + ':' + key)
        if 'm_UsedGlyphRects' in before and not all(item in after['m_UsedGlyphRects'] for item in before['m_UsedGlyphRects']):
            raise ValueError('A seeded TMP atlas rectangle changed: ' + relative)
    record['validation_cache'] = dict(sha256=hashlib.sha256(raw).hexdigest(), changed_fields=sorted(differences),
                                      scope='Editor-generated glyph/atlas cache and serialization whitespace; excluded from the committed source payload hash.')
    return record
