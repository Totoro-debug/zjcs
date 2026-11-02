"""Read the selected original keys used by the complete partner panel."""
import hashlib
import re

from official_apk import ROOT
from npc_travel_language import language


def panel_language(prefab):
    keys = {line.strip().strip("'\"") for line in re.findall(r'(?m)^  myKey: (.*)$', prefab.read_text('utf8'))}
    keys = {key for key in keys if key and not key.startswith('{')}
    keys.update(('ui_npc_panel_addressbook', 'ui_npc_panel_dialogue', 'ui_npc_star_god_title'))
    texts, inputs = language(keys)
    if keys != texts.keys(): raise ValueError('Original panel language missing: ' + repr(keys - texts.keys()))
    inputs[prefab.relative_to(ROOT).as_posix()] = hashlib.sha256(prefab.read_bytes()).hexdigest()
    return [dict(key=key, text=texts[key]) for key in sorted(texts)], inputs
