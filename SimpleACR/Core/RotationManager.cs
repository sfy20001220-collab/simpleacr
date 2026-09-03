using System.Reflection;
using SimpleACR.Rotations;

namespace SimpleACR.Core;

/// <summary>
/// 循环注册表。
///
/// 做法：启动时反射扫一遍本程序集，把所有带 [Rotation] 特性的 Rotation 子类
/// new 出来、跑一次 Build() 拿到技能表，按职业 ID 存起来。
///
/// 这就是 AE「ACR 脚本热加载」的简化版 —— 你想加一个职业，只要在
/// Rotations/Jobs 下新建一个类，别的什么都不用改。
/// </summary>
public sealed class RotationManager
{
    private readonly Dictionary<uint, List<Rotation>> _byJob = new();

    /// <summary>所有已注册的循环。</summary>
    public IReadOnlyList<Rotation> All { get; }

    /// <summary>加载期间发现的问题（技能 ID 在当前客户端里找不到等）。</summary>
    public IReadOnlyList<string> Warnings { get; }

    public RotationManager()
    {
        var list = new List<Rotation>();
        var warns = new List<string>();

        foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
        {
            if (type.IsAbstract || !type.IsSubclassOf(typeof(Rotation))) continue;

            var attr = type.GetCustomAttribute<RotationAttribute>();
            if (attr == null)
            {
                warns.Add($"{type.Name} 没有 [Rotation] 特性，已跳过");
                continue;
            }

            try
            {
                if (Activator.CreateInstance(type) is not Rotation rotation) continue;

                var builder = new RotationBuilder();
                rotation.Build(builder);
                rotation.Meta = new RotationMeta(attr.Name, attr.JobId, attr.Author, attr.Patch);
                rotation.Entries = builder.Build();

                warns.AddRange(Validate(rotation));

                list.Add(rotation);
                if (!_byJob.TryGetValue(attr.JobId, out var bucket))
                    _byJob[attr.JobId] = bucket = new List<Rotation>();
                bucket.Add(rotation);
            }
            catch (Exception ex)
            {
                var msg = $"加载循环 {type.FullName} 失败：{ex.Message}";
                warns.Add(msg);
                Service.Log.Error(ex, $"[SimpleACR] {msg}");
            }
        }

        All = list;
        Warnings = warns;
    }

    /// <summary>取某个职业的循环。preferredName 非空时优先按名字匹配（配置里的覆盖）。</summary>
    public Rotation? GetFor(uint jobId, string? preferredName = null)
    {
        if (!_byJob.TryGetValue(jobId, out var bucket) || bucket.Count == 0) return null;

        if (!string.IsNullOrWhiteSpace(preferredName))
        {
            var hit = bucket.FirstOrDefault(r =>
                string.Equals(r.Meta.Name, preferredName, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
        }

        return bucket[0];
    }

    public IEnumerable<Rotation> ForJob(uint jobId)
        => _byJob.TryGetValue(jobId, out var b) ? b : Enumerable.Empty<Rotation>();

    /// <summary>
    /// 校验：把循环里引用到的技能 ID 拿去 Lumina 表里对一遍。
    /// 找不到说明 ID 写错或该版本还没实装 —— 这类问题很难在运行中发现，
    /// 所以启动阶段就报出来。
    /// </summary>
    private static IEnumerable<string> Validate(Rotation rotation)
    {
        var bad = new List<string>();
        foreach (var e in rotation.Entries)
        {
            if (!ActionExecutor.Exists(e.ActionId))
                bad.Add($"[循环校验] {rotation.Meta.Name}：「{e.Name}」引用的技能 ID {e.ActionId} 在当前客户端不存在");
        }
        return bad;
    }
}
