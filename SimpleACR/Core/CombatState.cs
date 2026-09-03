using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
// IPlayerCharacter 在较新的 Dalamud 里从 Objects.Types 挪到了 Objects.SubKinds
using Dalamud.Game.ClientState.Objects.SubKinds;
using System.Numerics;

namespace SimpleACR.Core;

/// <summary>
/// 战斗状态快照 + 条件库。
///
/// 写循环脚本时你 90% 的时间是在跟这个类打交道 —— 它把 Dalamud 各种零散的
/// 服务（ClientState / TargetManager / ObjectTable / Condition / JobGauges）
/// 汇总成一个"此刻的战斗画面"，并提供了 AE 里那些条件函数（HasBuff / Ready /
/// ComboStep ...）的等价物。
///
/// 每帧由引擎 Snapshot() 一次，循环里所有的判断都读同一份快照，
/// 保证同一帧内数据一致，也避免反复调原生函数拖慢主线程。
/// </summary>
public sealed class CombatState
{
    // ================= 玩家 =================
    public IPlayerCharacter? Player { get; private init; }

    /// <summary>职业 ID（ClassJob RowId），19=骑士 21=战士，见 Data/Job.cs</summary>
    public uint JobId { get; private init; }

    public uint Level { get; private init; }

    public uint Hp { get; private init; }
    public uint MaxHp { get; private init; }
    public float HpPercent => MaxHp == 0 ? 0f : Hp * 100f / MaxHp;

    public uint Mp { get; private init; }
    public uint MaxMp { get; private init; }
    public float MpPercent => MaxMp == 0 ? 0f : Mp * 100f / MaxMp;

    // ================= 状态 =================
    public bool InCombat { get; private init; }
    public bool InDuty { get; private init; }
    public bool IsMounted { get; private init; }

    /// <summary>正在咏唱某个魔法。</summary>
    public bool IsCasting { get; private init; }

    /// <summary>咏唱剩余时间（秒）。读条最后 0.5s 就该准备插能力技了。</summary>
    public float CastRemaining { get; private init; }

    /// <summary>是否在移动（通过两帧位移判断，比读原生字段稳）。</summary>
    public bool IsMoving { get; private init; }

    /// <summary>位移速度（米/秒）。判断"站桩 vs 走位"用。</summary>
    public float MoveSpeed { get; private init; }

    // ================= 节奏 =================
    public float GcdTotal { get; private init; }
    public float GcdRemaining { get; private init; }

    /// <summary>GCD 是否正在转。</summary>
    public bool GcdRolling => GcdRemaining > 0.01f;

    /// <summary>
    /// 现在能不能插能力技。这是 oGCD 的闸门：
    /// GCD 快转好了（剩余 &lt; 窗口）才能插，否则会把 GCD 往后顶，俗称"吃 GCD"。
    /// </summary>
    public bool CanWeave() => GcdRemaining <= Service.Config.OgcdWindowSec;

    // ================= 连招 =================
    /// <summary>上一步打出的技能 ID。</summary>
    public uint ComboAction { get; private init; }

    public float ComboTimer { get; private init; }

    /// <summary>连招窗口是否还开着（一般 30s 内必须接下一段）。</summary>
    public bool InComboWindow => ComboTimer > 0.01f;

    // ================= 目标 =================
    public IBattleChara? Target { get; private init; }

    /// <summary>目标的 GameObjectId（传给 UseAction 用）。0 表示没目标。</summary>
    public ulong TargetId { get; private init; }

    public bool HasTarget => Target != null && TargetId != 0;

    /// <summary>到目标的距离（米）。超过技能射程就按不出来。</summary>
    public float TargetDistance { get; private init; }

    public uint TargetHp { get; private init; }
    public uint TargetMaxHp { get; private init; }
    public float TargetHpPercent => TargetMaxHp == 0 ? 0f : TargetHp * 100f / TargetMaxHp;

    // ================= 职业量谱（按需扩展）=================
    /// <summary>战士：兽魂（0~100）</summary>
    public int BeastGauge { get; private init; }

    /// <summary>骑士：忠义（0~100）</summary>
    public int OathGauge { get; private init; }

    // ==================================================================
    // 条件库：写循环时主要用下面这些
    // ==================================================================

    /// <summary>自己身上有没有某个 buff。</summary>
    public bool HasBuff(uint statusId, float minRemaining = 0f)
        => BuffRemaining(statusId) > minRemaining;

