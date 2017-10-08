using System;
using Tekito;
using System.Collections.Generic;

byte NormalActivateCount(byte level) => level;
byte Per2LV(byte level) => (byte)(level << 1);
byte Per4LV(byte level) => (byte)(level << 2);
byte Per6LV(byte level) => (byte)(6 * level);
byte Per10LV(byte level) => (byte)(10 * level);

Func<World, BattleUnit, ChainComponent, bool> このターン終了時まで使えるスキルCondition(World world) => (_, __, ___) => _.Turn == world.Turn;

IEnumerable<BattleUnit> 所持しているユニット(World world, ushort baseSkillId)
{
    var c = world.TeamDictionary.Values.GetEnumerator();
    while (c.MoveNext())
    {
        var ls = c.Current;
        for (int i = 0; i < ls.Count; i++)
        {
            var bsd = ls[i].BattleSkill.GetEnumerator();
            while (bsd.MoveNext())
            {
                var bs = bsd.Current;
                if (bs.Skill.Id == baseSkillId)
                    yield return ls[i];
            }
        }
    }
}
Func<World, BattleUnit, ChainComponent, bool> オーナー以外の所持しているユニットがあるやいなや(ushort baseSkillId)
{
    return (world, owner, cc) =>
    {
        var c = world.TeamDictionary.Values.GetEnumerator();
        while (c.MoveNext())
        {
            var ls = c.Current;
            for (int i = 0; i < ls.Count; i++)
            {
                if (ls[i] == owner) continue;
                var bsd = ls[i].BattleSkill.GetEnumerator();
                while (bsd.MoveNext())
                {
                    var bs = bsd.Current;
                    if (bs.Skill.Id == baseSkillId) return true;
                }
            }
        }
        return false;
    };
}
bool 一回だけCondition(World world, BattleUnit owner, ChainComponent cc) => cc.Skill.UsedCount == 0 && owner != null && world.IsAlive(owner);
bool 最高レアリティ(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc)) return false;
    var c = world.TeamDictionary.Values.GetEnumerator();
    byte ownerRarity = (byte)owner.CurrentRarity;
    while (c.MoveNext())
    {
        var ls = c.Current;
        for (int i = 0; i < ls.Count; i++)
        {
            if (ls[i] == owner) continue;
            if ((byte)ls[i].CurrentRarity >= ownerRarity)
                return false;
        }
    }
    return true;
}
bool 最低レアリティ(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc)) return false;
    var c = world.TeamDictionary.Values.GetEnumerator();
    byte ownerRarity = (byte)owner.CurrentRarity;
    while (c.MoveNext())
    {
        var ls = c.Current;
        for (int i = 0; i < ls.Count; i++)
        {
            if (ls[i] == owner) continue;
            if ((byte)ls[i].CurrentRarity <= ownerRarity)
                return false;
        }
    }
    return true;
}
bool DamageCalc3D10LVExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    owner.DamageDices.Add(new DamageDice(3, (byte)(cc.Skill.Level * 10)));
    return true;
}
bool DamageCalc2D20LVExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    owner.DamageDices.Add(new DamageDice(2, (byte)(cc.Skill.Level * 20)));
    return true;
}

Skill かばう = new Skill(nameof(かばう), _ => $"自分以外の味方を攻撃対象とした敵の攻撃宣言時に発動できる。攻撃対象を自分に変更する。", Skill.DefaultPercentage, NormalActivateCount, Timing.DeclarationAttack | Timing.UnitAction | Timing.AfterAttack)
{
    Condition = かばうCondition,
    Execute = かばうExecute
};
bool かばうCondition(World world, BattleUnit owner, ChainComponent cc)
{
    if (world.Timing == Timing.UnitAction || world.Timing == Timing.AfterAttack)
    {
        cc.Skill.Bag = null;
        return false;
    }
    if (world.IsAlive(owner) && world.CurrentDefender != null && world.CurrentDefender != owner && world.CurrentDefender.Team == owner.Team && world.IsAlive(world.CurrentDefender))
    {
        if (cc.Skill.Bag == null)
        {
            cc.Skill.Bag = "";
            return true;
        }
    }
    return false;
}
bool かばうExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    world.CurrentDefender = owner;
    return true;
}

