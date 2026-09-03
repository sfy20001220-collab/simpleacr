using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using SimpleACR.Core;
using SimpleACR.Windows;

namespace SimpleACR;

/// <summary>
/// 插件入口。
///
/// Dalamud 加载一个插件时做的事情（简化版）：
///   1. 扫描 DLL，找到唯一一个实现 IDalamudPlugin 的类
///   2. 读取清单 JSON（由 Dalamud.NET.Sdk 在构建时生成），检查 API 等级
///   3. 走 IoC 容器，把构造函数 / [PluginService] 静态属性上声明的服务注入进来
///   4. new 出实例 —— 构造函数里我们做初始化（读配置、建窗口、注册命令）
///   5. 用户禁用 / 重载 / 退出时调用 Dispose()
///
/// 铁律：构造函数里 += 了什么事件，Dispose 里就要 -= 回去，否则热重载会泄漏，
/// 表现为"卸载了插件但功能还在跑"。
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    // ---------------------------------------------------------------
    // 服务注入：Dalamud 会在构造前把实例塞进来。
    // 需要的服务就在这里声明，不需要的别写（写了就一定会被注入，缺服务会加载失败）。
    // ---------------------------------------------------------------
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IJobGauges JobGauges { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    public const string CommandName = "/sacr";

    public Configuration Configuration { get; init; }

    /// <summary>
    /// WindowSystem 是 Dalamud 封装的 ImGui 窗口管理器，负责
    /// 多窗口的绘制调度、字体缩放、窗口位置持久化。
    /// </summary>
    public readonly WindowSystem WindowSystem = new("SimpleACR");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    /// <summary>循环注册表：反射扫描本程序集里所有带 [Rotation] 的类。</summary>
    internal RotationManager RotationManager { get; init; }

    /// <summary>自动循环引擎。</summary>
    internal RotationEngine Engine { get; init; }

    public Plugin()
    {
        // 1) 读配置。GetPluginConfig() 反序列化失败时返回 null，给个默认值兜底。
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        // 2) 把注入的服务转发到 Service 静态类，供循环脚本使用
        Service.Initialize(this);

        // 3) 扫描并注册所有循环脚本
        RotationManager = new RotationManager();
        Log.Information(
            $"[SimpleACR] 已注册 {RotationManager.All.Count} 份循环：{string.Join(", ", RotationManager.All.Select(r => r.Meta.Name))}");

        // 4) 建引擎（此时还不跑，等开关）
        Engine = new RotationEngine();

        // 5) 建窗口
        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        // 6) 注册命令
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage =
                $"SimpleACR 自动循环。\n" +
                $"  {CommandName}            → 打开主窗口\n" +
                $"  {CommandName} on|off     → 开关自动循环\n" +
                $"  {CommandName} cfg        → 打开设置\n" +
                $"  {CommandName} find <关键字> → 在游戏 Action 表里搜技能 ID（核对版本用）\n" +
                $"  {CommandName} dump       → 打印当前战斗状态快照"
        });

        // 7) 挂 UI 回调
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Log.Information($"[SimpleACR] 已加载 {PluginInterface.Manifest.Name}，输入 {CommandName} 打开面板。");
    }

    public void Dispose()
    {
        // 顺序：先停引擎，再摘 UI，再摘命令
        Engine.Dispose();

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        Log.Information("[SimpleACR] 已卸载。");
    }

    private void OnCommand(string command, string args)
    {
        var (verb, rest) = SplitArgs(args);

        switch (verb)
        {
            case "on":
                Configuration.Enabled = true;
                Configuration.Save();
                ChatGui.Print("[SimpleACR] 自动循环：开");
                break;

            case "off":
                Configuration.Enabled = false;
                Configuration.Save();
                ChatGui.Print("[SimpleACR] 自动循环：关");
                break;

            case "cfg":
                ToggleConfigUi();
                break;

            case "find":
                Commands.FindAction(rest);
                break;

            case "dump":
                Commands.DumpState(Engine);
                break;

            case "entries":
                Commands.DumpEntries(Engine);
                break;

            default:
                MainWindow.Toggle();
                break;
        }
    }

    // 注意：元组元素名不能叫 Rest —— C# 里它是 8 元素以上元组的保留成员名，
    // 写成 (string Verb, string Rest) 会报 CS8126。这里改叫 Tail。
    private static (string Verb, string Tail) SplitArgs(string args)
    {
        if (string.IsNullOrWhiteSpace(args)) return (string.Empty, string.Empty);
        var s = args.Trim();
        var i = s.IndexOf(' ');
        if (i < 0) return (s.ToLowerInvariant(), string.Empty);
        return (s[..i].ToLowerInvariant(), s[(i + 1)..].Trim());
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
}