    public float BuffRemaining(uint statusId)
        => Remaining(Player, statusId);

    /// <summary>某个 buff 的层数（如狂暴层数、解放层数）。</summary>
    public int BuffStacks(uint statusId)
    {
        if (Player == null) return 0;
        foreach (var s in Player.StatusList)
            // 新版 Dalamud 的 IStatus 没有 StackCount，层数放在 Param 里
            if (s.StatusId == statusId) return s.Param;
        return 0;
    }

    /// <summary>目标身上有没有某个 debuff（DoT、破防等）。</summary>
    public bool TargetHasDebuff(uint statusId, float minRemaining = 0f)
        => TargetDebuffRemaining(statusId) > minRemaining;

    public float TargetDebuffRemaining(uint statusId)
        => Remaining(Target, statusId);

    /// <summary>技能是否在冷却（不含 GCD）。</summary>
    public float Cd(uint actionId) => ActionExecutor.CooldownRemaining(actionId);

    /// <summary>技能冷却是否好了（不含 GCD 判断）。</summary>
    public bool OffCooldown(uint actionId) => Cd(actionId) <= 0.01f;

    /// <summary>
    /// 这个技能**此刻**是不是真的按得出来（CD、射程、MP、等级、咏唱、前置状态
    /// 全部交给游戏判断）。
    ///
    /// 为什么条件库里需要它：
    ///   像蝰蛇的「祖灵连段」、武士的「回天返照」这类**连锁/触发型**技能，
    ///   每一步都只在特定时刻可用。如果只写"我有 Reawakened buff"就选中一式，
    ///   那打完一式后二式还没亮，引擎就会卡在一式上不动 ——
    ///   加上 CanUse 条件后，不成立就自动往后找当前真正能按的那一步。
    /// </summary>
    public bool CanUse(uint actionId)
    {
        ulong tid = TargetId != 0 ? TargetId : ActionExecutor.SelfTargetId;
        return ActionExecutor.CanUse(actionId, tid);
    }

    /// <summary>同上，但对**自己**施放（判断自保/自身增益技能用）。</summary>
    public bool CanUseSelf(uint actionId)
        => ActionExecutor.CanUse(actionId, ActionExecutor.SelfTargetId);

    /// <summary>当前可用的充能层数。</summary>
    public int Charges(uint actionId) => ActionExecutor.CurrentCharges(actionId);

    /// <summary>
    /// 是否处于连招的某一步。传多个 ID 表示"任意一段都算"。
    /// 例：s.ComboStep(ActionIds.HeavySwing) 表示"刚打完重殴，该打凶残裂了"。
    /// </summary>
    public bool ComboStep(params uint[] actionIds)
        => InComboWindow && actionIds.Contains(ComboAction);

    /// <summary>附近可攻击敌人的数量（AOE 判定用）。</summary>
    public int EnemyCount(float radius = 5f)
    {
        var p = Player;
        if (p == null) return 0;
        return Enemies(radius).Count();
    }

    /// <summary>附近可攻击敌人列表（按距离从近到远）。</summary>
    public IEnumerable<IBattleChara> Enemies(float radius = 25f)
    {
        var p = Player;
        if (p == null) yield break;

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IBattleChara bc) continue;
            if (obj.ObjectKind != ObjectKind.BattleNpc) continue;
            if (!bc.IsTargetable) continue;
            if (bc.CurrentHp <= 0) continue;

            var d = Vector3.Distance(p.Position, bc.Position);
            if (d > radius) continue;

