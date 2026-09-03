using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Types;
using SimpleACR.Rotations;

namespace SimpleACR.Core;

/// <summary>
/// 自动循环引擎 —— 整个插件的心脏。
///
/// 它每帧只做四件事：
///   1. 抓一帧战斗快照（CombatState）
///   2. 取当前职业的循环表
///   3. 自上而下求值，挑出第一条「条件成立」的技能
///   4. 过节奏闸门（GCD / oGCD 窗口）+ 防抖，然后真的按下去
///
/// 关键设计取舍：
///   * 所有逻辑跑在 IFramework.Update 上（游戏主线程）。FF14 的原生函数
///     不能在别的线程调，这是硬约束。
///   * 轮询间隔做成可配置（默认 100ms），别每帧全量计算。
///   * Try/Catch 包住整块逻辑：一旦循环脚本里某个条件抛异常，
///     最坏的结果是这一帧不动作，而不是把游戏卡死或崩掉。
/// </summary>
public sealed class RotationEngine : IDisposable
{
    private long _lastTickMs;
    private uint _currentJobId;
    private bool _running;

    /// <summary>当前生效的循环。</summary>
    public Rotation? Current { get; private set; }

    /// <summary>这一帧选中了哪条（UI 上显示用）。</summary>
    public RotationEntry? Next { get; private set; }

    /// <summary>上一帧的战斗快照（UI / 调试用）。</summary>
    public CombatState? LastState { get; private set; }

    /// <summary>引擎当前在干嘛，一行人话。</summary>
    public string StatusText { get; private set; } = "未启动";

    /// <summary>最近一次施放的技能名（UI 用）。</summary>
    public string LastActionText { get; private set; } = "—";

    /// <summary>技能 → 上次施放时间戳，用于防抖。</summary>
    private readonly Dictionary<uint, long> _lastUsedAt = new();

    public RotationEngine()
    {
        Service.Framework.Update += OnFrameworkUpdate;
        _running = true;
    }

    public void Dispose()
    {
        if (!_running) return;
        _running = false;
        Service.Framework.Update -= OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        var now = Environment.TickCount64;

        // 节流：不是每一帧都算，够快就行
        if (now - _lastTickMs < Service.Config.TickIntervalMs) return;
        _lastTickMs = now;

        try
        {
            Tick(now);
        }
        catch (Exception ex)
        {
            // 宁可这一帧什么都不做，也不要把异常抛进游戏主线程
            Service.Log.Error(ex, "[SimpleACR] 引擎帧内异常");
            StatusText = "异常，详见 /xllog";
        }
    }

    // ==================================================================

