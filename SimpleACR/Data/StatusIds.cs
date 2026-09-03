namespace SimpleACR.Data;

/// <summary>
/// 状态（buff / debuff）ID 常量表 = Status 表 RowId。
///
/// 同样的⚠️：版本更新会变。核对方法：
///   /sacr find 之后在 /xldata 里对照，或在游戏里对着目标用插件打印 StatusList。
///
/// 标注「?」的是我把握没那么大的，用之前务必自己验一遍 —— buff ID 写错的后果
/// 是"条件永远不成立"，症状是那个技能一次都不按，比技能 ID 错了更难查。
/// </summary>
public static class StatusIds
{
    // ================= 骑士 PLD =================
    public static class Pld
    {
        public const uint FightOrFlight = 76;      // 战逃（+伤害 20s）
        public const uint Requiescat = 1368;       // 安魂（魔法增伤 + 圣灵瞬发，按层数）
        public const uint GoringBladeDot = 725;    // 沥血剑 DoT（7.0 前）
        public const uint AtonementReady = 1902;   // 赎罪剑可执行
        public const uint DivineMight = 2675;      // 7.x 神圣威力：下一次圣灵瞬发+增伤 ?
        public const uint Sentinel = 74;           // 铁壁减伤
        public const uint HallowedGround = 82;     // 无敌
    }

    // ================= 战士 WAR =================
    public static class War
    {
        public const uint Berserk = 86;            // 狂暴（必直）
        public const uint InnerRelease = 1177;     // 原初的解放（免费裂石飞环，按层数）
        public const uint SurgingTempest = 2677;   // 激浪（暴风斩给的 +伤害 buff）
        public const uint NascentChaos = 1897;     // 混沌之种（下一次裂石必直）
        public const uint PrimalRendReady = 2624;  // 原初之血刃可执行 ?
        public const uint Vengeance = 89;          // 复仇减伤 ?
        public const uint ThrillOfBattle = 87;     // 战栗
        public const uint Holmgang = 409;          // 死斗 ?
        public const uint Defiance = 91;           // 守护姿态 ?
    }

    // ================= 蝰蛇剑士 VPR =================
    //
    // ⚠️ buff ID 写错的后果是"条件永远不成立" —— 症状是那个技能一次都不按，
    //    比技能 ID 写错更难查。所以下面标「?」的一律额外用 CanUse 兜底：
    //    循环里凡是不确定的 buff，都写成
    //        s.HasBuff(S.Xxx) && s.CanUse(A.Yyy)
    //    这样即使 buff ID 错了，技能本身能按就还是会按。
    public static class Vpr
    {
        public const uint Reawakened = 3670;        // 祖灵附体（Reawaken 连段中）
        public const uint ReadyToReawaken = 3671;   // 祖灵预备（量谱攒满，可开祖灵）
        public const uint HuntersInstinct = 3668;   // 猎人直觉（+10% 伤害，40s）
        public const uint Swiftscaled = 3669;       // 疾速之牙（+15% 技速，40s）
        public const uint HonedSteel = 3672;        // 咬噬强化（咬噬尖齿 +100 威力）
        public const uint HonedReavers = 3772;      // 穿裂强化（穿裂尖齿 +100 威力）

        // --- 毒（连击带出的各类 buff）---
        public const uint FlankstingVenom = 3645;   // 侧击之毒 ?
        public const uint FlanksbaneVenom = 3646;   // 侧裂之毒 ?
        public const uint HindstingVenom = 3647;    // 背击之毒 ?
        public const uint HindsbaneVenom = 3648;    // 背裂之毒 ?
        public const uint GrimhuntersVenom = 3649;  // 阴惨猎毒 ?
        public const uint GrimskinsVenom = 3650;    // 阴惨肤毒 ?
        public const uint HuntersVenom = 3657;      // 猎毒 ?
        public const uint SwiftskinsVenom = 3658;   // 疾速肤毒 ?
        public const uint FellhuntersVenom = 3659;  // 凶猎毒 ?
        public const uint FellskinsVenom = 3660;    // 凶肤毒 ?

        // --- 触发预备 ---
        public const uint PoisedForDeathRattle = 3667; // 夺命之毒（蛇尾追击可用）
        public const uint ReadyToRip = 3665;        // 双牙连击预备 ?
        public const uint ReadyToTear = 3666;       // 双牙乱击预备 ?

        // --- 通用 ---
        public const uint TrueNorth = 1250;         // 真北
        public const uint Bloodbath = 84;           // 浴血 ?
        public const uint Feint = 1195;             // 牵制 ?
    }
}
