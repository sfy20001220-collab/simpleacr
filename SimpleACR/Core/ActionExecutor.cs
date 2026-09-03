using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using Lumina.Text;

// 老版本 Dalamud/Lumina 里 Action 表在 Lumina.Excel.GeneratedSheets 下，
// 如果你编译报 "找不到 Action"，把上面两行 using 改成：
//   using Lumina.Excel.GeneratedSheets;
//   using Lumina.Text;
// 并把 ActionExecutor 里对 row.Value.X 的访问保持不变。
using ActionRow = Lumina.Excel.Sheets.Action;

namespace SimpleACR.Core;

/// <summary>
/// 技能执行层：所有对游戏 ActionManager 的直接调用都收敛在这一个类里。
///
/// 为什么需要它：
///   Dalamud 的官方服务（IClientState / IObjectTable / ...）只提供"读"的能力，
///   并不提供"施放技能"。要按技能，必须走 FFXIVClientStructs 里的原生
///   ActionManager —— 这也是所有自动循环插件（AE / RSR / WrathCombo）的共同做法。
///
/// 三个最核心的原生函数：
///   GetActionStatus(actionType, actionId, targetId) → 0 表示现在可以按，非 0 是不可用原因
///   UseAction(actionType, actionId, targetId)       → 真正按下技能，返回是否成功
///   GetRecastGroupDetail(index)                     → 读复唱组（GCD / 长 CD）的剩余时间
/// </summary>
internal static unsafe class ActionExecutor
{
    /// <summary>
    /// GCD 所在的复唱组索引。这是社区常用的常量（57 = 2.5s 通用 GCD 组）。
    /// 不同 ClientStructs / 版本可能不同，如果你发现 UI 上 GCD 一直是 0 或乱跳，
    /// 在调试面板里对比一下实际 GCD，把数值改掉即可。
    /// </summary>
    public const int GcdRecastGroup = 57;

    /// <summary>指向自己的目标 ID。FF14 里 0xE0000000 表示"自己"。</summary>
    public const ulong SelfTargetId = 0xE000_0000UL;

    private static ActionManager* AM => ActionManager.Instance();

    // ==================================================================
    // 基础三件套
    // ==================================================================

    /// <summary>技能当前是否可按（冷却、射程、MP、等级、咏唱状态全交给游戏判断）。</summary>
    public static bool CanUse(uint actionId, ulong targetId = SelfTargetId)
    {
        var am = AM;
        if (am == null) return false;
        return am->GetActionStatus(ActionType.Action, actionId, targetId) == 0;
    }

    /// <summary>施放技能。返回 true 表示这一帧真的按下去了。</summary>
    public static bool Use(uint actionId, ulong targetId = SelfTargetId)
    {
        var am = AM;
        if (am == null) return false;
        return am->UseAction(ActionType.Action, actionId, targetId);
    }

    /// <summary>
    /// 技能被连招/特质替换后的实际 ID。
    /// 例如骑士在连招第二段时，"快破剑"(9) 会被替换成 "暴乱剑"(15)，
    /// 用这个方法能拿到替换后的 ID —— 写条件判断时非常有用。
    /// </summary>
    public static uint Adjusted(uint actionId)
    {
        var am = AM;
        return am == null ? actionId : am->GetAdjustedActionId(actionId);
    }

    // ==================================================================
    // 冷却 / 充能
    // ==================================================================

    /// <summary>技能剩余冷却秒数（不受 GCD 影响的独立冷却）。</summary>
    public static float CooldownRemaining(uint actionId)
    {
        var am = AM;
        if (am == null) return 0f;

        var row = GetRow(actionId);
        if (row == null) return 0f;

        // CooldownGroup 为 0/1 表示没有独立冷却（跟着 GCD 走）
        int group = row.Value.CooldownGroup;
        if (group <= 1) return 0f;

        // 注意：表里是 1-based，GetRecastGroupDetail 要 0-based
        var detail = am->GetRecastGroupDetail(group - 1);
        if (detail == null) return 0f;

        // 注意：新版 FFXIVClientStructs 里 RecastDetail.IsActive 是 bool，
        // 老代码里的 `IsActive != 0` 会报 CS0019（bool 和 int 不能做 !=）。
        return detail->IsActive ? MathF.Max(0f, detail->Total - detail->Elapsed) : 0f;
    }

