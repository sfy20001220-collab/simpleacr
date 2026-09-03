namespace SimpleACR.Data;

/// <summary>
/// 职业 ID = ClassJob 表的 RowId。写在 [Rotation("...", Job.PLD)] 里用。
/// </summary>
public static class Job
{
    // ===== 战斗职业（基础职业）=====
    public const uint GLA = 1;   // 剑术师
    public const uint PGL = 2;   // 格斗家
    public const uint MRD = 3;   // 斧术师
    public const uint LNC = 4;   // 枪术师
    public const uint ARC = 5;   // 弓箭手
    public const uint CNJ = 6;   // 幻术师
    public const uint THM = 7;   // 咒术师

    // ===== 战斗职业（特职）=====
    public const uint PLD = 19;  // 骑士
    public const uint MNK = 20;  // 武僧
    public const uint WAR = 21;  // 战士
    public const uint DRG = 22;  // 龙骑士
    public const uint BRD = 23;  // 吟游诗人
    public const uint WHM = 24;  // 白魔法师
    public const uint BLM = 25;  // 黑魔法师
    public const uint SMN = 26;  // 召唤师
    public const uint SCH = 27;  // 学者
    public const uint NIN = 28;  // 忍者
    public const uint MCH = 29;  // 机工士
    public const uint DRK = 30;  // 暗黑骑士
    public const uint AST = 31;  // 占星术士
    public const uint SAM = 32;  // 武士
    public const uint RDM = 33;  // 赤魔法师
    public const uint BLU = 34;  // 青魔法师（有限职业）
    public const uint GNB = 35;  // 绝枪战士
    public const uint DNC = 36;  // 舞者
    public const uint RPR = 37;  // 钐镰客
    public const uint SGE = 38;  // 贤者
    public const uint VPR = 39;  // 蝰蛇剑士（7.0）
    public const uint PCT = 40;  // 绘灵法师（7.0）

    public static string Name(uint jobId) => jobId switch
    {
        GLA => "剑术师", PGL => "格斗家", MRD => "斧术师", LNC => "枪术师",
        ARC => "弓箭手", CNJ => "幻术师", THM => "咒术师",
        PLD => "骑士", MNK => "武僧", WAR => "战士", DRG => "龙骑士",
        BRD => "吟游诗人", WHM => "白魔法师", BLM => "黑魔法师", SMN => "召唤师",
        SCH => "学者", NIN => "忍者", MCH => "机工士", DRK => "暗黑骑士",
        AST => "占星术士", SAM => "武士", RDM => "赤魔法师", BLU => "青魔法师",
        GNB => "绝枪战士", DNC => "舞者", RPR => "钐镰客", SGE => "贤者",
        VPR => "蝰蛇剑士", PCT => "绘灵法师",
        _ => $"未知职业({jobId})",
    };
}
