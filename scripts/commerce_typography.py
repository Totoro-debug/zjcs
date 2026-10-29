"""Pinned native TMP inputs; see recovered original methods, not TMProWarpEx."""
import struct
from UnityPy.classes.PPtr import PPtr

ANIMATION_FIELDS = ['outlineWidth', 'shadowDis', 'underlay', 'RealtimeUpdate']
ANIMATION_FIELDS += [name + '.' + axis for name in ['outlineColor', 'underlayColor'] for axis in 'rgba']


def warp(e, obj, raw):
    assert len(raw) == 120 and struct.unpack_from('<i', raw, 44)[0] == 2
    keys = []
    for at in [48, 76]:
        t, value, incoming, outgoing, weighted, wi, wo = struct.unpack_from('<4fi2f', raw, at)
        assert weighted == 0
        keys.append([t, value, incoming, outgoing])
    pre, post, rotation = struct.unpack_from('<3i', raw, 104)
    assert (pre, post, rotation) == (2, 2, 4)
    e.input(obj)
    return dict(keys=keys, y_scale=struct.unpack_from('<f', raw, 116)[0],
                # TMProWarp.Awake changes both serialized wrap modes to Once.
                pre_wrap=1, post_wrap=1)


def chinese_font(e):
    bundle = 'textmesh_pro_assets_fonts_&_materials'
    obj = e.apk.asset(bundle, '/FZY4K SDF - dynamic.asset')
    raw = obj.get_raw_data()
    # Fixed v854 font prefix/tail; preserve checks rather than assume all TMP
    # assets have these offsets. Dynamic atlas is generated from the real TTF.
    assert len(raw) == 1012 and raw[124:136] == b'FZCuYuan-M03'
    point = struct.unpack_from('<i', raw, 148)[0]
    width, height, padding, render = struct.unpack_from('<4i', raw, 576)
    assert (point, width, height, padding, render) == (40, 2048, 2048, 4, 4165)
    italic, tab = struct.unpack_from('<2i', raw, 1004)
    assert (italic, tab) == (15, 10)
    file_id, path_id = struct.unpack_from('<iq', raw, 56)
    material = PPtr(m_FileID=file_id, m_PathID=path_id, assetsfile=obj.assets_file).deref()
    shader = material.read().m_Shader.deref()
    e.input(obj); e.input(material); e.input(shader)
    config = e.apk.asset(bundle, '/MultiLanguageFontConfig.json'); e.input(config)
    cap_line=struct.unpack_from('<f',raw,164)[0]
    assert 0<cap_line<point
    return dict(point_size=point, atlas_padding=padding, italic_style=italic, cap_line=cap_line,
                floats=dict(material.read().m_SavedProperties.m_Floats),
                colors={k: [v.r, v.g, v.b, v.a] for k, v in material.read().m_SavedProperties.m_Colors},
                native=dict(warp_refresh='0x1121e07c', effect_setup='0x1122f780',
                            effect_material='0x1123083c', shader_ratios='0x1126eb34'))
