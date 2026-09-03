using SimpleACR.Core;
using SimpleACR.Data;
using A = SimpleACR.Data.ActionIds.Vpr;
using S = SimpleACR.Data.StatusIds.Vpr;

namespace SimpleACR.Rotations.Jobs;

/// <summary>
/// 蝰蛇剑士（VPR）7.x 循环。
///
/// 【这个职业在引擎上有个特殊点】
///   VPR 大量技能是**连锁 / 触发型**的：
///     * 祖灵连段（一式→二式→三式→四式→大蛇牙）每一步只在轮到它时可按
///     * 蛇尾击 / 双牙连击这类追击技，靠 buff 亮起，亮了就该立刻按掉
///   这两类都不能只靠 "我有 xx buff" 判断 —— 打完一式后二式还没亮，
///   引擎会卡在一式上不动。所以下面**每一条都带 s.CanUse(...)**：
///   轮不到就自动往后找，这正是 RotationEngine 改成"穿透式求值"的原因。
///
/// 【循环思路】
///   1. 蛇灵气（+伤害 buff，同时攒祖灵量谱）→ 祖灵降临 → 祖灵连段 → 大蛇牙
///      —— 这是 VPR 最大的一坨伤害，2 分钟一次，优先级最高
///   2. 触发型追击（蛇尾 / 双牙）：亮了立刻打掉，别攒
///   3. 盘蛇（强碎灵蛇 → 猛袭盘蛇 / 疾速盘蛇）：CD 好了就开
///   4. 常规「牙」连击：两条分支轮换，维持 猎人直觉(+伤害) 与 疾速之牙(+技速)
///   5. 群体：换成「尖牙 / 利牙 / 獠牙」那一套 + 祖灵之蛇连段
///
/// 【两条分支怎么轮换】
///   咬噬尖齿 → 猛袭利齿 → 侧击獠齿 → 侧裂獠齿   → 给 猎人直觉（+10% 伤害）
///   穿裂尖齿 → 疾速利齿 → 背击獠齿 → 背裂獠齿   → 给 疾速之牙（+15% 技速）
///   两个 buff 都是 40s，所以起手时用"哪个快断了先补哪个"来轮换。
/// </summary>
[Rotation("蝰蛇剑士·7.x 循环", Job.VPR, Author = "SimpleACR", Patch = "7.x")]
public sealed class ViperRotation : Rotation
{
    /// <summary>起手爪：哪个增益快断了就从哪条分支进。</summary>
    private static bool NeedHuntersInstinct(CombatState s)
        => s.BuffRemaining(S.HuntersInstinct) <= s.BuffRemaining(S.Swiftscaled);