Skill 回避 = new Skill(nameof(回避), _ => $"自分を攻撃対象とした敵の攻撃宣言時に{4 * _}%の確率で発動する。攻撃を無効にする。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{
    IsForceActivate = true,
    Condition = 回避Condition,
    Execute = 回避Execute
};
bool 回避Condition(World world, BattleUnit owner, ChainComponent cc) => 一回だけCondition(world, owner, cc) && world.CurrentDefender == owner && world.CurrentAttacker.Team != owner.Team;
bool 回避Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    world.NegateAttack(); return true;
}

Skill 怪力 = new Skill(nameof(怪力), _ => $"自分の攻撃宣言時に発動する。ダメージ計算時に1D{20 * _}を加える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsForceActivate = true, Condition = 怪力Condition, Execute = 怪力Execute };
bool 怪力Condition(World world, BattleUnit owner, ChainComponent cc) => 一回だけCondition(world, owner, cc) && world.CurrentAttacker == owner;
bool 怪力Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    owner.DamageDices.Add(new DamageDice(1, (byte)(cc.Skill.Level * 20)));
    return true;
}

Skill クリティカル = new Skill(nameof(クリティカル), _ => $"自分の攻撃宣言時に{4 * _}%の確率で発動する。攻撃終了時まで攻撃力を発動時の攻撃力分上げる。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsForceActivate = true, IsReferAttack = true, Condition = クリティカルCondition, Cost = クリティカルCost, Execute = クリティカルExecute };
bool クリティカルCondition(World world, BattleUnit owner, ChainComponent cc) => 一回だけCondition(world, owner, cc) && world.CurrentAttacker == owner;
bool クリティカルCost(World world, BattleUnit owner, ChainComponent cc)
{
    cc.Bag = owner.CurrentAttack;
    return world.CurrentAttacker == owner;
}
bool クリティカルExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    var counter = new StatusCounter((int)cc.Bag);
    counter.WhenToRemove = WhenToRemoveHelper.AfterAttackRemove;
    owner.AttackChange.Add(counter);
    return true;
}

Skill 先制 = new Skill(nameof(先制), _ => $"ターン開始時に{10 * _}%の確率で発動する。自分はこのターン先に攻撃できる。\n※注釈：先に攻撃せずとも通常の攻撃順で攻撃することは可能である。", Per10LV, Skill.DefaultActivate, Timing.StartTurn, SkillHelper.LastDuringThisTurn)
{ IsForceActivate = true, IsReferAttackOrder = true, Condition = 一回だけCondition, Execute = 先制Execute };
bool 先制Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    world.FastUnitThisTurn.AddFirst(owner);
    return true;
}

