# Emuera.Xamarin Phase 1 Round 2 执行计划

> ⚠️ **本文件已归档**：持久知识已迁移至知识库 `xemuera/type-conflicts.md`（F/G/H 类新发现）。本文件可在 Round 3 完成后删除。
> **创建日期**：2026-05-15
> **当前状态**：336 个编译错误（Round 1 完成，git add 已执行，待 commit）
> **目标**：336 → 0 个编译错误

---

## 前置步骤：提交 Round 1 成果

### Step 0.1：Git Commit

- git add 已完成（26 个文件已暂存）
- 执行 `git commit -m "feat(xamarin): Phase 1 Round 1 - 编译错误从818收敛到336"`
- 验证 commit 成功

---

## Round 2 执行计划（细粒度任务）

### Round 2D：内核类型缺失 using（~36 个错误）— 最简单，先做

**根因**：Platform 层文件找不到 `EraStreamReader`、`FontFactory`、`IOperandTerm` 等内核类型。

| # | 任务 | 文件 | 具体操作 | 预期消除 |
|---|------|------|---------|---------|
| 2D-1 | 添加 EraStreamReader using | Platform/Config/ConfigData.cs | `using MinorShift.Emuera.Runtime.Utils;` | ~10 |
| 2D-2 | 添加 EraStreamReader using | Platform/KeyMacro.cs（如存在） | `using MinorShift.Emuera.Runtime.Utils;` | ~5 |
| 2D-3 | 添加 IOperandTerm using | Platform/GameView/EmueraConsole.cs | `using MinorShift.Emuera.Runtime.Script.Statements.Expression;` | ~8 |
| 2D-4 | 验证 FontFactory 可见性 | — | 编译检查 FontFactory 错误是否因级联消除 | ~5 |
| 2D-5 | 编译验证 | — | `dotnet build`，记录错误数 | — |
| 2D-6 | 认知整理 | type-conflicts.md | 更新 D 类修复进度 | — |

**风险**：FontFactory 可能需要额外的 using 或存根，取决于级联错误消除情况。

---

### Round 2E：Nullable 不匹配抑制（~22 个错误）— 配置变更，快速

**根因**：内核代码中 `ScriptPosition?` 赋值给 `ScriptPosition`（非 nullable），CS0037/CS8601 等。

| # | 任务 | 文件 | 具体操作 | 预期消除 |
|---|------|------|---------|---------|
| 2E-1 | 添加 Nullable 警告抑制 | Emuera.Xamarin.csproj | `<NoWarn>` 中追加 CS0037;CS8601;CS8602;CS8603;CS8604;CS8618;CS8625;CS0266 | ~22 |
| 2E-2 | 编译验证 | — | `dotnet build`，记录错误数 | — |
| 2E-3 | 认知整理 | type-conflicts.md | 更新 E 类修复进度 | — |

**注意**：不修改内核文件，仅抑制 nullable 警告。这是权宜之计，Phase 2 运行时适配时需重新审视。

---

### Round 2C：ConfigCode 枚举成员排除（~116 个错误）— 排除 Platform 文件

**根因**：Platform 层的 ConfigModel.cs 和 ConfigValuePopup.xaml.cs 引用了 XEmuera 特有的 ConfigCode 枚举成员（如 PanSpeed、AdaptiveFont、FontScale 等），这些成员在内核的 ConfigCode 中不存在。

**策略选择**：排除引用 XEmuera 特有 ConfigCode 的 Platform 文件，因为它们是 XEmuera 特有的配置 UI，不影响内核编译。

| # | 任务 | 文件 | 具体操作 | 预期消除 |
|---|------|------|---------|---------|
| 2C-1 | 识别引用 XEmuera ConfigCode 的文件 | — | 编译输出中筛选 CS0117 错误涉及的文件 | — |
| 2C-2 | 排除 ConfigModel.cs | Emuera.Xamarin.csproj | `<Compile Remove="Models\ConfigModel.cs" />` | ~30 |
| 2C-3 | 排除 ConfigValuePopup.xaml.cs | Emuera.Xamarin.csproj | `<Compile Remove="Views\ConfigValuePopup.xaml.cs" />` | ~78 |
| 2C-4 | 检查是否有其他文件引用 XEmuera ConfigCode | — | 编译输出中确认 | ~8 |
| 2C-5 | 编译验证 | — | `dotnet build`，记录错误数 | — |
| 2C-6 | 认知整理 | type-conflicts.md | 记录排除决策和原因 | — |

