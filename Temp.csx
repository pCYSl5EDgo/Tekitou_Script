using System;
using Tekito;
using System.Collections.Generic;

byte NormalActivateCount(byte level) => level;
byte Per2LV(byte level) => (byte)(level << 1);
byte Per4LV(byte level) => (byte)(level << 2);
byte Per6LV(byte level) => (byte)(6 * level);
byte Per10LV(byte level) => (byte)(10 * level);

Func<World, BattleUnit, ChainComponent, bool> このターン終了時まで使えるスキルCondition(World world) => (_, __, ___) => _.Turn == world.Turn;

IEnumerable<BattleUnit> 所持しているユニット(World world, ushort baseSkillId){
    var c = world.TeamDictionary.Values.GetEnumerator();
    while(c.MoveNext()){
        var ls = c.Current;
        for(int i = 0; i < ls.Count; i++){
            var bsd = ls[i].BattleSkill.Values.GetEnumerator();
            while(bsd.MoveNext()){
                var bs = bsd.Current;
                if(bs.Skill.Id == baseSkillId)
                    yield return ls[i];
            }
        }
    }
}
Func<World, BattleUnit, ChainComponent, bool> オーナー以外の所持しているユニットがあるやいなや(ushort baseSkillId){
    return (world, owner, cc) => {
        var c = world.TeamDictionary.Values.GetEnumerator();
        while(c.MoveNext()){
            var ls = c.Current;
            for(int i = 0; i < ls.Count; i++){
                if(ls[i] == owner) continue;
                var bsd = ls[i].BattleSkill.Values.GetEnumerator();
                while(bsd.MoveNext()){
                    var bs = bsd.Current;
                    if(bs.Skill.Id == baseSkillId) return true;
                }
            }
        }
        return false;
    };
}
bool 最低レアリティ(World world, BattleUnit owner, ChainComponent cc){
    var c = world.TeamDictionary.Values.GetEnumerator();
    byte ownerRarity = (byte)owner.CurrentRarity;
    while(c.MoveNext()){
        var ls = c.Current;
        for(int i = 0; i < ls.Count; i++){
            if(ls[i] == owner) continue;
            if((byte)ls[i].CurrentRarity <= ownerRarity)
                return false;
        }
    }
    return true;
}
bool DamageCalc2D20LVExecute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;    
    owner.DamageDices.Add(new DamageDice(2, (byte)(cc.Skill.Level * 20)));
    return true;
}

Skill かばう = new Skill(nameof(かばう), _ => $"自分以外の味方を攻撃対象とした敵の攻撃宣言時に発動できる。攻撃対象を自分に変更する。", Skill.DefaultPercentage, NormalActivateCount, Timing.DeclarationAttack){
    Condition = かばうCondition, Execute = かばうExecute
};
bool かばうCondition(World world, BattleUnit owner, ChainComponent cc) => world.CurrentDefender != null && world.CurrentDefender != owner && world.CurrentDefender.Team != owner.Team;
bool かばうExecute(World world, BattleUnit owner, ChainComponent cc){ 
    if(!world.IsAlive(owner)) return false;
    world.CurrentDefender = owner;
    return true;
}

Skill 回避 = new Skill(nameof(回避), _ => $"自分を攻撃対象とした敵の攻撃宣言時に{4 * _}%の確率で発動する。攻撃を無効にする。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack) {
    IsForceActivate = true,
    Condition = 回避Condition,
    Execute = 回避Execute
};
bool 回避Condition(World world, BattleUnit owner, ChainComponent cc) => world.CurrentDefender == owner && world.CurrentAttacker.Team != owner.Team;
bool 回避Execute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;    
    world.NegateAttack(); return true;
}

Skill 怪力 = new Skill(nameof(怪力), _ => $"自分の攻撃宣言時に発動する。ダメージ計算時に1D{20 * _}を加える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack) { IsForceActivate = true, Condition = 怪力Condition, Execute = 怪力Execute };
bool 怪力Condition(World world, BattleUnit owner, ChainComponent cc) => world.CurrentAttacker == owner;
bool 怪力Execute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;    
    owner.DamageDices.Add(new DamageDice(1, (byte)(cc.Skill.Level * 20)));
    return true;
}

