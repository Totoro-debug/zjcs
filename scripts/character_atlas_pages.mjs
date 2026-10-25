// Decode original skin attachment page dependencies for lazy native loading.
import fs from 'node:fs';
import * as S from '../.tools/character-art-node/node_modules/@esotericsoftware/spine-core/dist/index.js';
const jobs=JSON.parse(fs.readFileSync(process.argv[2],'utf8')),result={};
for(const [key,job] of Object.entries(jobs)){
 if(!key.startsWith('player:'))continue;
 const atlas=new S.TextureAtlas(fs.readFileSync(job.atlas,'utf8'));
 const data=new S.SkeletonBinary(new S.AtlasAttachmentLoader(atlas)).readSkeletonData(new Uint8Array(fs.readFileSync(job.skeleton)));
 result[key]={};
 for(const skin of data.skins){
  const pages=new Set();
  for(const {attachment:a} of skin.getAttachments()){
   if(a.region)pages.add(a.region.page.name);
   for(const region of a.sequence?.regions||[])if(region)pages.add(region.page.name);
  }
  result[key][skin.name]=[...pages].sort();
 }
}
fs.writeFileSync(process.argv[3],JSON.stringify(result));
