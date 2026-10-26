import fs from 'node:fs';
import * as S from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/index.js';
import {BinaryInput} from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/SkeletonBinary.js';
const job=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
function read(skeleton,atlas){return new S.SkeletonBinary(new S.AtlasAttachmentLoader(new S.TextureAtlas(fs.readFileSync(atlas,'utf8')))).readSkeletonData(new Uint8Array(fs.readFileSync(skeleton)));}
const data=read(job.skeleton,job.atlas),weaponData=read(job.weapon_skeleton,job.weapon_atlas);
for(const [name,file] of Object.entries(job.split_animations||{})){
 const bytes=new Uint8Array(fs.readFileSync(file)),input=new BinaryInput(bytes,job.string_table);
 if(input.readString()!==name)throw Error('Split animation name differs');
 const animation=new S.SkeletonBinary(new S.AtlasAttachmentLoader(new S.TextureAtlas(fs.readFileSync(job.atlas,'utf8')))).readAnimation(input,name,data);
 if(input.index!==bytes.length)throw Error('Unconsumed animation bytes');
 data.animations=data.animations.filter(a=>a.name!==name);data.animations.push(animation);
}
const output={textures:[],animations:{}};
for(const name of job.animations){
 const animation=data.findAnimation(name);if(!animation)throw Error(name);
 const fps=24,count=Math.max(1,Math.ceil(animation.duration*fps)),weapons={};
 for(const [id,skin] of Object.entries(job.weapons)){
  const original=new S.Skeleton(weaponData);original.setSkin(weaponData.findSkin(skin));original.setToSetupPose();original.updateWorldTransform(S.Physics.none);
  const slot=original.findSlot('primary_01'),attachment=slot.getAttachment();
  if(!(attachment instanceof S.RegionAttachment))throw Error('Expected original glow-stick region: '+skin);
  const local=new Array(8).fill(0);attachment.computeWorldVertices(slot,local,0,2);
  for(let i=0;i<local.length;i++)local[i]*=job.weapon_scale[i%2];
  const texture=attachment.region.page.name.replace(/\.png$/,'');if(!output.textures.includes(texture))output.textures.push(texture);
  const character=new S.Skeleton(data),frames=[];
  for(let frame=0;frame<count;frame++){
   character.setToSetupPose();animation.apply(character,0,frame/fps,false,[],1,S.MixBlend.replace,S.MixDirection.mixIn);character.updateWorldTransform(S.Physics.none);
   const follow=character.findBone('pWeapon_R2');if(!follow)throw Error('Native weapon bone unavailable');
   const vertices=[];for(let i=0;i<local.length;i+=2){vertices.push(Math.round((160+follow.worldX+follow.a*local[i]+follow.b*local[i+1])*2000)/1000,Math.round((388-follow.worldY-follow.c*local[i]-follow.d*local[i+1])*2000)/1000);}
   frames.push({vertices,texture,uv:Array.from(attachment.uvs),triangles:[0,1,2,2,3,0],color:[1,1,1,1],order:character.drawOrder.findIndex(s=>s.data.name==='W_jian_R/W_jian_R')});
  }
  weapons[id]=frames;
 }
 output.animations[name]={fps,duration:animation.duration,weapons};
}
fs.writeFileSync(process.argv[3],JSON.stringify(output));
