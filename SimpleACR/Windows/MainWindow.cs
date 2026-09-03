using Dalamud.Interface.Windowing;
// 注意：Dalamud API 12+ 起，ImGui 的 C# 绑定从第三方包 ImGui.NET
// 换成了 Dalamud 自带的 Dalamud.Bindings.ImGui。
// 老教程里的 `using ImGuiNET;` 在新版 Dalamud 上会报 CS0246。
using Dalamud.Bindings.ImGui;
using SimpleACR.Core;
using SimpleACR.Data;
using SimpleACR.Rotations;
using System.Numerics;

namespace SimpleACR.Windows;

/// <summary>
/// 主窗口：看引擎在想什么。
///
/// 自动循环插件最难的不是写循环，而是**调试** —— 你得知道它为什么按、为什么不按。
/// 所以这个窗口把引擎的内部状态全摊开：当前状态文字、GCD 进度、选中的那条、
/// 以及整张循环表和每条的命中次数。
/// </summary>
public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    public MainWindow(Plugin plugin)
        : base("SimpleACR 自动循环###SimpleACRMainWindow")
    {
        _plugin = plugin;
        Size = new Vector2(460, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = _plugin.Configuration;
        var engine = _plugin.Engine;

        // ---------------- 总开关 ----------------
        var enabled = cfg.Enabled;
        if (ImGui.Checkbox("启用自动循环", ref enabled))
        {
            cfg.Enabled = enabled;
            cfg.Save();
        }
        ImGui.SameLine();
        ImGui.TextDisabled("也可以用 /sacr on");

        if (enabled) ImGui.TextColored(new Vector4(0.2f, 0.9f, 0.3f, 1f), "运行中");
        else ImGui.TextColored(new Vector4(0.9f, 0.5f, 0.2f, 1f), "已停止");

        ImGui.Separator();

        // ---------------- 当前循环 ----------------
        var st = engine.LastState;
        ImGui.Text($"职业：{(st == null ? "—" : $"{Job.Name(st.JobId)} (Lv.{st.Level})")}");
        ImGui.Text($"循环：{engine.Current?.Meta.Name ?? "未加载"}");

        if (st != null)
        {
            ImGui.Text($"战斗状态：{(st.InCombat ? "战斗中" : "非战斗")}　{(st.InDuty ? "副本内" : "野外")}");
        }

        // ---------------- GCD 进度条 ----------------
        if (st != null && st.GcdTotal > 0)
        {
            var frac = 1f - Math.Clamp(st.GcdRemaining / st.GcdTotal, 0f, 1f);
            ImGui.Text($"GCD：{st.GcdRemaining:F2}s / {st.GcdTotal:F2}s");
            ImGui.ProgressBar(frac, new Vector2(-1, 0), st.GcdRolling ? "转圈中" : "就绪");
        }

        ImGui.Separator();

        // ---------------- 引擎决策 ----------------
        ImGui.Text($"状态：{engine.StatusText}");
        ImGui.Text($"本次：{engine.LastActionText}");

        var next = engine.Next;
        if (next != null)
        {
            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1f, 1f),
                $"下一技能：{ActionExecutor.NameOf(next.ActionId)}（{next.Category}）");
            ImGui.TextDisabled($"  └ {next.Name}");
        }
        else
        {
            ImGui.TextDisabled("下一技能：无");
        }

        if (st != null)
        {
            ImGui.Text($"目标：{(st.HasTarget ? $"{st.Target!.Name.TextValue}  {st.TargetHpPercent:F1}%  {st.TargetDistance:F1}m" : "无")}");
            if (st.JobId == Job.WAR) ImGui.Text($"兽魂：{st.BeastGauge} / 100");
            if (st.JobId == Job.PLD) ImGui.Text($"忠义：{st.OathGauge} / 100");
        }

        ImGui.Separator();

        // ---------------- 目标 ----------------
        if (ImGui.Button("打开设置"))
            _plugin.ToggleConfigUi();

        ImGui.SameLine();
        if (ImGui.Button("重载循环表"))
        {
            Service.ChatGui.Print("[SimpleACR] 循环表在插件重载后才会重新扫描，请在 /xlplugins 里卸载再加载");
        }

        ImGui.Spacing();

        // ---------------- 循环明细 ----------------
        DrawRotationTable(engine);

        ImGui.Spacing();
        DrawWarnings();
    }

    private static void DrawRotationTable(RotationEngine engine)
    {
        var rotation = engine.Current;
        if (rotation == null)
        {
            ImGui.TextDisabled("当前职业没有注册循环。在 Rotations/Jobs 下新建一个类即可。");
            return;
        }

        if (!ImGui.CollapsingHeader($"循环明细（{rotation.Entries.Count} 条，按优先级从上到下）###rot"))
            return;

        ImGui.TextDisabled("关掉某条可以单独验证它在循环里的作用；命中次数是该条被实际施放的次数。");
        ImGui.Separator();

        var changed = false;
        for (var i = 0; i < rotation.Entries.Count; i++)
        {
            var e = rotation.Entries[i];
            ImGui.PushID(i);

            var on = e.Enabled;
            if (ImGui.Checkbox("##en", ref on)) { e.Enabled = on; changed = true; }
            ImGui.SameLine();

            var color = e.Category switch
            {
                ActionCategory.Gcd => new Vector4(0.95f, 0.85f, 0.45f, 1f),
                ActionCategory.Ogcd => new Vector4(0.55f, 0.85f, 1.0f, 1f),
                ActionCategory.Defensive => new Vector4(0.6f, 0.95f, 0.6f, 1f),
                _ => new Vector4(0.8f, 0.8f, 0.8f, 1f),
            };

            ImGui.TextColored(color, $"{i + 1,2}. {ActionExecutor.NameOf(e.ActionId)}");
            ImGui.SameLine();
            ImGui.TextDisabled($"[{e.Category}] ×{e.HitCount}");
            ImGui.SameLine();
            ImGui.TextDisabled($"  {e.Name}");

            ImGui.PopID();
        }

        if (changed) Service.Config.Save();
    }

    private void DrawWarnings()
    {
        var warnings = _plugin.RotationManager.Warnings;
        if (warnings.Count == 0) return;

        if (!ImGui.CollapsingHeader($"启动校验警告（{warnings.Count}）###warn"))
            return;

        ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f),
            "这些技能 ID 在当前客户端的数据表里找不到，通常是版本变动导致的。");
        ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f),
            "用 /sacr find <技能名> 查到正确 ID 后改 Data/ActionIds.cs。");
        ImGui.Separator();

        foreach (var w in warnings)
            ImGui.TextWrapped(w);
    }
}