    public override void Build(RotationBuilder b)
    {
        // ============================================================
        // 1. 祖灵降临爆发（2 分钟大循环，最优先）
        // ============================================================

        // 蛇灵气先开：它给的 +伤害能覆盖整个祖灵连段，同时攒满祖灵量谱
        b.Ogcd("蛇灵气：起手 / CD 好就开",
            A.SerpentsIre,
            s => s.HasTarget && s.OffCooldown(A.SerpentsIre) && s.CanUse(A.SerpentsIre));

        // 祖灵降临：量谱攒满（或 CanUse 判定通过）就进连段
        b.Ogcd("祖灵降临：量谱够了就开",
            A.Reawaken,
            s => s.HasTarget && s.OffCooldown(A.Reawaken) && s.CanUse(A.Reawaken));

        // ---- 祖灵连段（单体）：一式 → 二式 → 三式 → 四式 ----
        // 每条都带 CanUse，轮不到就自动落到下一条；四式打完前面全不可按，
        // 于是自然落到下面的大蛇牙收尾。
        b.Gcd("祖灵·一式", A.FirstGeneration,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.FirstGeneration));
        b.Gcd("祖灵·二式", A.SecondGeneration,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.SecondGeneration));
        b.Gcd("祖灵·三式", A.ThirdGeneration,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.ThirdGeneration));
        b.Gcd("祖灵·四式", A.FourthGeneration,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.FourthGeneration));

        // ---- 祖灵连段（群体）：祖灵之蛇一式 ~ 四式 ----
        b.Gcd("祖灵·蛇一式(AOE)", A.FirstLegacy,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.FirstLegacy));
        b.Gcd("祖灵·蛇二式(AOE)", A.SecondLegacy,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.SecondLegacy));
        b.Gcd("祖灵·蛇三式(AOE)", A.ThirdLegacy,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.ThirdLegacy));
        b.Gcd("祖灵·蛇四式(AOE)", A.FourthLegacy,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.FourthLegacy));

        // ---- 收尾：祖灵大蛇牙 ----
        // 放最后：前面的连段还能按就会先按连段，都按完了才轮到它。
        b.Gcd("祖灵大蛇牙：连段收尾",
            A.Ouroboros,
            s => s.HasBuff(S.Reawakened) && s.CanUse(A.Ouroboros));

        // ============================================================
        // 2. 触发型追击（oGCD，亮了立刻打掉）
        //    这类全靠 CanUse 兜底，不依赖 buff ID —— buff 表错了也不会漏按
        // ============================================================

        b.Ogcd("蛇尾击：蛇尾追击（单体）",
            A.DeathRattle,
            s => s.HasTarget && s.CanUse(A.DeathRattle));

        b.Ogcd("蛇尾闪：蛇尾追击（群体）",
            A.LastLash,
            s => s.HasTarget && s.CanUse(A.LastLash));

        b.Ogcd("双牙连击：盘蛇后追击（单体）",
            A.TwinfangBite,
            s => s.HasTarget && s.CanUse(A.TwinfangBite));

        b.Ogcd("双牙连闪：盘蛇后追击（单体）",
            A.TwinbloodBite,
            s => s.HasTarget && s.CanUse(A.TwinbloodBite));

        b.Ogcd("双牙乱击：盘蛇后追击（群体）",
            A.TwinfangThresh,
            s => s.HasTarget && s.CanUse(A.TwinfangThresh));

        b.Ogcd("双牙乱闪：盘蛇后追击（群体）",
            A.TwinbloodThresh,
            s => s.HasTarget && s.CanUse(A.TwinbloodThresh));

        // ============================================================
        // 3. 盘蛇（Vicewinder 系）
        // ============================================================

        b.Gcd("猛袭盘蛇：盘蛇第二段（猎人侧）",
            A.HuntersCoil,
            s => s.ComboStep(A.Vicewinder) && s.CanUse(A.HuntersCoil));

        b.Gcd("疾速盘蛇：盘蛇第二段（疾速侧）",
            A.SwiftskinsCoil,
            s => s.ComboStep(A.Vicewinder) && s.CanUse(A.SwiftskinsCoil));

        b.Gcd("猛袭盘蝰：群体盘蛇第二段",
            A.HuntersDen,
            s => s.ComboStep(A.Vicepit) && s.CanUse(A.HuntersDen));

        b.Gcd("疾速盘蝰：群体盘蛇第二段",
            A.SwiftskinsDen,
            s => s.ComboStep(A.Vicepit) && s.CanUse(A.SwiftskinsDen));

        // 起盘蛇：不在连招中 + 可用就开（群体优先开 Vicepit）
        b.Gcd("强碎灵蝰：起群体盘蛇",
            A.Vicepit,
            s => s.HasTarget && !s.InComboWindow && s.EnemyCount(5f) >= 3 && s.CanUse(A.Vicepit));

        b.Gcd("强碎灵蛇：起盘蛇",
            A.Vicewinder,
            s => s.HasTarget && !s.InComboWindow && s.CanUse(A.Vicewinder));

        // ============================================================
        // 4. 群体「牙」连击（≥3 只怪）
        //    必须排在单体起手之前，否则开怪时会先按出单体的咬噬尖齿
        // ============================================================

        b.Gcd("乱击獠牙：群体第三段（猎人侧）",
            A.JaggedMaw,
            s => s.ComboStep(A.HuntersBite) && s.CanUse(A.JaggedMaw));

        b.Gcd("乱裂獠牙：群体第三段（疾速侧）",
            A.BloodiedMaw,
            s => s.ComboStep(A.SwiftskinsBite) && s.CanUse(A.BloodiedMaw));

        b.Gcd("猛袭利牙：群体第二段",
            A.HuntersBite,
            s => s.ComboStep(A.SteelMaw, A.ReavingMaw) && s.CanUse(A.HuntersBite));

        b.Gcd("疾速利牙：群体第二段",
            A.SwiftskinsBite,
            s => s.ComboStep(A.SteelMaw, A.ReavingMaw) && s.CanUse(A.SwiftskinsBite));

        b.Gcd("咬噬尖牙：群体起手（猎人侧）",
            A.SteelMaw,
            s => s.EnemyCount(5f) >= 3 && !s.InComboWindow
                 && NeedHuntersInstinct(s) && s.CanUse(A.SteelMaw));

        b.Gcd("穿裂尖牙：群体起手（疾速侧）",
            A.ReavingMaw,
            s => s.EnemyCount(5f) >= 3 && !s.InComboWindow && s.CanUse(A.ReavingMaw));

        // ============================================================
        // 5. 单体「牙」连击
        // ============================================================

        b.Gcd("侧裂獠齿：单体第四段（猎人侧）",
            A.FlanksbaneFang,
            s => s.ComboStep(A.FlankstingStrike) && s.CanUse(A.FlanksbaneFang));

        b.Gcd("背裂獠齿：单体第四段（疾速侧）",
            A.HindsbaneFang,
            s => s.ComboStep(A.HindstingStrike) && s.CanUse(A.HindsbaneFang));

        b.Gcd("侧击獠齿：单体第三段（猎人侧）",
            A.FlankstingStrike,
            s => s.ComboStep(A.HuntersSting) && s.CanUse(A.FlankstingStrike));

        b.Gcd("背击獠齿：单体第三段（疾速侧）",
            A.HindstingStrike,
            s => s.ComboStep(A.SwiftskinsSting) && s.CanUse(A.HindstingStrike));

        b.Gcd("猛袭利齿：单体第二段（猎人侧）",
            A.HuntersSting,
            s => s.ComboStep(A.SteelFangs, A.ReavingFangs) && s.CanUse(A.HuntersSting));

        b.Gcd("疾速利齿：单体第二段（疾速侧）",
            A.SwiftskinsSting,
            s => s.ComboStep(A.SteelFangs, A.ReavingFangs) && s.CanUse(A.SwiftskinsSting));

        // 起手：哪条增益快断了先补哪条
        b.Gcd("咬噬尖齿：单体起手（补猎人直觉）",
            A.SteelFangs,
            s => s.HasTarget && !s.InComboWindow
                 && NeedHuntersInstinct(s) && s.CanUse(A.SteelFangs));

        b.Gcd("穿裂尖齿：单体起手（补疾速之牙）",
            A.ReavingFangs,
            s => s.HasTarget && !s.InComboWindow && s.CanUse(A.ReavingFangs));

        // ============================================================
        // 6. 远程 / 机动（不在近战距离时）
        // ============================================================

        b.Gcd("飞蛇之尾：远程输出（耗盘蛇层）",
            A.UncoiledFury,
            s => s.HasTarget && s.TargetDistance > 3f && s.CanUse(A.UncoiledFury));

        b.Gcd("飞蛇之牙：远程填充",
            A.WrithingSnap,
            s => s.HasTarget && s.TargetDistance > 3f && s.CanUse(A.WrithingSnap));

        b.Utility("蛇行：拉近身位",
            A.Slither,
            s => s.HasTarget && s.TargetDistance > 10f && s.CanUse(A.Slither),
            TargetSlot.Self);

        // ============================================================
        // 7. 减伤 / 自保（受配置里"启用减伤"开关控制）
        // ============================================================

        b.Defensive("内丹：血量 < 40%",
            A.SecondWind,
            s => s.HpPercent < 40f && s.CanUseSelf(A.SecondWind),
            TargetSlot.Self);

        b.Defensive("浴血：血量 < 70% 且在副本内",
            A.Bloodbath,
            s => s.HpPercent < 70f && s.InDuty && s.CanUseSelf(A.Bloodbath),
            TargetSlot.Self);

        b.Defensive("牵制：副本内承伤",
            A.Feint,
            s => s.InDuty && s.CanUse(A.Feint));

        b.Utility("真北：需要打身位时",
            A.TrueNorth,
            s => s.InDuty && s.CanUseSelf(A.TrueNorth),
            TargetSlot.Self);
    }
}