Skill クリティカル = new Skill(nameof(クリティカル), _ => $"自分の攻撃宣言時に{4 * _}%の確率で発動する。攻撃終了時まで攻撃力を発動時の攻撃力分上げる。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack) { IsForceActivate = true, IsReferAttack = true, Condition = クリティカルCondition, Cost = クリティカルCost, Execute = クリティカルExecute };
bool クリティカルCondition(World world, BattleUnit owner, ChainComponent cc) => world.CurrentAttacker == owner;
bool クリティカルCost(World world, BattleUnit owner, ChainComponent cc){
    cc.Bag = owner.CurrentAttack;
    return world.CurrentAttacker == owner;
}
bool クリティカルExecute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;    
    var counter = new StatusCounter((int)cc.Bag);
    counter.WhenToRemove = WhenToRemoveHelper.AfterAttackRemove;
    owner.AttackChange.Add(counter);
    return true;
}

Skill 先制 = new Skill(nameof(先制), _ => $"ターン開始時に{10 * _}%の確率で発動する。自分はこのターン先に攻撃できる。\n※注釈：先に攻撃せずとも通常の攻撃順で攻撃することは可能である。", Per10LV, Skill.DefaultActivate, Timing.StartTurn) { IsForceActivate = true, IsReferAttackOrder = true, Execute = 先制Execute };
bool 先制Execute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;
    world.FastUnitThisTurn.AddFirst(owner);
    return true;
}

