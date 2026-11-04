# SkiaX CI 构建镜像

> **这不是原创项目。** 本仓库是 [SkiaX](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version)
> （Emuera Skia 内核的 Android 端）的**在线编译镜像**，唯一目的是用 GitHub Actions 出 APK。

| | |
|:---|:---|
| 上游 | `gitgud.io/minus010001/emuera_lazyloading_selfmodified_version` |
| 分支 | `feature/xamarin` |
| 基点提交 | `b7345b6` — `build(android): 0.79.0（内核 Skiav17）` |
| 内核版本 | Emuera 1824 + v24 + EMv18 + EEv56 + **Skiav17** |
| 产物 | `Emuera.SkiaX-CI-signed.apk`（约 70–80 MB） |

## 为什么有这个仓库

上游只在 gitgud.io 托管，本地又没有 .NET/Android 工具链，
所以借 GitHub Actions 的免费 Linux runner 编译。**代码一行没改功能**，
只把 `net8.0-windows` 目标条件化了（见 [`NOTICE.md`](NOTICE.md) §4）。

## 怎么编

推送触发，或手动：仓库 → Actions → `Build SkiaX APK` → Run workflow。

流程：

1. `setup-dotnet` 装 .NET 8，`setup-java` 装 JDK 17
2. `dotnet workload install android`（runner 自带 Android SDK，不另装）
3. `dotnet publish -c Release -f net8.0-android -p:AndroidKeyStore=false`
   —— 出未签名 APK
4. CI 内临时生成 keystore 并用 `apksigner` 签名（用完即删，不进产物）
5. 上传 artifact `skiax-apk`，保留 30 天

## 安装注意

- **CI 签名证书与上游官方包不同**。若设备上装过官方 SkiaX，必须先卸载再装
  （不同签名的 APK 无法覆盖安装）。
- 本仓库只出 **arm64-v8a / x86_64 等全 RID 合一包**（`RuntimeIdentifiers` 未裁剪），
  体积大但兼容。
- 应用包名 `Emuera.SkiaX`，显示名 `Emuera skiaX`。

## 与上游保持同步

```bash
git remote add upstream https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version.git
git fetch upstream feature/xamarin
git merge upstream/feature/xamarin    # 本仓库改动只有 3 处，冲突极低
git push origin feature/xamarin
```

## 许可

**分层授权，务必读懂 [`NOTICE.md`](NOTICE.md)**：

- 引擎与移植代码 → Emuera 许可（zlib 式，**允许商用**）
- 本仓库新增的 CI / 文档 → PolyForm Noncommercial 1.0.0，**禁止商用**

所以本仓库**整体不是非商业项目**，只是维护者自建部分不做商用。
