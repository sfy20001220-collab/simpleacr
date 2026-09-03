# SimpleACR —— 卫月（Dalamud）自动循环插件 · 学习项目

一个**教学用**的 FF14 自动循环（ACR）插件，复刻了 AEAssist 的核心架构，代码全开源、全中文注释。
目标不是做一个能打零式的成品，而是让你**看懂插件怎么跑起来、循环引擎怎么设计**，然后自己往上加。

内置两份可直接跑的示例循环：**骑士（PLD）** 和 **战士（WAR）**。

---

## ⚠️ 先读这段

| | |
|---|---|
| **卫月是什么** | 卫月 = Dalamud 的国服分支。XIVLauncher（国服叫"蓝月/卫月启动器"）→ Dalamud 插件框架 → 第三方插件。 |
| **法律/规则层面** | 第三方插件**违反《最终幻想14》用户协议，有封号风险**。这是事实，不是恐吓。 |
| **Dalamud 自己的立场** | 官方插件准则明确要求插件不得：超出规范与服务器交互、**在无用户直接交互的情况下做自动化**、绕过付费。自动循环落在第二条。 |
| **对他人的影响** | 在队友不知情时用于组队 / 零式 / 绝本，会污染他人体验与 FF Logs 排名数据。这在社区里是被普遍抵制的。 |
| **这个项目的定位** | 学习 Dalamud 插件开发与 FF14 战斗机制建模。**是否使用、在哪使用，你自己判断并承担后果。** |

---

## 0. 直接用线上仓库安装（最省事）

不想碰编译的话，直接装现成的：

1. 游戏里 `/xlsettings` → **实验性** → 自定义插件仓库 → 添加，填入：

   ```
   https://raw.githubusercontent.com/sfy20001220-collab/simpleacr/main/dist/pluginmaster.json
   ```

2. `/xlplugins` → 可用插件里找到 **SimpleACR** → 安装
3. `/sacr` 打开主窗口

> 仓库是 Public 的，Dalamud 才能拉到 raw 地址。
> 后续推了新版，在 `/xlplugins` 里点一下更新就行。
>
> 只自带骑士（PLD）与战士（WAR）两份循环，其它职业会提示「暂无循环」。

---

## 目录