Skill 特効_力 = new Skill("特効（力）", _ => $"力属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.Power) };
Skill 特効_技 = new Skill("特効（技）", _ => $"技属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.Skill) };
Skill 特効_魔 = new Skill("特効（魔）", _ => $"魔属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.Magic) };
Skill 特効_無 = new Skill("特効（無）", _ => $"無属性を攻撃対象とした自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Per10LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack) { IsForceActivate = true, Execute = DamageCalc2D20LVExecute, Condition = 特効Condition(Kind.None) };
Func<World, BattleUnit, ChainComponent, bool> 特効Condition(Kind kind) => (world, owner, cc) => 一回だけCondition(world, owner, cc) && world.CurrentAttacker == owner && (world.CurrentDefender.Kind & kind) != 0;

Skill 下克上 = new Skill(nameof(下克上), _ => $"自分のレアリティが最も低い場合、自分の攻撃宣言時に発動する。ダメージ計算時に2D{20 * _}を加える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsReferRarity = true, IsForceActivate = true, Condition = 最低レアリティ, Cost = 最低レアリティ, Execute = DamageCalc2D20LVExecute };

Skill 経験値 = new Skill("経験値＋", _ => $"戦闘による獲得経験値が{2 * _}増加する", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.None) { IsForceActivate = true };

Skill 肉染み = new Skill(nameof(肉染み), _ => $"自分が攻撃されず自分の味方が攻撃されたターンの終了時に発動する。敵全てに{_}D20ダメージを与える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.EndTurn, SkillHelper.LastUntilNextTurn)
{
    IsForceActivate = true,
    IsBurn = true,
    Condition = 肉染みCondition,
    Execute = 肉染みExecute
};
bool 肉染みCondition(World world, BattleUnit owner, ChainComponent cc)
=> 一回だけCondition(world, owner, cc) && !world.DefendedUnitsThisTurn.Contains(owner);
bool 肉染みExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    var d = cc.Skill.Level.D(20);
    var enemy = world.GetEnemyUnits(owner);
    for (int i = 0; i < enemy.Count; ++i)
    {
        world.GiveDamage(owner, enemy[i], d, Reason.Skill);
    }
    return true;
}

Skill バトンタッチ = new Skill(nameof(バトンタッチ), _ => $"自分の攻撃終了時に【バトンタッチ】を所持していない味方１体を対象にして発動する。このターン終了時まで、対象の速度をこのスキルを発動した時の自分の速度×{20 * _}%と同じ数値になるように調節する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack, SkillHelper.LastDuringAfterAttack)
{
    IsForceActivate = true,
    IsReferAgility = true,
    Condition = バトンタッチCondition,
    Cost = バトンタッチCost
};
bool バトンタッチCondition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc)) return false;
    var e = world.TeamDictionary.GetEnumerator();
    while (e.MoveNext())
    {
        if (e.Current.Key != owner.Team) continue;
        var ls = e.Current.Value;
        for (int i = 0; i < ls.Count; i++)
        {
            bool ihaveno = true;
            var c = ls[i].BattleSkill.GetEnumerator();
            while (c.MoveNext())
            {
                var sk = c.Current.Skill;
                if (sk.SimpleName == nameof(バトンタッチ))
                    ihaveno = false;
            }
            if (ihaveno)
                return true;
        }
    }
    return false;
}
bool バトンタッチCost(World world, BattleUnit owner, ChainComponent cc)
{
    var list = new List<string>(3);
    var bulist = new List<BattleUnit>(3);
    var e = world.TeamDictionary.GetEnumerator();
    while (e.MoveNext())
    {
        if (e.Current.Key != owner.Team) continue;
        var ls = e.Current.Value;
        for (int i = 0; i < ls.Count; i++)
        {
            bool ihaveno = true;
            var c = ls[i].BattleSkill.GetEnumerator();
            while (c.MoveNext())
            {
                var sk = c.Current.Skill;
                if (sk.SimpleName == nameof(バトンタッチ))
                    ihaveno = false;
            }
            if (ihaveno)
            {
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
bool バトンタッチExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || !world.IsAlive(cc.ObjectUnit)) return false;
    var counter = new StatusCounter((int)cc.Bag - cc.ObjectUnit.CurrentAgility) { WhenToRemove = WhenToRemoveHelper.TurnEndRemove };
    cc.ObjectUnit.AgilityChange.Add(counter);
    return true;
}


Skill 鬼殺し = new Skill(nameof(鬼殺し), _ => $"自分が【クリティカル】を持たず、自分のレアリティが最も低い場合、{6 * _}%の確率で攻撃宣言時に発動する。攻撃終了時まで攻撃力を発動時の攻撃力分上げる。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsReferRarity = true, IsForceActivate = true, IsReferAttack = true, Condition = 鬼殺しCondition, Cost = 鬼殺しCost, Execute = 鬼殺しExecute };
bool 鬼殺しCondition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc)) return false;
    for (int i = 0; i < owner.BattleSkill.Count; ++i)
    {
        if (owner.BattleSkill[i].Skill.Name == "クリティカル")
        {
            return 最低レアリティ(world, owner, cc);
        }
    }
    return false;
}
bool 鬼殺しCost(World world, BattleUnit owner, ChainComponent cc)
{
    for (int i = 0; i < owner.BattleSkill.Count; ++i)
    {
        if (owner.BattleSkill[i].Name == "クリティカル")
        {
            if (最低レアリティ(world, owner, cc))
            {
                cc.Bag = owner.CurrentAttack;
                return true;
            }
            else return false;
        }
    }
    return false;
}
bool 鬼殺しExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    owner.AttackChange.Add(new StatusCounter((int)cc.Bag) { WhenToRemove = WhenToRemoveHelper.AfterAttackRemove });
    return true;
}

Skill 革命の旗頭 = new Skill(nameof(革命の旗頭), _ => $"味方が誰も【革命の旗頭】を持たない場合にターン開始時に発動する。このターン、自分はスキルではダメージを与えられず、攻撃宣言できないようにしてもよい。そうした場合、ターン終了時まで味方１体に（１）～（３）を付与し、レアリティを1つ下げる。\n（１）自分よりレアリティが高いユニットを攻撃対象とする自分の攻撃宣言時に発動する。3D{10 * _}をダメージ計算時に加える。\n（２）レアリティの変化が発生した場合に発動する。（１）～（３）を消失する。\n（３）【革命の旗頭】を発動した味方が倒れた場合に発動する。（１）～（３）を消失する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.StartTurn, SkillHelper.LastDuringThisTurn)
{ IsReferRarity = true, IsForceActivate = true, IsEnchant = true, Condition = 革命の旗頭Condition };
bool 革命の旗頭Condition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc)) return false;
    var ls = world.TeamDictionary[owner.Team];
    if (ls.Count == 1) return false;
    for (int i = 0; i < ls.Count; i++)
    {
        if (ls[i] == owner) continue;
        for (int j = 0; j < ls[i].BattleSkill.Count; j++)
            if (ls[i].BattleSkill[j].Skill.Id == 革命の旗頭.Id)
                return false;
    }
    return true;
}
/*
bool 革命の旗頭Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    var ls = world.TeamDictionary[owner.Team];
    if (ls.Count == 1) return false;
    for (int i = 0; i < ls.Count; i++)
    {
        if (ls[i] == owner) continue;
        for (int j = 0; j < ls[i].BattleSkill.Count; j++)
            if (ls[i].BattleSkill[j].Skill.Id == 革命の旗頭.Id)
                return false;
    }
    if (world.SelectChoice("【革命の旗頭】によって味方に付与しますか？", "YES", "NO") == 0)
    {
        owner.IsUnableDeclareAttack = true;
        owner.IsUnableGiveDamageSkill = true;
        var unit = world.SelectUnit("【革命の旗頭】でスキルを付与し、レアリティを下げる味方は誰にしますか？", _ => _ != owner && _.Team == owner.Team && _.Rarity != Rarity.None);
        unit.Rarity = (Rarity)((byte)unit.Rarity - 1);
        var kaku_field = new ChainComponent(new BattleSkill(革命の旗頭Field.Clone()));
        world.LingeringSkills.Add(kaku_field);
        var kaku1 = 革命の旗頭付与1.Clone();
        kaku1.Level = cc.Skill.Level;
        var kaku2 = 革命の旗頭付与2.Clone();
        kaku2.Level = cc.Skill.Level;
        var kaku3 = 革命の旗頭付与3.Clone();
        kaku3.Level = cc.Skill.Level;
        unit.BattleSkill.AddRange(new BattleSkill[] { new BattleSkill(kaku1), new BattleSkill(kaku2), new BattleSkill(kaku3) });
    }
    return true;
}
Skill 革命の旗頭付与1 = new Skill(nameof(革命の旗頭付与1), _ => $"自分よりレアリティが高いユニットを攻撃対象とする自分の攻撃宣言時に発動する。3D{10 * _}をダメージ計算時に加える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsForceActivate = true, IsAnonymous = true, IsReferRarity = true, Condition = 革命の旗頭付与1Condition, Cost = 革命の旗頭付与1Condition, Execute = DamageCalc3D10LVExecute };
bool 革命の旗頭付与1Condition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || world.CurrentDefender != null || world.CurrentAttacker != owner || !world.IsAlive(world.CurrentDefender)) return false;
    return (byte)world.CurrentDefender.CurrentRarity > (byte)owner.CurrentRarity;
}
Skill 革命の旗頭付与2 = new Skill(nameof(革命の旗頭付与2), _ => $"レアリティの変化が発生した場合に発動する。（１）～（３）を消失する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack)
*/

