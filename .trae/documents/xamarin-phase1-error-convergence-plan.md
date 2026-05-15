# Emuera.Xamarin Phase 1 编译错误收敛计划

> ⚠️ **本文件已归档**：持久知识已迁移至知识库 `xemuera/type-conflicts.md` 和 `xemuera/net8-migration.md`。本文件可在 Round 3 完成后删除。
> **创建日期**：2026-05-15
> **当前状态**：818 → 336 个错误（Round 1 完成）
> **目标**：336 → 0 个错误

---

## 本轮经验教训（认知持久化）

### 教训 1：输出文件验证优先

**问题**：多次将 `dotnet build` 输出重定向到文件后，文件内容为空或仅 1 行（"有可用的工作负载更新"），导致 search agent 空转。

**根因**：PowerShell 5.1 中 `Out-File` 与 `dotnet build` 的 stderr 重定向组合有编码/缓冲问题。`2>&1 | Out-File` 在某些情况下会丢失输出。

**正确做法**：
1. **优先用终端直接输出** + `Select-String` 过滤，不写文件
2. 如需写文件，用 `$env:DOTNET_CLI_UI_LANGUAGE='en'` 确保英文输出
3. 调用 search agent 前，先用 `Read` 工具验证文件内容非空
4. 如果文件为空，改用终端直接输出 + `Measure-Object` / `Group-Object` 统计

### 教训 2：命名空间迁移是反模式

**问题**：将 Platform 类型的命名空间从 `GameView` 移到 `UI.Game` 以匹配内核期望，导致级联编译错误（818 → 828）。

**根因**：C# 命名空间是类型的"地址"，移动命名空间等于搬家，所有引用旧地址的代码都会断裂。

**正确做法**：用 csproj 全局 `<Using Include>` 做命名空间转发，不移动类型的命名空间。

### 教训 3：全局 using 有冲突限制

**问题**：全局 `<Using Include="MinorShift.Emuera.Runtime.Config" />` 导致 `ConfigData` 类与 `MinorShift.Emuera.Config` 中的同名类冲突（CS0104）。

**正确做法**：冲突命名空间用局部 `using` 别名处理，不加入全局 using。

### 教训 4：csproj 排除项需审计

**问题**：`Creator.Method.cs` 被排除（注释说"引用 PluginSystem"），但实际不引用，恢复后消除 ~300 个错误。

**正确做法**：每次修改排除项时，验证排除原因是否仍然成立。

---

## 当前 336 个错误精确分类

### 按错误代码

| 错误代码 | 含义 | 数量 | 占比 |
|---------|------|------|------|
| CS0103 | 名称不存在 | 180 | 31% |
| CS1061 | 方法不存在 | 158 | 27% |
| CS0117 | 类型不包含成员 | 116 | 20% |
| CS0021 | 表达式树不可包含动态操作 | 50 | 9% |
| CS1503 | 参数类型不匹配 | 50 | 9% |
| CS0246 | 类型未找到 | 18 | 3% |
| CS1729 | 构造函数无匹配重载 | 18 | 3% |
| CS0037 | null 不可赋值 | 14 | 2% |
| CS0122 | 不可访问（private） | 14 | 2% |
| 其他 | — | 38 | 7% |

### 按文件（Top 10）

| 文件 | 错误数 | 主要问题 |
|------|--------|---------|
| Creator.Method.cs | 200 | Bgm/Sound 存根方法不完整、dynamic 操作 |
| Instraction.Child.cs | 82 | ConfigCode 新增枚举成员、FontFactory |
| ConfigValuePopup.xaml.cs | 78 | XAML InitializeComponent |
| ConfigData.cs | 66 | EraStreamReader、ConfigCode 新增成员 |
| Config.cs | 36 | FontFactory、ConfigCode 新增成员 |
| ConfigModel.cs | 30 | ConfigCode 新增枚举成员 |
| EmueraConsole.cs | 22 | IOperandTerm、缺失方法 |
| SafeArithmetic.cs | 14 | ScriptPosition? null |
| MainMenuPage.xaml.cs | 12 | XAML InitializeComponent |
| GameItemPage.xaml.cs | 10 | XAML InitializeComponent |

