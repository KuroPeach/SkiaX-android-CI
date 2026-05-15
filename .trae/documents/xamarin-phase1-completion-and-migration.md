# Emuera.Xamarin Phase 1 收尾与 Phase 2 迁移计划

> ⚠️ **本文件已归档**：持久知识已迁移至知识库 `xemuera/porting-methodology.md` 和 `xemuera/architecture.md`。本文件可在 Round 3 完成后删除。
> 创建日期：2026-05-15
> 状态：待批准

## 当前状态

* **分支**：`feature/xamarin`（LazyLoading 仓库）

* **编译错误**：818 个（从 166→147→89→51→26→2→818，升级 net8.0-windows 后类型冲突全面暴露）

* **LazyLoading 主项目**：✅ 编译通过（0 错误）

* **项目文件数**：\~100 个（Platform/ + Drawing/ + Models/ + Views/ + Forms/ + Resources/ + Stubs/）

## 核心认知（本次会话关键洞见）

### 1. netstandard2.1 死路 → net8.0-windows

LazyLoading 内核大量使用 .NET 7+/8+ API（`Span.Sort`、`string.Create<TState>`、`ReadOnlySpan.Split`、`GeneratedRegex`、`SearchValues`），与 netstandard2.1 不兼容。**升级到 net8.0-windows 是唯一可行路径**。

### 2. UI 层不能直接用 LazyLoading 版本

LazyLoading 的 `UI/Game/` 深度耦合 `System.Drawing`（Font/Brush/Pen/Bitmap/Graphics），不能直接用于 Xamarin。**Platform 层的 XEmuera-1 Skia 版本是 UI 层的唯一来源**。

### 3. 类型冲突是核心难题

Platform 层（XEmuera-1）和 LazyLoading 内核有大量重复/冲突的类型：

| 冲突类型                      | Platform 层命名空间                | 内核命名空间                                           | 冲突性质                                 |
| ------------------------- | ----------------------------- | ------------------------------------------------ | ------------------------------------ |
| `ConfigCode` 枚举           | `MinorShift.Emuera`           | `MinorShift.Emuera.Runtime.Config`               | 重复定义，值不同                             |
| `MixedNum`                | `MinorShift.Emuera.GameView`  | `MinorShift.Emuera.Runtime.Utils.EvilMask.Utils` | 重复定义                                 |
| `FontStyle`               | `XEmuera.Drawing`             | `System.Drawing`                                 | 歧义（net8.0-windows 引入 System.Drawing） |
| `GraphicsUnit`            | `XEmuera.Drawing`             | `System.Drawing`                                 | 歧义                                   |
| `SolidBrush`              | `XEmuera.Drawing`             | `System.Drawing`                                 | 歧义                                   |
| `EmueraConsole`           | `MinorShift.Emuera.GameView`  | 内核 `Instraction.Child.cs` 期望更多方法                 | API 不完整                              |
| `DisplayLineAlignment`    | Platform 层未定义                 | 内核 `Process.SystemProc.cs` 引用                    | 缺失                                   |
| `AppContents`             | `MinorShift.Emuera.Content`   | 内核 `Process.State.cs` 引用                         | 命名空间不同                               |
| `HtmlManager`             | `MinorShift.Emuera.GameView`  | 内核 `Instraction.Child.cs` 引用                     | 命名空间不同                               |
| `ScriptPosition` nullable | 内核 `SafeArithmetic.cs` 传 null | 非 nullable 参数                                    | nullable 不匹配                         |
| `GlobalStatic.ctrlZ`      | Platform 层未定义                 | 内核引用                                             | 缺失属性                                 |

### 4. 内核期望的 EmueraConsole API 比 Platform 层更丰富

内核 `Instraction.Child.cs` 调用了 Platform 层 `EmueraConsole` 没有的方法：

* `PrintHTMLIsland()` / `ClearHTMLIsland()` — HTML Island 功能

* `AddBackgroundImage()` / `RemoveBackground()` / `ClearBackgroundImage()` — 背景图管理

* `TextBackgroundColor` — 文本背景色

* `PrintHtmlC()` — HTML 右对齐

* `PrintImg()` 6 参数重载 — 图片打印

* `printCustomBar()` 2 参数重载 — 自定义条

* `Window` 属性 — 窗口引用

* `PrintErrorButton()` 3 参数重载 — 错误按钮

***

## 执行计划

### Step 0：知识库持久化 + 提交基准版本（本轮执行）

**0.1 更新 xemuera 知识库**

更新以下条目以反映 net8.0-windows 决策和类型冲突认知：

* `porting-methodology.md`：添加 net8.0-windows 决策、类型冲突分类表

* `architecture.md`：更新项目结构（net8.0-windows TFM、Stubs/ 目录）

* `_index.md`：更新日期

新建条目：

* `type-conflicts.md`：类型冲突完整清单与解决方案

* `net8-migration.md`：net8.0-windows 迁移经验（GeneratedRegex/SearchValues 不需要排除、System.Drawing 歧义处理）

**0.2 更新 Emuera.Xamarin 项目文档**

