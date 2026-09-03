# -*- coding: utf-8 -*-
"""
把 SimpleACR 的线上仓库地址写进卫月（Dalamud）配置。

做两件事：
  1. ThirdRepoList 里加入本仓库的 pluginmaster.json 地址（放第一位，方便找）
  2. 清掉之前注册过的 Dev 插件路径 —— 同一个 InternalName 同时从
     Dev 目录和线上仓库加载会冲突，二选一

用法：
    python register_repo.py            # 注册
    python register_repo.py --remove   # 取消注册

注意：Dalamud 在游戏退出时会回写配置文件，
     所以请「关掉游戏 → 跑本脚本 → 再开游戏」。
"""

import json
import os
import shutil
import sys
import time

REPO_URL = ("https://raw.githubusercontent.com/"
            "sfy20001220-collab/simpleacr/main/dist/pluginmaster.json")
INTERNAL_NAME = "SimpleACR"

CFG_TYPE = ("Dalamud.Configuration.ThirdPartyRepoSettings, Dalamud")
LIST_TYPE = ("System.Collections.Generic.List`1[[Dalamud.Configuration."
             "ThirdPartyRepoSettings, Dalamud]], System.Private.CoreLib")


def cfg_path():
    return os.path.join(os.environ.get("APPDATA", ""),
                        "XIVLauncherCN", "dalamudConfig.json")


def main():
    path = cfg_path()
    if not os.path.isfile(path):
        sys.exit("找不到配置：%s" % path)

    remove = "--remove" in sys.argv

    with open(path, "r", encoding="utf-8") as f:
        cfg = json.load(f)

    bak = path + ".bak-repo-" + time.strftime("%Y%m%d-%H%M%S")
    shutil.copy2(path, bak)
    print("[备份] %s" % bak)

    # ---------- 1. ThirdRepoList ----------
    repos = cfg.setdefault("ThirdRepoList",
                           {"$type": LIST_TYPE, "$values": []})
    values = repos.setdefault("$values", [])
    repos["$type"] = LIST_TYPE

    hit = [v for v in values if v.get("Url", "").strip() == REPO_URL]
    old = [v for v in values if INTERNAL_NAME.lower() in v.get("Url", "").lower()]
    for v in old:
        values.remove(v)
        print("[清理] 旧地址 -> %s" % v.get("Url"))

    if remove:
        print("[完成] 已从仓库列表移除（%d -> %d 条）"
              % (len(values) + len(old), len(values)))
    else:
        values.insert(0, {"$type": CFG_TYPE, "Url": REPO_URL, "IsEnabled": True})
        print("[写入] ThirdRepoList[0] = %s" % REPO_URL)
        print("       仓库总数 %d 条" % len(values))

    # ---------- 2. 清掉 Dev 插件注册 ----------
    dev_locs = cfg.get("DevPluginLoadLocations", {}).setdefault("$values", [])
    n0 = len(dev_locs)
    cfg["DevPluginLoadLocations"]["$values"] = [
        v for v in dev_locs
        if INTERNAL_NAME.lower() not in str(v.get("Path", "")).lower()]
    removed = n0 - len(cfg["DevPluginLoadLocations"]["$values"])

    dev_cfg = cfg.get("DevPluginSettings", {})
    for k in [k for k in dev_cfg if INTERNAL_NAME.lower() in k.lower()]:
        dev_cfg.pop(k)
        print("[清理] DevPluginSettings -> %s" % k)
    if removed:
        print("[清理] DevPluginLoadLocations 移除 %d 条（改用线上仓库）" % removed)

    with open(path, "w", encoding="utf-8") as f:
        json.dump(cfg, f, ensure_ascii=False, indent=2)

    # ---------- 校验 ----------
    with open(path, "r", encoding="utf-8") as f:
        check = json.load(f)
    print()
    print("[校验] JSON 有效，顶层字段 %d 个" % len(check))
    urls = [v.get("Url", "") for v in check["ThirdRepoList"]["$values"]]
    print("[校验] 仓库地址在列表里：%s（位置 %d）"
          % (REPO_URL in urls,
             urls.index(REPO_URL) if REPO_URL in urls else -1))
    leftover = [v.get("Path") for v in check["DevPluginLoadLocations"]["$values"]
                if INTERNAL_NAME.lower() in str(v.get("Path", "")).lower()]
    print("[校验] Dev 路径残留：%s" % (leftover if leftover else "无"))
    print()
    print("下一步：开游戏 → /xlplugins → 可用插件 → SimpleACR → 安装")


if __name__ == "__main__":
    main()
