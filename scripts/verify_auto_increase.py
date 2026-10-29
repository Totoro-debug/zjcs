"""Run the pinned C# auto-increase math with thin player/time adapters."""
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / '.tools/source-packages/official-code/common-source/Common'
OUT = ROOT / '.tools/gameplay-fixtures/auto-increase'


def method(text, name):
    start = text.index('private long ' + name) if name.startswith('Get') else text.index('private void ' + name)
    brace = text.index('{', start)
    depth = 1
    end = brace + 1
    while depth:
        depth += (text[end] == '{') - (text[end] == '}')
        end += 1
    return text[start:end]


def build():
    OUT.mkdir(parents=True, exist_ok=True)
    player_source = (SOURCE / 'PlayerItemDataWrap.cs').read_text('utf-8-sig')
    read = method(player_source, 'GetActualCountWithAutoIncrease')
    write = method(player_source, 'SetCountWithAutoIncrease')
    # Notification/logging does not alter count/param math. Preserve the whole
    # original calculation and final assignment; replace only callback dispatch.
    write = write[:write.index('if (itemParamAutoIncrease != null && _Count != num2)')] + '_Count = num2;\n}'
    stubs = r'''
using System;
using System.Collections.Generic;
namespace Common {
public class PlayerItemParamAutoIncreaseWrap {
 public double Decimal, IncreaseSpeed, BonusSeconds; public long MaxCount;
 public DateTime LastUpdateTime;
}
public class SegmentedIncreaseData { public DateTime StartTime,EndTime; public double IncreaseSpeed; public long MaxCount; }
public class Provider { public List<SegmentedIncreaseData> GetSegmentedIncreaseData(PlayerItemDataWrap i,DateTime a,DateTime b){return null;} }
public static class MathUtils { public static long Clamp(long n,long a,long b){return Math.Max(a,Math.Min(n,b));} }
public static class Formula { public static float Int2Float(int n){return n/10000f;} }
public static class TimeUtils { public static double SecondToHour(double s){return s/3600.0;} public static double HourToSecond(double h){return h*3600.0;} }
public class TimeManager { public static TimeManager Instance=new TimeManager(); public DateTime Now=new DateTime(2026,10,15);
 public double CalcElapsedSecond(DateTime t){return (Now-t).TotalSeconds;} public double CalcElapsedHour(DateTime t){return (Now-t).TotalHours;}
 public DateTime GetDateTime(){return Now;}
}
public class ItemEffectAutoIncrease {}
public class ItemInfo { public int ClassId=56; public T GetEffect<T>() where T:new(){return new T();} }
public class ItemLimit { public long MaxCount=1000000000; }
public class ItemLimitParser { public ItemLimit Value=new ItemLimit(); public ItemLimit Get(int id){return Value;} }
public class HangUpRewardInfo { public int RewardRuleId; }
public class RewardParser { public HangUpRewardInfo Get(int id,Player p){return new HangUpRewardInfo();} }
public class RuleParser { public List<Tuple<int,int>> GetRewardItemsAndProbabilities(int id,Player p){return new List<Tuple<int,int>>{Tuple.Create(56,1)};} }
public class ItemParser { public ItemInfo Get(int id){return new ItemInfo();} }
public enum OutputAdditionPropType { None }
public class OutputSetting { public Dictionary<int,OutputAdditionPropType> ItemIncreaseSpeedAddPercentTypeDict=new Dictionary<int,OutputAdditionPropType>(); public Dictionary<int,OutputAdditionPropType> ItemMaxCountAddPercentTypeDict=new Dictionary<int,OutputAdditionPropType>(); }
public class Settings { public OutputSetting OutputAdditionSetting=new OutputSetting(); }
public class Parsers { public static Parsers Instance=new Parsers(); public static Settings GameSettings=new Settings();
 public ItemLimitParser ItemCountLimitInfoParser=new ItemLimitParser(); public RewardParser HangUpRewardInfoParser=new RewardParser(); public RuleParser RewardRuleInfoParser=new RuleParser(); public ItemParser ItemInfoParser=new ItemParser(); }
public class HangUpInfo { public int RewardId; }
public class HangData { public Dictionary<int,double> AutoIncreaseItemTimeDict=new Dictionary<int,double>(); }
public class Player { public PlayerItemDataWrap Item; public int OutputPercent; public HangData HangUpData=new HangData();
 public PlayerItemDataWrap GetItemById(int id){return Item;} public int GetOutputAdditionPropValue(OutputAdditionPropType p){return OutputPercent;} }
public interface IHangUpRewardHandlerBase { void SettleHangUpReward(Player p,HangUpInfo h,double hour,double scaled,bool addition); ItemInfo GetRewardItem(HangUpInfo h,Player p); }
public class PlayerItemDataWrap {
 public long _Count; public ItemInfo Info=new ItemInfo(); public int Id=56;
 public PlayerItemParamAutoIncreaseWrap Param=new PlayerItemParamAutoIncreaseWrap();
 public Func<Provider> AutoIncreasementPropProviderGetter;
 public PlayerItemParamAutoIncreaseWrap GetItemParamAutoIncrease(){return Param;}
 public long ActualCountWithoutAutoIncrease {get{return _Count;}}
 public long Read(){return GetActualCountWithAutoIncrease();}
 public void SetCount(long n){SetCountWithAutoIncrease(n);}
/*METHODS*/
}
public static class AutoIncreaseHarness {
 public static List<Dictionary<string,object>> Run(){
 var rows=new List<Dictionary<string,object>>(); var rng=new Random(20261015);
 for(int i=0;i<640;i++){
 double speed=i<10?0.25:(rng.Next(1,2001)/400.0);
 long max=i<10?15:rng.Next(1,120); long count=i<10?new long[]{0,0,14,15,19,0,1,14,15,0}[i]:rng.Next(0,160);
 double dec=i<10?0:rng.Next(0,10000)/10000.0;
 double elapsed=i<10?new double[]{0,3600,7200,0,0,0,14400,1800,14400,3600}[i]:rng.Next(0,200000);
 double bonus=i<10?0:rng.Next(0,10000);
 double hours=i<10?new double[]{40,0,8,40,1,1,2,0.1,1,0.25}[i]:rng.Next(0,30000)/1000.0;
 int percent=i>=10 && i%3==0?1234:0; bool addition=i%2==0;
 var item=new PlayerItemDataWrap();item._Count=count;item.Param.IncreaseSpeed=speed;item.Param.MaxCount=max;item.Param.Decimal=dec;item.Param.BonusSeconds=bonus;item.Param.LastUpdateTime=TimeManager.Instance.Now.AddSeconds(-elapsed);
 var p=new Player();p.Item=item;p.OutputPercent=percent;
 var before=item.Read();
 new AutoIncreaseItemHangUpRewardHandler().SettleHangUpReward(p,new HangUpInfo(),hours,1,addition);
 double remainder;p.HangUpData.AutoIncreaseItemTimeDict.TryGetValue(56,out remainder);
 rows.Add(new Dictionary<string,object>{{"count",count},{"speed",speed},{"maximum",max},{"elapsed",elapsed},{"decimal",dec},{"bonus_seconds",bonus},{"hours",hours},{"output_percent",percent},{"has_output_addition",addition},{"actual_before",before},{"stored_after",item._Count},{"actual_after",item.Read()},{"decimal_after",item.Param.Decimal},{"bonus_after",item.Param.BonusSeconds},{"elapsed_after",TimeManager.Instance.CalcElapsedSecond(item.Param.LastUpdateTime)},{"time_dict_after",remainder}});
 }
 return rows;
 }
}
}
'''.replace('/*METHODS*/', read + '\n' + write)
    adapter = OUT / 'Adapter.cs'
    adapter.write_text(stubs, encoding='utf-8')
    names = ['AutoIncreaseItemUtils.cs', 'AutoIncreaseItemPropUtils.cs',
             'AutoIncreaseItemOutputAdditionUtils.cs', 'AutoIncreaseItemHangUpRewardHandler.cs']
    files = [adapter, *(SOURCE / n for n in names)]
    # Original decompiler output uses file-scoped namespaces; Add-Type's current
    # compiler supports them. The source files remain unchanged.
    runner = OUT / 'run.ps1'
    vector_file = OUT / 'vectors.json'
    runner.write_text("$ErrorActionPreference='Stop'\nAdd-Type -Path @(\n" +
                      ',\n'.join("'" + str(p).replace("'", "''") + "'" for p in files) +
                      ")\n[Common.AutoIncreaseHarness]::Run() | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8NoBOM -LiteralPath '" +
                      str(vector_file).replace("'", "''") + "'\n", encoding='utf-8')
    subprocess.run(['pwsh', '-NoProfile', '-File', str(runner)], check=True)
    rows = json.loads(vector_file.read_text('utf-8'))
    receipts = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in files}
    receipts['PlayerItemDataWrap.cs'] = hashlib.sha256((SOURCE / 'PlayerItemDataWrap.cs').read_bytes()).hexdigest()
    (OUT / 'source-receipts.json').write_text(json.dumps(dict(
        manifest='854_156383', vectors=len(rows), sources=receipts,
        adaptations=['Time/player/parser wrappers', 'SetCount notifications and logging excluded; original math and count assignment retained']), indent=2) + '\n', encoding='utf-8')
    print('Pinned auto-increase C# vectors:', len(rows))


if __name__ == '__main__':
    build()