Skill 牽引 = new Skill(nameof(牽引), _ => $"自分の攻撃後に味方1人を対象として発動する。対象に（１）（２）を付与する。\n（１）自分の攻撃宣言時に発動する。1D{20 * _}をダメージ計算時に加える。\n（２）【牽引】を発動した味方が戦場から消失した場合、（１）を消失する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack, SkillHelper.LastDuringAfterAttack)
{ IsForceActivate = true, IsEnchant = true };
bool 牽引Condition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc)) return false;
    if (world.TeamDictionary[owner.Team].Count == 1) return false;
    throw new NotImplementedException();
}

Skill なぎ払い = new Skill(nameof(なぎ払い), _ => $"自分の攻撃宣言時に{4 * _}%の確率で発動する。自分は敵全てに攻撃する。", Per4LV, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsForceActivate = true };

Skill ザラキ = new Skill(nameof(ザラキ), _ => $"「パンドラボックス」ユニットのみ発動可能。自分の攻撃後に発動する。敵1体毎に{2 * _}%の確率で即死判定を行う。即死判定に成功した敵の生命力を0にする。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack, SkillHelper.LastDuringAfterAttack)
{ IsForceActivate = true };

Skill 突撃の大号令 = new Skill(nameof(突撃の大号令), _ => $"このターン誰も攻撃宣言をそれ以前に行っていない場合に自分の味方の攻撃宣言時に発動できる。ターン終了時まで自分は攻撃宣言を行えない。攻撃する味方に（１）（２）を付与する。\n（１）攻撃宣言時に発動する。1D{20 * _}をダメージ計算時に加える。\n（２）自分を含む味方に攻撃順序または素早さを参照するスキルがこのスキルと【突撃の大号令】以外に存在せず、攻撃宣言時に{4 * _}%の確率で自分の攻撃力を上げる/下げる効果の影響を排除して発動する。ターン終了時まで自分の攻撃力をこのスキルを発動した時点の攻撃力分上げる。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.DeclarationAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsReferAttackOrder = true, IsEnchant = true };

Skill 布石 = new Skill(nameof(布石), _ => $"自分の攻撃宣言時に発動する。この攻撃を無効としてもよい。その場合、攻撃対象に（１）をターン終了時まで付与する。\n（１）自分が攻撃対象となった攻撃宣言時に{6 * _}%の確率で発動する。攻撃後まで、自分の守備力をこのスキルの発動時の半分下げる。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack, SkillHelper.LastDuringDeclarationAttack)
{ IsForceActivate = true, IsEnchant = true };
bool 布石Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc) || !world.IsAlive(world.CurrentDefender)) return false;
    var tf = world.SelectChoice("この攻撃を無効にして、敵に効果を付与しますか？", "Yes", "No") == 0;
    if (!tf) return false;
    cc.ObjectUnit = world.CurrentDefender;
    var sk = new Skill(owner.OriginalData.Name + "の布石付与（１）", _ => $"自分が攻撃対象の攻撃宣言時に{6 * _}%の確率で発動する。攻撃後まで、自分の守備力をこのスキルの発動時の[1/2 小数点以下切り上げ]分下げる。", Per6LV, Skill.DefaultActivate, Timing.DeclarationAttack)
    { IsAnonymous = true, IsForceActivate = true, IsReferDefense = true, Condition = このターン終了時まで使えるスキルCondition(world), Cost = 布石Cost_1, Execute = 布石Execute_1, Level = cc.Skill.Level };
    var bs = new BattleSkill(sk);
    cc.ObjectUnit.BattleSkill.Add(bs);
    return true;
}
bool 布石Cost_1(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    cc.Bag = owner.CurrentDefense / 2;
    return true;
}
bool 布石Execute_1(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    owner.DefenseChange.Add(new StatusCounter((int)cc.Bag) { WhenToRemove = WhenToRemoveHelper.AfterAttackRemove });
    return true;
}

