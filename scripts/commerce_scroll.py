"""Read the pinned UGUI scroll fields and the actual EventSystem drag threshold."""
import struct


KINDS = {'ScrollRect', 'ClampingScrollRect', 'LoopVerticalScrollRect', 'LoopHorizontalScrollRect',
         'LoopVerticalScrollRectMulti', 'LoopHorizontalScrollRectMulti'}


def configuration(e, obj, kind):
    e.input(obj)
    if kind.startswith('Loop'):
        fields = e.schemas.read(obj)
        assert not fields['m_OnValueChanged']['m_PersistentCalls']['m_Calls']
        fields = {k: e.convert(v, obj) for k, v in fields.items() if k.startswith('m_')}
    else:
        raw = obj.get_raw_data()
        name = obj.read(check_read=False).m_Name
        at = (32 + len(name.encode()) + 3) & ~3
        assert len(raw) >= at + 96
        horizontal, vertical, movement = struct.unpack_from('<3i', raw, at + 12)
        elasticity = struct.unpack_from('<f', raw, at + 24)[0]
        inertia = struct.unpack_from('<i', raw, at + 28)[0]
        deceleration, sensitivity = struct.unpack_from('<2f', raw, at + 32)
        assert horizontal in [0, 1] and vertical in [0, 1] and inertia in [0, 1]
        assert movement in range(3) and elasticity > 0 and 0 <= deceleration <= 1
        assert struct.unpack_from('<i', raw, at + 92)[0] == 0, 'ScrollRect has persistent value callbacks'
        def pointer(offset):
            file_id, path_id = struct.unpack_from('<iq', raw, at + offset)
            return e.convert(dict(m_FileID=file_id, m_PathID=path_id), obj)
        fields = dict(m_Content=pointer(0), m_Horizontal=horizontal, m_Vertical=vertical,
                      m_MovementType=movement, m_Elasticity=elasticity, m_Inertia=inertia,
                      m_DecelerationRate=deceleration, m_ScrollSensitivity=sensitivity,
                      m_Viewport=pointer(40), m_HorizontalScrollbar=pointer(52), m_VerticalScrollbar=pointer(64))
    assert fields['m_HorizontalScrollbar'] is None and fields['m_VerticalScrollbar'] is None
    return dict(horizontal=bool(fields['m_Horizontal']), vertical=bool(fields['m_Vertical']),
                content=fields['m_Content'], viewport=fields['m_Viewport'],
                sensitivity=fields['m_ScrollSensitivity'], movement=fields['m_MovementType'],
                elasticity=fields['m_Elasticity'], inertia=bool(fields['m_Inertia']),
                deceleration=fields['m_DecelerationRate'], source=e.apk.provenance(obj), kind=kind)


def event_system(e):
    config = next(obj for obj in e.apk.objects('defaultpackage_assets_scriptableconfig_modules_eventsystem.bundle')
                  if obj.type.name == 'MonoBehaviour' and e.mono(obj)[0] == 'EventSystemManagerConfig')
    e.input(config)
    fields = e.schemas.read(config)
    prefab = e.guid_object(fields['EventSystemProxy']['mProxyRes']['mAssetGuid'])
    e.input(prefab)
    component = next(part.component.deref() for part in prefab.read().m_Component
                     if part.component.type.name == 'MonoBehaviour' and e.mono(part.component.deref())[0] == 'EventSystem')
    raw = component.get_raw_data(); e.input(component)
    assert len(raw) == 52 and struct.unpack_from('<i', raw, 28)[0] == 0
    send_navigation, drag_threshold = struct.unpack_from('<2i', raw, 44)
    assert send_navigation in [0, 1] and drag_threshold > 0
    return dict(drag_threshold=drag_threshold, source=e.apk.provenance(component))