            yield return bc;
        }
    }

    /// <summary>最近的敌人。</summary>
    public IBattleChara? NearestEnemy(float radius = 25f)
    {
        var p = Player;
        if (p == null) return null;
        return Enemies(radius)
            .OrderBy(e => Vector3.Distance(p.Position, e.Position))
            .FirstOrDefault();
    }

    /// <summary>小队里 HP 百分比最低的成员（不在队伍时就是自己）。</summary>
    public IGameObject? LowestHpPartyMember()
    {
        if (Player == null) return null;

        IGameObject? best = Player;
        float bestPct = HpPercent;

        foreach (var m in Service.PartyList)
        {
            var go = m.GameObject;
            if (go is not IBattleChara bc) continue;
            if (bc.CurrentHp <= 0) continue;
            var pct = bc.MaxHp == 0 ? 0f : bc.CurrentHp * 100f / bc.MaxHp;
            if (pct < bestPct) { bestPct = pct; best = bc; }
        }

        return best;
    }

    /// <summary>小队里最低的 HP 百分比。</summary>
    public float PartyMinHpPercent()
    {
        var m = LowestHpPartyMember();
        if (m is not IBattleChara bc) return HpPercent;
        return bc.MaxHp == 0 ? 0f : bc.CurrentHp * 100f / bc.MaxHp;
    }

    // ==================================================================
    // 构造
    // ==================================================================

    private static Vector3 _lastPosition;
    private static long _lastSnapshotMs;

    /// <summary>抓一帧快照。任何一步失败都会返回 null，引擎会跳过这一帧。</summary>
    public static CombatState? Snapshot()
    {
        // 注意：新版 Dalamud 里 LocalPlayer 从 IClientState 挪到了 IObjectTable 上
        if (Service.ObjectTable.LocalPlayer is not { } player) return null;

        var target = Service.TargetManager.Target as IBattleChara;
        var now = Environment.TickCount64;
        float dt = _lastSnapshotMs == 0 ? 0.016f : (now - _lastSnapshotMs) / 1000f;

        var pos = player.Position;
        float moved = dt > 0 ? Vector3.Distance(pos, _lastPosition) / dt : 0f;

        var st = new CombatState
        {
            Player = player,
            JobId = player.ClassJob.RowId,
            Level = player.Level,
            Hp = player.CurrentHp,
            MaxHp = player.MaxHp,
            Mp = player.CurrentMp,
            MaxMp = player.MaxMp,

            InCombat = Service.Condition[ConditionFlag.InCombat],
            InDuty = Service.Condition[ConditionFlag.BoundByDuty],
            IsMounted = Service.Condition[ConditionFlag.Mounted],

            IsCasting = player.IsCasting,
            CastRemaining = player.IsCasting
                ? MathF.Max(0f, player.TotalCastTime - player.CurrentCastTime)
                : 0f,

            IsMoving = moved > 0.5f,
            MoveSpeed = moved,

            GcdTotal = ActionExecutor.GcdTotal(),
            GcdRemaining = ActionExecutor.GcdRemaining(),

            ComboAction = ActionExecutor.ComboLastAction(),
            ComboTimer = ActionExecutor.ComboRemaining(),

            Target = target,
            TargetId = target?.GameObjectId ?? 0,
            TargetDistance = target == null ? float.MaxValue : Vector3.Distance(pos, target.Position),
            TargetHp = target?.CurrentHp ?? 0,
            TargetMaxHp = target?.MaxHp ?? 0,

            BeastGauge = ReadGauge(JobGaugeKind.Beast),
            OathGauge = ReadGauge(JobGaugeKind.Oath),
        };

        _lastPosition = pos;
        _lastSnapshotMs = now;
        return st;
    }

    public override string ToString() =>
        $"Job={JobId} Lv={Level} GCD={GcdRemaining:F2}/{GcdTotal:F2} " +
        $"Target={(HasTarget ? $"{Target!.Name.TextValue} {TargetHpPercent:F1}%" : "无")} " +
        $"Combo={ComboAction}({ComboTimer:F1}s)";

    // ------------------------------------------------------------------

    private static float Remaining(IBattleChara? who, uint statusId)
    {
        if (who == null) return 0f;
        foreach (var s in who.StatusList)
            if (s.StatusId == statusId)
                return (float)s.RemainingTime;
        return 0f;
    }

    private enum JobGaugeKind { Beast, Oath }

    /// <summary>
    /// 读职业量谱。不同版本的 ClientStructs / Dalamud 里量谱字段名会变
    /// （比如 7.0 之后 PLD 的字段换过名），所以统一包一层 try/catch，
    /// 读不到就返回 0，绝不让量谱问题把整个引擎拖崩。
    /// </summary>
    private static int ReadGauge(JobGaugeKind kind)
    {
        try
        {
            return kind switch
            {
                JobGaugeKind.Beast => (int)Service.JobGauges
                    .Get<Dalamud.Game.ClientState.JobGauge.Types.WARGauge>().BeastGauge,
                JobGaugeKind.Oath => (int)Service.JobGauges
                    .Get<Dalamud.Game.ClientState.JobGauge.Types.PLDGauge>().OathGauge,
                _ => 0,
            };
        }
        catch
        {
            return 0;
        }
    }
}