1. [五分钟跑通第一个插件](#1-五分钟跑通第一个插件)
2. [卫月插件生态速览](#2-卫月插件生态速览)
3. [SimpleACR 项目结构](#3-simpleacr-项目结构)
4. [自动循环插件的原理](#4-自动循环插件的原理ae-架构拆解)
5. [核心 API 速查](#5-核心-api-速查)
6. [怎么加一个新职业的循环](#6-怎么加一个新职业的循环)
7. [调试](#7-调试)
8. [打包与发布](#8-打包与发布)
9. [国服适配注意事项](#9-国服适配注意事项)
10. [首次编译可能要修的地方](#10-首次编译可能要修的地方)

深入内容见 `docs/`：
- [`docs/01-环境搭建.md`](docs/01-环境搭建.md) —— 从零装好开发环境
- [`docs/02-架构原理.md`](docs/02-架构原理.md) —— 引擎每一层在干什么
- [`docs/03-编写循环.md`](docs/03-编写循环.md) —— 循环 DSL 手册 + 坦克循环的设计思路

---

## 1. 五分钟跑通第一个插件

```bash
# 1) 环境：.NET 8.0 SDK（要 SDK 不是 Runtime）+ VS2022 / Rider
dotnet --version        # 应该 >= 8.0.100

# 2) 拿官方模板跑一遍（先确认工具链通）
git clone https://github.com/goatcorp/SamplePlugin.git MyFirstPlugin
cd MyFirstPlugin
dotnet build

# 3) 把 SimpleACR 放进来编译
cd ..\SimpleACR
dotnet build
```

游戏里加载（**必须**通过启动器进游戏，走官方 launcher 不会有 Dalamud）：

```
/xlsettings → 实验性 → 勾选 "启用 DevPlugin 加载"
            → DevPlugins → 添加 → 选 SimpleACR\bin\Debug\
/xlplugins → Dev Plugins 标签页 → 找到 SimpleACR → Load
```

验证：

```
/sacr            → 打开主窗口
/sacr find 圣灵  → 搜技能 ID（国服客户端直接搜中文）
/sacr dump       → 打印当前战斗状态
/sacr on         → 开自动循环（先去打木人）
```

**热重载**：改完代码 → `dotnet build` → `/xlplugins` 里 Unload 再 Load。
前提是 `Dispose()` 把事件都摘干净了，否则会出现"卸载了但还在跑"的鬼畜现象。

---

## 2. 卫月插件生态速览

```
启动器（XIVLauncher / 国服蓝月）
   └── 注入 Dalamud 框架
         ├── 提供插件运行时（IoC 容器、事件系统、ImGui、Lumina 数据表访问）
         ├── 提供一堆"服务"：IClientState / IObjectTable / ITargetManager / ...
         ├── 内置 FFXIVClientStructs（游戏内部结构体的 C# 映射）
         └── 插件仓库（pluginmaster.json）→ /xlplugins 里装插件
```

**插件 = 一个 .NET 类库 DLL + 一份清单 JSON。** 就这么多。

Dalamud 加载插件时做的事：
1. 扫 DLL 找唯一一个实现 `IDalamudPlugin` 的类
2. 读清单，检查 `DalamudApiLevel` 是否兼容
3. IoC 容器往构造函数 / `[PluginService]` 静态属性里塞服务
4. `new` 出来（构造函数里做初始化：读配置、建窗口、注册命令）
5. 禁用 / 重载 / 退出时调 `Dispose()`

**铁律**：构造函数里 `+=` 了什么，`Dispose()` 里就要 `-=` 回去。

---

## 3. SimpleACR 项目结构

```
SimpleACR/
├── SimpleACR.sln
├── README.md                        ← 你在这
├── docs/
│   ├── 01-环境搭建.md
│   ├── 02-架构原理.md
│   └── 03-编写循环.md
└── SimpleACR/
    ├── SimpleACR.csproj             ← Dalamud.NET.Sdk，清单字段也写在这
    ├── Plugin.cs                    ← 入口：注入服务、建窗口、注册命令
    ├── Service.cs                   ← 服务定位器（把注入的服务抄成静态的）
    ├── Configuration.cs             ← 配置 + 持久化
    ├── Commands.cs                  ← /sacr 子命令（find 查技能 ID 超好用）
    │
    ├── Core/
    │   ├── ActionExecutor.cs        ← 唯一的"按技能"出口（UseAction / 冷却 / GCD）
    │   ├── CombatState.cs           ← 战斗状态快照 + 条件库（HasBuff/Ready/ComboStep...）
    │   ├── RotationEngine.cs        ← 心脏：每帧求值 → 过闸门 → 按下
    │   └── RotationManager.cs       ← 反射扫描并注册所有循环
    │
    ├── Rotations/
    │   ├── Rotation.cs              ← 循环 DSL：RotationEntry / RotationBuilder / 特性
    │   └── Jobs/
    │       ├── PaladinRotation.cs   ← 骑士示例
    │       └── WarriorRotation.cs   ← 战士示例
    │
    ├── Data/
    │   ├── Job.cs                   ← 职业 ID
    │   ├── ActionIds.cs             ← 技能 ID 常量表
    │   └── StatusIds.cs             ← buff / debuff ID 常量表
    │
    └── Windows/
        ├── MainWindow.cs            ← 引擎状态可视化（调试靠它）
        └── ConfigWindow.cs          ← 设置
```

---

## 4. 自动循环插件的原理（AE 架构拆解）

AE（AEAssist）之所以强，不是因为它"会打"，而是它的**架构选对了**：
把"什么时候按什么技能"建模成**一张按优先级排序的规则表**，而不是一堆 if-else。

SimpleACR 复刻的就是这个模型：

```
循环表（Rotations/Jobs/*.cs）
  ┌──────────────────────────────────────────────┐
  │ 1. [oGCD] 战逃       条件：冷却好            │  ← 优先级最高
  │ 2. [oGCD] 安魂       条件：有战逃 buff       │
  │ 3. [GCD ] 悔罪告白   条件：有安魂 buff       │
  │ 4. [GCD ] 赎罪剑     条件：有赎罪 buff       │
  │ 5. [GCD ] 王权剑     条件：连招第二段打完    │
  │ 6. [GCD ] 快破剑     条件：无（兜底）        │  ← 优先级最低
  └──────────────────────────────────────────────┘
                        │
                        │  引擎每帧自上而下问："你成立吗？"
                        │  第一个回答"是"的胜出，后面的不看
                        ▼
              ┌──────────────────┐
              │  节奏闸门         │  GCD 类：GCD 转好 + 不在咏唱
              │                  │  oGCD 类：GCD 剩余 < 窗口
              ├──────────────────┤
              │  目标解析         │  Target / Self / 最低血队友 / 最近敌人
              ├──────────────────┤
              │  防抖 + 可用性检查 │  GetActionStatus == 0 ?
              ├──────────────────┤
              │  UseAction       │  真按下
              └──────────────────┘
```

### 为什么"条件成立"还不够，还需要节奏闸门

FF14 的战斗节奏由 GCD（约 2.0~2.5s 的公共冷却）驱动：

- **GCD 技能**（战技/魔法）：GCD 在转的时候按不出来，必须等
- **能力技（oGCD）**：随时能按，但如果你在 GCD 刚转好那一刻之前按，动画锁会
  把 GCD 往后顶一点。多插几个就会明显掉 DPS，俗称"吃 GCD"

所以标准做法是：**只在 GCD 快转好（剩余 < 0.6s）的窗口里插能力技**。
这个窗口就是配置里的"能力技窗口"，网络延迟高就调小。

### 几个设计取舍

| 决定 | 原因 |
|---|---|
| 所有逻辑跑在 `IFramework.Update`（游戏主线程） | 原生函数不能跨线程调，这是硬约束 |
| 轮询间隔 100ms 而不是每帧 | GCD 最短 2s，100ms 绰绰有余；每帧全量计算是白烧 CPU |
| 整块逻辑包 try/catch | 循环脚本里一个条件抛异常 → 最坏这一帧不动，而不是游戏崩溃 |
| 技能 ID 用常量表 + 启动校验 | ID 写错很难在运行中发现，启动时就报出来 |
| 条件写成 `Func<CombatState,bool>` | 循环是数据不是代码，改一行不影响引擎 |

---

## 5. 核心 API 速查

### Dalamud 官方服务（`[PluginService]` 注入）

| 服务 | 用途 |
|---|---|
| `IClientState` | 本地玩家 `LocalPlayer`、登录/登出/切图事件 |
| `IObjectTable` | 场上所有对象，遍历敌人靠它 |
| `ITargetManager` | 当前目标 / 焦点目标，也可以**设置**目标 |
| `ICondition` | 状态标志：`InCombat` / `BoundByDuty` / `Mounted` / `Casting` |
| `IJobGauges` | 职业量谱：`Get<WARGauge>()`、`Get<PLDGauge>()` |
| `IDataManager` | Lumina 数据表：`GetExcelSheet<Action>()` |
| `IFramework` | 主线程帧事件 `Update`，所有游戏逻辑必须在这里跑 |
| `IPartyList` | 小队成员 |
| `ICommandManager` | 注册 `/` 命令 |
| `IPluginLog` | 日志，`/xllog` 查看 |
| `IChatGui` | 往游戏聊天框输出 |

### FFXIVClientStructs（游戏原生函数，本项目的执行层）

```csharp
using FFXIVClientStructs.FFXIV.Client.Game;

var am = ActionManager.Instance();

am->GetActionStatus(ActionType.Action, actionId, targetId);  // 0 = 现在能按
am->UseAction(ActionType.Action, actionId, targetId);         // 按下去
am->GetRecastGroupDetail(group - 1);                          // 复唱组（GCD / 长CD）
am->GetAdjustedActionId(actionId);                            // 连招替换后的实际 ID
am->Combo.Action;                                             // 连招上一步的技能 ID
am->Combo.Timer;                                              // 连招剩余时间
```

⚠️ **Dalamud 官方服务只管"读"，不提供"施放技能"。**
所有自动循环插件都必须走 ClientStructs 的 `ActionManager` —— 这也是为什么
这类插件必然跟版本强绑定（函数签名、结构体偏移每次大更新都可能变）。

### 本项目封装的条件库（`CombatState`）

```csharp
s.HasBuff(statusId)                        // 自己身上有 buff
s.BuffRemaining(statusId)                  // buff 剩余秒数
s.BuffStacks(statusId)                     // buff 层数
s.TargetHasDebuff(statusId)                // 目标身上有 debuff
s.TargetDebuffRemaining(statusId)          // DoT 剩余秒数（补 DoT 用）
s.Cd(actionId) / s.OffCooldown(actionId)   // 冷却查询
s.Charges(actionId)                        // 充能层数（调停/猛攻这类）
s.ComboStep(actionId)                      // 连招上一步是不是它
s.BeastGauge / s.OathGauge                 // 职业量谱
s.GcdRemaining / s.CanWeave()              // 节奏
s.TargetDistance / s.TargetHpPercent       // 目标
s.EnemyCount(5f)                           // 附近敌人数（AOE 判定）
s.PartyMinHpPercent()                      // 小队最低血量（写治疗循环用）
s.IsMoving / s.IsCasting / s.Mp            // 杂项
```

---

## 6. 怎么加一个新职业的循环

三分钟，不改任何引擎代码。

**第 1 步**：确认职业 ID（`Data/Job.cs` 里有全表，比如龙骑士 = `Job.DRG` = 22）

**第 2 步**：在 `Rotations/Jobs/` 下新建 `DragoonRotation.cs`

```csharp
using SimpleACR.Core;
using SimpleACR.Data;

namespace SimpleACR.Rotations.Jobs;

[Rotation("龙骑·7.x 简化循环", Job.DRG, Author = "你", Patch = "7.x")]
public sealed class DragoonRotation : Rotation
{
    public override void Build(RotationBuilder b)
    {
        // 按优先级从高到低排。引擎自上而下求值，命中即止。
        b.Ogcd("武神枪", 12345, s => s.HasTarget && s.OffCooldown(12345));
        b.Gcd ("樱花怒放", 12346, s => s.TargetDebuffRemaining(1234) < 3f);
        b.Gcd ("贯穿刺·连招3", 12347, s => s.ComboStep(12348));
        b.Gcd ("直刺", 12348);   // 兜底
    }
}
```

**第 3 步**：`dotnet build`，进游戏切到龙骑。没了。

`RotationManager` 会在启动时反射扫到这个类，自动注册。

**技能 ID 怎么确认**：这是最容易卡住的地方，用插件自带的命令：

```
/sacr find 贯穿刺
```

会直接从**你当前客户端**的 Action 表里搜，输出 ID + 咏唱 + 复唱 + 充能层数。
比翻 wiki 准，因为它读的就是游戏自己的数据。

---

## 7. 调试

| 手段 | 说明 |
|---|---|
| 主窗口（`/sacr`） | 看引擎状态文字、GCD 进度条、选中的那条、整张循环表和命中次数 |
| "循环明细"折叠栏 | 每条可以单独勾掉，用来验证某条在循环里的作用 |
| `/sacr dump` | 把 `CombatState` 打成一行，看条件判断的原始数据 |
| `/sacr find <名字>` | 查技能 / buff 的真实 ID |
| `/xllog` | Dalamud 日志。插件所有 `Log.*` 都在这 |
| IDE 附加到进程 | VS / Rider → 附加到 `ffxiv_dx11.exe` → 打断点。**要 Debug 构建** |
| 启动校验警告 | 主窗口底部：技能 ID 在当前客户端不存在的会全列出来 |

### 常见症状 → 原因

| 症状 | 大概率原因 |
|---|---|
| 一次都不按 | ① 没勾"启用" ② 勾了"只在战斗中"但没进战斗 ③ 当前职业没有循环 |
| 只按快破剑，别的都不按 | 上面的条件写太严，全被跳过了。开 `/sacr dump` 看数据 |
| 某个技能从来不按 | 技能 ID 或 buff ID 写错（看启动校验警告 + `/sacr find`） |
| 打起来一顿一顿的 | "能力技窗口"调太大，能力技在吃 GCD。调到 0.4 试试 |
| 同一个技能被连按两次 | "防抖"调大，比如 300ms |
| 日志刷 "ActionManager 为 null" | 还没进游戏 / 在过场动画里，正常 |

---

## 8. 打包与发布

```bash
dotnet build -c Release
```

输出目录里会出现 `SimpleACR/` 文件夹，里面有**补全后的清单 JSON** 和 `latest.zip`。

要挂到自己的插件仓库，写一个 `pluginmaster.json`：

```json
[
  {
    "Author": "YourName",
    "Name": "SimpleACR",
    "InternalName": "SimpleACR",
    "Punchline": "学习用的自动循环插件（AE 简化版）",
    "Description": "...",
    "AssemblyVersion": "0.1.0.0",
    "DalamudApiLevel": 15,
    "ApplicableVersion": "any",
    "RepoUrl": "https://github.com/YourName/SimpleACR",
    "Tags": ["战斗", "自动循环"],
    "IsHide": false,
    "IsTestingExclusive": false,
    "DownloadLinkInstall": "https://.../latest.zip",
    "DownloadLinkUpdate": "https://.../latest.zip",
    "LoadPriority": 0,
    "LastUpdate": "1756000000"
  }
]
```

放到任意可通过 HTTP GET 拿到的地方（GitHub Raw 就行），
玩家在 `/xlsettings` → 自定义插件仓库里加上这个 URL。

本仓库里这一切是自动的：

```bash
# 只编译 + 生成 dist/ 两个文件
python make_repo.py --owner <你的GitHub用户名>

# 编译 → 生成 dist/ → git add → commit → push（Windows 上直接双击 publish.bat）
publish.bat "改了蝰蛇循环"
```

`make_repo.py` 会按 `--owner` 把 `Author`、`RepoUrl`、三个 `DownloadLink*`
一起填好，不用手改 JSON。

> 分发地址用的是 `raw.githubusercontent.com/.../dist/latest.zip`，
> 而不是 GitHub Release —— 这样推上去**立刻**生效，不用先建 release。

> `InternalName` **一旦发布就不能改**：它是配置目录名、日志前缀、插件 ID。
> `DalamudApiLevel` 由 Dalamud.NET.Sdk 自动填，一般不用管。

---

## 9. 国服适配注意事项

1. **SDK 版本**：`SimpleACR.csproj` 第一行是 `Dalamud.NET.Sdk/15.0.0`。
   国服卫月的 API 等级通常滞后于国际服，如果编译报 API 等级不匹配，
   把版本号调低（如 `13.0.0`）。可用版本见
   https://www.nuget.org/packages/Dalamud.NET.Sdk

2. **没有 Dalamud.NET.Sdk 的老环境**：退回手工引用模式
   ```xml
   <PropertyGroup>
     <TargetFramework>net8.0-windows</TargetFramework>
     <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
     <DalamudLibPath>$(AppData)\XIVLauncher\addon\Hooks\dev\</DalamudLibPath>
   </PropertyGroup>
   <ItemGroup>
     <Reference Include="Dalamud"><HintPath>$(DalamudLibPath)Dalamud.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="FFXIVClientStructs"><HintPath>$(DalamudLibPath)FFXIVClientStructs.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="ImGui.NET"><HintPath>$(DalamudLibPath)ImGui.NET.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="Lumina"><HintPath>$(DalamudLibPath)Lumina.dll</HintPath><Private>false</Private></Reference>
     <Reference Include="Lumina.Excel"><HintPath>$(DalamudLibPath)Lumina.Excel.dll</HintPath><Private>false</Private></Reference>
     <PackageReference Include="DalamudPackager" Version="2.1.13" />
   </ItemGroup>
   ```
   国服的 `DalamudLibPath` 一般在卫月安装目录下的 `addon\Hooks\dev\`。

3. **技能名是中文的**：`/sacr find 圣灵` 这种直接搜中文就行 —— 它读的是你本地客户端的表。

4. **版本差异**：本文的技能 / buff ID 基于 7.x 整理，标注 `?` 的把握不大。
   国服版本落后国际服时差异会更明显，**一切以 `/sacr find` 的输出为准**。

---

## 10. 首次编译可能要修的地方

这份代码是照着 Dalamud 的最新 API 和 FFXIVClientStructs 写的，但**没有在本机编译过**
（写它的机器上没有 Dalamud 运行时）。大概率会遇到下面这些签名差异，
每一处代码里都有注释指路，改起来都是一两分钟的事：

| 报错 | 改法 |
|---|---|
| `Lumina.Excel.Sheets.Action` 找不到 | 老版本 Dalamud 用 `Lumina.Excel.GeneratedSheets.Action`，改 using |
| `ExtractText()` 找不到 | 改成 `.ToString()` |
| `GetRecastGroupDetail` 参数类型不对 | 看 ClientStructs 的实际签名，通常是 `int` |
| `am->Combo.Action` / `Combo.Timer` 字段名变了 | 用 IDE 的自动补全看 `ActionManager` 里连招字段叫什么 |
| GCD 一直显示 0 或乱跳 | `ActionExecutor.GcdRecastGroup`（默认 57）在你的版本上不对，改掉 |
| `WARGauge.BeastGauge` / `PLDGauge.OathGauge` 改名 | 7.0 之后量谱字段调整过，按实际字段名改 |
| `SeString` / `ExtractText` 命名空间 | `using Lumina.Text;` |

遇到别的报错，先去 `docs/02-架构原理.md` 看那一层在干什么，再对照官方
https://dalamud.dev 和 https://github.com/goatcorp/SamplePlugin 的最新代码。

---

## 参考

- Dalamud 官方文档 —— https://dalamud.dev
- 官方示例插件 —— https://github.com/goatcorp/SamplePlugin
- FFXIVClientStructs —— https://github.com/aers/FFXIVClientStructs
- 插件清单字段全表 —— https://dalamud.dev/plugin-development/project-layout
- 自定义插件仓库 —— https://dalamud.dev/plugin-publishing/custom-repositories
