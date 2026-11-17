#!/usr/bin/env python3
"""从合一 APK 里剥掉其它架构的 native 库，产出单架构 APK。

为什么不用 `dotnet publish -p:RuntimeIdentifiers=<RID>` 逐个编：
.NET 8 + Android workload 下重复 publish 会因中间产物 obj/ 布局与
`GetAssemblyAttributes` 期望不一致而报 MSB4018（找不到
obj/<cfg>/<tfm>/<rid>/XEmuera.Core.dll），并且每架构都要重编一遍，
CI 上要 4 倍时间。直接在已编好的合一二进制上做裁剪，秒级完成，
且不依赖 MSBuild 的 RID 行为。

用法：
    split_apk_abi.py <输入.apk> <输出.apk> <abi>[,<abi>...]

只保留 lib/<给定abi>/ 下的条目，其余 lib/<其他abi>/ 一律丢弃。
assemblies/*.blob（按架构分片的程序集）全部保留：总量约 7 MB，
删错会导致运行时找不到程序集，不值得省这点体积。
输出仍是不签名 APK，签名与 zipalign 由调用方（CI）负责。
"""

import os
import sys
import zipfile

# 重新打包会改变条目内容，旧签名必然失效；一律剔除，避免留下
# 「打了新签名又残留旧 .SF/.RSA」的包（安装时会校验失败）。
SIG_SUFFIXES = (".SF", ".RSA", ".DSA", ".EC")


def is_signature_entry(name):
    if not name.startswith("META-INF/"):
        return False
    base = name[len("META-INF/"):].upper()
    if "/" in base:
        return False
    return base == "MANIFEST.MF" or base.endswith(SIG_SUFFIXES)


def main(argv):
    if len(argv) != 4:
        print(__doc__)
        return 2
    src, dst, abi_arg = argv[1], argv[2], argv[3]
    keep = {a.strip() for a in abi_arg.split(",") if a.strip()}
    if not keep:
        print("::error::未指定要保留的 ABI")
        return 2

    with zipfile.ZipFile(src, "r") as zin:
        infos = zin.infolist()
        present = {
            i.filename.split("/")[1]
            for i in infos
            if i.filename.startswith("lib/") and i.filename.count("/") >= 2
        }
        unknown = keep - present
        if unknown:
            print(
                "::error::输入 APK 里没有这些架构的 lib/：%s（实际有：%s）"
                % (",".join(sorted(unknown)), ",".join(sorted(present)))
            )
            return 1

        kept, dropped = [], []
        sig_entries = []
        for i in infos:
            parts = i.filename.split("/")
            if is_signature_entry(i.filename):
                sig_entries.append(i.filename)
                continue
            if len(parts) >= 3 and parts[0] == "lib" and parts[1] not in keep:
                dropped.append(i.filename)
                continue
            kept.append(i)

        if sig_entries:
            print("剔除旧签名条目：%s" % ", ".join(sig_entries))

        dropped_bytes = sum(
            i.file_size for i in infos if i.filename in set(dropped)
        )
        if not dropped:
            print("::warning::没有可裁剪的其它架构，输出与输入相同")

        tmp = dst + ".tmp"
        with zipfile.ZipFile(tmp, "w", allowZip64=True) as zout:
            for i in kept:
                out = zipfile.ZipInfo(i.filename, date_time=i.date_time)
                out.compress_type = i.compress_type
                out.external_attr = i.external_attr
                out.create_system = i.create_system
                out.internal_attr = i.internal_attr
                out.comment = i.comment
                data = zin.read(i.filename)
                zout.writestr(out, data)
        os.replace(tmp, dst)

    dropped_by_abi = {}
    for name in dropped:
        dropped_by_abi[name.split("/")[1]] = dropped_by_abi.get(name.split("/")[1], 0) + 1
    print(
        "裁剪 %s -> %s：保留 %s，丢弃 %s"
        % (
            os.path.basename(src),
            os.path.basename(dst),
            ",".join(sorted(keep)),
            ", ".join("%s(%d 个)" % (k, v) for k, v in sorted(dropped_by_abi.items()))
            or "无",
        )
    )
    print(
        "条目 %d -> %d，未压缩体积减少 %.1f MB，输出 %.1f MB"
        % (
            len(infos),
            len(kept),
            dropped_bytes / 1048576.0,
            os.path.getsize(dst) / 1048576.0,
        )
    )
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
