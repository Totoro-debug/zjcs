// Reassemble original split binaries without baking poses. The official decoder
// supplies byte boundaries; retained skin/timeline bytes are copied unchanged.
import fs from 'node:fs';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import crypto from 'node:crypto';
import * as S from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/index.js';
const moduleDir=path.resolve('.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist');
let source=fs.readFileSync(path.join(moduleDir,'SkeletonBinary.js'),'utf8');
const replacements=[
 ['// Deform timelines.','// Deform timelines.\ninput.deformStart=input.index; input.deformBlocks=[];'],
 ['let skin = skeletonData.skins[input.readInt(true)];','const block={start:input.index}; input.deformBlocks.push(block);\nlet skin = skeletonData.skins[input.readInt(true)]; block.skin=skin.name;'],
 ['// Draw order timeline.','input.deformEnd=input.index; for(let b=0;b<input.deformBlocks.length;b++)input.deformBlocks[b].end=b+1<input.deformBlocks.length?input.deformBlocks[b+1].start:input.index;\n// Draw order timeline.']
];
for(const [before,after] of replacements){if(source.split(before).length!==2)throw Error('Official decoder instrumentation marker changed');source=source.replace(before,after);}
const probe=path.join(moduleDir,'BattleBinaryProbe.js');fs.writeFileSync(probe,source);
const {SkeletonBinary,BinaryInput}=await import(pathToFileURL(probe));
const jobs=JSON.parse(fs.readFileSync(process.argv[2],'utf8'));
const varint=n=>{const a=[];do{const b=n&127;n>>>=7;a.push(b|(n?128:0));}while(n);return Buffer.from(a);};
const sha=b=>crypto.createHash('sha256').update(b).digest('hex');
function patched(bytes,edits){let at=0;const chunks=[];for(const e of edits.sort((a,b)=>a.start-b.start)){if(e.start<at)throw Error('Overlapping binary edits');chunks.push(bytes.subarray(at,e.start),e.bytes);at=e.end;}chunks.push(bytes.subarray(at));return Buffer.concat(chunks);}
const results={};
const cached=fs.existsSync(process.argv[3])?JSON.parse(fs.readFileSync(process.argv[3],'utf8')):{};
const toolHash=sha(fs.readFileSync(new URL(import.meta.url)));
for(const [key,job] of Object.entries(jobs)){
 const signature=sha(Buffer.from(JSON.stringify({toolHash,keep_skins:job.keep_skins,inputs:[job.skeleton,job.atlas,...Object.values(job.split_animations||{})].map(f=>[f,sha(fs.readFileSync(f))])})));
 const prior=cached[key];
 if(prior?.signature===signature&&prior.atlas_data&&fs.existsSync(job.output)&&sha(fs.readFileSync(job.output))===prior.output_sha256){results[key]=prior;fs.writeFileSync(job.output_atlas,prior.atlas_data);continue;}
 const atlasText=fs.readFileSync(job.atlas,'utf8'),atlas=new S.TextureAtlas(atlasText);
 const reader=new SkeletonBinary(new S.AtlasAttachmentLoader(atlas)),bytes=Buffer.from(fs.readFileSync(job.skeleton));
 const skins=[],animations=[];let stream;
 const readSkin=reader.readSkin.bind(reader),readAnimation=reader.readAnimation.bind(reader);
 reader.readSkin=(input,data,def,nonessential)=>{
  stream=input;const start=input.index,skin=readSkin(input,data,def,nonessential);
  const header=new BinaryInput(new Uint8Array(bytes),[],start);if(!def){header.readString();if(nonessential)header.readInt32();}
  skins.push({start,end:input.index,skin,def,headerEnd:header.index});return skin;
 };
 reader.readAnimation=(input,name,data)=>{const start=input.index,clip=readAnimation(input,name,data);animations.push({start,end:input.index,name,clip,deformStart:input.deformStart,deformEnd:input.deformEnd,blocks:input.deformBlocks});return clip;};
 const data=reader.readSkeletonData(new Uint8Array(bytes));
 let animationCountPos;
 if(!data.animations.length){if(bytes[bytes.length-1]!==0)throw Error('Unexpected empty animation table');animationCountPos=bytes.length-1;}
 else {
  const name=Buffer.from(data.animations[0].name,'utf8');const prefix=Buffer.concat([varint(name.length+1),name]);
  const nameStart=animations[0].start-prefix.length;
  const count=varint(data.animations.length);animationCountPos=nameStart-count.length;
  if(!bytes.subarray(animationCountPos,nameStart).equals(count)||!bytes.subarray(nameStart,animations[0].start).equals(prefix))throw Error('Animation table boundary mismatch');
 }
 if(job.string_table&&JSON.stringify(job.string_table)!==JSON.stringify(stream.strings))throw Error('Original split string table differs from skeleton: '+key);
 const merged=new Map(animations.map(a=>[a.name,{...a,bytes:bytes.subarray(a.start-(varint(Buffer.byteLength(a.name)+1).length+Buffer.byteLength(a.name)),a.end),offset:a.start-(varint(Buffer.byteLength(a.name)+1).length+Buffer.byteLength(a.name))}]));
 for(const [name,file] of Object.entries(job.split_animations||{})){
  const raw=Buffer.from(fs.readFileSync(file)),input=new BinaryInput(new Uint8Array(raw),stream.strings);
  if(input.readString()!==name)throw Error('Split animation name mismatch');
  const clip=readAnimation(input,name,data);if(input.index!==raw.length)throw Error('Unconsumed original split animation bytes');
  merged.set(name,{name,clip,bytes:raw,offset:0,deformStart:input.deformStart,deformEnd:input.deformEnd,blocks:input.deformBlocks});
 }
 const retained=new Set(job.keep_skins||data.skins.map(s=>s.name));if(data.defaultSkin)retained.add(data.defaultSkin.name);
 // Linked meshes retain their original parent skins and original table indexes.
 let changed=true;while(changed){changed=false;for(const skin of data.skins.filter(s=>retained.has(s.name)))for(const item of skin.getAttachments()){
  const parent=item.attachment.parentMesh;if(!parent)continue;
  for(const candidate of data.skins)if(!retained.has(candidate.name)&&candidate.getAttachments().some(a=>a.attachment===parent)){retained.add(candidate.name);changed=true;}
 }}
 const edits=[];for(const s of skins)if(s.skin&&!s.def&&!retained.has(s.skin.name))edits.push({start:s.start,end:s.end,bytes:Buffer.concat([bytes.subarray(s.start,s.headerEnd),Buffer.alloc(6)])});
 const clips=[];for(const a of merged.values()){
  const blocks=a.blocks.filter(b=>retained.has(b.skin));
  const deform=Buffer.concat([varint(blocks.length),...blocks.map(b=>a.bytes.subarray(b.start-a.offset,b.end-a.offset))]);
  clips.push(patched(a.bytes,[{start:a.deformStart-a.offset,end:a.deformEnd-a.offset,bytes:deform}]));
 }
 const prefix=patched(bytes.subarray(0,animationCountPos),edits),output=Buffer.concat([prefix,varint(clips.length),...clips]);
 const pages=new Set();for(const skin of data.skins.filter(s=>retained.has(s.name)))for(const a of skin.getAttachments()){
  if(a.attachment.region)pages.add(a.attachment.region.page.name);
  for(const region of a.attachment.sequence?.regions||[])if(region)pages.add(region.page.name);
 }
 const atlasBlocks=atlasText.trim().split(/\r?\n\s*\r?\n/),selectedAtlas=atlasBlocks.filter(b=>pages.has(b.split(/\r?\n/)[0].trim())).join('\n\n')+'\n';
 if(new S.TextureAtlas(selectedAtlas).pages.length!==pages.size)throw Error('Atlas page filter mismatch');
 const verified=new S.SkeletonBinary(new S.AtlasAttachmentLoader(new S.TextureAtlas(selectedAtlas))).readSkeletonData(new Uint8Array(output));
 if(verified.bones.length!==data.bones.length||verified.slots.length!==data.slots.length||verified.animations.length!==merged.size)throw Error('Reassembled skeleton mismatch');
 // All retained attachments and full timelines must decode successfully.
 for(const skin of data.skins.filter(s=>retained.has(s.name)))if(verified.findSkin(skin.name).getAttachments().length!==skin.getAttachments().length)throw Error('Retained attachments changed');
 fs.mkdirSync(path.dirname(job.output),{recursive:true});fs.writeFileSync(job.output,output);fs.writeFileSync(job.output_atlas,selectedAtlas);
 results[key]={signature,atlas_data:selectedAtlas,version:data.version,bones:data.bones.length,slots:data.slots.length,skins:[...retained],pages:[...pages],animations:Object.fromEntries([...merged].map(([name,a])=>[name,a.clip.duration])),source_sha256:sha(bytes),output_sha256:sha(output),atlas_sha256:sha(Buffer.from(selectedAtlas))};
 if(key.startsWith('player:')||Object.keys(results).length%100===0)console.log('Original Spine',Object.keys(results).length,key,data.bones.length,'bones',merged.size,'clips',pages.size,'pages');
}
fs.writeFileSync(process.argv[3],JSON.stringify(results));