    /// <summary>
    /// 当前可用的充能层数（例如调停 2 层、猛攻 3 层）。
    /// 这是基于 Lumina 表的**估算**；如果你用的 ClientStructs 有
    /// ActionManager.GetCurrentCharges(uint)，优先换成原生函数：
    ///     return (int)AM->GetCurrentCharges(actionId);
    /// </summary>
    public static int CurrentCharges(uint actionId)
    {
        var row = GetRow(actionId);
        if (row == null) return 1;

        // MaxCharges 在数据表里是 byte，不转 int 的话 Math.Max 会在 byte/int 重载间二义（CS0121）
        int max = Math.Max(1, (int)row.Value.MaxCharges);
        float remaining = CooldownRemaining(actionId);
        if (remaining <= 0.01f) return max;

        // 充能类技能：表里 Recast100ms 是"回一层"的时间
        float perCharge = MathF.Max(0.01f, row.Value.Recast100ms / 100f);
        int pending = (int)MathF.Ceiling(remaining / perCharge);
        return Math.Clamp(max - pending, 0, max);
    }

    // ==================================================================
    // GCD
    // ==================================================================

    /// <summary>当前 GCD 总时长（秒）。受技速、狂暴、武神等影响，会实时变。</summary>
    public static float GcdTotal()
    {
        var am = AM;
        if (am == null) return 2.5f;
        var d = am->GetRecastGroupDetail(GcdRecastGroup);
        return d == null ? 2.5f : d->Total;
    }

    /// <summary>GCD 还剩多久转好（秒）。0 表示 GCD 已经转好，可以接下一个战技/魔法。</summary>
    public static float GcdRemaining()
    {
        var am = AM;
        if (am == null) return 0f;
        var d = am->GetRecastGroupDetail(GcdRecastGroup);
        if (d == null) return 0f;
        return d->IsActive ? MathF.Max(0f, d->Total - d->Elapsed) : 0f;
    }

    /// <summary>GCD 是否正在转（=true 则此刻按不出任何 GCD 技能）。</summary>
    public static bool IsGcdRolling() => GcdRemaining() > 0.01f;

    // ==================================================================
    // 连招状态
    // ==================================================================

    /// <summary>上一步实际打出的技能 ID（用于判断"我现在在连招第几段"）。</summary>
    public static uint ComboLastAction()
    {
        var am = AM;
        return am == null ? 0u : am->Combo.Action;
    }

    /// <summary>连招还剩多少秒断掉。</summary>
    public static float ComboRemaining()
    {
        var am = AM;
        return am == null ? 0f : am->Combo.Timer;
    }

    // ==================================================================
    // 数据查询辅助
    // ==================================================================

    private static readonly Dictionary<uint, ActionRow?> RowCache = new();

    /// <summary>从 Lumina 的 Action 表取一行。带缓存，别每帧查表。</summary>
    public static ActionRow? GetRow(uint actionId)
    {
        if (RowCache.TryGetValue(actionId, out var cached)) return cached;

        ActionRow? row = null;
        try
        {
            var sheet = Service.DataManager.GetExcelSheet<ActionRow>();
            if (sheet != null) row = sheet.GetRow(actionId);
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, $"[SimpleACR] 读取 Action 表 {actionId} 失败");
        }

        RowCache[actionId] = row;
        return row;
    }

    /// <summary>技能在游戏内的显示名（跟随客户端语言）。用于 UI 和 /sacr find。</summary>
    public static string NameOf(uint actionId)
    {
        var row = GetRow(actionId);
        if (row == null) return $"<未知 {actionId}>";
        try
        {
            // SeString 带富文本标记，ExtractText() 剥掉标记取纯文本。
            // 老版本 Lumina 没有这个扩展方法时，改成 row.Value.Name.ToString() 即可。
            var s = row.Value.Name.ExtractText();
            return string.IsNullOrEmpty(s) ? $"#{actionId}" : s;
        }
        catch
        {
            return $"#{actionId}";
        }
    }

    /// <summary>技能是否存在于当前客户端的数据表里（用来判断 ID 有没有写错/过时）。</summary>
    public static bool Exists(uint actionId) => GetRow(actionId) != null;
}
