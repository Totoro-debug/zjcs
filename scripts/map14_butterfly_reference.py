"""Independent double-precision affine checks from decoded original model fields.

These values verify the C# native data projection and player, not a Spine runtime
comparison. They do not replace actual animated scene rendering checks.
"""
import bisect
import math


def checks(skeleton, geometry, scale):
    names = {b["name"]: i for i, b in enumerate(skeleton["bones"])}
    def cubic(points, u):
        points = list(points)
        while len(points) > 1: points = [(1-u)*a+u*b for a,b in zip(points,points[1:])]
        return points[0]
    def component(keys, time, name, default, curve_offset):
        times = [k.get("time", 0) for k in keys]
        i = bisect.bisect_right(times, time) - 1
        if i < 0: return default
        left = keys[i]; value = left.get(name, default)
        if i+1 == len(keys) or left.get("curve") == "stepped": return value
        right = keys[i+1]; other = right.get(name, default)
        curve = left.get("curve")
        if not curve: return value+(other-value)*(time-times[i])/(times[i+1]-times[i])
        a,b,c,d = curve[curve_offset:curve_offset+4]
        lo,hi = 0.,1.
        for _ in range(50):
            u=(lo+hi)/2
            if cubic((times[i],a,c,times[i+1]),u)<time:lo=u
            else:hi=u
        return cubic((value,b,d,other),(lo+hi)/2)
    output=[]
    for animation_name, animation in skeleton["animations"].items():
        end=max(k.get("time",0)for b in animation["bones"].values()for v in b.values()for k in v)
        # Include interior samples, exact keys, wrap and zero. Midpoints expose
        # stepped curves, negative scales, quarter-rotated UVs and shear changes.
        times={0.,end*.137,end*.381,end*.719,end*.941}
        for channels in animation["bones"].values():
            for keys in channels.values():
                times.update(k.get("time",0) for k in keys)
        times=sorted(t for t in times if t<end)
        for time in times:
            matrices=[]
            for bone in skeleton["bones"]:
                x,y=bone.get("x",0),bone.get("y",0)
                rotation=bone.get("rotation",0);sx,sy=bone.get("scaleX",1),bone.get("scaleY",1)
                shx,shy=bone.get("shearX",0),bone.get("shearY",0)
                for channel, keys in animation["bones"].get(bone["name"],{}).items():
                    if time < keys[0].get("time",0):continue
                    if channel=="rotate":rotation+=component(keys,time,"value",0,0)
                    elif channel=="translate":x+=component(keys,time,"x",0,0);y+=component(keys,time,"y",0,4)
                    elif channel=="scale":sx*=component(keys,time,"x",1,0);sy*=component(keys,time,"y",1,4)
                    elif channel=="shear":shx+=component(keys,time,"x",0,0);shy+=component(keys,time,"y",0,4)
                    else:raise ValueError("Unknown original reference channel")
                ax,ay=map(math.radians,(rotation+shx,rotation+90+shy))
                a,b,c,d=math.cos(ax)*sx,math.cos(ay)*sy,math.sin(ax)*sx,math.sin(ay)*sy
                if "parent" in bone:
                    p,q,r,s,tx,ty=matrices[names[bone["parent"]]]
                    x,y=p*x+q*y+tx,r*x+s*y+ty
                    if bone.get("inherit",bone.get("transform","normal"))=="normal":a,b,c,d=p*a+q*c,p*b+q*d,r*a+s*c,r*b+s*d
                matrices.append((a,b,c,d,x,y))
            for skin in geometry["skins"]:
                vertices=[]
                for point,bone in zip(skin["vertices"],skin["vertexBones"]):
                    a,b,c,d,x,y=matrices[bone]
                    vertices.append(dict(x=(a*point["x"]+b*point["y"]+x)*scale,y=(c*point["x"]+d*point["y"]+y)*scale))
                output.append(dict(animation=animation_name,skin=skin["name"],time=time,vertices=vertices))
    return output
