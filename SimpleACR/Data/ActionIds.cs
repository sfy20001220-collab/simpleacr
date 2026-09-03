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

    // ==================================================================
    // 蝰蛇剑士 VPR（7.0 黄金的遗产）
    //
    // ID 来源：中文维基（huijiwiki）的 Action 搜索表 + 社区 VPR 常量表交叉核对。
    // 中文名可能和你的客户端有出入（国服翻译会调整），但**行为由 ID 决定**，
    // 名字只影响注释和 UI 显示（UI 显示走 ActionExecutor.NameOf，读的是客户端数据）。
    // 想核对某一项：游戏里 /sacr find 技能名
    // ==================================================================
    public static class Vpr
    {
        // --- 单体「牙」连击 ---
        // 两条分支：咬噬尖齿 → 猛袭利齿 → 侧击獠齿 → 侧裂獠齿（走 猎人直觉 +伤害）
        //           穿裂尖齿 → 疾速利齿 → 背击獠齿 → 背裂獠齿（走 疾速之牙 +技速）
        public const uint SteelFangs = 34606;      // 咬噬尖齿（起手，给 穿裂强化）
        public const uint ReavingFangs = 34607;    // 穿裂尖齿（起手，给 咬噬强化）
        public const uint HuntersSting = 34608;    // 猛袭利齿（第二段·猎人侧）
        public const uint SwiftskinsSting = 34609; // 疾速利齿（第二段·疾速侧）
        public const uint FlankstingStrike = 34610;// 侧击獠齿（第三段·猎人侧）
        public const uint FlanksbaneFang = 34611;  // 侧裂獠齿（第四段·猎人侧）
        public const uint HindstingStrike = 34612; // 背击獠齿（第三段·疾速侧）
        public const uint HindsbaneFang = 34613;   // 背裂獠齿（第四段·疾速侧）

        // --- 群体「牙」连击 ---
        public const uint SteelMaw = 34614;        // 咬噬尖牙（AOE 起手）
        public const uint ReavingMaw = 34615;      // 穿裂尖牙（AOE 起手）
        public const uint HuntersBite = 34616;     // 猛袭利牙（AOE 第二段·猎人侧）
        public const uint SwiftskinsBite = 34617;  // 疾速利牙（AOE 第二段·疾速侧）
        public const uint JaggedMaw = 34618;       // 乱击獠牙（AOE 第三段·猎人侧）
        public const uint BloodiedMaw = 34619;     // 乱裂獠牙（AOE 第三段·疾速侧）

        // --- 盘蛇（Vicewinder 系）---
        public const uint Vicewinder = 34620;      // 强碎灵蛇（单体，起盘蛇）
        public const uint HuntersCoil = 34621;     // 猛袭盘蛇
        public const uint SwiftskinsCoil = 34622;  // 疾速盘蛇
        public const uint Vicepit = 34623;         // 强碎灵蝰（AOE 版盘蛇）
        public const uint HuntersDen = 34624;      // 猛袭盘蝰（AOE 盘蛇·猎人侧）
        public const uint SwiftskinsDen = 34625;   // 疾速盘蝰（AOE 盘蛇·疾速侧）

        // --- 祖灵降临（Reawaken）爆发连段 ---
        // 单体：一式 → 二式 → 三式 → 四式 → 祖灵大蛇牙（Ouroboros）
        // 群体：一式 → 二式 → 三式 → 四式 → 同上（用「蛇」那套）
        public const uint Reawaken = 34626;        // 祖灵降临
        public const uint FirstGeneration = 34627; // 祖灵之牙一式
        public const uint SecondGeneration = 34628;// 祖灵之牙二式
        public const uint ThirdGeneration = 34629; // 祖灵之牙三式
        public const uint FourthGeneration = 34630;// 祖灵之牙四式
        public const uint Ouroboros = 34631;       // 祖灵大蛇牙（收尾，AOE 大伤害）
        public const uint FirstLegacy = 34640;     // 祖灵之蛇一式（AOE 连段）?
        public const uint SecondLegacy = 34641;    // 祖灵之蛇二式（AOE 连段）?
        public const uint ThirdLegacy = 34642;     // 祖灵之蛇三式（AOE 连段）?
        public const uint FourthLegacy = 34643;    // 祖灵之蛇四式（AOE 连段）?

        // --- 远程 / 机动 ---
        public const uint WrithingSnap = 34632;    // 飞蛇之牙（远程 GCD）
        public const uint UncoiledFury = 34633;    // 飞蛇之尾（远程 GCD，耗盘蛇层数）
        public const uint UncoiledTwinfang = 34644;// 飞蛇连尾击（飞蛇之尾后接续）
        public const uint UncoiledTwinblood = 34645;// 飞蛇乱尾击
        public const uint Slither = 34646;         // 蛇行（位移，oGCD）

        // --- 触发型追击（打完特定技能后亮起）---
        public const uint DeathRattle = 34634;     // 蛇尾击（单体蛇尾追击）
        public const uint LastLash = 34635;        // 蛇尾闪（AOE 蛇尾追击）
        public const uint TwinfangBite = 34636;    // 双牙连击（单体）
        public const uint TwinfangThresh = 34637;  // 双牙乱击（AOE）
        public const uint TwinbloodBite = 34638;   // 双牙连闪（单体）
        public const uint TwinbloodThresh = 34639; // 双牙乱闪（AOE）

        // --- 能力技 / 增益 ---
        public const uint SerpentsIre = 34647;     // 蛇灵气（+伤害 buff，攒祖灵量谱）

        // --- 通用（全近战共有，ID 通用）---
        public const uint SecondWind = 7541;       // 内丹
        public const uint Bloodbath = 7542;        // 浴血
        public const uint TrueNorth = 7546;        // 真北
        public const uint ArmsLength = 7548;       // 亲疏自行
        public const uint Feint = 7549;            // 牵制
        public const uint LegSweep = 7863;         // 扫腿（打断/眩晕）
    }
}
