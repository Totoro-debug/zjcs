"""Project-owned, independent atlas/attachment conversion reused from the summon module.

Origin: prepare_battle_summon_models.py at da348660971cbc087ce6a8c1f92d5ffa2cefc7c6.
Only the two pure format-conversion functions are reused; no animation runtime.
"""

def atlas_regions(text):
    header, regions, name = {}, {}, None
    for line in text.strip().splitlines()[1:]:
        if not line.strip(): continue
        if ':' not in line:
            name=line.strip(); regions[name]={}
        else:
            key,value=map(str.strip,line.split(':',1))
            (regions[name] if name else header)[key]=value
    if set(header)-{'size','filter','scale'} or header['filter']!='Linear, Linear':
        raise ValueError('Restore original atlas page settings')
    width,height=map(int,header['size'].split(','))
    if float(header.get('scale',1))<=0: raise ValueError('Invalid source atlas scale')
    for name,region in regions.items():
        if set(region)-{'bounds','offsets','rotate'}: raise ValueError('Restore original atlas region settings')
        x,y,w,h=map(int,region['bounds'].split(','))
        ox,oy,ow,oh=map(int,region.get('offsets',f'0,0,{w},{h}').split(','))
        rotation=int(region.get('rotate',0))
        if rotation not in (0,90,180,270): raise ValueError('Restore atlas rotation')
        regions[name]=dict(x=x,y=y,w=w,h=h,ox=ox,oy=oy,ow=ow,oh=oh,rotation=rotation)
    return width,height,regions


