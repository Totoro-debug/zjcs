"""Recover selected task regions and their original recommendation/rank formulas."""
import argparse,hashlib,json,re,shutil
from pathlib import Path
from official_apk import Apk,ROOT
from official_tables import Constructors
from official_unity_schema import ScriptSchemas
from prepare_unity_mainline_tasks import write_meta

OUT=ROOT/'unity/Assets/Game/Resources/Home/home-task-map-data.json'
CODE=ROOT/'.tools/source-packages/current/884_156692/official-code'

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def method(text,signature):
 start=text.index(signature);brace=text.index('{',start);depth=1;end=brace+1
 while depth:
  depth+=(text[end]=='{')-(text[end]=='}');end+=1
 return text[start:end]
def build():
 inputs={}
 def read(path):
  inputs[path.relative_to(ROOT).as_posix()]=sha(path)
  return path.read_text('utf-8-sig')
 tasks_path=ROOT/'unity/Assets/Game/Resources/Home/home-mainline-task-data.json'
 tasks=json.loads(read(tasks_path))
 pairs={(c['MapClassId'],c['Region']) for t in tasks['tasks'] if (c:=t['FinishCondition'])['_type']=='WorldMapProgressCondition'}
 tables={};proofs={}
 with Apk.current() as apk:
  assert apk.version=='884_156692'
  constructors=Constructors(CODE)
  mapping={name.rsplit('/',1)[-1]:kind for kind,name in re.findall(r'new (Tb\w+)\(loader\("([^"]+)"\)\)',constructors.source('Tables'))}
  for obj in apk.objects('defaultpackage_assets_config_luban.bundle')+apk.objects('defaultpackage_assets_config_luban_map.bundle'):
   if obj.type.name!='TextAsset':continue
   name=obj.read().m_Name.rsplit('/',1)[-1].removesuffix('.g')
   if name not in ('map_region','game_settings'):continue
   raw=obj.read().m_Script.encode('utf8','surrogateescape')
   tables[name]=constructors.table(raw,mapping[name]);proofs[name]={'source':apk.provenance(obj),'payload_sha256':hashlib.sha256(raw).hexdigest(),'object_sha256':hashlib.sha256(obj.get_raw_data()).hexdigest()}
  assert len(tables)==2,tables.keys()
  color_obj=apk.asset('scriptableconfig_modules_combatrating','/CombatRatingConfig.asset')
  colors=ScriptSchemas(code_root=CODE).read(color_obj)
  proofs['combat_colors']={'source':apk.provenance(color_obj),'object_sha256':hashlib.sha256(color_obj.get_raw_data()).hexdigest()}
  read(CODE/'client-source/CombatRatingConfig.cs')
  regions=[r for r in tables['map_region'] if (r['ClassId'],r['Region']) in pairs]
  assert {(r['ClassId'],r['Region']) for r in regions}==pairs
  for name in constructors.sources:
   path=CODE/'common-source/Common'/(name+'.cs')
   if not path.exists():path=CODE/'common-source/Common.LubanGen'/(name+'.cs')
   read(path)
 settings=tables['game_settings']
 enumext=read(CODE/'common-source/Common/EnumExt.cs')
 enumvalues={}
 for name in ('SubRank','Rank'):
  body=read(CODE/('common-source/Common/'+name+'.cs'))
  entries=body[body.index('{')+1:body.rindex('}')].strip().split(',')
  values={};value=-1
  for entry in entries:
   entry=entry.strip()
   if not entry:continue
   parts=entry.split('=');value=int(parts[1]) if len(parts)>1 else value+1;values[parts[0].strip()]=value
  enumvalues[name]=values
 to_rank=method(enumext,'public static Rank ToRank(this SubRank subRank)')
 rankmap={};pending=[]
 for line in to_rank.splitlines():
  if match:=re.search(r'case SubRank\.(\w+):',line):pending.append(match[1])
  if match:=re.search(r'return Rank\.(\w+);',line):
   for sub in pending:rankmap[str(enumvalues['SubRank'][sub])]=enumvalues['Rank'][match[1]]
   pending=[]
 assert len(rankmap)==len(enumvalues['SubRank'])
 formula=method(read(CODE/'common-source/Common/FormulaHandler.cs'),'public int GetCombatRatingLevel(')
 suppress=method(read(CODE/'common-source/Common/BattleFormulaHandler.cs'),'public int CalcRankSuppression(')
 source=read(CODE/'common-source/Common/MapRegionInfo.cs')
 for fragment in ('CombatRatingLevelValues[Parsers.GameSettings.RecommendCombatRatingIndex]','if (level < LimitLevel)','return IsLimitedByType(level, mapPlanesRankInfo) != MapRegionLimitType.None;'):
  assert fragment in source
 data={'manifest':'884_156692','combatColors':colors,'regions':regions,'recommendCombatRatingIndex':settings['RecommendCombatRatingIndex'],'suppressUnlockRank':settings['SuppressUnlockRank'],'subRankToRank':rankmap}
 OUT.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 checksum=OUT.with_name('home-task-map-data-hash.json');checksum.write_text(json.dumps({'manifest':'884_156692','output_sha256':sha(OUT)},indent=2)+'\n','utf8',newline='\n')
 formula=formula.replace('public int','public static int',1)
 suppress=suppress.replace('public int CalcRankSuppression(SubRank mySubRank, SubRank otherSubRank)','public static int CalcRankSuppression(int mySubRank, int otherSubRank, int suppressUnlockRank, Func<int,int> toRank)').replace('Parsers.GameSettings.SuppressUnlockRank','suppressUnlockRank').replace('mySubRank.ToRank()','toRank(mySubRank)').replace('otherSubRank.ToRank()','toRank(otherSubRank)')
 target=ROOT/'unity/Assets/Game/Runtime/Official/HomeTaskUI/HomeTaskMapFormula.cs'
 target.write_text('// Selected official methods; explicit settings and enum projection.\nusing System;\nusing System.Collections.Generic;\nnamespace Zjcs.Official.HomeTaskUI\n{\npublic static class HomeTaskMapFormula\n{\n'+formula+'\n'+suppress+'\n}\n}\n','utf8',newline='\n')
 wrapper=ROOT/'unity/Assets/Game/Runtime/Offline/HomeTaskUI/OfflineHomeTaskMapData.cs'
 paths=[OUT,checksum,target,wrapper]
 for path in list(paths):
  write_meta(path,'text');meta=Path(str(path)+'.meta')
  if path.suffix=='.cs':meta.write_text('fileFormatVersion: 2\nguid: '+hashlib.md5(('zjcs-official:'+path.relative_to(ROOT/'unity').as_posix()).encode()).hexdigest()+'\nMonoImporter:\n  serializedVersion: 2\n  externalObjects: {}\n','utf8',newline='\n')
  meta.write_text('\n'.join(x.rstrip() for x in meta.read_text('utf8').splitlines())+'\n','utf8',newline='\n');paths.append(meta)
 for path in (ROOT/'scripts/official_tables.py',ROOT/'scripts/official_apk.py',ROOT/'scripts/official_bundle_reader.py',ROOT/'scripts/official_unity_schema.py',ROOT/'scripts/fixtures/home_task_map_data_validation.cs'):read(path)
 proof={'manifest':'884_156692','generator_sha256':sha(Path(__file__)),'inputs':inputs,'tables':proofs,'outputs':{p.relative_to(ROOT).as_posix():sha(p) for p in paths},'offline_policy':'Confirmed offline-access policy removes server-rank and calendar map gates; personal level remains. Original limit fields remain in payload.','scope':'Original map metadata for all supported WorldMapProgress task targets and original combat/rank formulas; production task host binding remains pending.'}
 (ROOT/'assets/reference/home-task-map-data-source-manifest.json').write_text(json.dumps(proof,ensure_ascii=False,indent=2)+'\n','utf8',newline='\n')
 print('Recovered',len(regions),'task regions;',len(rankmap),'rank mappings',flush=True)
def stage(project,proof):
 from validation_workspace import validation_directory
 assert project.resolve().is_relative_to(validation_directory().resolve())
 for relative,digest in proof['outputs'].items():
  assert sha(ROOT/relative)==digest
  target=project/(ROOT/relative).relative_to(ROOT/'unity');target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(ROOT/relative,target)
 folder=project/'Assets/HomeTaskMapDataValidation';folder.mkdir(exist_ok=True)
 shutil.copy2(ROOT/'scripts/fixtures/home_task_map_data_validation.cs',folder/'HomeTaskMapDataValidation.cs')
 (folder/'HomeTaskMapDataValidation.asmdef').write_text(json.dumps({'name':'HomeTaskMapDataValidation','references':['Zjcs.Offline.HomeTaskUI','Zjcs.Official.HomeTaskUI','Unity.Newtonsoft.Json'],'includePlatforms':['Editor']}),'utf8')
if __name__=='__main__':
 parser=argparse.ArgumentParser(__doc__);parser.add_argument('--project',type=Path);args=parser.parse_args();proof=build()
 if args.project:stage(args.project,proof)
