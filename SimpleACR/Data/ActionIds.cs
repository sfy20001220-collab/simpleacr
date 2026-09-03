namespace SimpleACR.Data;

/// <summary>
/// 技能 ID 常量表（Action 表 RowId）。
///
/// ⚠️ 重要：技能 ID 是写死在客户端数据表里的，版本更新时可能变动。
/// 本表基于 7.x（黄金的遗产）整理，少数较新技能标了「?」表示需要你自己核对。
///
/// 核对方法（三选一）：
///   1. 游戏里输入 /sacr find 技能名    → 本插件自带，直接搜 Action 表（推荐）
///   2. 用卫月自带的 /xldata → Addon Inspector 或 Lumina 查阅
///   3. 查社区维护的技能 ID 表
///
/// 如果某个 ID 在当前客户端不存在，RotationManager 启动校验时会打日志警告，
/// 对应条目仍会保留但永远按不出来 —— 所以看到警告一定要去核对。
/// </summary>
public static class ActionIds
{
    // ==================================================================
    // 骑士 PLD（含剑术师 GLA）
    // ==================================================================
    public static class Pld
    {
        // --- 单体连招 ---
        public const uint FastBlade = 9;          // 快破剑
        public const uint RiotBlade = 15;         // 暴乱剑
        public const uint RageOfHalone = 21;      // 战女神之怒
        public const uint RoyalAuthority = 3539;  // 王权剑
        public const uint Atonement = 16460;      // 赎罪剑
        public const uint GoringBlade = 3538;     // 沥血剑（7.0 前为 DoT，7.x 可能已改）

        // --- 群体连招 ---
        public const uint TotalEclipse = 7380;    // 全蚀斩
        public const uint Prominence = 16457;     // 日珥斩

        // --- 魔法 ---
        public const uint HolySpirit = 7384;      // 圣灵
        public const uint HolyCircle = 16458;     // 圣环
        public const uint Clemency = 3541;        // 深仁厚泽
        public const uint Requiescat = 7383;      // 安魂

        // --- 安魂后的「告白」连击（7.x 核心爆发）---
        public const uint Confiteor = 16459;      // 悔罪告白
        public const uint BladeOfFaith = 16462;   // 信仰之剑
        public const uint BladeOfTruth = 16463;   // 真理之剑
        public const uint BladeOfValor = 16464;   // 勇气之剑

        // --- 能力技 ---
        public const uint FightOrFlight = 20;     // 战逃
        public const uint SpiritsWithin = 29;     // 深奥之灵（86 级后被 Expiacion 替换）
        public const uint Expiacion = 25747;      // 赎罪（6.x 起替换深奥之灵）?
        public const uint CircleOfScorn = 23;     // 悔罪（AOE DoT）
        public const uint Intervene = 16461;      // 调停（位移，2 层）
        public const uint ShieldBash = 16;        // 盾牌猛击（打断/眩晕）
        public const uint ShieldLob = 24;         // 投盾（远程）

        // --- 减伤 / 自保 ---
        public const uint Sentinel = 17;          // 铁壁
        public const uint Sheltron = 3542;        // 预警（耗忠义）
        public const uint Intervention = 7381;    // 干预（给队友）
        public const uint DivineVeil = 3540;      // 神盾
        public const uint HallowedGround = 30;    // 神圣领域（无敌）
        public const uint PassageOfArms = 7385;   // 武装展翼（群体减伤）
    }

    // ==================================================================
    // 战士 WAR（含斧术师 MRD）
    // ==================================================================
    public static class War
    {
        // --- 单体连招 ---
        public const uint HeavySwing = 31;        // 重殴
        public const uint Maim = 37;              // 凶残裂
        public const uint StormsPath = 42;        // 暴风碎
        public const uint StormsEye = 45;         // 暴风斩（7.0 前用来续 Surging Tempest）

        // --- 群体连招 ---
        public const uint Overpower = 41;         // 超压斧
        public const uint MythrilTempest = 16468; // 秘银暴风
        public const uint Decimate = 3550;        // 地毁人亡
        public const uint ChaoticCyclone = 16466; // 混乱旋风

        // --- 兽魂消耗 ---
        public const uint InnerBeast = 49;        // 原初之兽（单体）
        public const uint FellCleave = 3549;      // 裂石飞环（单体）
        public const uint InnerChaos = 16467;     // 内部混沌（必直，需 Nascent Chaos）
        public const uint SteelCyclone = 51;      // 钢铁旋风（群体）
        public const uint Infuriate = 52;         // 激怒（+50 兽魂）

        // --- 能力技 ---
        public const uint Berserk = 38;           // 狂暴
        public const uint InnerRelease = 7389;    // 原初的解放
        public const uint Upheaval = 7387;        // 动乱
        public const uint Onslaught = 7386;       // 猛攻（位移，3 层）
        public const uint Orogeny = 16469;        // 地鸣（群体版动乱）
        public const uint PrimalRend = 16470;     // 原初之血刃（解放后的收尾）
        public const uint Tomahawk = 46;          // 飞斧（远程）

        // --- 减伤 / 自保 ---
        public const uint Vengeance = 44;         // 复仇
        public const uint Holmgang = 43;          // 死斗（无敌）
        public const uint ThrillOfBattle = 40;    // 战栗
        public const uint Equilibrium = 3552;     // 泰然自若（回血）
        public const uint ShakeItOff = 7388;      // 摆脱（群体盾）
        public const uint RawIntuition = 3551;    // 直觉
        public const uint Bloodwhetting = 25751;  // 血气（6.1 起替换直觉）
        public const uint NascentFlash = 16465;   // 原初的勇猛（给队友）
        public const uint Defiance = 48;          // 守护（坦克姿态）
        public const uint ReleaseDefiance = 32066;// 解除守护
    }
}