Skill 怨嗟 = new Skill(nameof(怨嗟), _ => $"味方に【怨嗟】を持つものがいない場合に、自分の味方が倒れたターンの終了時に発動する。敵全体に{10 * _}ダメージを与える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.EndTurn, SkillHelper.LastUntilNextTurn)
{ IsForceActivate = true, IsBurn = true, Condition = 怨嗟Condition, Execute = 怨嗟Execute };
bool 怨嗟Condition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc) || world.DeadUnitThisTurn.Count == 0)
        return false;
    var tmpAns = true;
    for (int i = 0; i < world.DeadUnitThisTurn.Count; ++i)
    {
        if (world.DeadUnitThisTurn[i].Team == owner.Team)
        {
            tmpAns = false;
            break;
        }
    }
    if (tmpAns)
        return false;
    var ls = world.TeamDictionary[owner.Team];
    if (ls.Count <= 1) return false;
    for (int i = 0; i < ls.Count; i++)
    {
        if (ls[i] == owner) continue;
        var bs = ls[i].BattleSkill.GetEnumerator();
        while (bs.MoveNext())
            if (bs.Current.Skill.Id == 怨嗟.Id)
                return false;
    }
    return true;
}
bool 怨嗟Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    var c = world.TeamDictionary.GetEnumerator();
    while (c.MoveNext())
    {
        if (c.Current.Key == owner.Team) continue;
        var ls = c.Current.Value;
        for (int i = 0; i < ls.Count; i++)
        {
            world.GiveDamage(owner, ls[i], cc.Skill.Level * 10, Reason.Skill);
        }
    }
    return true;
}
Skill ためる = new Skill(nameof(ためる), _ => $"1ターンに1度、攻撃権を1つ放棄して発動できる。自分が【ためる】を発動していないターンの終了時まで自分の攻撃力を1D{20 * _}上げる。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.UnitAction, SkillHelper.LastDuringThisTurn)
{
    IsReferAttack = true,
    Condition = ためるCondition,
    Cost = ためるCost,
    Execute = ためるExecute
};
bool ためるCondition(World world, BattleUnit owner, ChainComponent cc) => 一回だけCondition(world, owner, cc) && world.Actor == owner && owner.AttackRight > 0;
bool ためるCost(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || owner.AttackRight == 0)
        return false;
    --owner.AttackRight;
    return true;
}
bool ためるExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    owner.AttackChange.Add(new StatusCounter(1.D(20 * cc.Skill.Level)) { WhenToRemove = ためるWhenToRemove(cc.Skill.Id) });
    return true;
}
Func<World, BattleUnit, bool> ためるWhenToRemove(long ためるId) => (World world, BattleUnit _) => world.Timing == Timing.EndTurn && !world.ActivatedSkillThisTurn.Contains(new ValueTuple<byte, long>(_.Id, ためるId));

