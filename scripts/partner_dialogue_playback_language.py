"""Select original localized playback text and branch labels from current nodes."""
import hashlib
import json
import re

from official_apk import ROOT
from npc_travel_language import language


def playback_language(prefab):
    path = ROOT / 'unity/Assets/Official/Partners/partner-dialogue-rules.json'
    source = json.loads(path.read_bytes())
    if source['manifest'] != '884_156692': raise ValueError('Dialogue language version differs')
    keys = {f"npc_dialogue_{row['ClassId']}_{row['Node']}_text" for row in source['nodes']}
    keys.update(f"npc_dialogue_{row['ClassId']}_{node}_option_text" for row in source['nodes']
        if len(row['NextNodes'] or []) > 1 for node in row['NextNodes'])
    keys.update(line.strip().strip("'\"") for line in re.findall(r'(?m)^  myKey: (.*)$', prefab.read_text('utf8'))
        if line.strip().strip("'\""))
    texts, inputs = language(keys)
    if keys != texts.keys(): raise ValueError('Inspect original missing playback keys: ' + repr(sorted(keys - texts.keys())[:30]))
    inputs[path.relative_to(ROOT).as_posix()] = hashlib.sha256(path.read_bytes()).hexdigest()
    return [dict(key=key, text=texts[key]) for key in sorted(texts)], inputs
