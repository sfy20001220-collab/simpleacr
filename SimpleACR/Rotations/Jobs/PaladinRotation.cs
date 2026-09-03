using SimpleACR.Core;
using SimpleACR.Data;
using A = SimpleACR.Data.ActionIds.Pld;
using S = SimpleACR.Data.StatusIds.Pld;

namespace SimpleACR.Rotations.Jobs;

/// <summary>
/// 骑士（PLD）7.x 循环 —— 简化版。
///
/// 【循环思路】
///   PLD 的输出围绕两个 20s/60s 的爆发窗口：战逃（+伤害）和安魂（魔法增伤）。
///   安魂期间打「告白」四连（悔罪告白 → 信仰 → 真理 → 勇气），这是 PLD 最大的
///   一坨伤害，所以排在所有 GCD 之前。
///   其余时间：赎罪剑（有就打）> 沥血剑（DoT 快断了补）> 王权剑连招。
///
/// 【优先级怎么排】
///   引擎自上而下求值，命中即止。所以：
///     1. 先排"窗口限定"的（安魂告白连 —— 错过就没了）
///     2. 再排"资源限定"的（赎罪剑 —— 有 buff 才有）
///     3. 再排"要维持"的（DoT）
///     4. 最后排填充（王权连招 / 快破剑）
///
/// 【几个值得注意的写法】
///   * s.HasBuff(S.Requiescat) —— 直接读 buff，不用自己算 CD
///   * s.ComboStep(A.RiotBlade) —— 读游戏自己的连招状态，比自己记变量稳
///   * 位移（调停）留 1 层：s.Charges(A.Intervene) >= 2 才用
/// </summary>
[Rotation("骑士·7.x 简化循环", Job.PLD, Author = "SimpleACR", Patch = "7.x")]
public sealed class PaladinRotation : Rotation
{
    public override void Build(RotationBuilder b)
    {
        // ============ 爆发窗口 ============

        b.Ogcd("战逃：起手/冷却好就开",
            A.FightOrFlight,
            s => s.HasTarget && s.OffCooldown(A.FightOrFlight));

        b.Ogcd("安魂：战逃期间开，接告白连",
            A.Requiescat,
            s => s.HasBuff(S.FightOrFlight) && s.OffCooldown(A.Requiescat));

        // ============ 安魂 → 告白四连 ============
        // 这四步必须在安魂 buff 里打完，所以放在最前面，压过所有常规 GCD。
        // 后三步靠"前一步打完"作为条件推进：s.ComboStep(前一步)
        b.Gcd("告白连·1 悔罪告白",
            A.Confiteor,
            s => s.HasBuff(S.Requiescat) && !s.ComboStep(A.Confiteor, A.BladeOfFaith, A.BladeOfTruth));

        b.Gcd("告白连·2 信仰之剑",
            A.BladeOfFaith,
            s => s.ComboStep(A.Confiteor));

        b.Gcd("告白连·3 真理之剑",
            A.BladeOfTruth,
            s => s.ComboStep(A.BladeOfFaith));

        b.Gcd("告白连·4 勇气之剑",
            A.BladeOfValor,
            s => s.ComboStep(A.BladeOfTruth));

        // ============ 填充 GCD ============

        b.Gcd("赎罪剑：有 buff 优先打掉",
            A.Atonement,
            s => s.HasBuff(S.AtonementReady));

        // 7.0 起骑士的 DoT（沥血剑）有改动，如果你的版本里沥血剑已经不是 DoT，
        // 把下面这行注释掉即可 —— 这就是"条件写成数据"的好处，改一行不用动引擎。
        b.Gcd("沥血剑：DoT 剩余 < 3s 补",
            A.GoringBlade,
            s => s.TargetDebuffRemaining(S.GoringBladeDot) < 3f);

        b.Gcd("王权剑：连招第三段",
            A.RoyalAuthority,
            s => s.ComboStep(A.RiotBlade));

        b.Gcd("暴乱剑：连招第二段",
            A.RiotBlade,
            s => s.ComboStep(A.FastBlade));

        b.Gcd("快破剑：连招起手",
            A.FastBlade);

        // ============ 能力技（GCD 后段插入）============

        b.Ogcd("赎罪/深奥之灵：卡 CD 打",
            A.SpiritsWithin,
            s => s.HasTarget && s.OffCooldown(A.SpiritsWithin));

        b.Ogcd("悔罪：单体也卡 CD 打（自带 DoT）",
            A.CircleOfScorn,
            s => s.HasTarget && s.OffCooldown(A.CircleOfScorn));

        b.Ogcd("调停：留 1 层保位移，只在近身时当伤害用",
            A.Intervene,
            s => s.HasTarget && s.TargetDistance <= 3f && s.Charges(A.Intervene) >= 2);

        // ============ 群体（≥3 只怪）============

        b.Gcd("全蚀斩：AOE 起手",
            A.TotalEclipse,
            s => s.EnemyCount(5f) >= 3);

        b.Gcd("日珥斩：AOE 第二段",
            A.Prominence,
            s => s.EnemyCount(5f) >= 3 && s.ComboStep(A.TotalEclipse));

        b.Gcd("圣环：AOE 魔法",
            A.HolyCircle,
            s => s.EnemyCount(5f) >= 3);

        // ============ 减伤 / 自保 ============
        // Defensive 类受配置里的"启用减伤"开关控制，默认开。

        b.Defensive("铁壁：血量 < 80%",
            A.Sentinel,
            s => s.HpPercent < 80f,
            TargetSlot.Self);

        b.Defensive("预警：忠义 ≥ 50 且血量 < 90%",
            A.Sheltron,
            s => s.OathGauge >= 50 && s.HpPercent < 90f,
            TargetSlot.Self);

        b.Defensive("神圣领域：血量 < 12%（副本内）",
            A.HallowedGround,
            s => s.HpPercent < 12f && s.InDuty,
            TargetSlot.Self);

        b.Defensive("深仁厚泽：血量 < 25% 自救",
            A.Clemency,
            s => s.HpPercent < 25f && s.Mp >= 1000,
            TargetSlot.Self);
    }
}
