using SimpleACR.Core;

namespace SimpleACR.Rotations;

/// <summary>
/// 技能分类。引擎据此决定"什么时候允许按"。
///
/// FF14 的技能分两类：
///   GCD（战技 / 魔法）：共用 2.5s 左右的公共冷却，转好才能按下一个
///   oGCD（能力）：有自己的 CD，但必须在 GCD 后段插入才不吃 GCD
/// </summary>
public enum ActionCategory
{
    /// <summary>战技 / 魔法，占用 GCD。只有 GCD 转好时才会被按出。</summary>
    Gcd,

    /// <summary>能力技，不占 GCD。只在 GCD 剩余 &lt; 配置窗口 时才插入。</summary>
    Ogcd,

    /// <summary>减伤 / 自保类。逻辑上也是 oGCD，但可以单独开关。</summary>
    Defensive,

    /// <summary>功能性（打断、驱散、位移等）。当前引擎里和 Ogcd 同样处理。</summary>
    Utility,
}

/// <summary>技能施放目标的选择策略。</summary>
public enum TargetSlot
{
    /// <summary>当前选中的目标（没有目标时用自动选敌结果）</summary>
    Target,

    /// <summary>自己（0xE0000000）</summary>
    Self,

    /// <summary>小队里 HP 百分比最低的人（写治疗/保护技能用）</summary>
    LowestHpParty,

    /// <summary>最近的敌人（AOE 或开怪用）</summary>
    NearestEnemy,

    /// <summary>焦点目标</summary>
    Focus,
}

/// <summary>
/// 循环表里的**一条**记录 = "在满足某某条件时，对某某目标按某某技能"。
///
/// AE / RSR / WrathCombo 本质上都是这个模型，只是表达方式不同：
///   AE  : ACR 脚本里的一行（条件 + 技能）
///   RSR : IAction 的 UseIf(...)
///   这里: RotationEntry
/// </summary>
public sealed class RotationEntry
{
    /// <summary>给自己看的备注，会显示在 UI 上。写清楚为什么放这一行。</summary>
    public string Name { get; init; } = string.Empty;

    public uint ActionId { get; init; }

    /// <summary>触发条件。null 表示无条件（一定会被选中，仅受可用性与节奏限制）。</summary>
    public Func<CombatState, bool>? Condition { get; init; }

    public ActionCategory Category { get; init; } = ActionCategory.Gcd;

    public TargetSlot Target { get; init; } = TargetSlot.Target;

    /// <summary>false 时整条被跳过（可以在 UI 上动态关掉某条先做验证）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>调试用：本条命中的次数。</summary>
    public int HitCount { get; set; }

    public override string ToString() =>
        $"[{Category,-9}] {ActionId,6} {ActionExecutor.NameOf(ActionId)}  ← {Name}";
}

/// <summary>循环的元信息（来自类上的 [Rotation] 特性）。</summary>
public sealed record RotationMeta(string Name, uint JobId, string Author, string Patch);

/// <summary>
/// 标记一个类是"某职业的循环脚本"。RotationManager 靠反射扫这个特性来注册。
///
/// 用法：
/// <code>
/// [Rotation("骑士·7.x 基础循环", Job.PLD, Author = "you", Patch = "7.1")]
/// public sealed class MyPaladin : Rotation { ... }
/// </code>
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RotationAttribute : Attribute
{
    public RotationAttribute(string name, uint jobId)
    {
        Name = name;
        JobId = jobId;
    }

    public string Name { get; }
    public uint JobId { get; }
    public string Author { get; set; } = "unknown";
    public string Patch { get; set; } = "unknown";
}

/// <summary>
/// 一份职业循环的基类。
///
/// 你只需要继承它、实现 Build()，在里面按**优先级从高到低**把技能排下来。
/// 引擎每帧从第一条开始问："你现在成立吗？"，成立就选它，不再往后看。
///
/// 所以：爆发技能写前面，填充技能写最后 —— 这就是全部的核心思想。
/// </summary>
public abstract class Rotation
{
    public RotationMeta Meta { get; internal set; } = null!;

    /// <summary>排好序的技能表。</summary>
    public IReadOnlyList<RotationEntry> Entries { get; internal set; } = Array.Empty<RotationEntry>();

    public abstract void Build(RotationBuilder b);
}

/// <summary>
/// 用来流畅地拼出一张循环表。
///
/// <code>
/// b.Gcd ("沥血剑",  ActionIds.GoringBlade, s => s.TargetDebuffRemaining(StatusIds.GoringBlade) &lt; 3f);
/// b.Ogcd("战逃",    ActionIds.FightOrFlight, s => !s.IsCasting);
/// </code>
/// </summary>
public sealed class RotationBuilder
{
    private readonly List<RotationEntry> _entries = new();

    /// <summary>加一条 GCD 技能。</summary>
    public RotationBuilder Gcd(string name, uint actionId,
        Func<CombatState, bool>? condition = null,
        TargetSlot target = TargetSlot.Target)
        => Add(name, actionId, condition, ActionCategory.Gcd, target);

    /// <summary>加一条能力技（oGCD）。</summary>
    public RotationBuilder Ogcd(string name, uint actionId,
        Func<CombatState, bool>? condition = null,
        TargetSlot target = TargetSlot.Target)
        => Add(name, actionId, condition, ActionCategory.Ogcd, target);

    /// <summary>加一条减伤 / 自保。</summary>
    public RotationBuilder Defensive(string name, uint actionId,
        Func<CombatState, bool>? condition = null,
        TargetSlot target = TargetSlot.Self)
        => Add(name, actionId, condition, ActionCategory.Defensive, target);

    /// <summary>加一条功能性技能。</summary>
    public RotationBuilder Utility(string name, uint actionId,
        Func<CombatState, bool>? condition = null,
        TargetSlot target = TargetSlot.Target)
        => Add(name, actionId, condition, ActionCategory.Utility, target);

    /// <summary>通用添加。</summary>
    public RotationBuilder Add(string name, uint actionId,
        Func<CombatState, bool>? condition,
        ActionCategory category,
        TargetSlot target)
    {
        _entries.Add(new RotationEntry
        {
            Name = name,
            ActionId = actionId,
            Condition = condition,
            Category = category,
            Target = target,
        });
        return this;
    }

    internal IReadOnlyList<RotationEntry> Build() => _entries;
}
