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
}