### 按根因分类（6 大类）

| 根因 | 错误数 | 修复策略 |
|------|--------|---------|
| **F1: XAML 代码生成** | ~78 | InitializeComponent/ConfigEntry/ConfigListView 等 XAML 生成代码缺失 |
| **F2: Creator.Method.cs 存根不完整** | ~200 | Bgm/Sound/EmueraConsole 存根缺少方法 |
| **F3: ConfigCode 新增枚举成员** | ~116 | Platform 层引用了 XEmuera 特有配置项 |
| **F4: 内核类型缺失 using** | ~36 | EraStreamReader/FontFactory/IOperandTerm |
| **F5: Nullable 不匹配** | ~22 | ScriptPosition? → ScriptPosition |
| **F6: dynamic 与表达式树** | ~50 | GlobalStatic.Pfc 是 dynamic，不可用于表达式树 |

---

## 修复计划（细粒度任务）

### Round 2A：XAML 代码生成问题（F1，~78 个错误）

**根因**：XAML 文件的代码生成（`InitializeComponent`、控件类型等）需要 Xamarin.Forms 构建工具链。当前 csproj 使用 `<EnableDefaultCompileItems>false</EnableDefaultCompileItems>`，可能影响了 XAML 代码生成。

**可行性评估**：
- XAML 代码生成是 Xamarin.Forms 构建管线的一部分
- `InitializeComponent` 由 XAML 编译器自动生成
- 如果 XAML 工具链不工作，这些错误无法通过手动修复解决
- **可能需要调整 csproj 配置**，确保 XAML 代码生成正确

**任务**：
1. [ ] 检查 XAML 文件的 `DependentUpon` 和 `Generator` 配置
2. [ ] 确认 `.xaml.cs` 文件与 `.xaml` 文件的关联正确
3. [ ] 如需修改 csproj，添加正确的 `<Compile>` 和 `<EmbeddedResource>` 配置
4. [ ] 编译验证

### Round 2B：Creator.Method.cs 存根补全（F2，~200 个错误）

**根因**：`Creator.Method.cs` 引用了 `Bgm`、`Sound`、`EmueraConsole` 的方法，这些类型的 Platform 存根不完整。

**可行性评估**：
- 所有缺失方法都可以添加空壳存根
- 需要从内核源码获取精确签名
- `dynamic` 类型问题（F6）需要特殊处理

**任务**：
1. [ ] 提取 Creator.Method.cs 中所有 CS1061 错误的缺失方法名
2. [ ] 查找内核源码中的精确签名
3. [ ] 补全 Bgm 存根（pause/resume/stop/SetPreservePitch/getVolume/setVolume/GetTotalTime/GetCurrentTime/getSpeed/setSpeed/play/isPlaying）
4. [ ] 补全 Sound 存根（类似 Bgm 的方法）
5. [ ] 补全 EmueraConsole 存根（ExistsImageLayer 等）
6. [ ] 编译验证

### Round 2C：ConfigCode 枚举成员补全（F3，~116 个错误）

**根因**：Platform 层的 ConfigModel.cs 和 ConfigValuePopup.xaml.cs 引用了 XEmuera 特有的 ConfigCode 枚举成员（如 PanSpeed、AdaptiveFont、FontScale 等），这些成员在内核的 ConfigCode 中不存在。

**可行性评估**：
- 方案 A：在内核 ConfigCode 枚举中添加 XEmuera 特有成员（会破坏主项目编译）
- 方案 B：在 Platform 层创建 ConfigCode 扩展枚举，用 partial class 或扩展方法
- 方案 C：排除引用 XEmuera 特有 ConfigCode 的 Platform 文件
- **推荐方案 C**：排除 ConfigModel.cs 和 ConfigValuePopup.xaml.cs，因为它们是 XEmuera 特有的配置 UI，不影响内核编译

