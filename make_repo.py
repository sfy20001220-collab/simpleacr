# -*- coding: utf-8 -*-
"""
生成 Dalamud 线上插件仓库需要的两个文件：

    dist/latest.zip        —— 插件本体，Dalamud 从这里下载安装
    dist/pluginmaster.json —— 仓库索引（/xlsettings 里填的就是它的 raw 地址）

为什么 dist/latest.zip 要进 git：
  仓库建好后的第一次推送就该能直接装。用 GitHub Release 的话必须先建 release，
  多一步；用 raw.githubusercontent.com 推上去立刻生效（zip 才 78KB）。

用法：
    python make_repo.py --owner <GitHub用户名> [--repo SimpleACR] [--no-build]

跑完之后把下面这行填进卫月的自定义插件仓库：
    https://raw.githubusercontent.com/<owner>/<repo>/main/dist/pluginmaster.json
"""

import argparse
import json
import os
import shutil
import subprocess
import sys
import time

ROOT = os.path.dirname(os.path.abspath(__file__))
SLN = os.path.join(ROOT, "SimpleACR.sln")

# 优先用环境变量 DOTNET_ROOT，其次本机固定安装位 D:\dotnet，最后退回 PATH 里的 dotnet。
# 这样仓库 clone 到别的机器上也能直接跑。
DOTNET_ROOT = os.environ.get("DOTNET_ROOT") or (
    r"D:\dotnet" if os.path.isdir(r"D:\dotnet") else "")
DOTNET = os.path.join(DOTNET_ROOT, "dotnet.exe") if DOTNET_ROOT else "dotnet"
DALAMUD_HOME = os.environ.get("DALAMUD_HOME") or os.path.join(
    os.environ.get("APPDATA", ""), "XIVLauncherCN", "addon", "Hooks", "dev")

# Release 构建时 DalamudPackager 的输出目录
RELEASE_OUT = os.path.join(ROOT, "SimpleACR", "bin", "x64", "Release")
PKG_DIR = os.path.join(RELEASE_OUT, "SimpleACR")
PKG_ZIP = os.path.join(PKG_DIR, "latest.zip")
PKG_JSON = os.path.join(PKG_DIR, "SimpleACR.json")

# 源码里的清单模板（Author / RepoUrl 默认是占位的 YourName，这里顺手改成真地址）
SRC_JSON = os.path.join(ROOT, "SimpleACR", "SimpleACR.json")

DIST = os.path.join(ROOT, "dist")
DIST_ZIP = os.path.join(DIST, "latest.zip")
DIST_MASTER = os.path.join(DIST, "pluginmaster.json")


def die(msg):
    print("[错误] " + msg)
    sys.exit(1)


def build():
    print("[构建] 使用 %s" % DOTNET)
    env = dict(os.environ)
    if DOTNET_ROOT:
        env["DOTNET_ROOT"] = DOTNET_ROOT
    env["DALAMUD_HOME"] = DALAMUD_HOME
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    print("[构建] Release ...")
    r = subprocess.run([DOTNET, "build", SLN, "-c", "Release"], env=env)
    if r.returncode != 0:
        die("构建失败")
    if not os.path.isfile(PKG_ZIP):
        die("构建成功但没找到 %s（DalamudPackager 可能没跑）" % PKG_ZIP)


def patch_source_manifest(owner, repo):
    """把源码清单里的 Author / RepoUrl 占位符换成真实地址，后续构建就一直是对的。"""
    if not os.path.isfile(SRC_JSON):
        return
    with open(SRC_JSON, "r", encoding="utf-8") as f:
        data = json.load(f)
    url = "https://github.com/%s/%s" % (owner, repo)
    changed = False
    if data.get("Author") != owner:
        data["Author"] = owner
        changed = True
    if data.get("RepoUrl") != url:
        data["RepoUrl"] = url
        changed = True
    if changed:
        with open(SRC_JSON, "w", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        print("[修正] 源码清单 Author/RepoUrl -> %s" % owner)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--owner", required=True, help="GitHub 用户名")
    ap.add_argument("--repo", default="SimpleACR", help="仓库名，默认 SimpleACR")
    ap.add_argument("--no-build", action="store_true", help="跳过编译，直接打包现有产物")
    args = ap.parse_args()

    owner, repo = args.owner, args.repo

    patch_source_manifest(owner, repo)

    if args.no_build:
        if not os.path.isfile(PKG_ZIP):
            die("没有构建产物，去掉 --no-build 重新跑")
    else:
        build()

    os.makedirs(DIST, exist_ok=True)
    shutil.copy2(PKG_ZIP, DIST_ZIP)

    with open(PKG_JSON, "r", encoding="utf-8") as f:
        manifest = json.load(f)

    # 分发地址用 raw.githubusercontent.com：推上去立即生效，不用先建 GitHub Release
    download = "https://raw.githubusercontent.com/%s/%s/main/dist/latest.zip" % (owner, repo)

    manifest.update({
        "Author": owner,
        "Name": "SimpleACR",
        "InternalName": "SimpleACR",
        "RepoUrl": "https://github.com/%s/%s" % (owner, repo),
        "DalamudApiLevel": 15,
        "ApplicableVersion": "any",
        "DownloadLinkInstall": download,
        "DownloadLinkUpdate": download,
        "DownloadLinkTesting": download,
        "LastUpdate": int(time.time() * 1000),
        "SourceRepo": "https://github.com/%s/%s" % (owner, repo),
        "IsHide": False,
        "IsTestingExclusive": False,
    })

    with open(DIST_MASTER, "w", encoding="utf-8") as f:
        json.dump([manifest], f, ensure_ascii=False, indent=2)

    size = os.path.getsize(DIST_ZIP)
    print()
    print("[完成] %s  (%.0f KB)" % (DIST_ZIP, size / 1024.0))
    print("[完成] %s" % DIST_MASTER)
    print()
    print("仓库地址（填进卫月 /xlsettings → 实验性 → 自定义插件仓库）：")
    print("    https://raw.githubusercontent.com/%s/%s/main/dist/pluginmaster.json"
          % (owner, repo))


if __name__ == "__main__":
    main()
