# AGENTS.md — emuera_lazyloading_selfmodified_version（L2 薄壳）

> 本文件是 **引擎源码仓的 L2 薄壳，只写本仓特有规则**。
> 集群通用规范（仓库拓扑 / 默认技能 / ERB 死规矩 / 会话结束检查）在 **L1 正本**：
> `D:\emuera\shared-trae\AGENTS.md` ＋ `D:\emuera\shared-trae\knowledge\meta\**` —— **开工前先读 L1**。
> 分层模型：[instruction-layering.md](file:///d:/emuera/shared-trae/knowledge/meta/instruction-layering.md)。
> Trae 侧同构入口：`.trae\rules\project_rules.md`。

---

## 0. 本仓定位

- **角色**：eratw 实际运行的 Emuera 引擎（**LazyLoading / SkiaSharp 变体**）源码。.NET C# WinForms 桌面应用，解决方案 `Emuera.sln`，内核在 `Emuera/`。
- **分支**：主工作区 checkout `develop-skiasharp`（内核开发）；`main-skiasharp`（稳定发布）；`develop-legency` / `legency-textrender`（旧版 GDI+/文本渲染）。
- **上游**：`ee+em` remote = `gitlab.com/EvilMask/emuera.em`（EM+EE）。
- **构建 / 测试**：`dotnet build "D:\emuera\emuera_lazyloading_selfmodified_version\Emuera.sln" 2>&1`；`dotnet test 2>&1`。
- ⚠️ 与 `D:\emuera\emuera.em`（上游原版，无变体修改）区分——引用源码前核对路径前缀。

## 1. 本仓技能子集

**默认 4 个**（erabasic / powershell-git / knowledge-builder / kb-consolidation）见 [L1 §2](file:///d:/emuera/shared-trae/AGENTS.md)。本仓额外高频：

| 技能 | 激活条件 |
|------|---------|
| **lazyloading** | 内核开发、分支管理、Xamarin 移植回流、PlatformInterop 抽象 |
| **skiax** | 操作 `Emuera.Xamarin*/`（Android 端）时 |
| **erabasic** | 触及 ERABASIC 解析器/运行时/指令时 |

> 完整 30 技能总表 → [meta/skill-routing.md](file:///d:/emuera/shared-trae/knowledge/meta/skill-routing.md)。

## 2. 本仓死规矩

1. **Git worktree 拓扑（强制认知）**：`D:\emuera\SkiaX` 是**本仓的 Git worktree**（checkout `feature/xamarin`），与本仓共享同一 `.git`——任一工作区的提交对另一个立即可见。`m-emuera`（`D:\emuera\m-emuera`）是**完全独立的 Git 仓库**；`XEmuera` 是上游原型（**禁改**）。三者禁止混淆。
2. **版本签名**：只需改 `Emuera/Emuera.csproj` 的 `<InformationalVersion>`，运行时经 `PlatformInterop.GetProductVersion()` 自动读取。
   - 段位：`1824`（Emuera 原版）/ `v24`（私家改编）为历史段不再递增；功能版本走 `Skiav` 段；上游同步走 `EMv` / `EEv` 段。
   - **禁止**改 `Sys.cs` 的 `EmueraVersionText`、`Program.cs` 的 `GetProductVersion` 委托。
   - Android 版本独立维护 `AndroidManifest.xml` 的 `versionCode` / `versionName`。
   - 改完**同步更新 README 标题签名与页脚版本**（见 [branch-strategy.md §版本签名](file:///d:/emuera/shared-trae/knowledge/lazyloading/branch-strategy.md)）。
3. **内核修改回流分类**：改动前先判定 A（应回流 upstream）/ B（需 PlatformInterop 抽象）/ C（不回流）三类 → `.trae\rules\kernel\modification-classification.md` ＋ [kernel-audit.md](file:///d:/emuera/shared-trae/knowledge/lazyloading/kernel-audit.md)。
4. **新增/修改 ERB 内置函数 = 两个提交**：① 引擎侧（实现 + 版本 + 日志）② 文档侧（`emuera.em.doc-1` 三语参考页 + 摘要 + 日志 + 索引）。表达式函数 3 文件、命令型 7+ 文件 —— 见 [skia-engine-dev-workflow.md](file:///d:/emuera/shared-trae/knowledge/erabasic/skia-engine-dev-workflow.md)。
5. **分支单向流**：`develop-skiasharp` → `feature/xamarin`（SkiaX）；A 类修复经 cherry-pick 回流 upstream。详见 [branch-strategy.md](file:///d:/emuera/shared-trae/knowledge/lazyloading/branch-strategy.md)。
