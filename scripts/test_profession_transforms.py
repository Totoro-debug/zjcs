"""Regression fixtures for the pinned UI weapon prefabs' authored transforms."""
import math
from prepare_profession_appearance import AppearanceAssets, components, weapon_matrix
from official_manifest import read_asset_manifest
from map_timeline_assets import pointer


def run():
    e=AppearanceAssets()
    rows={r['path'].rsplit('/',1)[-1]:r for n in e.apk.archive.namelist()
          if n.startswith('assets/yoo/') and n.endswith('_854_156383.v3.bytes')
          for r in read_asset_manifest(e.apk.archive.read(n))}
    fixtures={
        'pWeapon_100_G_007': [0,.5,-.5,0,253,0],
        'pWeapon_200_G_007': [0,.5,-.5,0,253,0],
        'sWeapon_11_001': [.5,0,0,.5,0,0],
        'sWeapon_12_001': [-.5,0,0,-.5,0,0],
        'sWeapon_22_001': [.5,0,0,.5,0,0],
        'sWeapon_21_001': [.5,0,0,.5,0,0],
    }
    for name,expected in fixtures.items():
        filename=name+'_UI_Integrated.prefab';row=rows[filename]
        root=e.apk.asset(row['bundle'],'/'+filename)
        part=next(o for o in components(e,root) if e.mono(o)[0] in ['AppearanceSkeletonPart','AppearanceSkeletonMultiSidesPart'])
        fields=e.schemas.read(part);follow=pointer(fields['mSkeletonFollower'],part)
        actual,_=weapon_matrix(e,pointer(fields['Skeleton'],part),pointer(e.schemas.read(follow)['m_GameObject'],follow))
        assert all(math.isclose(a,b,abs_tol=1e-6) for a,b in zip(actual,expected)),(name,actual,expected)
    print('Pinned profession weapon transforms: 6 passed')


if __name__=='__main__':run()
