#!/usr/bin/env python3
"""校验 APK 里的 native 库只含预期 ABI。

存在的意义：`dotnet publish -r <rid>` 在 csproj 写死了
`<RuntimeIdentifiers>` 列表时**不会**让产物变成单架构，而是静默地
照旧打出全部 ABI。CI 里只看「步骤成功」会漏掉这种失败，把四合一包
当成 arm64 包发出去。所以编完必须验一遍。

用法：
    check_apk_abi.py <apk> <期望abi>[,<期望abi>...]
退出码 0 = 符合；1 = 不符；2 = 用法错误。
"""

import sys
import zipfile


def main(argv):
    if len(argv) != 3:
        print(__doc__)
        return 2
    apk, expect_arg = argv[1], argv[2]
    expect = sorted(a.strip() for a in expect_arg.split(",") if a.strip())

    try:
        with zipfile.ZipFile(apk) as z:
            names = z.namelist()
    except (OSError, zipfile.BadZipFile) as e:
        print("::error::读不了 APK：%s" % e)
        return 1

    abis = sorted(
        {n.split("/")[1] for n in names if n.startswith("lib/") and n.count("/") >= 2}
    )
    print("APK 内含 ABI：%s（期望 %s）" % (abis, expect))

    if abis == expect:
        print("OK：架构符合预期")
        return 0

    print(
        "::warning::架构不符——期望 %s，实际 %s。多半是 csproj 里的 "
        "RuntimeIdentifiers 列表没被覆盖，产物仍是多架构。" % (expect, abis)
    )
    return 1


if __name__ == "__main__":
    sys.exit(main(sys.argv))