**风险**：排除 ConfigModel.cs 可能导致其他文件编译失败（级联依赖）。需要验证。

**替代方案**：如果排除导致级联问题，改为在内核 ConfigCode 中添加 XEmuera 特有成员（条件编译 `#if XEMUERA`）。

---

### Round 2A：XAML 代码生成问题（~78 个错误）— 需要调查

**根因**：XAML 文件的代码生成（`InitializeComponent`、控件类型等）需要 Xamarin.Forms 构建工具链。当前 csproj 使用 `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`，可能影响了 XAML 代码生成。

| # | 任务 | 文件 | 具体操作 | 预期消除 |
|---|------|------|---------|---------|
| 2A-1 | 调查 XAML 代码生成机制 | — | 确认 Xamarin.Forms XAML 编译器是否在 net8.0-windows TFM 下工作 | — |
| 2A-2 | 检查 XAML 文件关联 | Emuera.Xamarin.csproj | 确认 `.xaml` 和 `.xaml.cs` 的 `DependentUpon`/`Generator` 配置 | — |
| 2A-3 | 尝试修复 XAML 代码生成 | Emuera.Xamarin.csproj | 添加/修正 `<Compile>` 和 `<EmbeddedResource>` 配置 | ~78 |
| 2A-4 | 备选：排除 XAML 页面 | Emuera.Xamarin.csproj | 如果 XAML 工具链不兼容，排除所有 XAML 页面文件 | ~78 |
| 2A-5 | 编译验证 | — | `dotnet build`，记录错误数 | — |
| 2A-6 | 认知整理 | 新建 xaml-codegen.md | 记录 XAML 代码生成在 net8.0-windows 下的兼容性结论 | — |

**关键不确定性**：Xamarin.Forms 5.0 的 XAML 编译器是否支持 net8.0-windows TFM。如果不支持，需要排除所有 XAML 页面或切换到其他 UI 框架。

**验证方法**：先检查编译输出中 XAML 相关错误的具体内容，判断是"找不到 InitializeComponent"（代码生成未运行）还是"找不到控件类型"（命名空间问题）。

---

### Round 2F：dynamic 与表达式树（~50 个错误）— 需要创建存根类

**根因**：`GlobalStatic.Pfc` 声明为 `dynamic`，但 `Creator.Method.cs` 中的表达式树（lambda → Expression<T>）不能包含 dynamic 操作（CS0021）。

| # | 任务 | 文件 | 具体操作 | 预期消除 |
|---|------|------|---------|---------|
| 2F-1 | 检查内核 Preload 类接口 | Runtime/ 中搜索 Preload | 确认 Preload 类的公共方法和属性 | — |
| 2F-2 | 创建 Preload 存根 | Platform/Stubs/Preload.cs | 包含 Creator.Method.cs 引用的所有方法和属性 | — |
| 2F-3 | 修改 GlobalStatic.Pfc 类型 | Platform/GlobalStatic.cs | `public static dynamic Pfc;` → `public static Preload Pfc;` | ~50 |
| 2F-4 | 编译验证 | — | `dotnet build`，记录错误数 | — |
| 2F-5 | 认知整理 | type-conflicts.md | 记录 dynamic→Preload 类型变更 | — |

**风险**：Preload 类可能有很多方法和属性，需要逐一确认 Creator.Method.cs 中使用了哪些。

**替代方案**：如果 Preload 接口过于复杂，可以将 Creator.Method.cs 中引用 Pfc 的方法单独排除，或用 `object` 类型替代 `dynamic`。

---

### Round 2B：Creator.Method.cs 存根补全（~200 个错误）— 工作量最大

**根因**：`Creator.Method.cs` 引用了 `Bgm`、`Sound`、`EmueraConsole` 的方法，这些类型的 Platform 存根不完整。

