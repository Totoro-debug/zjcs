"""Pinned UGUI Selectable and compiled AnimatorController state bindings."""
import struct
from UnityPy.classes.PPtr import PPtr

def selectable(e,obj,raw):
    assert len(raw)>=312
    transition=struct.unpack_from('<i',raw,88)[0];assert transition in range(4)
    colors=[list(struct.unpack_from('<4f',raw,92+16*i)) for i in range(5)]
    multiplier,fade=struct.unpack_from('<2f',raw,172)
    def pointer(at):
        file,id=struct.unpack_from('<iq',raw,at)
        return e.convert(dict(m_FileID=file,m_PathID=id),obj)
    sprites=[pointer(180+12*i) for i in range(4)]
    p=228;triggers=[]
    for _ in range(5):
        count=struct.unpack_from('<i',raw,p)[0];assert 0<=count<=128
        triggers.append(raw[p+4:p+4+count].decode());p=(p+4+count+3)&~3
    assert raw[p] in [0,1]
    e.input(obj)
    return dict(transition=transition,colors=colors,multiplier=multiplier,fade=fade,
        sprites=sprites,triggers=triggers,interactable=bool(raw[p]),graphic=pointer(p+4))

def animator(e,ptr,node):
    obj=ptr.deref();e.input(obj);overrides={}
    while obj.type.name=='AnimatorOverrideController':
        original=obj.read()
        for pair in original.m_Clips:
            if pair.m_OverrideClip.path_id:overrides[pair.m_OriginalClip.path_id]=pair.m_OverrideClip
        obj=original.m_Controller.deref();e.input(obj)
    data=obj.read_typetree();names=dict(data['m_TOS']);clips=[]
    for value in data['m_AnimationClips']:
        p=PPtr(**value,assetsfile=obj.assets_file);p=overrides.get(p.path_id,p)
        if p.path_id:
            e.input(p.deref());clips.append(e.clip(p.deref(),node['path'],node))
        else:clips.append(None)
    machines=[]
    def transition(value):
        row=value['data'];return dict(destination=row['m_DestinationState'],duration=row['m_TransitionDuration'],
            fixed=row['m_HasFixedDuration'],exit_time=row['m_ExitTime'],has_exit=row['m_HasExitTime'],
            conditions=[dict(mode=c['data']['m_ConditionMode'],parameter=names.get(c['data']['m_EventID'],str(c['data']['m_EventID'])),threshold=c['data']['m_EventThreshold']) for c in row['m_ConditionConstantArray']])
    for machine in data['m_Controller']['m_StateMachineArray']:
        machine=machine['data'];states=[]
        for row in machine['m_StateConstantArray']:
            row=row['data'];leaves=[n['data'] for tree in row['m_BlendTreeConstantArray'] for n in tree['data']['m_NodeArray'] if not n['data']['m_ChildIndices']]
            assert row['m_WriteDefaultValues'],data['m_Name']
            # Pinned commerce controllers use a single clip per state, no blend trees.
            assert len(leaves)<=1,(data['m_Name'],names.get(row['m_NameID']),len(leaves))
            id=leaves[0]['m_ClipID'] if leaves else -1
            clip=clips[id] if 0<=id<len(clips) else None
            states.append(dict(name=names.get(row['m_NameID'],str(row['m_NameID'])),clip=clip,speed=row['m_Speed'],
                transitions=[transition(v) for v in row['m_TransitionConstantArray']]))
        machines.append(dict(default=machine['m_DefaultState'],states=states,any=[transition(v) for v in machine['m_AnyStateTransitionConstantArray']]))
    return dict(machines=machines,name=data['m_Name'])
