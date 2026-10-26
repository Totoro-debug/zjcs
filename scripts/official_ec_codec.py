"""Decode original EC configuration using its generated MessagePack formatters.

The pinned client uses untagged big endian Single/Double values. Ordinary
MessagePack decoding alone silently corrupts those component payloads.
"""
import re
import struct
from pathlib import Path
from official_map_codec import unpack
from official_apk import ROOT
import msgpack

SOURCE=ROOT/'.tools/source-packages/official-code/common-source'


def args(text):
    depth=0;start=0;result=[]
    for i,c in enumerate(text):
        depth+=(c=='<')-(c=='>')
        if c==',' and depth==0:result.append(text[start:i].strip());start=i+1
    return result+[text[start:].strip()]


class Reader:
    def __init__(self,data):self.data=data;self.pos=0
    def take(self,n):
        if self.pos+n>len(self.data):raise ValueError('Truncated EC payload')
        r=self.data[self.pos:self.pos+n];self.pos+=n;return r
    def value(self):
        start=self.pos;code=self.take(1)[0]
        if code<0x80:return code
        if code>=0xe0:return code-256
        if code==0xc0:return None
        if code in [0xc2,0xc3]:return code==0xc3
        formats={0xcc:'>B',0xcd:'>H',0xce:'>I',0xcf:'>Q',0xd0:'>b',0xd1:'>h',0xd2:'>i',0xd3:'>q',0xca:'>f',0xcb:'>d'}
        if code in formats:
            fmt=formats[code];return struct.unpack(fmt,self.take(struct.calcsize(fmt)))[0]
        if 0xa0<=code<=0xbf:return self.take(code&31).decode('utf8')
        lengths={0xd9:'>B',0xda:'>H',0xdb:'>I',0xc4:'>B',0xc5:'>H',0xc6:'>I'}
        if code in lengths:
            fmt=lengths[code];count=struct.unpack(fmt,self.take(struct.calcsize(fmt)))[0];raw=self.take(count)
            return raw if code in [0xc4,0xc5,0xc6] else raw.decode('utf8')
        self.pos=start
        u=msgpack.Unpacker(raw=False,strict_map_key=False);u.feed(self.data[self.pos:]);v=next(u);self.pos+=u.tell();return v
    def count(self,kind):
        code=self.take(1)[0];base=0x90 if kind=='array' else 0x80
        if base<=code<base+16:return code&15
        if code==(0xdc if kind=='array' else 0xde):return struct.unpack('>H',self.take(2))[0]
        if code==(0xdd if kind=='array' else 0xdf):return struct.unpack('>I',self.take(4))[0]
        raise ValueError('Expected EC '+kind+' header')


