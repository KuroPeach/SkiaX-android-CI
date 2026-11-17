# SkiaX CI 构建镜像

> **这不是原创项目。** 本仓库是 [SkiaX](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version)
> （Emuera Skia 内核的 Android 端）的**在线编译镜像**，唯一目的是用 GitHub Actions 出 APK。

| | |
|:---|:---|
| 上游 | `gitgud.io/minus010001/emuera_lazyloading_selfmodified_version` |
| 分支 | `feature/xamarin` |
| 基点提交 | `be44a9a` — `docs(release): 18.0 转正式版标记` |
| 内核版本 | Emuera 1824 + v24 + EMv18 + EEv56 + **Skiav18.0** |
| 应用版本 | `versionCode 81` / `versionName 0.81.0` |
| 产物 | `Emuera.SkiaX-universal-signed.apk`（约 70 MB）<br>`Emuera.SkiaX-<abi>-signed.apk` × 4（各约 50 MB） |

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
4. 从合一包里裁掉其它架构的 `lib/<abi>/`，得到 4 个单架构包
   （`scripts/split_apk_abi.py`，纯裁剪不重编，秒级完成）
5. `zipalign -p 4` 对齐后，CI 内临时生成 keystore 并用 `apksigner` 签名
   （keystore 用完即删，不进产物）
6. 上传 artifact `skiax-apk`（合一包 + 4 个单架构包），保留 30 天

### 为什么按 ABI 拆分是「裁剪」而不是「重编」

早先的写法是循环 `dotnet publish -p:RuntimeIdentifiers=<单个 RID>`，
每架构重编一遍。在 .NET 8 + Android workload 下这条路会撞两个坑：

1. **MSB4018**：重复 publish 后 `Emuera.Xamarin.Core/obj` 的布局变成
   `obj/Release/net8.0/<rid>/XEmuera.Core.dll`，而 `GetAssemblyAttributes`
   仍在老路径找，直接 `FileNotFoundException`；
2. 每架构重编一次，CI 时间 ×4。

现在的做法是在已编好的合一包上剪枝，`assemblies/*.blob` 全部保留（约 7 MB）
——按架构分片的 blob 删错会导致运行时缺程序集，不值得为这点体积冒险。

## 安装注意

- **CI 签名证书与上游官方包不同**。若设备上装过官方 SkiaX，必须先卸载再装
  （不同签名的 APK 无法覆盖安装）。
- 产物有两类：**合一包**（四架构全含，兼容所有设备，约 70 MB）与
  **单架构包**（只含一个 ABI，约 50 MB）。绝大多数手机装 `arm64-v8a` 那个即可。
- 所有包都由 `zipalign -p 4` 对齐后再签名。
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
