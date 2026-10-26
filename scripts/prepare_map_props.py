"""Convert the pinned original level_prop binary rows used by map rewards."""
import hashlib
import json
import struct
from pathlib import Path
from official_apk import ROOT
from prepare_map_regions import ART, REF, save


def decode(raw, wanted):
    def number(pos, code):
        size=struct.calcsize(code)
        return struct.unpack_from(code,raw,pos)[0],pos+size
    def varint(pos):
        value=shift=0
        while True:
            b=raw[pos];pos+=1;value|=(b&127)<<shift;shift+=7
            if not b&128:break
            if shift>=70:raise ValueError('Invalid original level_prop varint')
        return (value>>1)^-(value&1),pos
    magic,version,sections_at,index_at=struct.unpack_from('<IHii',raw)
    if magic!=1280328262 or version!=1:raise ValueError('Unexpected level_prop header')
    count,p=number(sections_at,'<i');sections=[]
    for _ in range(count):
        n,p=number(p,'<H');sections.append(list(struct.unpack_from('<'+'H'*n,raw,p)));p+=n*2
    count,p=number(index_at,'<i');output={}
    for _ in range(count):
        cls,lo,hi=struct.unpack_from('<iii',raw,p);p+=12
        offsets=struct.unpack_from('<'+'i'*(hi-lo+1),raw,p);p+=4*len(offsets)
        if cls not in wanted:continue
        rows={}
        for level,offset in enumerate(offsets,lo):
            if offset<0:continue
            section,cursor=varint(offset);props={}
            for prop in sections[section]:
                value,cursor=varint(cursor)
                if value:props[str(prop)]=value
            rows[str(level)]=props
        output[str(cls)]={'min':lo,'max':hi,'rows':rows}
    if wanted-set(map(int,output)):raise ValueError('Missing map property classes: '+str(wanted-set(map(int,output))))
    return output


def build():
    wanted=set()
    inputs=[ART/'map-entities.json',*sorted((ART/'regions').glob('*-entities.json'))]
    def visit(value):
        if isinstance(value,dict):
            if set(['PropId','Level','PropType'])<=value.keys():wanted.add(int(value['PropId']))
            for child in value.values():visit(child)
        elif isinstance(value,list):
            for child in value:visit(child)
    for path in inputs:visit(json.loads(path.read_text(encoding='utf8')))
    path=ROOT/'.tools/source-packages/WwiseBnk/wwisebnk_assets_config_binary_level_prop_g_bytes.rawfile'
    raw=path.read_bytes();output=decode(raw,wanted);save(ART/'map-props.json',output)
    sources=json.loads((path.parent/'bundle-sources.json').read_text(encoding='utf8'))
    # The extraction receipt retains the rawfile's original package URL/hash.
    save(REF/'props-source-manifest.json',{'manifest':'854_156383','package':'WwiseBnk','asset':path.name,'source':sources[path.name],'input_sha256':hashlib.sha256(raw).hexdigest(),'classes':sorted(wanted),'output_sha256':hashlib.sha256((ART/'map-props.json').read_bytes()).hexdigest(),'operation':'LevelPropBinaryReader header/index and signed varint rows; original values, no formulas generated'})
    print('Map property classes',len(output))


if __name__=='__main__':build()
