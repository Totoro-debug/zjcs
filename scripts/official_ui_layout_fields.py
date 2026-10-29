"""Decode pinned TMP/UGUI extension fields omitted by the base prefab reader."""
import hashlib
import math
import struct


def enrich_layout_node(exporter, node, component, cls, raw):
    if cls == 'TextMeshProUGUI':
        length = struct.unpack_from('<i', raw, 88)[0]
        aligned = (92 + length + 3) & ~3
        shared = struct.unpack_from('<i', raw, aligned + 28)[0]
        materials = struct.unpack_from('<i', raw, aligned + 44 + 12 * shared)[0]
        style = aligned + 12 * (shared + materials)
        wrapping = struct.unpack_from('<i', raw, style + 256)[0]
        overflow = struct.unpack_from('<i', raw, style + 264)[0]
        if wrapping not in (0, 1) or overflow not in range(7):
            raise ValueError('Unexpected original TMP layout: ' + node['path'])
        node['text'].update(word_wrap=bool(wrapping), overflow=overflow,
                            margin=list(struct.unpack_from('<4f', raw, style + 352)))
        if raw[style + 204]:
            node['text']['auto_size'] = list(struct.unpack_from('<2f', raw, style + 208))
    elif cls == 'ClampingScrollRect':
        width, height, wmin, wmax, hmin, hmax, move = struct.unpack_from('<ii4fi', raw, len(raw)-28)
        # Pinned MailContentSkillRankChangeContent really serializes 440,420.
        # Preserve its native range instead of silently swapping the values.
        if any(v not in (0, 1) for v in [width, height, move]) or not all(math.isfinite(v) for v in [wmin,wmax,hmin,hmax]):
            raise ValueError('Unexpected original scroll clamp: ' + node['path'])
        node['clamping_scroll'] = dict(width=bool(width), height=bool(height),
            width_range=[wmin, wmax], height_range=[hmin, hmax], clamp_move=bool(move))
    elif cls == 'LoopScrollGridLayoutGroup':
        if len(raw) != 104:
            raise ValueError('Unexpected original loop grid: ' + node['path'])
        flexible, min_x, min_y, minimum, maximum = struct.unpack_from('<i2f2i', raw, 84)
        if flexible not in (0, 1):
            raise ValueError('Invalid original loop grid flag')
        node['grid']['loop'] = dict(flexible=bool(flexible), min_spacing=[min_x, min_y],
                                    minimum=minimum, maximum=maximum)
    else:
        return
    exporter.layout_inputs.append(dict(exporter.apk.provenance(component),
        object_path=node['path'], script=cls, sha256=hashlib.sha256(raw).hexdigest()))