**任务**：
1. [ ] 识别引用 XEmuera 特有 ConfigCode 成员的文件
2. [ ] 在 csproj 中排除这些文件（`<Compile Remove>`）
3. [ ] 编译验证

### Round 2D：内核类型缺失 using（F4，~36 个错误）

**根因**：Platform 层文件找不到 `EraStreamReader`、`FontFactory`、`IOperandTerm` 等内核类型。

**可行性评估**：
- EraStreamReader：内核已有，需添加 using
- FontFactory：内核已有，可能因级联错误不可见
- IOperandTerm：内核已有，需添加 using

**任务**：
1. [ ] 在 ConfigData.cs 和 KeyMacro.cs 中添加 `using MinorShift.Emuera.Runtime.Utils;`（EraStreamReader）
2. [ ] 在 EmueraConsole.cs 中添加 `using MinorShift.Emuera.Runtime.Script.Statements.Expression;`（IOperandTerm）
3. [ ] FontFactory 问题：验证是否因级联错误导致不可见，修复上游后自动解决
4. [ ] 编译验证

### Round 2E：Nullable 不匹配（F5，~22 个错误）

**根因**：内核代码中 `ScriptPosition?` 赋值给 `ScriptPosition`（非 nullable）。

**可行性评估**：
- 方案 A：修改内核文件（会破坏主项目编译）
- 方案 B：在 csproj 中设置 `<Nullable>disable</Nullable>`（过于激进）
- 方案 C：用 `<NoWarn>` 抑制特定 nullable 警告作为错误
- **推荐方案 C**：添加 CS8601/CS8602/CS8603/CS8604/CS8618/CS8625/CS0037/CS0266 到 NoWarn

**任务**：
1. [ ] 在 csproj `<NoWarn>` 中添加 nullable 相关抑制
2. [ ] 编译验证

### Round 2F：dynamic 与表达式树（F6，~50 个错误）

**根因**：`GlobalStatic.Pfc` 声明为 `dynamic`，但 `Creator.Method.cs` 中的表达式树（lambda → Expression<T>）不能包含 dynamic 操作。

**可行性评估**：
- 方案 A：将 Pfc 改为具体类型（需要 Preload 类定义）
- 方案 B：将 Pfc 改为 `object` 并在使用处强制转换
- 方案 C：排除 Creator.Method.cs 中引用 Pfc 的方法
- **推荐方案 A**：创建 Preload 存根类，将 Pfc 类型从 dynamic 改为 Preload

**任务**：
1. [ ] 检查内核 Preload 类的接口
2. [ ] 创建 Platform/Stubs/Preload.cs 存根
3. [ ] 将 GlobalStatic.Pfc 类型从 dynamic 改为 Preload
4. [ ] 编译验证

---

## 执行顺序

```
Round 2A (XAML) → Round 2C (ConfigCode排除) → Round 2D (using) → Round 2E (Nullable) → Round 2F (dynamic) → Round 2B (存根补全)
```

**优先级理由**：
1. XAML 问题可能需要 csproj 配置变更，先解决
2. ConfigCode 排除可以快速消除 ~116 个错误
3. using 修复简单快速
4. Nullable 抑制是配置变更
5. dynamic 问题需要创建存根类
6. Creator.Method.cs 存根补全工作量最大，放最后

## 验证与反思步骤

每个 Round 完成后：
1. **编译验证**：运行 `dotnet build`，记录错误数变化
2. **认知整理**：更新 type-conflicts.md 中的修复进度表
3. **反思**：检查是否有新引入的问题、是否有更优方案
4. **TODO 更新**：更新 TODO.md 中的任务状态

## 预期结果

| Round | 预期减少 | 预期剩余 |
|-------|---------|---------|
| 2A | ~78 | ~258 |
| 2B | ~200 | ~58 |
| 2C | ~116 | ~220 |
| 2D | ~36 | ~300 |
| 2E | ~22 | ~314 |
| 2F | ~50 | ~286 |

**注意**：以上是独立计算，实际错误可能有重叠。按执行顺序逐轮验证。
