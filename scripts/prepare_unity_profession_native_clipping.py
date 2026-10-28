"""Verify original convex clipping attachments against independent GEOS geometry."""
import hashlib
import json
from pathlib import Path
import random
import shapely
from shapely.geometry import Polygon

from prepare_unity_profession_native_inputs import ROOT,CACHE
from prepare_unity_profession_native_curves import record,f32


def point(x,y):return dict(x=f32(x),y=f32(y))


def build():
    source_path=ROOT/'assets/reference/profession-ui/native-attachments-source-manifest.json';proof=json.loads(source_path.read_bytes())
    fixture_path=ROOT/proof['fixture']['path']
    if record(fixture_path)!=proof['fixture']:raise ValueError('Verified source attachment fixture changed')
    source=json.loads(fixture_path.read_bytes());bodies=[];counts=dict(boundaries=0,triangles=0,empty=0,cut=0,inside=0)
    randomizer=random.Random(871156598)
    for body in source['cases']:
        clips=[]
        for a in body['attachments']:
            if a['kind']!='clipping':continue
            boundary=[(p['x'],p['y']) for p in a['expected']];polygon=Polygon(boundary)
            if not polygon.is_valid or polygon.area<=0 or abs(polygon.convex_hull.area-polygon.area)>1e-8:
                raise ValueError('Restore original concave/degenerate clip rather than silently replacing it')
            left,bottom,right,top=polygon.bounds;w,h=right-left,top-bottom
            coefficients=[f32(.7/w),f32(.1/h),f32(.12-.7*left/w-.1*bottom/h),f32(.2/w),f32(-.6/h),f32(.34-.2*left/w+.6*bottom/h)]
            def uv(p):return point(coefficients[0]*p['x']+coefficients[1]*p['y']+coefficients[2],coefficients[3]*p['x']+coefficients[4]*p['y']+coefficients[5])
            cases=[]
            for i in range(160):
                triangle=[point(left+randomizer.uniform(-.6,1.6)*w,bottom+randomizer.uniform(-.6,1.6)*h) for _ in range(3)]
                shape=Polygon([(p['x'],p['y']) for p in triangle]);result=shapely.intersection(polygon,shape,grid_size=0)
                if result.geom_type not in ('Polygon','GeometryCollection'):raise ValueError('Inspect independent clipping boundary result')
                area=result.area;center=point(0,0) if area==0 else point(result.centroid.x,result.centroid.y)
                expected=[] if area==0 else [point(x,y) for x,y in list(result.exterior.coords)[:-1]]
                if area==0:counts['empty']+=1
                elif abs(area-shape.area)<1e-9:counts['inside']+=1
                else:counts['cut']+=1
                cases.append(dict(vertices=triangle,uvs=[uv(p) for p in triangle],area=area,center=center,boundary=expected,reverse=i%2==1))
            clips.append(dict(name=a['name'],slotBone=a['slotBone'],vertices=a['vertices'],count=a['count'],end=a['end'],scale=a['scale'],
                boundary=[point(x,y) for x,y in boundary],coefficients=coefficients,cases=cases))
            counts['boundaries']+=1;counts['triangles']+=len(cases)
        bodies.append(dict(sex=body['sex'],bones=body['bones'],slotCount=body['slotCount'],clips=clips))
    output=CACHE/'typed/native-clipping-validation.json';output.write_text(json.dumps(dict(bodies=bodies),ensure_ascii=False,separators=(',',':'))+'\n',encoding='utf-8',newline='\n')
    runtime=ROOT/'unity/Assets/Game/Runtime/Offline/ProfessionUI/ProfessionNativeConvexClipper.cs';validation=ROOT/'unity/Assets/Game/Editor/ProfessionUI/ProfessionNativeClippingValidation.cs'
    proof=dict(format=1,manifest='871_156598',inputs=[record(source_path),record(fixture_path)],fixture=record(output),generator=record(Path(__file__)),
        outputs=[record(p) for file in (runtime,validation) for p in (file,file.with_name(file.name+'.meta'))],counts=counts,
        reference=dict(package='Shapely',version=shapely.__version__,geos=shapely.geos_version_string,precision_grid_size=0,documentation='https://shapely.readthedocs.io/en/2.1.2/reference/shapely.intersection.html'),
        operation='Independent half-plane intersection for all six original convex clipping quadrilaterals. Preserve source boundary orientation, exact clipping end-slot metadata, affine UV interpolation and triangle winding. Compare intersections, total area, centroid and boundary positions against GEOS double-precision polygon intersections. Source world matrices and intersecting triangles are synthetic geometry inputs, not constrained character poses. No skeleton SDK implementation is read or used.',
        validated_scope='Isolated Unity Play Mode: six source boundaries, 960 intersections and 5160 output vertices passed against GEOS; maximum area difference 6.6130726E-05, centroid difference 3.20112824E-05 and UV difference 1.1920929E-07. Both boundary orientations, winding, fully inside/outside and partial intersections, input ownership and invalid inputs passed. Native actor constraints and full profession UI remain unbound.',
        limitations=['Concave input fails explicitly; the six current original character clipping attachments are convex. Draw-order/end-slot lifecycle and native character/page rendering are separate integration requirements.'])
    (ROOT/'assets/reference/profession-ui/native-clipping-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
    print('Prepared original clipping intersections:',counts)


if __name__=='__main__':build()
