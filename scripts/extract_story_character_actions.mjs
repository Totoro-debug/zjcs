// Offline original player actions, retaining the existing customizable parts.
import fs from 'node:fs';
import path from 'node:path';
import * as S from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/index.js';
import {BinaryInput} from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/SkeletonBinary.js';
const job=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const atlas=new S.TextureAtlas(fs.readFileSync(job.atlas,'utf8'));
const loader=new S.AtlasAttachmentLoader(atlas);
const data=new S.SkeletonBinary(loader).readSkeletonData(new Uint8Array(fs.readFileSync(job.skeleton)));
for(const [name,file] of Object.entries(job.split_animations||{})){
 const bytes=new Uint8Array(fs.readFileSync(file)),input=new BinaryInput(bytes,job.string_table);
 if(input.readString()!==name)throw Error('Original split animation name differs: '+name);
 const animation=new S.SkeletonBinary(loader).readAnimation(input,name,data);
 if(input.index!==bytes.length)throw Error('Unconsumed original split animation bytes: '+name);
 data.animations=data.animations.filter(a=>a.name!==name);data.animations.push(animation);
}
const output={parameters:job.parameters,animations:{}};
for(const name of job.animations){
 const animation=data.findAnimation(name);if(!animation)throw Error('Original player action unavailable: '+name);
 const fps=24,count=Math.max(1,Math.ceil(animation.duration*fps)),parts={};
 for(const [part,skinName] of Object.entries(job.parts)){
  const skin=data.findSkin(skinName);if(!skin)throw Error('Original player skin unavailable: '+skinName);
  const skeleton=new S.Skeleton(data);skeleton.setSkin(skin);parts[part]={};
  const slots=job.slots[part];for(const slot of slots)parts[part][slot]=[];
  for(let frame=0;frame<count;frame++){
   skeleton.setToSetupPose();animation.apply(skeleton,0,frame/fps,false,[],1,S.MixBlend.replace,S.MixDirection.mixIn);skeleton.updateWorldTransform(S.Physics.none);
   for(const name of slots){
    const slot=skeleton.findSlot(name),a=slot.getAttachment();let sample={vertices:[]};
    if(a?.region){
     let vertices,triangles;
     if(a instanceof S.RegionAttachment){vertices=new Array(8).fill(0);a.computeWorldVertices(slot,vertices,0,2);triangles=[0,1,2,2,3,0];}
     else if(a instanceof S.MeshAttachment){vertices=new Array(a.worldVerticesLength).fill(0);a.computeWorldVertices(slot,0,a.worldVerticesLength,vertices,0,2);triangles=Array.from(a.triangles);}
     if(vertices)sample={order:skeleton.drawOrder.indexOf(slot),texture:a.region.page.name.replace(/\.png$/,''),vertices:vertices.map((v,i)=>Math.round((i%2?388-v:160+v)*2000)/1000),uv:Array.from(a.uvs),triangles,color:[slot.color.r*a.color.r,slot.color.g*a.color.g,slot.color.b*a.color.b,slot.color.a*a.color.a]};
    }
    parts[part][name].push(sample);
   }
  }
 }
 const value={duration:animation.duration,fps,parts};
 if(job.output_directory){
  const file=name.replaceAll('/','_')+'.json';fs.mkdirSync(job.output_directory,{recursive:true});fs.writeFileSync(path.join(job.output_directory,file),JSON.stringify(value));
  output.animations[name]={duration:animation.duration,fps,file:job.resource_directory+'/'+file};
 }else output.animations[name]=value;
}
fs.writeFileSync(process.argv[3],JSON.stringify(output));
console.log('Original customizable player actions:',Object.keys(output.animations));
