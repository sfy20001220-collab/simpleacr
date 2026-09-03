using Lumina.Excel.Sheets;
using Lumina.Text;
using SimpleACR.Core;
using ActionRow = Lumina.Excel.Sheets.Action;

namespace SimpleACR;

/// <summary>
/// /sacr 子命令的实现。
/// </summary>
internal static class Commands
{
    /// <summary>
    /// 在 Action 表里按名字搜技能。
    ///
    /// 这是本插件最实用的一条命令 —— 技能 ID 和 buff ID 会随版本变，
    /// 与其每次去翻 wiki，不如让插件自己把客户端里的真值打出来。
    /// 客户端是中文就搜中文，是英文就搜英文。
    /// </summary>
    internal static void FindAction(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            Service.ChatGui.Print("[SimpleACR] 用法：/sacr find <技能名关键字>");
            return;
        }

        var sheet = Service.DataManager.GetExcelSheet<ActionRow>();
        if (sheet == null)
        {
            Service.ChatGui.Print("[SimpleACR] 读不到 Action 表");
            return;
        }

        var hits = new List<(uint Id, string Name, float Cast, float Recast, byte Charges)>();
        foreach (var row in sheet)
        {
            var name = NameOf(row);
            if (string.IsNullOrEmpty(name)) continue;
            if (!name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

            hits.Add((row.RowId, name, row.Cast100ms / 100f, row.Recast100ms / 100f, row.MaxCharges));
            if (hits.Count >= 40) break;
        }

        if (hits.Count == 0)
        {
            Service.ChatGui.Print($"[SimpleACR] 没找到包含「{keyword}」的技能");
            return;
        }

        Service.ChatGui.Print($"[SimpleACR] 找到 {hits.Count} 条（最多显示 40）：");
        foreach (var h in hits)
        {
            Service.ChatGui.Print(
                $"  {h.Id,6}  {h.Name,-12}  咏唱 {h.Cast,5:F2}s  复唱 {h.Recast,5:F2}s  充能 {Math.Max(1, (int)h.Charges)}");
        }
    }

    /// <summary>把当前战斗状态打成一行，调试循环条件时很有用。</summary>
    internal static void DumpState(RotationEngine engine)
    {
        var st = engine.LastState;
        if (st == null)
        {
            Service.ChatGui.Print("[SimpleACR] 还没有战斗状态快照（需要先进战斗，或关掉设置里的「仅战斗中」）");
            return;
        }

        Service.ChatGui.Print($"[SimpleACR] 状态：{st}");
        Service.ChatGui.Print($"  引擎：{engine.StatusText}");
        Service.ChatGui.Print($"  循环：{engine.Current?.Meta.Name ?? "无"}（{engine.Current?.Entries.Count ?? 0} 条）");
        Service.ChatGui.Print($"  下一技能：{(engine.Next == null ? "无" : engine.Next.ToString())}");
        Service.ChatGui.Print($"  兽魂={st.BeastGauge}  忠义={st.OathGauge}  MP={st.Mp}/{st.MaxMp}");
        Service.ChatGui.Print($"  移动={st.IsMoving}({st.MoveSpeed:F1}m/s)  咏唱={st.IsCasting}({st.CastRemaining:F2}s)  附近敌人={st.EnemyCount(25f)}");
    }

    /// <summary>
    /// 取技能显示名。SeString 是带富文本标记的字符串，
    /// ExtractText() 会剥掉标记拿到纯文本；老版本 Lumina 没有这个扩展方法时
    /// 退回 ToString()，显示上会带一点标记但不影响查 ID。
    /// </summary>
    private static string NameOf(ActionRow row)
    {
        try { return row.Name.ExtractText(); }
        catch { return row.Name.ToString() ?? string.Empty; }
    }
}
