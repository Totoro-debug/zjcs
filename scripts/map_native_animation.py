"""Read native bridge Animator clips and their WorldStage loop bindings."""
import zlib
from UnityPy.classes.PPtr import PPtr
from official_animation import mecanim_tracks, PROPERTY_HASHES


def state_animations(exporter,root):
    controllers=[];drivers=[];positions={}
    def visit(obj,parent=''):
        go=obj.read();path=parent+'/'+go.m_Name
        components=[c.component.deref() for c in go.m_Component]
        transform=next(c.read() for c in components if c.type.name=='Transform')
        positions[path]={'x':transform.m_LocalPosition.x,'y':transform.m_LocalPosition.y,'z':transform.m_LocalPosition.z}
        for component in components:
            if component.type.name=='Animator' and component.read().m_Controller.path_id:
                controllers.append(component.read().m_Controller.deref());exporter.input(component)
            elif component.type.name=='MonoBehaviour' and exporter.mono(component)[0]=='ECViewDriver':
                drivers.append((component,exporter.schemas.read(component)))
        return dict(name=go.m_Name,path=path,children=[visit(c.read().m_GameObject.deref(),path)for c in transform.m_Children])
    tree=visit(root)
    if not controllers:return {}
    PROPERTY_HASHES[zlib.crc32(b'UpdownValue')]='UpdownValue'
    clips={}
    for controller in controllers:
        exporter.input(controller)
        for pointer in controller.read().m_AnimationClips:
            obj=pointer.deref();exporter.input(obj)
            tracks,duration,loop,unknown=mecanim_tracks(obj,tree)
            for track in tracks:
                if track['property'].startswith('position.'):
                    track['base']=positions[track['path']][track['property'][-1]]
            if unknown:raise ValueError('Unresolved original bridge animation channels: '+repr(unknown))
            clips[obj.read().m_Name]=dict(tracks=tracks,duration=duration,loop=loop)
    def animator_params(pointer,assets_file,seen=None):
        if not pointer.get('m_PathID'):return []
        obj=PPtr(**pointer,assetsfile=assets_file).deref();seen=seen if seen is not None else set()
        key=(obj.assets_file.name,obj.path_id)
        if key in seen:return []
        seen.add(key);result=[]
        if obj.type.name=='GameObject':
            for component in obj.read().m_Component:
                if component.component.type.name=='MonoBehaviour':
                    result+=animator_params(dict(m_FileID=component.component.m_FileID,m_PathID=component.component.path_id),obj.assets_file,seen)
        elif obj.type.name=='MonoBehaviour':
            kind=exporter.mono(obj)[0]
            if kind not in ['MyPlayableSequence','MyPlayableAnimator']:return []
            exporter.input(obj);data=exporter.schemas.read(obj)
            if kind=='MyPlayableAnimator':
                if data['myBoolParam'] and data['myBoolValue']:result.append(data['myBoolParam'])
            else:
                def refs(value):
                    if isinstance(value,dict):
                        if 'm_PathID' in value:result.extend(animator_params(value,obj.assets_file,seen))
                        else:
                            for key,child in value.items():
                                if key not in ['m_GameObject','m_Script']:refs(child)
                    elif isinstance(value,list):
                        for child in value:refs(child)
                refs(data)
        return result
    states={}
    for obj,data in drivers:
        for reference in data.get('references',{}).get('RefIds',[]):
            if reference.get('type',{}).get('class')!='ECWorldStageCfgNormalProvider':continue
            for index,state in enumerate(reference['data']['mStateCfgs']):
                for parameter in animator_params(state['myLoopPlayable'],obj.assets_file):
                    loop=parameter+'_loop'
                    if loop not in clips:raise ValueError('Native Animator loop missing: '+loop)
                    states[str(index)]=dict(clips[loop],transition=clips.get(parameter,{}),parameter=parameter)
    return states
