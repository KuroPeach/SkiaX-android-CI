# 许可分层说明 / Licensing Notice

本仓库是 **SkiaX（Emuera Skia 内核 Android 端）的构建镜像**，并非原创项目。
仓库内绝大部分代码来自上游，各部分的授权如下，请勿混用。

## 1. 上游引擎与移植代码（不受本仓库 LICENSE 约束）

| 部分 | 来源 | 许可 |
|:---|:---|:---|
| `Emuera/` 及 `Emuera/Runtime/**` 内核 | [Emuera 原版](https://gitlab.com/EvilMask/emuera.em) → LazyLoading / SkiaSharp 变体 | Emuera 许可（zlib 式） |
| `Emuera.Xamarin*/` Android / Xamarin 移植端 | [minus010001/emuera_lazyloading_selfmodified_version](https://gitgud.io/minus010001/emuera_lazyloading_selfmodified_version) 的 `feature/xamarin` 分支 | 随上游；无独立授权声明 |
| UI 框架 | [Future-R/XEmuera](https://github.com/Future-R/XEmuera) | 随上游 |
| SkiaSharp 渲染移植 | VVII 的 "SkiaSharpへの置き換え" 提交 | 随上游 |

**Emuera 许可要点**（原文见 [`LICENSES/Emuera.LICENSE.txt`](LICENSES/Emuera.LICENSE.txt)）：
允许包括商业应用在内的任意目的使用、自由修改与再分发，条件是 ——
不得虚称软件来源、不得声称自己是原作者、**修改后必须明示这一点**、不得删除或改写许可声明。

> 因此：**本仓库整体不能声明为「非商业」**，因为引擎部分的许可本身允许商用。
> 本仓库所做的「非商业」标注，仅约束本仓库维护者自己新增的 CI 与文档部分（见 `LICENSE`）。

## 2. 第三方依赖（各自许可）

| 依赖 | 许可 |
|:---|:---|
| LibWebP | BSD（见 `LICENSES/LibWebp.LICENSE.txt`） |
| BIZ UDGothic 字体 | SIL Open Font License |
| MS Gothic / SimHei / Microsoft YaHei | 随各字体原作者授权 |
| SkiaSharp | MIT |
| Xamarin.Forms / Xamarin.Essentials | MIT |
| .NET / Android SDK | MIT / Apache-2.0 |

## 3. 本仓库新增部分

CI 配置、构建文档、以及对 Core 项目目标框架的条件化处理 ——
**[PolyForm Noncommercial License 1.0.0](https://polyformproject.org/licenses/noncommercial/1.0.0)，禁止商业使用**。全文见根目录 [`LICENSE`](LICENSE)。

## 4. 修改声明（Emuera 许可第 2 条要求）

本仓库相对上游 `feature/xamarin` 分支（提交 `b7345b6`，Skiav17 / v0.79.0）的改动：

1. `Emuera.Xamarin.Core/Emuera.Xamarin.Core.csproj`
   —— `TargetFrameworks` 改为按操作系统条件化：仅 Windows 主机追加 `net8.0-windows`，
   其余平台只用 `net8.0`。原因：`net8.0-windows` + `UseWindowsForms` 在 Linux 上触发
   `NETSDK1100`，导致 GitHub Actions（Linux runner）无法构建。**不影响 Windows 桌面端行为。**
2. 新增 `.github/workflows/android.yml` —— 在 GitHub Actions 上构建 APK。
3. 新增本文件、`LICENSE`、`README-CI.md`、`LICENSES/`。

除上述内容外，**代码与上游一致，未做任何功能修改。**

## 5. 免责

本仓库仅为通用文本脚本运行环境的构建产物，不提供、不制作、不背书任何第三方游戏脚本。
严禁用于违反当地法律法规的用途。因使用产生的任何责任由使用者自行承担。
