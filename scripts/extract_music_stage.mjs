// Development conversion of original concert performer Spine animations.
import fs from 'node:fs';
import * as S from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/index.js';
import {BinaryInput} from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/SkeletonBinary.js';
const jobs=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const result={};
for(const [id,job] of Object.entries(jobs)){
 const atlas=new S.TextureAtlas(fs.readFileSync(job.atlas,'utf8'));
 const raw=fs.readFileSync(job.skeleton);
 const loader=new S.AtlasAttachmentLoader(atlas);
 const isJson=/^\s*\{\s*"/.test(raw.toString('utf8',0,80));
 const data=isJson ? new S.SkeletonJson(loader).readSkeletonData(raw.toString('utf8')) : new S.SkeletonBinary(loader).readSkeletonData(new Uint8Array(raw));
 for(const [name,file] of Object.entries(job.split_animations||{})){
  const bytes=new Uint8Array(fs.readFileSync(file));const input=new BinaryInput(bytes,job.string_table);
  if(input.readString()!==name)throw Error(`Original split animation name differs: ${name}`);
  const animation=new S.SkeletonBinary(loader).readAnimation(input,name,data);
  if(input.index!==bytes.length)throw Error(`Unconsumed original split animation bytes: ${name}`);
  data.animations=data.animations.filter(a=>a.name!==name);data.animations.push(animation);
 }
 const skeleton=new S.Skeleton(data);
 if(job.skin && data.findSkin(job.skin)) skeleton.setSkinByName(job.skin);
 if(job.skins){
  const combined=new S.Skin('original-combined');
  for(const name of job.skins){
   const skin=data.findSkin(name);
   if(!skin){if(job.ignore_missing_skins)continue;throw new Error(`Missing original skin: ${name}`);}
   combined.addSkin(skin);
  }
  skeleton.setSkin(combined);
 }
 skeleton.setToSetupPose();
 const animation=job.animation ? data.findAnimation(job.animation) : data.animations.find(a=>/^(idle|idle_01|stand|daiji)$/i.test(a.name)) || data.animations.find(a=>/idle|stand/i.test(a.name));
 if(job.animation && !animation)throw new Error(`Missing original animation: ${job.animation}`);
 const duration=animation?.duration || 0;
 const frames=[];
 const count=job.pose_only ? 1 : duration>0 ? Math.max(1,Math.ceil(duration*12)):1;
 for(let frame=0;frame<count;frame++){
  skeleton.setToSetupPose();
  if(animation)animation.apply(skeleton,0,job.pose_only ? duration : frame/12,!job.pose_only,[],1,S.MixBlend.replace,S.MixDirection.mixIn);
  skeleton.updateWorldTransform(S.Physics.none);
  const meshes=[];
  for(let order=0;order<skeleton.drawOrder.length;order++){
   const slot=skeleton.drawOrder[order],a=slot.getAttachment();
   if(!a?.region || slot.color.a*a.color.a<=0)continue;
   let vertices,triangles;
   if(a instanceof S.RegionAttachment){vertices=new Array(8).fill(0);a.computeWorldVertices(slot,vertices,0,2);triangles=[0,1,2,2,3,0];}
   else if(a instanceof S.MeshAttachment){vertices=new Array(a.worldVerticesLength).fill(0);a.computeWorldVertices(slot,0,a.worldVerticesLength,vertices,0,2);triangles=Array.from(a.triangles);}
   else continue;
   const mesh={order,slot:slot.data.name,page:a.region.page.name,vertices:vertices.map(v=>Math.round(v*1000)/1000),uv:Array.from(a.uvs),triangles,color:[slot.color.r*a.color.r,slot.color.g*a.color.g,slot.color.b*a.color.b,slot.color.a*a.color.a]};
   if(job.animation)mesh.blend=slot.data.blendMode;
   meshes.push(mesh);
  }
  frames.push(meshes);
 }
 result[id]={animation:animation?.name || '',duration,fps:12,frames,available_animations:data.animations.map(a=>({name:a.name,duration:a.duration}))};
}
fs.writeFileSync(process.argv[3],JSON.stringify(result));
console.log('Converted concert animations:',Object.keys(result).length);
