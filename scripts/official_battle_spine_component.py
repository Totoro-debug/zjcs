"""Pinned Spine.Unity 4.2 serialized renderer fields, in APK metadata order.

Field order verified against metadata-types.json SkeletonRenderer and
SkeletonMecanim, and upstream 4.2 SkeletonRenderer/MecanimTranslator source.
Every Unity primitive field is aligned to four bytes in these components.
"""
import struct


def player_renderers(apk, context=None):
    result = {}
    for obj in apk.objects('defaultpackage_assets_prefabs_appearance_player.bundle'):
        if obj.type.name != 'MonoBehaviour':
            continue
        base = obj.read(check_read=False)
        if base.m_Script.read().m_ClassName != 'SkeletonMecanim':
            continue
        renderer = dict(read_component(obj.get_raw_data(), 'SkeletonMecanim'), source=apk.provenance(obj))
        if context is not None:
            for ref in base.m_GameObject.read().m_Component:
                component = ref.component.deref()
                if component.type.name != 'MonoBehaviour' or component.read(check_read=False).m_Script.read().m_ClassName != 'AppearanceRendererHsvOffset':continue
                cfg = context.schemas.read(component);context.input(component)
                renderer['hsv_offset'] = dict(hue=float(cfg.get('Hue',0)),
                    saturation=float(cfg.get('Saturation',0.2)),value=float(cfg.get('Value',0.2)))
                break
        result[base.m_GameObject.read().m_Name] = renderer
    return result


def read_component(raw, kind):
    offset = 32

    def integer():
        nonlocal offset
        value = struct.unpack_from('<i', raw, offset)[0]
        offset += 4
        return value

    def number():
        nonlocal offset
        value = struct.unpack_from('<f', raw, offset)[0]
        offset += 4
        return value

    def boolean():
        value = integer()
        if value not in [0, 1]:
            raise ValueError('Invalid aligned Spine component boolean')
        return bool(value)

    def pointer():
        nonlocal offset
        file_id, path_id = struct.unpack_from('<iq', raw, offset)
        offset += 12
        return dict(m_FileID=file_id, m_PathID=path_id)

    def string():
        nonlocal offset
        length = integer()
        value = raw[offset:offset+length].decode('utf8')
        offset = (offset + length + 3) & ~3
        return value

    def array(read):
        count = integer()
        if count < 0 or count > 10000:
            raise ValueError('Invalid original Spine component array length')
        return [read() for _ in range(count)]

    result = dict(skeletonDataAsset=pointer(), initialSkinName=string(), initialFlipX=boolean(), initialFlipY=boolean(),
                  updateWhenInvisible=integer(), separatorSlotNames=array(string), zSpacing=number())
    for name in ['useClipping', 'immutableTriangles', 'pmaVertexColors', 'clearStateOnDisable', 'tintBlack', 'singleSubmesh', 'fixDrawOrder', 'addNormals', 'calculateTangents']:
        result[name] = boolean()
    result['maskInteraction'] = integer()
    result['maskMaterials'] = {name: array(pointer) for name in ['materialsMaskDisabled', 'materialsInsideMask', 'materialsOutsideMask']}
    result['disableRenderingOnOverride'] = boolean()
    result['physicsPositionInheritanceFactor'] = [number(), number()]
    result['physicsRotationInheritanceFactor'] = number()
    result['physicsMovementRelativeTo'] = pointer()
    if kind == 'SkeletonMecanim':
        result['translator'] = dict(autoReset=boolean(), useCustomMixMode=boolean(), layerMixModes=array(integer), layerBlendModes=array(integer))
        result['updateTiming'] = integer()
    elif kind == 'SkeletonAnimation':
        result['updateTiming'] = integer()
        result['unscaledTime'] = boolean()
        result['animationName'] = string()
        result['loop'] = boolean()
        result['timeScale'] = number()
    else:
        raise ValueError('Unsupported original Spine component: ' + kind)
    if offset != len(raw):
        raise ValueError(f'Unconsumed original Spine component: {kind}: {offset}/{len(raw)}')
    return result