| # | 任务 | 文件 | 具体操作 | 预期消除 |
|---|------|------|---------|---------|
| 2B-1 | 提取 Creator.Method.cs 中所有 CS1061 错误 | — | 编译输出中筛选 CS1061 + Creator.Method.cs | — |
| 2B-2 | 分类缺失方法 | — | 按 Bgm/Sound/EmuConsole 分组 | — |
| 2B-3 | 补全 Bgm 存根方法 | Platform/Stubs/SoundAndBgm.cs | pause/resume/stop/SetPreservePitch/getVolume/setVolume/GetTotalTime/GetCurrentTime/getSpeed/setSpeed/play/isPlaying | ~80 |
| 2B-4 | 补全 Sound 存根方法 | Platform/Stubs/SoundAndBgm.cs | 类似 Bgm 的方法 | ~60 |
| 2B-5 | 补全 EmueraConsole 存根方法 | Platform/GameView/EmueraConsole.cs | ExistsImageLayer 等 | ~30 |
| 2B-6 | 补全 MainWindow 存根方法 | Platform/Stubs/MainWindow.cs | Creator.Method.cs 引用的方法 | ~20 |
| 2B-7 | 编译验证 | — | `dotnet build`，记录错误数 | — |
| 2B-8 | 认知整理 | type-conflicts.md | 更新 C 类修复进度 | — |

**风险**：方法签名可能需要从内核源码中精确获取，不能凭猜测。

---

## 执行顺序与依赖关系

```
Step 0: Git Commit（前置）
  ↓
Round 2D (using) → Round 2E (Nullable) → Round 2C (ConfigCode排除)
  ↓
Round 2A (XAML) — 可与 2F 并行调查
  ↓
Round 2F (dynamic→Preload)
  ↓
Round 2B (存根补全) — 依赖 2F 完成
```

**优先级理由**：
1. 2D/2E 最简单，快速消除 ~58 个错误，建立信心
2. 2C 排除文件可快速消除 ~116 个错误
3. 2A 需要调查，可能需要排除 XAML 文件
4. 2F 是 2B 的前置条件（Pfc 类型影响 Creator.Method.cs 编译）
5. 2B 工作量最大，放最后

---

## 验证与反思步骤（每个 Round 后必执行）

```
1. 编译验证：dotnet build，记录错误数
2. 错误分类：如果错误数未如预期减少，分析新出现的错误
3. 认知整理：更新 type-conflicts.md 中的修复进度表
4. 反思：
   - 是否有新引入的问题？
   - 是否有更优方案？
   - 排除的文件是否会导致运行时缺失？
5. TODO 更新：更新 TODO.md 中的任务状态
```

---

## 经验教训（认知持久化）

### 教训 1：输出文件验证优先
- 调用 search agent 前，先用 Read 工具验证文件内容非空
- 优先用终端直接输出 + Select-String 过滤，不写文件
- `dotnet build 2>&1 | Out-File` 在 PS5.1 中可能产生空文件

### 教训 2：命名空间迁移是反模式
- 不能移动 Platform 类型的命名空间来匹配内核期望
- 用 csproj 全局 `<Using Include>` 做命名空间转发

### 教训 3：全局 using 有冲突限制
- `MinorShift.Emuera.Runtime.Config` 与 `MinorShift.Emuera.Config` 冲突
- 冲突命名空间用局部 using 别名处理

### 教训 4：csproj 排除项需审计
- Creator.Method.cs 误排除导致 ~300 个错误
- 每次修改排除项时，验证排除原因是否仍然成立

### 教训 5：编译验证前先整理认知
- 不要连续修改多个 Round 后才编译
- 每个 Round 完成后立即编译+反思，避免错误叠加

---

## 预期结果

| Round | 预期减少 | 预期剩余 |
|-------|---------|---------|
| 2D | ~36 | ~300 |
| 2E | ~22 | ~278 |
| 2C | ~116 | ~162 |
| 2A | ~78 | ~84 |
| 2F | ~50 | ~34 |
| 2B | ~200 | ~0（或接近 0） |

**注意**：以上是独立计算，实际错误可能有重叠。按执行顺序逐轮验证。