* `TODO.md`：更新为 Phase 2 迁移计划

* `CHANGELOG.md`：记录 net8.0-windows 升级和当前状态

**0.3 提交基准版本**

```bash
git add Emuera.Xamarin/
git commit -m "feat(xamarin): Emuera.Xamarin 项目基准版本 (net8.0-windows, 818 errors)"
```

提交内容：

* csproj（net8.0-windows + 内核引用 + 排除项）

* Platform/ 层全部文件（从 XEmuera-1 复制 + 命名空间适配）

* Stubs/ 层全部文件

* Drawing/ + Models/ + Views/ + Forms/ + Resources/

* README.md + TODO.md + CHANGELOG.md

***

### Step 1：解决类型冲突（ConfigCode / MixedNum / FontStyle）

**1.1 ConfigCode 枚举冲突**

排除 Platform/Config/ConfigCode.cs，使用 LazyLoading 内核的 `MinorShift.Emuera.Runtime.Config.ConfigCode`。
需要修复 Platform 层中所有 `MinorShift.Emuera.ConfigCode` → `MinorShift.Emuera.Runtime.Config.ConfigCode` 的引用。

**1.2 MixedNum 类型冲突**

排除 Platform/Stubs/EvilMaskUtils.cs 中的 `MixedNum`，使用 LazyLoading 内核的版本。
需要修复 Platform 层中 `MinorShift.Emuera.GameView.MixedNum` → `MinorShift.Emuera.Runtime.Utils.EvilMask.Utils.MixedNum` 的引用。

**1.3 FontStyle/GraphicsUnit/SolidBrush 歧义**

在 Platform 层文件中添加 `using Font = XEmuera.Drawing.Font` 等别名（已完成 17 个文件）。
还需要添加 `using GraphicsUnit = XEmuera.Drawing.GraphicsUnit` 和 `using SolidBrush = XEmuera.Drawing.SolidBrush` 别名。

***

### Step 2：补全 EmueraConsole 缺失方法

在 Platform/GameView/EmueraConsole.cs 中添加内核期望的方法存根：

```csharp
// HTML Island
public void PrintHTMLIsland(string html) { }
public void ClearHTMLIsland() { }

// 背景图管理
public void AddBackgroundImage(string name) { }
public void RemoveBackground() { }
public void ClearBackgroundImage() { }

// 文本背景色
public SKColor TextBackgroundColor { get; set; }

// HTML 右对齐
public void PrintHtmlC(string html) { }

// PrintImg 6 参数重载
public void PrintImg(string name, int x, int y, int width, int height, int opacity) { }

// printCustomBar 2 参数重载
public void printCustomBar(string barName, int value) { }

// Window 属性
public MainWindow Window { get; set; }

// PrintErrorButton 3 参数重载
public void PrintErrorButton(string text, int value, string hint) { }
```

***

### Step 3：补全缺失类型和命名空间

**3.1 DisplayLineAlignment 枚举**

在 Stubs 中定义或找到 Platform 层中的定义。

**3.2 AppContents 命名空间**

Platform 层的 `AppContents` 在 `MinorShift.Emuera.Content` 命名空间，内核期望的命名空间可能不同。添加 using 别名。

**3.3 HtmlManager 命名空间**

Platform 层的 `HtmlManager` 在 `MinorShift.Emuera.GameView`，内核引用的命名空间可能不同。添加 using 别名。

**3.4 GlobalStatic.ctrlZ**

在 Platform/GlobalStatic.cs 中添加 `ctrlZ` 属性。

**3.5 ScriptPosition nullable**

修复 `SafeArithmetic.cs` 中的 nullable 参数问题（添加 `!` 操作符或修改参数签名）。

**3.6 InputType 枚举**

Platform 层引用 `MinorShift.Emuera.GameProc.InputType`，需要映射到 LazyLoading 的命名空间。

***

### Step 4：迭代编译直到错误收敛

每轮编译后：

1. 分析错误分布
2. 按优先级修复（类型冲突 > 缺失方法 > 命名空间 > nullable）
3. 验证 LazyLoading 主项目未受影响
4. 更新 WORKLOG

***

### Step 5：提交 Phase 1 完成版本

当编译错误收敛到 0（或接近 0）时：

1. 更新 TODO.md 标记 Phase 1 完成
2. 更新 CHANGELOG.md
3. 提交：`git commit -m "feat(xamarin): Phase 1 编译通过"`

***

## 后续 Phase（Phase 2+，不在本轮执行范围）

| Phase   | 目标                                          | 前置条件         |
| ------- | ------------------------------------------- | ------------ |
| Phase 2 | 运行时适配（入口点、GlobalStatic 初始化、Config 加载）       | Phase 1 编译通过 |
| Phase 3 | Skia 渲染集成（EmueraConsole.OnPaint → SKCanvas） | Phase 2      |
| Phase 4 | Xamarin.Forms UI 集成                         | Phase 3      |
| Phase 5 | 平台特定功能（音频、IO 路径、SQL）                        | Phase 4      |

