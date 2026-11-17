# SkiaX CI 构建镜像

> **这不是原创项目。** 本仓库是 [SkiaX](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version)
> （Emuera Skia 内核的 Android 端）的**在线编译镜像**，唯一目的是用 GitHub Actions 出 APK。

| | |
|:---|:---|
| 上游 | `gitgud.io/minus010001/emuera_lazyloading_selfmodified_version` |
| 上游分支 | `feature/xamarin` |
| 本仓库开发分支 | `ux-fix`（含自用改动：软清理+返回列表、输入框底行、枚举容错、CI 等） |
| 基点提交 | `be44a9a` — `docs(release): 18.0 转正式版标记` |
| 内核版本 | Emuera 1824 + v24 + EMv18 + EEv56 + **Skiav18.0** |
| 应用版本 | `versionCode 81` / `versionName 0.81.0` |
| 产物 | 首选 `Emuera.SkiaX-arm64-v8a-signed.apk`（约 46 MB）<br>单架构编不出来时退回 `Emuera.SkiaX-universal-signed.apk`（四合一，约 70 MB） |

## 为什么有这个仓库

上游只在 gitgud.io 托管，本地又没有 .NET/Android 工具链，
所以借 GitHub Actions 的免费 Linux runner 编译。**代码一行没改功能**，
只把 `net8.0-windows` 目标条件化了（见 [`NOTICE.md`](NOTICE.md) §4）。

## 怎么编

推送触发，或手动：仓库 → Actions → `Build SkiaX APK` → Run workflow。

流程：

1. `setup-dotnet` 装 .NET 8，`setup-java` 装 JDK 17
2. `dotnet workload install android`（runner 自带 Android SDK，不另装）
3. `dotnet publish -c Release -f net8.0-android -r android-arm64` —— 只编
   arm64-v8a，出未签名 APK
4. 如果第 3 步失败，自动转**兜底路径**：编四合一包，**原样交付不裁剪**
   （代价是体积大，好处是一定能装）
5. `zipalign -f -p 4` 对齐后，CI 内临时生成 keystore 并用 `apksigner` 签名
   （keystore 用完即删，不进产物），最后 `verify --print-certs` 回显
6. 上传 artifact `skiax-apk`，保留 30 天；Job Summary 里写明本次走的是
   哪条路径、产物多大

### 只出 arm64 的理由

2026 年了，arm64-v8a 之外的 ABI（armeabi-v7a / x86 / x86_64）实际只覆盖
极老的 32 位机和模拟器，出一份就够。单架构包比四合一少约 20 MB，CI 也省
三次编译。

真要跑模拟器（x86_64）或很老的 32 位机，把 workflow 里的 `-r android-arm64`
换成对应的 RID 再跑一次即可：`android-arm`（armeabi-v7a）、`android-x86`、
`android-x64`。也可以同时保留多个（`-r android-arm64 -r android-x64`），
MSBuild 会按 RID 各出一个包。

### 单架构编译：三个坑要一起处理（实测复盘）

`Emuera.Xamarin.Android.csproj:45` 写死了四个 RID：

```xml
<RuntimeIdentifiers>android-arm;android-x86;android-x64;android-arm64</RuntimeIdentifiers>
```

由此引出一连串坑，**踩过两次才全打通**：

1. **只给 `-r android-arm64` 会静默失败**。`-r` 只设 `RuntimeIdentifier`，
   打包仍按 `RuntimeIdentifiers` 列表来，产物还是四架构——而且 CI 报成功，
   文件名却叫 `arm64-v8a`，体积一点没小。所以必须**再用
   `-p:RuntimeIdentifiers=android-arm64` 覆盖列表**。
2. **光覆盖列表会报 MSB4018**。`-p:RuntimeIdentifiers=<单个 RID>` 让 Core
   的 `TargetPath` 带上 rid 子目录（`obj/<cfg>/<tfm>/<rid>/`），而实际编译
   产物落在无 rid 目录 → `GetAssemblyAttributes` `FileNotFoundException`。
   解法：加 `-p:AppendRuntimeIdentifierToOutputPath=false` 把两边对齐。
3. **所以必须验产物**。光看步骤成功不够，编完用
   `scripts/check_apk_abi.py <apk> arm64-v8a` 检查 `lib/` 里真的只有一个
   ABI，不是就判失败转兜底。

完整命令：

```bash
dotnet publish Emuera.Xamarin.Android/Emuera.Xamarin.Android.csproj \
  -c Release -f net8.0-android \
  -r android-arm64 \
  -p:RuntimeIdentifiers=android-arm64 \
  -p:AppendRuntimeIdentifierToOutputPath=false \
  -p:AndroidPackageFormat=apk -p:AndroidKeyStore=false \
  -p:RunAOTCompilation=false -p:CheckEolWorkloads=false
```

背景：`$(AndroidCreatePackagePerAbi)` 自 .NET 9 起弃用（dotnet/android
PR #9586），官方推荐改用 `-r` 逐个编译。

## 安装注意

- **CI 签名证书与上游官方包不同**。若设备上装过官方 SkiaX，必须先卸载再装
  （不同签名的 APK 无法覆盖安装）。
- 正常情况是 **arm64-v8a 单架构包**（约 46 MB）；若单架构编译失败，本次
  会退回**四合一包**（约 70 MB）——名字里带 `universal` 就是没裁剪的。
- 包由 `zipalign -f -p 4` 对齐后再签名。
- 应用包名 `Emuera.SkiaX`，显示名 `Emuera skiaX`。

## 与上游保持同步

```bash
git remote add upstream https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version.git
git fetch upstream '+refs/heads/*:refs/remotes/upstream/*'
git merge upstream/feature/xamarin    # 本仓库自己的改动很少，冲突极低
git push origin ux-fix
```

## 许可

**分层授权，务必读懂 [`NOTICE.md`](NOTICE.md)**：

- 引擎与移植代码 → Emuera 许可（zlib 式，**允许商用**）
- 本仓库新增的 CI / 文档 → PolyForm Noncommercial 1.0.0，**禁止商用**

所以本仓库**整体不是非商业项目**，只是维护者自建部分不做商用。