def flatten(skeleton, atlas):
    if set(skeleton) - {'skeleton', 'bones', 'slots', 'skins', 'animations', 'ik'}:
        raise ValueError('Restore additional official summon skeleton features')
    if len(skeleton['skins']) != 1 or skeleton['skins'][0]['name'] != 'default':
        raise ValueError('Restore original summon skin selection')
    width, height, regions = atlas_regions(atlas)
    bones = {b['name']: i for i, b in enumerate(skeleton['bones'])}
    slots = {s['name']: i for i, s in enumerate(skeleton['slots'])}
    constraints = {c['name']: i for i, c in enumerate(skeleton.get('ik', []))}
    result = dict(bones=[], slots=[], constraints=[], attachments=[], animations=[])
    for bone in skeleton['bones']:
        if set(bone) - {'name', 'parent', 'rotation', 'x', 'y', 'length', 'scaleX', 'scaleY', 'inherit'}:
            raise ValueError('Restore original summon bone inheritance or shear')
        if bone.get('inherit','normal') not in ('normal','noRotationOrReflection'):
            raise ValueError('Restore original summon inheritance mode')
        result['bones'].append(dict(name=bone['name'], parent=bones.get(bone.get('parent'), -1),
            x=bone.get('x', 0), y=bone.get('y', 0), rotation=bone.get('rotation', 0),
            scaleX=bone.get('scaleX', 1), scaleY=bone.get('scaleY', 1), length=bone.get('length', 0),
            inherit=bone.get('inherit','normal')))
    for constraint in skeleton.get('ik', []):
        if set(constraint) - {'name', 'order', 'bones', 'target', 'bendPositive', 'mix'}:
            raise ValueError('Restore original summon IK softness/stretch/compress')
        ids = [bones[n] for n in constraint['bones']]
        if len(ids) not in (1, 2): raise ValueError('Restore original summon IK chain length')
        result['constraints'].append(dict(name=constraint['name'], order=constraint.get('order', 0),
            parent=ids[0], child=ids[1] if len(ids) == 2 else -1, target=bones[constraint['target']],
            mix=constraint.get('mix', 1), bend=1 if constraint.get('bendPositive', True) else -1))
    ids = {}
    skins = skeleton['skins'][0]['attachments']
    for slot in skeleton['slots']:
        if set(slot) - {'name', 'bone', 'attachment', 'color', 'blend'}:
            raise ValueError('Restore original summon slot setup')
        result['slots'].append(dict(name=slot['name'], bone=bones[slot['bone']],
            color=slot.get('color', 'ffffffff'), attachment=-1, blend=slot.get('blend', 'normal')))
        for name, attachment in skins.get(slot['name'], {}).items():
            ids[slot['name'], name] = len(result['attachments'])
            kind = attachment.get('type', 'region')
            if kind not in ('region', 'mesh', 'clipping'):
                raise ValueError('Restore original summon attachment: ' + kind)
            if set(attachment)-{'type','path','name','width','height','rotation','x','y','scaleX','scaleY','hull','triangles','uvs','vertices','vertexCount','end'}:
                raise ValueError('Restore additional original summon attachment fields')
            definition = dict(name=name, slot=slots[slot['name']], kind=kind,
                endSlot=slots.get(attachment.get('end'), -1), vertices=[], influences=[], uv=[], triangles=[])
            def point(influences):
                definition['vertices'].append(dict(first=len(definition['influences']), count=len(influences)))
                definition['influences'].extend(influences)
            def influence(bone, x, y, weight=1): return dict(bone=bone, x=x, y=y, weight=weight)
            if kind == 'region':
                import math
                region = regions[attachment.get('path', attachment.get('name', name))]
                left, bottom = -region['ow']/2 + region['ox'], -region['oh']/2 + region['oy']
                right, top = left + region['w'], bottom + region['h']
                r = math.radians(attachment.get('rotation', 0)); c, s = math.cos(r), math.sin(r)
                uvs = []
                for x, y in [(left,bottom), (left,top), (right,top), (right,bottom)]:
                    uvs += [(x + region['ow']/2)/region['ow'], 1 - (y + region['oh']/2)/region['oh']]
                    x *= attachment['width']/region['ow'] * attachment.get('scaleX', 1)
                    y *= attachment['height']/region['oh'] * attachment.get('scaleY', 1)
                    point([influence(bones[slot['bone']], c*x-s*y+attachment.get('x',0), s*x+c*y+attachment.get('y',0))])
                definition['triangles'] = [0,1,2,2,3,0]
            else:
                values = attachment['vertices']; cursor = 0
                count = attachment['vertexCount'] if kind == 'clipping' else len(attachment['uvs'])//2
                for i in range(count):
                    if len(values) == count*2:
                        point([influence(bones[slot['bone']], *values[i*2:i*2+2])]); cursor += 2
                    else:
                        n = int(values[cursor]); cursor += 1; influences = []
                        for _ in range(n):
                            bone, x, y, weight = values[cursor:cursor+4]; cursor += 4
                            if int(bone) != bone or not 0 <= bone < len(bones): raise ValueError('Original summon influence bone differs')
                            influences.append(influence(int(bone), x, y, weight))
                        point(influences)
                if cursor != len(values): raise ValueError('Original summon influence data is not fully consumed')
                uvs = attachment.get('uvs', [])
                definition['triangles'] = attachment.get('triangles', [])
            if kind != 'clipping':
                region = regions[attachment.get('path', attachment.get('name', name))]
                for u, v in zip(uvs[::2], uvs[1::2]):
                    x = u*region['ow'] - region['ox']
                    y = v*region['oh'] - (region['oh']-region['oy']-region['h'])
                    rotation=region['rotation']
                    px,py=({0:(x,y),90:(y,region['w']-x),180:(region['w']-x,region['h']-y),
                            270:(region['h']-y,x)})[rotation]
                    px+=region['x'];py+=region['y']
                    definition['uv'].extend([px/width, 1-py/height])
            result['attachments'].append(definition)
        result['slots'][-1]['attachment'] = ids.get((slot['name'], slot.get('attachment')), -1)
    for name, animation in skeleton['animations'].items():
        if set(animation) - {'bones', 'slots', 'ik', 'drawOrder'}:
            raise ValueError('Restore original summon deform/event timeline')
        anim = dict(name=name, duration=0, tracks=[], attachments=[], drawOrders=[])
        def track(kind, index, axis, keys, field, default=0, component=0):
            frames = []
            for key in keys:
                curve = key.get('curve', [])
                if isinstance(curve, list): curve = curve[component*4:component*4+4]
                elif curve != 'stepped': raise ValueError('Unknown original interpolation')
                frames.append(dict(time=key.get('time',0), value=key.get(field,default),
                    stepped=curve=='stepped', curve=curve if isinstance(curve,list) else []))
            anim['tracks'].append(dict(kind=kind,index=index,axis=axis,keys=frames))
        for bone, channels in animation.get('bones', {}).items():
            for channel, keys in channels.items():
                if channel == 'rotate': track(0,bones[bone],2,keys,'value')
                elif channel in ('translate','scale'):
                    for axis, field in enumerate(('x','y')):
                        track(0 if channel=='translate' else 1,bones[bone],axis,keys,field,1 if channel=='scale' else 0,axis)
                else: raise ValueError('Restore original summon bone timeline: '+channel)
        for slot, channels in animation.get('slots', {}).items():
            for channel, keys in channels.items():
                if channel == 'attachment':
                    anim['attachments'].extend(dict(time=k.get('time',0),slot=slots[slot],
                        attachment=ids.get((slot,k.get('name')),-1)) for k in keys)
                elif channel == 'rgba':
                    for axis in range(4):
                        converted=[dict(k, value=int(k.get('color','ffffffff')[axis*2:axis*2+2],16)/255) for k in keys]
                        track(2,slots[slot],axis,converted,'value',component=axis)
                else: raise ValueError('Restore original summon color channel: '+channel)
        for constraint, keys in animation.get('ik', {}).items():
            track(3,constraints[constraint],0,keys,'mix',1)
            track(3,constraints[constraint],1,[dict(k, bend=1 if k.get('bendPositive',True) else -1,curve='stepped') for k in keys],'bend')
        for key in animation.get('drawOrder', []):
            order = [-1]*len(slots); unchanged = []; cursor = 0
            for offset in key.get('offsets', []):
                index = slots[offset['slot']]
                while cursor < index: unchanged.append(cursor); cursor += 1
                delta=offset['offset']
                # The MIT reader exposes this unsigned varint as uint32; the binary field is
                # a signed 32-bit slot offset. Preserve its two's-complement value.
                if delta>=2**31: delta-=2**32
                destination=cursor+delta
                if not 0<=destination<len(order): raise ValueError('Invalid original signed draw-order offset')
                order[destination] = cursor; cursor += 1
            unchanged.extend(range(cursor,len(slots)))
            for index in range(len(order)-1,-1,-1):
                if order[index] == -1: order[index] = unchanged.pop()
            if sorted(order) != list(range(len(slots))): raise ValueError('Original draw order is not a permutation')
            anim['drawOrders'].append(dict(time=key.get('time',0),slots=order))
        anim['duration'] = max([0]+[k['time'] for t in anim['tracks'] for k in t['keys']]
            +[k['time'] for k in anim['attachments']+anim['drawOrders']])
        result['animations'].append(anim)
    return result
