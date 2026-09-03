# -*- coding: utf-8 -*-
"""
用 GitHub Git Data API 推送，绕开 git-over-HTTPS 被代理掐断的问题。

为什么不用 git push：
  本机代理（localhost:15236）在上传稍大的 pack 时稳定
  "send-pack: unexpected disconnect while reading sideband packet"。
  API 每次只传一个文件（最大的 latest.zip 也才 48KB），不容易断。

用法：
    set GH_TOKEN=xxxx  &&  python push_api.py [--repo simpleacr]
"""

import base64
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

ROOT = os.path.dirname(os.path.abspath(__file__))
GIT = r"C:\Users\asus\.workbuddy\binaries\PortableGit\versions\1.2.0\cmd\git.exe"
API = "https://api.github.com"
PROXY = "http://localhost:15236"

LOG = []


def log(msg):
    print(msg)
    LOG.append(msg)


def api(path, method="GET", payload=None, token="", retry=3):
    url = API + path
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    last = None
    for attempt in range(retry):
        # api.github.com 直连通常就通，失败再退回系统代理
        for opener in (build_opener(None), build_opener(PROXY)):
            try:
                req = urllib.request.Request(url, data=data, method=method)
                req.add_header("Authorization", "Bearer " + token)
                req.add_header("Accept", "application/vnd.github+json")
                req.add_header("User-Agent", "simpleacr-push")
                if data:
                    req.add_header("Content-Type", "application/json")
                with opener.open(req, timeout=120) as r:
                    body = r.read().decode("utf-8")
                    return json.loads(body) if body else {}
            except urllib.error.HTTPError as e:
                body = e.read().decode("utf-8", "ignore")
                last = "HTTP %s %s | %s" % (e.code, e.reason, body[:300])
                if e.code in (401, 403, 404, 422):
                    return {"__error__": last}
            except Exception as e:
                last = "%s: %s" % (type(e).__name__, e)
        time.sleep(2)
    return {"__error__": last}


def build_opener(proxy):
    if proxy:
        return urllib.request.build_opener(
            urllib.request.ProxyHandler({"http": proxy, "https": proxy}))
    return urllib.request.build_opener(urllib.request.ProxyHandler({}))


def tracked_files():
    out = subprocess.run([GIT, "-C", ROOT, "ls-files", "-z"],
                         capture_output=True)
    if out.returncode != 0:
        raise SystemExit("git ls-files 失败: " + out.stderr.decode("utf-8", "ignore"))
    return [p for p in out.stdout.decode("utf-8").split("\0") if p]


def main():
    token = os.environ.get("GH_TOKEN", "").strip()
    if not token:
        raise SystemExit("请先设置环境变量 GH_TOKEN")
    owner = "sfy20001220-collab"
    repo = "simpleacr"
    if "--repo" in sys.argv:
        repo = sys.argv[sys.argv.index("--repo") + 1]
    base = "/repos/%s/%s" % (owner, repo)

    info = api(base, token=token)
    if "__error__" in info:
        raise SystemExit("读不到仓库: " + info["__error__"])
    log("仓库: %s  默认分支=%s" % (info.get("full_name"), info.get("default_branch")))

    # 判断是不是空仓库（空仓库没有 HEAD ref）
    ref = api(base + "/git/ref/heads/main", token=token)
    empty = "__error__" in ref
    log("远端状态: %s" % ("空仓库，将创建首次提交" if empty else "已有提交"))

    if empty:
        # GitHub 不允许对空仓库直接建 blob（409 "Git Repository is empty"），
        # 必须先用 Contents API 落一个文件，把仓库"激活"。
        b = api(base + "/contents/README.md", method="PUT", token=token, payload={
            "message": "init",
            "content": base64.b64encode(b"# SimpleACR\n").decode("ascii"),
        })
        if "__error__" in b:
            raise SystemExit("引导首次提交失败: " + b["__error__"])
        log("已用 Contents API 引导出首次提交，仓库激活")
        ref = api(base + "/git/ref/heads/main", token=token)
        if "__error__" in ref:
            raise SystemExit("引导后仍读不到 main ref: " + ref["__error__"])

    files = tracked_files()
    log("待上传文件: %d 个" % len(files))

    tree = []
    for i, rel in enumerate(files, 1):
        full = os.path.join(ROOT, rel.replace("/", os.sep))
        if not os.path.isfile(full):
            log("  [跳过] 不存在: %s" % rel)
            continue
        blob = api(base + "/git/blobs", method="POST", token=token,
                   payload={"content": base64.b64encode(
                       open(full, "rb").read()).decode("ascii"),
                       "encoding": "base64"})
        if "__error__" in blob or "sha" not in blob:
            log("  [失败] %s -> %s" % (rel, blob.get("__error__")))
            raise SystemExit("上传 blob 失败: " + rel)
        tree.append({"path": rel, "mode": "100644", "type": "blob", "sha": blob["sha"]})
        if i % 10 == 0 or i == len(files):
            log("  blob %d/%d ..." % (i, len(files)))

    log("创建 tree (%d 条目) ..." % len(tree))
    t = api(base + "/git/trees", method="POST", token=token, payload={"tree": tree})
    if "__error__" in t:
        raise SystemExit("创建 tree 失败: " + t["__error__"])

    log("创建 commit ...")
    c = api(base + "/git/commits", method="POST", token=token, payload={
        "message": "SimpleACR: 卫月(国服)自动循环插件，适配 Dalamud API 15 / net10",
        "tree": t["sha"],
        "parents": [] if empty else [ref["object"]["sha"]],
    })
    if "__error__" in c:
        raise SystemExit("创建 commit 失败: " + c["__error__"])

    r = api(base + "/git/refs/heads/main", method="PATCH", token=token,
            payload={"sha": c["sha"], "force": True})
    if "__error__" in r:
        raise SystemExit("更新 ref 失败: " + r["__error__"])

    log("")
    log("提交: %s" % c["sha"])
    log("https://github.com/%s/%s" % (owner, repo))
    log("")
    log("仓库地址（填进卫月 /xlsettings -> 实验性 -> 自定义插件仓库）：")
    log("    https://raw.githubusercontent.com/%s/%s/main/dist/pluginmaster.json"
        % (owner, repo))


if __name__ == "__main__":
    main()
