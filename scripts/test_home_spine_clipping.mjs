import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const root=process.cwd();
const output=path.join(root,'output');
fs.mkdirSync(output,{recursive:true});
const temporary=fs.mkdtempSync(path.join(output,'home-spine-clip-'));
try {
 const atlas=path.join(temporary,'fixture.atlas');
 const skeleton=path.join(temporary,'fixture.json');
 const jobs=path.join(temporary,'jobs.json');
 const result=path.join(temporary,'result.json');
 fs.writeFileSync(atlas,'page.png\nsize: 16,16\nformat: RGBA8888\nfilter: Linear,Linear\nrepeat: none\npma: true\nregion\nrotate: false\nxy: 0,0\nsize: 16,16\norig: 16,16\noffset: 0,0\nindex: -1\n');
 fs.writeFileSync(skeleton,JSON.stringify({
  skeleton:{spine:'4.2.110',width:16,height:16},
  bones:[{name:'root'}],
  slots:[{name:'clipSlot',bone:'root',attachment:'clip'},{name:'quadSlot',bone:'root',attachment:'region',blend:'screen'}],
  skins:[{name:'default',attachments:{
   clipSlot:{clip:{type:'clipping',vertexCount:4,vertices:[0,0,8,0,8,8,0,8],end:'quadSlot'}},
   quadSlot:{region:{type:'region',path:'region',width:16,height:16}}
  }}],
  animations:{}
 }));
 fs.writeFileSync(jobs,JSON.stringify({fixture:{atlas,skeleton,pose_only:true}}));
 execFileSync(process.execPath,[path.join(root,'scripts/extract_home_meshes.mjs'),jobs,result],{cwd:root,stdio:'pipe'});
 const mesh=JSON.parse(fs.readFileSync(result,'utf8')).fixture.frames[0][0];
 assert.equal(JSON.parse(fs.readFileSync(result,'utf8')).fixture.frames[0].length,1,'clipping attachments do not emit a drawable mesh');
 assert.equal(mesh.blend,3,'original Spine Screen blend enum is retained');
 assert.equal(mesh.pma,true,'original atlas PMA setting is retained');
 assert.equal(mesh.vertices.length,mesh.uv.length,'clipped vertices retain their interpolated atlas UVs');
 assert(mesh.triangles.length>=3,'clipping produces drawable triangles');
 for(let i=0;i<mesh.vertices.length;i+=2){
  assert(mesh.vertices[i]>=-0.002 && mesh.vertices[i]<=8.002,'clipped vertex x remains inside the original clip polygon');
  assert(mesh.vertices[i+1]>=-0.002 && mesh.vertices[i+1]<=8.002,'clipped vertex y remains inside the original clip polygon');
 }
 console.log('Home Spine clipping checks: clipping geometry, UV interpolation, Screen blend and PMA passed');
} finally {
 fs.rmSync(temporary,{recursive:true,force:true});
}
