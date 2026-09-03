using SimpleACR.Core;
using SimpleACR.Data;
using A = SimpleACR.Data.ActionIds.War;
using S = SimpleACR.Data.StatusIds.War;

namespace SimpleACR.Rotations.Jobs;

/// <summary>
/// 战士（WAR）7.x 循环 —— 简化版。
///
/// 【循环思路】
///   战士的核心是「兽魂」（Beast Gauge，0~100）：
///     - 打连招攒兽魂（重殴 +10 / 凶残裂 +10 / 暴风碎 +20）
///     - 兽魂 ≥ 50 时消耗掉打裂石飞环（单体）或地毁人亡（群体）
///     - 激怒（Infuriate）直接 +50，用来避免兽魂溢出或强行凑爆发
///   爆发窗口是「原初的解放」：期间裂石飞环不耗兽魂，所以要在窗口里把层数打光。
///
/// 【为什么裂石飞环排在连招前面】
///   因为兽魂 100 就溢出了，溢出 = 实打实的亏损。
///   而连招是"填充"，随时可以补。资源 > 连招，这是所有资源型职业的通用原则。
///
/// 【注意 ComboStep 的用法】
///   s.ComboStep(A.HeavySwing) = "上一步刚打完重殴" = 现在该打凶残裂。
///   这比自己维护一个 int 计数器靠谱得多 —— 游戏自己记得清清楚楚，
///   还能正确处理"被打断/切目标/超时"这些边界情况。
/// </summary>
[Rotation("战士·7.x 简化循环", Job.WAR, Author = "SimpleACR", Patch = "7.x")]
public sealed class WarriorRotation : Rotation
{
    public override void Build(RotationBuilder b)
    {
        // ============ 爆发窗口 ============

        b.Ogcd("原初的解放：冷却好且身上没解放 buff",
            A.InnerRelease,
            s => s.HasTarget && s.OffCooldown(A.InnerRelease) && !s.HasBuff(S.InnerRelease));

        b.Ogcd("狂暴：冷却好就开（必直窗口）",
            A.Berserk,
            s => s.HasTarget && s.OffCooldown(A.Berserk) && !s.HasBuff(S.Berserk));

        // ============ 兽魂消耗（优先于一切填充）============

        b.Gcd("内部混沌：有 Nascent Chaos 时必直，最高优先级",
            A.InnerChaos,
            s => s.HasBuff(S.NascentChaos));

        b.Gcd("原初之血刃：解放后收尾",
            A.PrimalRend,
            s => s.HasBuff(S.PrimalRendReady, 0f));

        b.Gcd("裂石飞环：解放中（不耗兽魂）或兽魂 ≥ 50",
            A.FellCleave,
            s => s.HasBuff(S.InnerRelease) || s.BeastGauge >= 50);

        b.Gcd("地毁人亡：AOE 版，≥3 只怪且兽魂 ≥ 50",
            A.Decimate,
            s => s.EnemyCount(5f) >= 3 && s.BeastGauge >= 50);

        b.Gcd("混乱旋风：AOE 版必直",
            A.ChaoticCyclone,
            s => s.EnemyCount(5f) >= 3 && s.HasBuff(S.NascentChaos));

        // ============ 能力技 ============

        b.Ogcd("动乱：卡 CD 打",
            A.Upheaval,
            s => s.HasTarget && s.OffCooldown(A.Upheaval));

        b.Ogcd("地鸣：≥2 只怪时的群体版动乱",
            A.Orogeny,
            s => s.EnemyCount(5f) >= 2 && s.OffCooldown(A.Orogeny));

        b.Ogcd("激怒：兽魂 < 50 时补（防溢出 + 凑爆发）",
            A.Infuriate,
            s => s.BeastGauge < 50 && s.Charges(A.Infuriate) >= 1);

        b.Ogcd("猛攻：留 1 层保位移，近身且有富余层数时当伤害",
            A.Onslaught,
            s => s.HasTarget && s.TargetDistance <= 20f && s.Charges(A.Onslaught) >= 2);

        // ============ 填充连招 ============

        b.Gcd("暴风碎：连招第三段（+20 兽魂）",
            A.StormsPath,
            s => s.ComboStep(A.Maim));

        b.Gcd("凶残裂：连招第二段（+10 兽魂 +增伤）",
            A.Maim,
            s => s.ComboStep(A.HeavySwing));

        b.Gcd("重殴：连招起手",
            A.HeavySwing);

        // ============ AOE 连招 ============

        b.Gcd("秘银暴风：AOE 第二段（+20 兽魂）",
            A.MythrilTempest,
            s => s.EnemyCount(5f) >= 3 && s.ComboStep(A.Overpower));

        b.Gcd("超压斧：AOE 起手",
            A.Overpower,
            s => s.EnemyCount(5f) >= 3);

        // ============ 远程 / 开怪 ============

        b.Gcd("飞斧：够不着时开怪",
            A.Tomahawk,
            s => s.HasTarget && s.TargetDistance > 4f);

        // ============ 减伤 / 自保 ============
        // 顺序 = 优先级：先开小减伤，最后才是无敌。

        b.Defensive("血气：血量 < 85%（自带回血，小怪/大怪都好用）",
            A.Bloodwhetting,
            s => s.HpPercent < 85f,
            TargetSlot.Self);

        b.Defensive("复仇：血量 < 70%",
            A.Vengeance,
            s => s.HpPercent < 70f,
            TargetSlot.Self);

        b.Defensive("战栗：血量 < 55%",
            A.ThrillOfBattle,
            s => s.HpPercent < 55f,
            TargetSlot.Self);

        b.Defensive("泰然自若：血量 < 50%（一次性大回血）",
            A.Equilibrium,
            s => s.HpPercent < 50f,
            TargetSlot.Self);

        b.Defensive("摆脱：血量 < 45%（群体盾，团本里价值高）",
            A.ShakeItOff,
            s => s.HpPercent < 45f,
            TargetSlot.Self);

        b.Defensive("死斗：血量 < 12%（副本内，8s 无敌）",
            A.Holmgang,
            s => s.HpPercent < 12f && s.InDuty,
            TargetSlot.Self);
    }
}