Skill 特効_力 = new Skill("特効（力）", _ => $"力属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.Power) };
Skill 特効_技 = new Skill("特効（技）", _ => $"技属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.Skill) };
Skill 特効_魔 = new Skill("特効（魔）", _ => $"魔属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.Magic) };
Skill 特効_無 = new Skill("特効（無）", _ => $"無属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.None) };
Func<World, BattleUnit, ChainComponent, bool> 特効Condition(Kind kind) => (world, owner, cc) => world.CurrentAttacker == owner && (world.CurrentDefender.Kind & kind) != 0;

Skill 下克上 = new Skill(nameof(下克上), _ => $"自分のレアリティが最も低い場合、自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack) { IsReferRarity = true, IsForceActivate = true , Condition = 最低レアリティ, Cost = 最低レアリティ, Execute = DamageCalc2D20LVExecute };

Skill 経験値 = new Skill("経験値＋", _ => $"戦闘による獲得経験値が{2 * _}増加する", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.None) { IsForceActivate = true };

Skill 肉染み = new Skill(nameof(肉染み), _ => $"自分が攻撃されず自分の味方が攻撃されたターンの終了時に発動する。敵全てに{_}D20ダメージを与える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.EndTurn)
{
    IsForceActivate = true, IsBurn = true, Condition = 肉染みCondition, Execute = 肉染みExecute
};
bool 肉染みCondition(World world, BattleUnit owner, ChainComponent cc) => !world.AttackedUnitsThisTurn.Contains(owner);
bool 肉染みExecute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;
    var d = cc.Skill.Level.D(20);
    var enemy = world.GetEnemyUnits(owner);
    for(int i = 0; i < enemy.Count; ++i){
        world.GiveDamage(enemy[i], d, Reason.Skill);
    }
    return true;
}

Skill バトンタッチ = new Skill(nameof(バトンタッチ), _ => $"自分の攻撃終了時に【バトンタッチ】を所持していない味方１体を対象にして発動する。このターン終了時まで、対象の速度をこのスキルを発動した時の自分の速度×{20 * _}%と同じ数値になるように調節する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack) {
    IsForceActivate = true, IsReferAgility = true, Condition = バトンタッチCondition, Cost = バトンタッチCost
};
bool バトンタッチCondition(World world, BattleUnit owner, ChainComponent cc){
    var e = world.TeamDictionary.GetEnumerator();
    while(e.MoveNext()){
        if(e.Current.Key != owner.Team) continue;
        var ls = e.Current.Value;
        for(int i = 0; i < ls.Count; i++){
            bool ihaveno = true;
            var c = ls[i].BattleSkill.Values.GetEnumerator();
            while(c.MoveNext()){
                var sk = c.Current.Skill;
                if(sk.SimpleName == nameof(バトンタッチ))
                    ihaveno = false;
            }
            if(ihaveno)
                return true;
        }
    }
    return false;
}
bool バトンタッチCost(World world, BattleUnit owner, ChainComponent cc){
    var list = new List<string>(3);
    var bulist = new List<BattleUnit>(3);
    var e = world.TeamDictionary.GetEnumerator();
    while(e.MoveNext()){
        if(e.Current.Key != owner.Team) continue;
        var ls = e.Current.Value;
        for(int i = 0; i < ls.Count; i++){
            bool ihaveno = true;
            var c = ls[i].BattleSkill.Values.GetEnumerator();
            while(c.MoveNext()){
                var sk = c.Current.Skill;
                if(sk.SimpleName == nameof(バトンタッチ))
                    ihaveno = false;
            }
            if(ihaveno){
                list.Add(ls[i].OriginalData.Name);
                bulist.Add(ls[i]);
            }
        }
    }
    var bu = world.SelectChoice("対象を選んでください", list.ToArray());
    cc.ObjectUnit = bulist[bu];
    cc.Bag = cc.Skill.Level * owner.CurrentAgility / 5;
    return list.Count != 0;
}
bool バトンタッチExecute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner) || !world.IsAlive(cc.ObjectUnit)) return false;
    var counter = new StatusCounter((int)cc.Bag - cc.ObjectUnit.CurrentAgility){WhenToRemove = WhenToRemoveHelper.TurnEndRemove};
    cc.ObjectUnit.AgilityChange.Add(counter);
    return true;
}


Skill 鬼殺し = new Skill(nameof(鬼殺し), _ => $"自分が【クリティカル】を持たず、自分のレアリティが最も低い場合、{6 * _}%の確率で攻撃宣言時に発動する。攻撃終了時まで攻撃力を発動時の攻撃力分上げる。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack) { IsReferRarity = true, IsForceActivate = true, IsReferAttack = true, Condition = 鬼殺しCondition, Cost = 鬼殺しCost, Execute = 鬼殺しExecute };
bool 鬼殺しCondition(World world, BattleUnit owner, ChainComponent cc){
    for(int i = 0; i < owner.BattleSkill.Count; ++i){
        if(owner.BattleSkill[i].Skill.Name == "クリティカル"){
            return 最低レアリティ(world, owner, cc);
        }
    }
    return false;
}
bool 鬼殺しCost(World world, BattleUnit owner, ChainComponent cc){
    for(int i = 0; i < owner.BattleSkill.Count; ++i){
        if(owner.BattleSkill[i].Skill.Name == "クリティカル"){
            if(最低レアリティ(world, owner, cc)){
                cc.Bag = owner.CurrentAttack;
                return true;
            }
            else return false;
        }
    }
    return false;
}
bool 鬼殺しExecute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;
    owner.AttackChange.Add(new StatusCounter((int)cc.Bag){WhenToRemove = WhenToRemoveHelper.AfterAttackRemove});
    return true;
}

Skill 革命の旗頭 = new Skill(nameof(革命の旗頭), _ => $"味方が誰も【革命の旗頭】を持たない場合にターン開始時に発動する。このターン、自分はダメージを与えるスキルを発動できず、攻撃宣言できないようにしてもよい。そうした場合、ターン終了時まで味方１体に（１）～（４）を付与する。\n（１）自分のレアリティを一つ下げる。\n（２）自分よりレアリティが高いユニットを攻撃対象とする自分の攻撃宣言時に発動する。3D{10 * _}をダメージ計算時に加える。\n（３）他にレアリティの変化が発生した場合に発動する。（１）を消失する。\n（４）【革命の旗頭】を発動した味方が戦場から消失した場合に発動する。（１）（２）を消失する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.StartTurn)
{ IsReferRarity = true, IsForceActivate = true, IsEnchant = true };

Skill 牽引 = new Skill(nameof(牽引), _ => $"自分の攻撃後に味方1人を対象として発動する。対象に（１）（２）を付与する。\n（１）自分の攻撃宣言時に発動する。1D{20 * _}をダメージ計算時に加える。\n（２）【牽引】を発動した味方が戦場から消失した場合、（１）を消失する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack)
{ IsForceActivate = true, IsEnchant = true };
bool 牽引Condition(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;
    if(world.TeamDictionary[owner.Team].Count == 1) return false;
    throw new NotImplementedException();
}

Skill なぎ払い = new Skill(nameof(なぎ払い), _ => $"自分の攻撃宣言時に{4 * _}%の確率で発動する。自分は敵全てに攻撃する。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack)
{ IsForceActivate = true };

Skill ザラキ = new Skill(nameof(ザラキ), _ => $"「パンドラボックス」ユニットのみ発動可能。自分の攻撃後に発動する。敵1体毎に{2 * _}%の確率で即死判定を行う。即死判定に成功した敵の生命力を0にする。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack)
{ IsForceActivate = true };

Skill 突撃の大号令 = new Skill(nameof(突撃の大号令), _ => $"このターン誰も攻撃宣言をそれ以前に行っていない場合に自分の味方の攻撃宣言時に発動できる。ターン終了時まで自分は攻撃宣言を行えない。攻撃する味方に（１）（２）を付与する。\n（１）攻撃宣言時に発動する。1D{20 * _}をダメージ計算時に加える。\n（２）自分を含む味方に攻撃順序または素早さを参照するスキルがこのスキルと【突撃の大号令】以外に存在せず、攻撃宣言時に{4 * _}%の確率で自分の攻撃力を上げる/下げる効果の影響を排除して発動する。ターン終了時まで自分の攻撃力をこのスキルを発動した時点の攻撃力分上げる。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack)
{ IsReferAttackOrder = true, IsEnchant = true };

Skill 布石 = new Skill(nameof(布石), _ => $"自分の攻撃宣言時に発動する。この攻撃を無効としてもよい。その場合、攻撃対象に（１）をターン終了時まで付与する。\n（１）自分が攻撃対象となった攻撃宣言時に{6 * _}%の確率で発動する。攻撃後まで、自分の守備力をこのスキルの発動時の半分下げる。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack)
{ IsForceActivate = true, IsEnchant = true };
bool 布石Execute(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner) || !world.IsAlive(world.CurrentDefender)) return false;
    var tf = world.SelectChoice("この攻撃を無効にして、敵に効果を付与しますか？", "Yes", "No") == 0;
    if(!tf) return false;
    cc.ObjectUnit = world.CurrentDefender;    
    var sk = new Skill(owner.OriginalData.Name+"の布石付与（１）", _=>$"自分が攻撃対象の攻撃宣言時に{6*_}%の確率で発動する。攻撃後まで、自分の守備力をこのスキルの発動時の[1/2 小数点以下切り上げ]分下げる。", Per6LV, Skill.DefaultActivate, Timing.DeclarationAttack)
    {IsForceActivate = true, IsReferDefense = true, Condition = このターン終了時まで使えるスキルCondition(world),Cost = 布石Cost_1, Execute = 布石Execute_1, Level = cc.Skill.Level };
    var bs = new BattleSkill(sk);
    cc.ObjectUnit.BattleSkill[bs.Id] = bs;
    return true;
}
bool 布石Cost_1(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;
    cc.Bag = owner.CurrentDefense / 2;
    return true;
}
bool 布石Execute_1(World world, BattleUnit owner, ChainComponent cc){
    if(!world.IsAlive(owner)) return false;
    owner.DefenseChange.Add(new StatusCounter((int)cc.Bag){WhenToRemove = WhenToRemoveHelper.AfterAttackRemove});
    return true;
}

Skill 怨嗟 = new Skill(nameof(怨嗟), _ => $"味方に【怨嗟】を持つものがいない場合に、自分の味方が倒れた時に発動する。敵全体に{10 * _}ダメージを与える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.Event)
{ IsForceActivate = true, IsBurn = true };


new Skill[]{かばう, 回避, 怪力, クリティカル, 先制, 特効_力, 特効_技, 特効_魔, 特効_無, 下克上, 経験値, 肉染み, バトンタッチ, 鬼殺し, 革命の旗頭, 牽引, なぎ払い, ザラキ, 突撃の大号令, 布石, 怨嗟}