class Formats:
    def __init__(self, code_root=None):
        self.cache={};self.sources={}
        self.source=Path(code_root)/'common-source' if code_root else SOURCE
        self.unions={}
        resolver=self.source/'MessagePack.Resolvers/GeneratedStaticGSerializableFormatterResolver.cs'
        if resolver.exists():
            self.unions={int(opcode):name for opcode,name in re.findall(
                r'(\d+) => .*?new StaticGSerializableFormatter<([^>]+)>', resolver.read_text('utf-8-sig'))}
        self.abstracts={}

    def abstract(self, typ):
        if typ not in self.abstracts:
            path=self.source/'Common'/(typ+'.cs')
            text=path.read_text('utf-8-sig') if path.exists() else ''
            self.abstracts[typ]=bool(re.search(r'public abstract class '+re.escape(typ)+r'\b',text))
        return self.abstracts[typ]
    def schema(self,typ):
        typ=typ.removeprefix('Common.').removeprefix('UnityEngine.')
        if typ in self.cache:return self.cache[typ]
        path=self.source/'MessagePack.Formatters.Common'/(typ.replace('.','_')+'Formatter.cs')
        if not path.exists():path=self.source/'MessagePack.Formatters.UnityEngine'/(typ+'Formatter.cs')
        if not path.exists():self.cache[typ]=None;return None
        text=path.read_text(encoding='utf-8-sig').split('public '+typ+' Deserialize',1)[0]
        self.sources[typ]=path
        if not re.search(r'Write(?:FixedArrayHeaderUnsafe|ArrayHeader)\(ref bytes, offset, \d+\)',text):
            if 'WriteInt32(ref bytes, offset' in text:self.cache[typ]='enum';return 'enum'
            self.cache[typ]=None;return None
        fields=[]
        for line in text.splitlines():
            if 'offset += MessagePackBinary.WriteNil(ref bytes, offset);' in line:
                fields.append(('_unused_'+str(len(fields)),'Nil'));continue
            m=re.search(r'GetFormatterWithVerify<(.+)>\(formatterResolver\)\.Serialize\(ref bytes, offset, value\.(\w+),',line)
            if m:fields.append((m[2],m[1]));continue
            m=re.search(r'MessagePackBinary\.Write(\w+)\(ref bytes, offset, value\.(\w+)\)',line)
            if m:fields.append((m[2],m[1]))
        count=int(re.search(r'Write(?:FixedArrayHeaderUnsafe|ArrayHeader)\(ref bytes, offset, (\d+)\)',text)[1])
        if len(fields)!=count:raise ValueError('Unsupported formatter '+typ)
        self.cache[typ]=fields;return fields
    def read(self,r,typ):
        typ=typ.strip().removeprefix('Common.').removeprefix('UnityEngine.')
        if typ in ['Single','float']:return struct.unpack('>f',r.take(4))[0]
        if typ in ['Double','double']:
            value=r.value()
            if not isinstance(value,(int,float)):raise ValueError('Expected MessagePack Double')
            return float(value)
        if typ in ['Vector2','Vector3','Vector4','Quaternion']:
            if typ=='Vector2' and r.count('array')!=2:raise ValueError('Vector2 must have two fields')
            return [struct.unpack('>f',r.take(4))[0] for _ in range({'Vector2':2,'Vector3':3}.get(typ,4))]
        if r.data[r.pos]==0xc0:r.pos+=1;return None
        if typ=='Nil':raise ValueError('Expected reserved nil field')
        if typ=='byte[]':return list(r.value())
        if typ in ['Boolean','bool','Int16','Int32','Int64','UInt32','UInt64','Byte','SByte','String','string','int','long','byte','short','uint','ushort']:return r.value()
        if typ.endswith('[]'):
            return [self.read(r,typ[:-2]) for _ in range(r.count('array'))]
        generic=re.fullmatch(r'(\w+)<(.+)>',typ)
        if generic:
            inner=args(generic[2])
            if generic[1] in ['List','HashSet','IReadOnlyList','IList','IEnumerable']:return [self.read(r,inner[0]) for _ in range(r.count('array'))]
            if generic[1] in ['Dictionary','SortedDictionary','IReadOnlyDictionary']:
                return {str(self.read(r,inner[0])):self.read(r,inner[1]) for _ in range(r.count('map'))}
            if generic[1]=='Nullable':return self.read(r,inner[0])
        schema=self.schema(typ)
        if schema is None and self.abstract(typ):
            # The official static resolver selects generated concrete formatters
            # with a raw little endian ushort discriminator (zero means null).
            opcode=struct.unpack('<H',r.take(2))[0]
            if opcode==0:return None
            concrete=self.unions.get(opcode)
            if concrete is None:raise ValueError(f'Unknown official {typ} opcode {opcode}')
            value=self.read(r,concrete)
            if not isinstance(value,dict):raise ValueError('Union formatter must return an object: '+concrete)
            return dict(value,_type=concrete)
        if schema is None:return r.value()
        if schema=='enum':return r.value()
        count=r.count('array')
        if count>len(schema):raise ValueError(f'{typ} fields {count} > {len(schema)}')
        result={}
        for i,(name,kind) in enumerate(schema):
            start=r.pos
            try:
                result[name]=(self.read(r,kind) if i<count else (False if kind in ['bool','Boolean'] else 0 if kind in ['Int32','Int64','Single','int','float'] or self.schema(kind)=='enum' else None))
            except Exception as error:
                raise ValueError(f'{typ}.{name} ({kind}) at {start}: {error}') from error
        return result
    def component(self,name,data):
        if data is None:return {}
        info=name+'Info'
        # These concrete components inherit their configuration type without
        # declaring a new Info class. Use the pinned generic base's schema.
        info={
            'Common.StageBehStageNpcAssistBattleComponent':'Common.StageBehStageComponentInfo',
            'Common.StageBehStageConsumingRandomEventComponent':'Common.StageBehStageComponentInfo',
            'Common.WorldBulletTargetStateCompleteComponent':'Common.WorldBulletTargetBaseComponentInfo',
            'Common.StageResultChangeMapComponent':'Common.LoStageResultChangeMapComponentInfo',
        }.get(name,info)
        r=Reader(data);value=self.read(r,info)
        if r.pos!=len(data):raise ValueError(f'{name}: consumed {r.pos}/{len(data)}')
        return value
