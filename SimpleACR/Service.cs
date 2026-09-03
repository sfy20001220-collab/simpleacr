using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SimpleACR.Core;

namespace SimpleACR;

/// <summary>
/// 全局服务定位器。
///
/// Dalamud 的依赖注入（[PluginService]）只作用于**插件主类的构造函数**。
/// 我们的循环脚本、引擎、窗口这些普通类拿不到注入，所以这里在插件初始化时
/// 把主类上注入好的服务抄一份到静态字段上，供全工程使用。
///
/// 这是几乎所有 Dalamud 插件都会写的一个小辅助类（DelvUI / Pandora / WrathCombo
/// 里都能看到同款），用法：Service.ClientState.LocalPlayer。
/// </summary>
internal static class Service
{
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    internal static ICommandManager CommandManager { get; private set; } = null!;
    internal static IClientState ClientState { get; private set; } = null!;
    internal static IObjectTable ObjectTable { get; private set; } = null!;
    internal static ITargetManager TargetManager { get; private set; } = null!;
    internal static ICondition Condition { get; private set; } = null!;
    internal static IJobGauges JobGauges { get; private set; } = null!;
    internal static IDataManager DataManager { get; private set; } = null!;
    internal static IFramework Framework { get; private set; } = null!;
    internal static IPartyList PartyList { get; private set; } = null!;
    internal static IChatGui ChatGui { get; private set; } = null!;
    internal static IPluginLog Log { get; private set; } = null!;

    /// <summary>插件配置（从 Plugin 转发，方便各处读写）</summary>
    internal static Configuration Config => Plugin.Configuration;

    /// <summary>循环注册表（引擎需要按职业查循环，转发一下主类上的实例）</summary>
    internal static RotationManager RotationManager => Plugin.RotationManager;

    /// <summary>插件主类实例</summary>
    internal static Plugin Plugin { get; private set; } = null!;

    internal static void Initialize(Plugin plugin)
    {
        Plugin = plugin;

        // 从主类的静态注入属性转发
        PluginInterface = Plugin.PluginInterface;
        CommandManager = Plugin.CommandManager;
        ClientState = Plugin.ClientState;
        ObjectTable = Plugin.ObjectTable;
        TargetManager = Plugin.TargetManager;
        Condition = Plugin.Condition;
        JobGauges = Plugin.JobGauges;
        DataManager = Plugin.DataManager;
        Framework = Plugin.Framework;
        PartyList = Plugin.PartyList;
        ChatGui = Plugin.ChatGui;
        Log = Plugin.Log;
    }
}
