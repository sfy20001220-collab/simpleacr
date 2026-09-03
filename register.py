# -*- coding: utf-8 -*-
"""
把 SimpleACR 的编译输出目录注册进卫月（Dalamud CN）的 Dev Plugins 加载列表。

为什么需要这个脚本：
  Dalamud 的 Dev 插件加载位置写在
      %AppData%\\XIVLauncherCN\\dalamudConfig.json
  的 DevPluginLoadLocations 里，只能通过游戏内 /xlsettings 手动添加，
  或者像本脚本这样直接改配置。

  ⚠ 重点 2：本脚本会把 SimpleACR **插到加载列表第一位**。
  Dalamud 按顺序扫描 Dev 插件目录，遇到失效路径（目录已删除，或历史残留把 DLL
  文件路径当成目录填进去）就可能整轮中止扫描，且异常只记 INF/WRN ——
  LogLevel 设成 4(Error) 时日志里一片干净，表现为"配置写对了却扫不到"。
  追加到末尾 = 排在 20 条失效路径之后 = 永远扫不到。

用法：
    python register.py             # 注册（置顶）
    python register.py --clean-dead # 顺带清掉失效的历史 Dev 路径（可选）
    python register.py --remove     # 取消注册
"""

import json
import os
import shutil
import sys
import time

APPDATA = os.environ.get("APPDATA", "")
CONFIG = os.path.join(APPDATA, "XIVLauncherCN", "dalamudConfig.json")

# 编译输出目录（SimpleACR.dll 和 SimpleACR.json 所在的地方）
PLUGIN_DIR = os.path.join("D:", os.sep, "SimpleACR", "SimpleACR", "bin", "x64", "Debug")
PLUGIN_DLL = os.path.join(PLUGIN_DIR, "SimpleACR.dll")

LOC_TYPE = "Dalamud.Configuration.DevPluginLocationSettings, Dalamud"
SETTINGS_TYPE = "Dalamud.Configuration.Internal.DevPluginSettings, Dalamud"
EMPTYLIST_TYPE = (
    "System.Collections.Generic.List`1[[System.String, System.Private.CoreLib]], "
    "System.Private.CoreLib"
)


def die(msg):
    print("[错误] " + msg)
    sys.exit(1)


def main():
    remove = "--remove" in sys.argv

    if not os.path.isfile(CONFIG):
        die("找不到 Dalamud 配置：%s\n      请用卫月启动器至少成功进过一次游戏。" % CONFIG)

    if not remove and not os.path.isfile(PLUGIN_DLL):
        die("找不到 %s\n      请先运行 build.bat 编译插件。" % PLUGIN_DLL)

    # 备份，带时间戳，方便回滚
    stamp = time.strftime("%Y%m%d-%H%M%S")
    backup = CONFIG + ".bak-" + stamp
    shutil.copy2(CONFIG, backup)
    print("[备份] %s" % backup)

    with open(CONFIG, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    # ---------------- 1. DevPluginLoadLocations（加载位置列表）----------------
    locs = cfg.get("DevPluginLoadLocations")
    if not isinstance(locs, dict):
        locs = cfg["DevPluginLoadLocations"] = {
            "$type": (
                "System.Collections.Generic.List`1[[Dalamud.Configuration."
                "DevPluginLocationSettings, Dalamud]], System.Private.CoreLib"
            ),
            "$values": [],
        }
    values = locs.setdefault("$values", [])

    # 可选：清掉失效的历史路径。失效路径（尤其被填成 DLL 文件路径的那些）
    # 会让 Dalamud 的 Dev 插件扫描中途炸掉，清掉最省心。默认不动，怕误删。
    clean = "--clean-dead" in sys.argv
    if clean:
        keep, dropped = [], []
        for v in values:
            path = str(v.get("Path", ""))
            if os.path.isdir(path):
                keep.append(v)
            else:
                dropped.append(path)
        if dropped:
            print("[清理] 移除 %d 条失效的 Dev 路径：" % len(dropped))
            for p in dropped:
                print("       - %s" % p)
        values[:] = keep

    before = len(values)
    values[:] = [v for v in values
                 if os.path.normcase(str(v.get("Path", ""))) != os.path.normcase(PLUGIN_DIR)]

    if not remove:
        # 关键：必须插到列表**最前面**，不能 append 到末尾。
        # Dalamud 按列表顺序扫描 Dev 插件目录，遇到失效路径（目录已删除，
        # 或历史残留里把 DLL 文件路径当成目录填进去）就可能整轮中止扫描，
        # 而这类异常只记 INF/WRN —— 一旦 LogLevel 设成 4(Error) 就完全看不到，
        # 表现为"配置明明写对了，/xlplugins 里却什么都没有"。
        # 这台机器上原有 22 条位置里 20 条已失效，追加到末尾 = 永远扫不到。
        values.insert(0, {
            "$type": LOC_TYPE,
            "Path": PLUGIN_DIR,
            "IsEnabled": True,
            "Nickname": None,
        })
        print("[写入] DevPluginLoadLocations[0] = %s（置顶，避免被前面失效路径中断扫描）" % PLUGIN_DIR)
    else:
        print("[移除] DevPluginLoadLocations -= %s" % PLUGIN_DIR)

    # ---------------- 2. DevPluginSettings（开机自动加载等）----------------
    settings = cfg.get("DevPluginSettings")
    if not isinstance(settings, dict):
        settings = cfg["DevPluginSettings"] = {}

    keys = [k for k in settings.keys()
            if os.path.normcase(k) == os.path.normcase(PLUGIN_DLL)]
    for k in keys:
        del settings[k]

    if not remove:
        settings[PLUGIN_DLL] = {
            "$type": SETTINGS_TYPE,
            # 进游戏就自动加载，省得每次去 /xlplugins 里点 Load
            "StartOnBoot": True,
            "NotifyForErrors": True,
            "AutomaticReloading": False,
            "WorkingPluginId": "00000000-0000-0000-0000-000000000000",
            "DismissedValidationProblems": {
                "$type": EMPTYLIST_TYPE,
                "$values": [],
            },
        }
        print("[写入] DevPluginSettings[%s] StartOnBoot=True" % PLUGIN_DLL)

    with open(CONFIG, "w", encoding="utf-8") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)

    # 回读校验
    with open(CONFIG, "r", encoding="utf-8") as f:
        check = json.load(f)
    paths = [v.get("Path", "") for v in check["DevPluginLoadLocations"]["$values"]]
    ok = PLUGIN_DIR in paths

    print()
    if remove:
        print("[完成] 已取消注册。重启游戏后生效。")
    else:
        print("[完成] 已注册（条目数 %d → %d）。" % (before, len(paths)))
        print("       重启游戏（必须走卫月启动器）后：")
        print("         /xlplugins → Dev Plugins 标签页 → SimpleACR")
        print("         游戏内输入 /sacr 打开主窗口")
    print()
    print("[注意] 若 /xlplugins 里看不到，多半是游戏退出时覆盖了配置，重跑本脚本即可。")

    sys.exit(0 if (ok or remove) else 1)


if __name__ == "__main__":
    main()