Skill 暴君 = new Skill(nameof(暴君), _ => $"自分のレアリティが最も高く、最もレアリティの低いユニットが１体のみ存在する場合にその最もレアリティの低い敵ユニット1体を対象に発動できる。対象のスキル効果処理時に所有する全てのスキルのレベルを{6 - _}とする。\n※自分が倒れた後もこの効果は持続する。", Skill.DefaultPercentage, NormalActivateCount, Timing.UnitAction | Timing.EndTurn)
{ Condition = 暴君Condition, Cost = 暴君Cost, Execute = 暴君Execute };
bool 暴君Condition(World world, BattleUnit owner, ChainComponent cc)
{
    if (world.Timing == Timing.EndTurn)
    {
        cc.Skill.Bag = null;
        return false;
    }
    else if (world.Timing == Timing.UnitAction)
    {
        if (cc.Skill.Bag == null)
            cc.Skill.Bag = "";
        else return false;
    }
    if (world.Actor != owner) return false;
    if (!world.IsAlive(owner)) return false;
    var c = world.TeamDictionary.Values.GetEnumerator();
    byte rarity = (byte)owner.CurrentRarity;
    BattleUnit 最低 = null;
    int count = 1;
    while (c.MoveNext())
    {
        var ls = c.Current;
        for (int i = 0; i < ls.Count; i++)
        {
            if (ls[i] == owner) continue;
            var tmp = (byte)ls[i].CurrentRarity;
            if (tmp >= rarity)
                return false;
            if (最低 == null)
            {
                最低 = ls[i];
                continue;
            }
            if ((byte)最低.CurrentRarity > tmp)
            {
                最低 = ls[i];
                count = 1;
            }
            if ((byte)最低.CurrentRarity == tmp)
            {
                count++;
            }
        }
    }
    return count == 1 && 最低.Team != owner.Team;
}
bool 暴君Cost(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    var c = world.TeamDictionary.Values.GetEnumerator();
    byte rarity = (byte)owner.CurrentRarity;
    BattleUnit 最低 = null;
    int count = 1;
    while (c.MoveNext())
    {
        var ls = c.Current;
        for (int i = 0; i < ls.Count; i++)
        {
            if (ls[i] == owner) continue;
            var tmp = (byte)ls[i].CurrentRarity;
            if (tmp >= rarity)
                return false;
            if (最低 == null)
            {
                最低 = ls[i];
                continue;
            }
            if ((byte)最低.CurrentRarity > tmp)
            {
                最低 = ls[i];
                count = 1;
            }
            if ((byte)最低.CurrentRarity == tmp)
            {
                count++;
            }
        }
    }
    if (count == 1 && 最低.Team != owner.Team)
    {
        cc.ObjectUnit = 最低;
        return true;
    }
    return false;
}
bool 暴君Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || !world.IsAlive(cc.ObjectUnit))
        return false;
    foreach (var item in cc.ObjectUnit.BattleSkill)
        item.Level = (byte)(6 - cc.Skill.Level);
    return true;
}
Skill 指揮 = new Skill(nameof(指揮), _ => $"発動率が元々の値から変更されていない{70 - 2 * _}%以下の確率発動するスキルを持つ味方1人を対象としてターン開始時に発動する。発動率が{70 - 2 * _}%以下の対象のスキルを1つ選び、ターン終了時までその発動率を{2 * _}%上昇する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.StartTurn, SkillHelper.LastDuringThisTurn)
{ Condition = 指揮Condition, Cost = 指揮Cost, Execute = 指揮Execute };
bool 指揮Condition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc)) return false;
    var ls = world.TeamDictionary[owner.Team];
    if (ls.Count == 1) return false;
    for (int i = 0; i < ls.Count; i++)
    {
        if (ls[i] == owner) continue;
        var sks = ls[i].BattleSkill;
        if (sks.Count == 0) continue;
        for (int j = 0; j < sks.Count; j++)
        {
            if (sks[j].Percentage <= 70 - 2 * cc.Skill.Level && sks[j].Percentage == sks[j].Skill.Percentage)
                return true;
        }
    }
    return false;
}
bool 指揮Cost(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner)) return false;
    var ls = world.TeamDictionary[owner.Team];
    if (ls.Count == 1) return false;
    var units = new List<BattleUnit>();
    for (int i = 0; i < ls.Count; i++)
    {
        if (ls[i] == owner || ls[i].BattleSkill.Count == 0 || ls[i].BattleSkill.All(_ => _.Percentage > 70 - 2 * cc.Skill.Level || _.Percentage != _.Skill.Percentage))
            continue;
        units.Add(ls[i]);
    }
    if (units.Count == 0)
        return false;
    var arr = units.ToArray();
    cc.ObjectUnit = world.SelectChoice<BattleUnit>("指揮の対象となる味方を1人選んでください。", arr, _ => _.OriginalData.Name);
    return true;
}
bool 指揮Execute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || !world.IsAlive(cc.ObjectUnit)) return false;
    var ls = new List<BattleSkill>();
    for (int i = 0; i < cc.ObjectUnit.BattleSkill.Count; i++)
    {
        if (cc.ObjectUnit.BattleSkill[i].Percentage > 70 - 2 * cc.Skill.Level || cc.ObjectUnit.BattleSkill[i].Percentage != cc.ObjectUnit.BattleSkill[i].Skill.Percentage)
            continue;
        ls.Add(cc.ObjectUnit.BattleSkill[i]);
    }
    if (ls.Count == 0) return false;
    var skill = world.SelectChoice("", ls.ToArray(), _ => _.Name);
    skill.Percentage += (byte)(2 * cc.Skill.Level);
    return true;
}
Skill トリシューラ = new Skill(nameof(トリシューラ), _ => $"自分が同一の敵を3回攻撃した場合の攻撃後に発動する。その敵を[LV]ターン後の開始時まで除外する。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.AfterAttack, SkillHelper.LastDuringAfterAttack)
{ IsForceActivate = true, Condition = トリシューラCondition, Cost = トリシューラCost, Execute = トリシューラExecute };
bool トリシューラCondition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc) || world.CurrentAttacker != owner) return false;
    var bag = cc.Skill.Bag as Dictionary<byte, byte>;
    if (bag == null)
        cc.Skill.Bag = bag = new Dictionary<byte, byte>();
    if (!bag.ContainsKey(world.CurrentDefender.Id))
        bag[world.CurrentDefender.Id] = 1;
    else bag[world.CurrentDefender.Id]++;
    return bag[world.CurrentDefender.Id] == 3;
}
bool トリシューラCost(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || world.CurrentAttacker != owner) return false;
    var bag = cc.Skill.Bag as Dictionary<byte, byte>;
    return bag != null && bag.TryGetValue(world.CurrentDefender.Id, out byte count) && count == 3;
}
bool トリシューラExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || world.CurrentDefender == null || !world.IsAlive(world.CurrentDefender))
        return false;
    world.RemoveUnit(world.CurrentDefender, (byte)(world.Turn + cc.Skill.Level));
    return true;
}

