using Dalamud.Interface.Windowing;
// 注意：Dalamud API 12+ 起，ImGui 的 C# 绑定从第三方包 ImGui.NET
// 换成了 Dalamud 自带的 Dalamud.Bindings.ImGui。
// 老教程里的 `using ImGuiNET;` 在新版 Dalamud 上会报 CS0246。
using Dalamud.Bindings.ImGui;
using SimpleACR.Core;
using SimpleACR.Data;
using System.Numerics;

namespace SimpleACR.Windows;

/// <summary>
/// 设置窗口。
///
/// 写 Dalamud 的 ImGui 界面有两个坑，这里都绕开了：
///
///   坑 1：ImGui 是即时模式（每帧重画），所有控件都要 ref 传值。
///         但 C# 的**属性不能按引用传**（ref cfg.Enabled 编译不过），
///         所以下面用了一组小辅助方法：读属性 → 传局部变量 → 写回属性。
///
///   坑 2：改了配置必须调 Save()，否则只在插件卸载时才落盘。
///         这里用一个 _dirty 标记统一在 Draw 末尾保存一次。
/// </summary>
public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private bool _dirty;

    public ConfigWindow(Plugin plugin)
        : base("SimpleACR 设置###SimpleACRConfigWindow")
    {
        _plugin = plugin;
        Size = new Vector2(500, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var cfg = _plugin.Configuration;
        _dirty = false;

        // ==================== 触发条件 ====================
        if (ImGui.CollapsingHeader("触发条件###cond", ImGuiTreeNodeFlags.DefaultOpen))
        {
            cfg.Enabled = Flag("启用自动循环", cfg.Enabled, "也可以用 /sacr on|off");
            cfg.OnlyInCombat = Flag("只在战斗中执行", cfg.OnlyInCombat);
            cfg.OnlyInDuty = Flag("只在副本内执行", cfg.OnlyInDuty);
            cfg.AutoTargetNearest = Flag("没有目标时自动选最近的敌人", cfg.AutoTargetNearest);
            cfg.AutoTargetRange = SliderF("自动选敌最大距离（米）", cfg.AutoTargetRange, 3f, 40f, "%.0f");
        }

        // ==================== 执行节奏 ====================
        if (ImGui.CollapsingHeader("执行节奏###timing", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextWrapped("这几个参数决定手感。默认值是 7.x 下比较通用的设定。");

            cfg.TickIntervalMs = SliderI("轮询间隔（毫秒）", cfg.TickIntervalMs, 50, 500,
                "引擎多久重新求值一次循环。50~150 都合理，再低只是白增主线程负担。");

            cfg.OgcdWindowSec = SliderF("能力技窗口（秒）", cfg.OgcdWindowSec, 0.2f, 1.2f, "%.2f",
                "GCD 剩余时间小于这个值才允许插能力技。\n" +
                "调大 → 能力技按得早，但容易把 GCD 往后顶（吃 GCD）；\n" +
                "调小 → 按得晚，可能漏掉。网络延迟高就调小一点。");

            cfg.ActionDebounceMs = SliderI("同技能防抖（毫秒）", cfg.ActionDebounceMs, 0, 1000,
                "同一个技能两次施放之间的最小间隔。\n防服务器回包延迟期间被重复按下。");

            cfg.UseOgcd = Flag("启用能力技（oGCD）", cfg.UseOgcd);
            cfg.UseDefensives = Flag("启用减伤 / 自保条目", cfg.UseDefensives);
        }

        // ==================== 循环选择 ====================
        if (ImGui.CollapsingHeader("循环选择###rot", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var jobId = _plugin.Engine.LastState?.JobId ?? 0;

            if (jobId == 0)
            {
                ImGui.TextDisabled("还没检测到职业 —— 先登录角色并让引擎产生一帧快照（打一下怪即可）。");
            }
            else
            {
                ImGui.Text($"当前职业：{Job.Name(jobId)}");

                var available = _plugin.RotationManager.ForJob(jobId).ToList();
                if (available.Count == 0)
                {
                    ImGui.TextColored(new Vector4(1f, 0.6f, 0.3f, 1f), $"还没有为 {Job.Name(jobId)} 写循环。");
                    ImGui.TextWrapped(
                        "做法：在 Rotations/Jobs 下新建一个继承 Rotation 的类，加上 " +
                        "[Rotation(\"名字\", Job.XXX)] 特性并实现 Build()，重新编译即可自动注册。");
                }
                else if (available.Count == 1)
                {
                    ImGui.Text($"已加载循环：{available[0].Meta.Name}");
                    ImGui.TextDisabled(
                        $"作者 {available[0].Meta.Author}　版本 {available[0].Meta.Patch}　{available[0].Entries.Count} 条");
                }
                else
                {
                    cfg.JobRotationOverride.TryGetValue(jobId, out var current);
                    var index = available.FindIndex(r => r.Meta.Name == current);
                    if (index < 0) index = 0;

                    var names = available.Select(r => r.Meta.Name).ToArray();
                    ImGui.SetNextItemWidth(280);
                    var newIndex = index;
                    if (ImGui.Combo("选择循环", ref newIndex, names, names.Length) && newIndex != index)
                    {
                        cfg.JobRotationOverride[jobId] = names[newIndex];
                        _dirty = true;
                        Service.ChatGui.Print("[SimpleACR] 循环已切换，切换一次职业或重载插件后生效");
                    }
                }
            }
        }

        // ==================== 调试 ====================
        if (ImGui.CollapsingHeader("调试###dbg"))
        {
            cfg.ShowDebugPanel = Flag("主窗口显示调试面板", cfg.ShowDebugPanel);
            cfg.DebugLog = Flag("输出引擎决策到日志（很刷屏）", cfg.DebugLog);

            ImGui.Spacing();
            if (ImGui.Button("打印战斗状态到聊天框"))
                Commands.DumpState(_plugin.Engine);
            ImGui.SameLine();
            ImGui.TextDisabled("等价 /sacr dump");
        }

        // ==================== 风险提示 ====================
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.TextColored(new Vector4(1f, 0.35f, 0.35f, 1f), "使用前请读：");
        ImGui.TextWrapped(
            "1. 卫月 / Dalamud 及其插件均属第三方工具，违反《最终幻想14》用户协议，存在封号风险。\n" +
            "2. 自动循环属于 Dalamud 官方插件准则中明确不鼓励的「无用户交互的自动化」。\n" +
            "3. 不要在队友不知情的情况下用于组队、零式、绝本等场景，会影响他人体验与排名数据。\n" +
            "4. 本项目仅用于学习 Dalamud 插件开发与 FF14 战斗机制建模，请自行承担使用后果。");

        if (_dirty) cfg.Save();
    }

    // ==================================================================
    // ImGui 辅助方法：绕开「属性不能 ref 传参」
    // ==================================================================

    private bool Flag(string label, bool current, string? hint = null)
    {
        var v = current;
        ImGui.Checkbox(label, ref v);
        if (hint != null)
        {
            ImGui.SameLine();
            ImGui.TextDisabled(hint);
        }
        if (v != current) _dirty = true;
        return v;
    }

    private float SliderF(string label, float current, float min, float max, string fmt, string? help = null)
    {
        var v = current;
        ImGui.SetNextItemWidth(200);
        ImGui.SliderFloat(label, ref v, min, max, fmt);
        if (help != null) { ImGui.SameLine(); HelpMarker(help); }
        if (Math.Abs(v - current) > 0.0001f) _dirty = true;
        return v;
    }

    private int SliderI(string label, int current, int min, int max, string? help = null)
    {
        var v = current;
        ImGui.SetNextItemWidth(200);
        ImGui.SliderInt(label, ref v, min, max);
        if (help != null) { ImGui.SameLine(); HelpMarker(help); }
        if (v != current) _dirty = true;
        return v;
    }

    private static void HelpMarker(string text)
    {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered()) return;

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 40f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
}