    private void Tick(long now)
    {
        var cfg = Service.Config;

        if (!cfg.Enabled) { Idle("已关闭"); return; }
        // 注意：新版 Dalamud 里 LocalPlayer 从 IClientState 挪到了 IObjectTable 上
        if (Service.ObjectTable.LocalPlayer is null) { Idle("未登录"); return; }
        if (Service.Condition[ConditionFlag.Mounted]) { Idle("骑乘中"); return; }
        if (cfg.OnlyInCombat && !Service.Condition[ConditionFlag.InCombat]) { Idle("非战斗"); return; }
        if (cfg.OnlyInDuty && !Service.Condition[ConditionFlag.BoundByDuty]) { Idle("非副本"); return; }

        var st = CombatState.Snapshot();
        if (st == null) { Idle("状态不可用"); return; }
        LastState = st;

        // 职业变了 → 换循环（也处理覆盖配置）
        if (_currentJobId != st.JobId)
        {
            _currentJobId = st.JobId;
            cfg.JobRotationOverride.TryGetValue(st.JobId, out var preferred);
            Current = Service.RotationManager.GetFor(st.JobId, preferred);
            _lastUsedAt.Clear();
        }

        if (Current == null)
        {
            Idle($"职业 {st.JobId} 暂无循环");
            return;
        }

        // 没目标时自动选敌（AE 也有这个行为）
        if (!st.HasTarget && cfg.AutoTargetNearest)
        {
            var enemy = st.NearestEnemy(cfg.AutoTargetRange);
            if (enemy != null)
            {
                Service.TargetManager.Target = enemy;
                StatusText = $"自动选敌：{enemy.Name.TextValue}";
                return; // 选完这帧就结束，下一帧再打
            }
        }

        // ---- 求值：自上而下，取"条件成立 且 这一刻真的按得出"的第一条 ----
        //
        // 为什么不是"第一条条件成立就锁定"：
        //   老写法会在条件命中后就 break，然后才去 CanUse。一旦这条现在按不出来
        //   （连锁技能的下一步没亮、目标超出射程、资源不够），整帧就直接放弃了，
        //   循环表后面的技能一条都不会被考虑 —— 表现为"打着打着突然不动了"。
        //   连锁/触发型职业（蝰蛇祖灵连段、武士回天返照、机工过热）必然踩到。
        //
        //   现在改成：条件不成立 → 跳过；节奏不对 / 按不出来 → 也跳过，继续往后找。
        //   同时记下"第一条条件成立但被挡住"的原因，UI 上照样能看到诊断信息。
        RotationEntry? pick = null;
        ulong pickTarget = 0;
        string? blocked = null;

        foreach (var entry in Current.Entries)
        {
            if (!entry.Enabled) continue;
            if (entry.Condition != null && !entry.Condition(st)) continue;

            // 已经找到可执行的了，就不用再看后面
            if (pick != null) break;

            if (!TimingAllowed(entry, st))
            {
                blocked ??= $"等待节奏：{ActionExecutor.NameOf(entry.ActionId)}";
                continue;
            }

            var tid = ResolveTarget(entry.Target, st);
            if (tid == 0)
            {
                blocked ??= $"{ActionExecutor.NameOf(entry.ActionId)}：无有效目标";
                continue;
            }

            if (!ActionExecutor.CanUse(entry.ActionId, tid))
            {
                blocked ??= $"{ActionExecutor.NameOf(entry.ActionId)} 暂时不可用";
                continue;
            }

            pick = entry;
            pickTarget = tid;
        }

        Next = pick;

        if (pick == null)
        {
            // 条件一条都没成立，或者成立的全被挡住了
            StatusText = blocked ?? "无满足条件的技能";
            return;
        }

        // ---- 防抖 ----
        if (_lastUsedAt.TryGetValue(pick.ActionId, out var last) &&
            now - last < cfg.ActionDebounceMs)
            return;

        var targetId = pickTarget;

        // ---- 真的按下去 ----
        if (ActionExecutor.Use(pick.ActionId, targetId))
        {
            _lastUsedAt[pick.ActionId] = now;
            pick.HitCount++;
            var name = ActionExecutor.NameOf(pick.ActionId);
            LastActionText = name;
            StatusText = $"施放 {name}";

            if (cfg.DebugLog)
                Service.Log.Debug($"[SimpleACR] {name} ({pick.ActionId}) → {targetId:X} | {st}");
        }
        else
        {
            StatusText = $"{ActionExecutor.NameOf(pick.ActionId)} 被游戏拒绝";
        }
    }

    private void Idle(string why)
    {
        StatusText = why;
        Next = null;
    }

    /// <summary>
    /// 节奏闸门：决定这条技能"现在这个时刻"能不能按。
    /// 条件成立 ≠ 现在就能按，中间还隔着 GCD 和咏唱。
    /// </summary>
    private static bool TimingAllowed(RotationEntry entry, CombatState st)
    {
        switch (entry.Category)
        {
            case ActionCategory.Gcd:
                // GCD 技能：必须 GCD 转好 + 不在咏唱中
                return !st.GcdRolling && !st.IsCasting;

            case ActionCategory.Ogcd:
            case ActionCategory.Utility:
                return Service.Config.UseOgcd && st.CanWeave();

            case ActionCategory.Defensive:
                return Service.Config.UseDefensives && st.CanWeave();

            default:
                return true;
        }
    }

    private static ulong ResolveTarget(TargetSlot slot, CombatState st)
    {
        switch (slot)
        {
            case TargetSlot.Self:
                return ActionExecutor.SelfTargetId;

            case TargetSlot.LowestHpParty:
                return st.LowestHpPartyMember()?.GameObjectId ?? ActionExecutor.SelfTargetId;

            case TargetSlot.NearestEnemy:
                return st.NearestEnemy(Service.Config.AutoTargetRange)?.GameObjectId ?? 0;

            case TargetSlot.Focus:
                return Service.TargetManager.FocusTarget?.GameObjectId ?? ActionExecutor.SelfTargetId;

            case TargetSlot.Target:
            default:
                return st.TargetId != 0 ? st.TargetId : ActionExecutor.SelfTargetId;
        }
    }
}