Skill バベル = new Skill(nameof(バベル), _ => $"ターン終了時に敵１体を対象に発動する。{_}D（敵がこのターン開始時から終了時以前に発動した全てのスキルレベルの合計（最大100））ダメージを対象に与える。", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.EndTurn, SkillHelper.LastUntilNextTurn)
{ IsForceActivate = true, IsBurn = true, Condition = バベルCondition, Cost = バベルCost, Execute = バベルExecute };
bool バベルCondition(World world, BattleUnit owner, ChainComponent cc)
{
    if (!一回だけCondition(world, owner, cc))
        return false;
    return world.ActivatedSkillThisTurn.Count != 0;
}
bool バベルCost(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner))
        return false;
    var ally = world.TeamDictionary[owner.Team];
    if (world.ActivatedSkillThisTurn.All(_ => ally.Find(_2 => _2.Id == _.Item1) != null))
        return false;
    return (cc.ObjectUnit = world.SelectUnit("バベルの対象としてダメージを与える敵を選んでください。", _ => _.Team != owner.Team)) != null;
}
bool バベルExecute(World world, BattleUnit owner, ChainComponent cc)
{
    if (!world.IsAlive(owner) || cc.ObjectUnit == null || !world.IsAlive(cc.ObjectUnit))
        return false;
    var d = world.ActivatedSkillThisTurn.Aggregate(0, (ans, next) =>
    {
        var (unitid, skillid) = next;
        var unit = world[unitid];
        if (unit.Team == owner.Team) return ans;
        var sk = unit.BattleSkill.Find(_ => _.Id == skillid);
        if (sk == null) return ans;
        return ans + sk.Level;
    });
    d = Math.Min(Math.Max(d, 0), 100);
    if (d == 0) return false;
    world.GiveDamage(owner, cc.ObjectUnit, cc.Skill.Level.D((byte)d), Reason.Skill);
    return true;
}

Skill 軽減_無 = new Skill("軽減（無）", _ => $" 1D{20 * _}分無属性からのダメージを減少する", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.None)
{ IsContinuous = true };
Skill 軽減_力 = new Skill("軽減（力）", _ => $" 1D{20 * _}分力属性からのダメージを減少する", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.None)
{ IsContinuous = true };
Skill 軽減_魔 = new Skill("軽減（魔）", _ => $" 1D{20 * _}分魔属性からのダメージを減少する", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.None)
{ IsContinuous = true };
Skill 軽減_技 = new Skill("軽減（技）", _ => $" 1D{20 * _}分技属性からのダメージを減少する", Skill.DefaultPercentage, Skill.DefaultActivate, Timing.None)
{ IsContinuous = true };


new Skill[] { かばう, 回避, 怪力, クリティカル, 先制, 特効_力, 特効_技, 特効_魔, 特効_無, 下克上, 経験値, 肉染み, バトンタッチ, 鬼殺し, 革命の旗頭, 牽引, なぎ払い, ザラキ, 突撃の大号令, 布石, 怨嗟, ためる, 暴君, トリシューラ, バベル, 軽減_無, 軽減_力, 軽減_魔, 軽減_技 }