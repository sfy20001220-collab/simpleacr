using Dalamud.Configuration;

namespace SimpleACR;

/// <summary>
/// 插件配置。继承自 IPluginConfiguration，Dalamud 会把它序列化成 JSON 存到
///   %AppData%\XIVLauncher\pluginConfigs\SimpleACR\SimpleACR.json
/// （国服路径：卫月安装目录下的 pluginConfigs）
///
/// 要点：
///   1. Version 必须保留，Dalamud 用它做配置迁移判断。
///   2. 只放可序列化的类型（基础类型、List、Dictionary<string, ...>）。
///   3. 改完调用 Save()，否则只在下次插件卸载/存档时才会落盘。
/// </summary>
[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    // ===================== 总开关 =====================

    /// <summary>是否开启自动循环。默认关，避免一进游戏就自动打。</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>只在战斗中执行。</summary>
    public bool OnlyInCombat { get; set; } = true;

    /// <summary>只在副本（BoundByDuty）中执行。</summary>
    public bool OnlyInDuty { get; set; } = false;

    /// <summary>没有目标时自动选最近的敌人。</summary>
    public bool AutoTargetNearest { get; set; } = true;

    /// <summary>自动选敌的最大距离（米）。</summary>
    public float AutoTargetRange { get; set; } = 25f;

    // ===================== 执行节奏 =====================

    /// <summary>
    /// 引擎轮询间隔（毫秒）。
    /// FF14 的 GCD 最短约 2.0~2.5s，能力技的插入窗口很窄，
    /// 100ms 足够快且不会给主线程造成可感知负担。不要低于 50。
    /// </summary>
    public int TickIntervalMs { get; set; } = 100;

    /// <summary>
    /// 能力技（oGCD）插入窗口：当 GCD 剩余时间 &lt;= 该值时才允许按能力技。
    /// 这样能避免 oGCD 把 GCD 卡住（俗称"吃 GCD"）。
    /// 0.6 是社区常用值；网络延迟高就调小一点。
    /// </summary>
    public float OgcdWindowSec { get; set; } = 0.6f;

    /// <summary>
    /// 同一个技能的最小重复间隔（毫秒）防抖。
    /// 防止因服务器回包延迟，在技能还没进 CD 的那一帧被连按两次。
    /// </summary>
    public int ActionDebounceMs { get; set; } = 200;

    /// <summary>是否启用能力技（关掉就是纯 GCD 循环，学习时可以先关）。</summary>
    public bool UseOgcd { get; set; } = true;

    /// <summary>是否启用防御/减伤类条目（坦克用）。</summary>
    public bool UseDefensives { get; set; } = true;

    // ===================== 循环选择 =====================

    /// <summary>
    /// 职业 → 循环名 的覆盖表。
    /// 键是 ClassJob RowId（如 19 = 骑士、21 = 战士），值是循环的 Meta.Name。
    /// 留空则用 RotationManager 里注册的默认那份。
    /// </summary>
    public Dictionary<uint, string> JobRotationOverride { get; set; } = new();

    // ===================== UI =====================

    /// <summary>战斗中显示"下一个技能"的小浮窗。</summary>
    public bool ShowOverlay { get; set; } = true;

    /// <summary>在主窗口里打印引擎决策（调试用，刷屏很凶）。</summary>
    public bool DebugLog { get; set; } = false;

    /// <summary>把 gp 值、GCD 等做成 UI 上的调试面板。</summary>
    public bool ShowDebugPanel { get; set; } = false;

    // ===================== 方法 =====================

    public void Save() => Service.PluginInterface.SavePluginConfig(this);
}
