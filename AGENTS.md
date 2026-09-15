# AGENTS.md — SkiaX（L2 薄壳）

> 本文件是 **SkiaX 工作区的 L2 薄壳，只写本仓特有规则**。
> 集群通用规范在 **L1 正本**：`D:\emuera\shared-trae\AGENTS.md` ＋ `knowledge\meta\**` —— **开工前先读 L1**。
> 分层模型：[instruction-layering.md](file:///d:/emuera/shared-trae/knowledge/meta/instruction-layering.md)。
> Trae 侧同构入口：`.trae\rules\project_rules.md`。

---

## 0. 本仓定位（**先读这一条，此处曾是错误高发区**）

> **SkiaX（`D:\emuera\SkiaX`）不是独立仓库，也不是主工作区**——它是 `emuera_lazyloading_selfmodified_version` 仓库的 **Git worktree**，checkout 到 **`feature/xamarin`** 分支。
> 两个工作区共享同一个 `.git`：**任一分支上的提交对另一个工作区立即可见**。
> **禁止**把本仓描述成"主工作区"（历史上 `.trae\rules\project_rules.md` 是 LazyLoading 规则的陈旧副本，曾对本仓发出错误指令）；也**禁止与 `m-emuera` 混淆**（后者是完全独立的 Git 仓库）。

- **角色**：LazyLoading 仓库内的 Xamarin/Android 移植端（`Emuera.Xamarin*`）——直接编译 LazyLoading `Runtime/` 内核 + XEmuera-1 的 `Platform/` Skia UI 层。
- **技术栈**：包名 `Emuera.SkiaX`，TFM `net8.0-android`（.NET 8 工具链，非旧 MonoAndroid）。
- **主仓规则同样适用**：分支管理、版本签名、内核回流分类见 `D:\emuera\emuera_lazyloading_selfmodified_version\AGENTS.md`。

## 1. 本仓技能子集

**默认 4 个**（erabasic / powershell-git / knowledge-builder / kb-consolidation）见 [L1 §2](file:///d:/emuera/shared-trae/AGENTS.md)。本仓额外高频：

| 技能 | 激活条件 |
|------|---------|
| **skiax** | 强制：SkiaX 移植操作、ADB 测试、Android 渲染/IO/字体/AWAIT 难点 |
| **lazyloading** | 触及 `Runtime/` 内核、分支管理、PlatformInterop 抽象时 |

> 完整 30 技能总表 → [meta/skill-routing.md](file:///d:/emuera/shared-trae/knowledge/meta/skill-routing.md)。

## 2. 本仓死规矩

1. **worktree 纪律**：本仓的提交直接落在 `feature/xamarin` 分支上，主工作区可见。改内核前先确认"这个改动属于内核（回流）还是移植端专有"，避免把平台专有代码提交进内核分支。
2. **ADB 调试**：包名 `Emuera.SkiaX`；APK 必须 `EmbedAssembliesIntoApk` 开启；渲染诊断看 logcat tag `EmueraRender` / `EmueraHost`；引擎初始化顺序 `FontModel.Load()` 必须在 `Program.Init()` 之前，且 **AOT 必须禁用**。详见 [adb-testing.md](file:///d:/emuera/shared-trae/knowledge/skiax/adb-testing.md) 与 [android-engine-init.md](file:///d:/emuera/shared-trae/knowledge/skiax/android-engine-init.md)。
3. **坐标系**：物理像素 / 逻辑像素混用是渲染偏移与点击不准的主因；`pointY` 语义变更需补偿 `LineHeight`；`FontSize` / `LineHeight` **不乘 density**。详见 [coordinate-system.md](file:///d:/emuera/shared-trae/knowledge/skiax/coordinate-system.md)。
4. **禁止改 `XEmuera` / `emuera.em`**：前者是上游原型（仅参考），后者是上游原版（对照基线）。
5. **ERB 死规矩同样适用**：本仓附带可运行脚本与调试用 ERB，见 [L1 §3](file:///d:/emuera/shared-trae/AGENTS.md)。
