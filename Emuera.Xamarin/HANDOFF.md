# Handoff: Phase 8A 可行性验证完成

## 会话目标

继续推进移植任务，包括一至两次完整提交。随后转向 Phase 8A 可行性验证。

## 已完成

- [x] Slice 5A-D1：IO 跨平台适配（FileUtils + WorkingDir）
- [x] Slice 5C-D1：SqlManager 资源清理 + FormClosing 退出流程
- [x] G6：GDI 注释残留清理 + 删除 5 组死对话框文件（-9402 行）
- [x] 项目定位修正：桌面端只是验证手段，APK 是唯一目标产物
- [x] XEmuera-1 遗产勘测：Android 基础设施完整记录
- [x] Phase 8A 可行性验证：三大阻塞问题发现
- [x] 知识库更新：[android-migration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-migration.md)
- [x] TODO.md 重构：以 APK 为主线，Phase 8 切片细化
- [x] 4 次 Git 提交

## 未完成

- [ ] 切片 8A-P0：System.Drawing 值类型垫片（Color/Rectangle/Point/Size）— 前置条件
- [ ] 切片 8A-P1：Runtime/ WinForms 依赖清理（7 个文件）— 前置条件
- [ ] 切片 8A：创建 Core 项目（net8.0）
- [ ] 切片 8B：创建 Desktop 项目（net8.0-windows）
- [ ] 切片 8C：创建 Android 项目（net8.0-android）
- [ ] 切片 8D：Android 功能补全

## 关键决策

1. **项目定位修正**：LazyLoading 原版桌面端已完备，Emuera.Xamarin 的唯一目标是 APK。桌面端仅用于功能验证。
2. **方案 B（项目拆分）**：Core(net8.0) + Desktop(net8.0-windows) + Android(net8.0-android)。不能用 netstandard2.1 因为 Runtime/ 依赖 .NET 8 API。
3. **XEmuera-1 遗产不能直接复制**：旧 Xamarin 工具链（MonoAndroid）不能引用 net8.0 库，必须用 .NET 8 Android 工作负载重写 Android 层。
4. **System.Drawing 值类型需要自建垫片**：Color/Rectangle/Point/Size 在 net8.0（非 -windows）下不可用，20+ 个文件受影响。方案：在 XEmuera.Drawing 下创建兼容值类型 + csproj Using Alias 全局替换。

## 三大阻塞问题（勘测发现）

1. **Runtime/ 依赖 .NET 8 API**：GeneratedRegex(5文件)/SearchValues(2文件)/Span.Sort/string.Create — Core 必须用 net8.0
2. **旧 Xamarin Android 不能引用 net8.0 库**：必须用 .NET 8 Android 工作负载（net8.0-android）
3. **System.Drawing 值类型不可用**：需自建 Color/Rectangle/Point/Size 垫片（~200-300 行）

## 阻塞项

- 无代码阻塞。下一步 8A-P0 可立即开始。

## 下一步建议

1. **切片 8A-P0**：创建 XEmuera.Drawing.Color/Rectangle/Point/Size 值类型垫片 + csproj Alias + 编译验证
2. **切片 8A-P1**：清理 Runtime/ 中 7 个文件的 WinForms 依赖
3. **切片 8A**：创建 Core 项目（net8.0）

## 推荐技能

- `powershell-git`：终端命令
- `knowledge-builder`：8A-P0 完成后更新知识库
- `erabasic`：如需确认 Runtime/ 中 ERB 相关代码

## 关键文件

### 本次修改的文件

| 文件 | 变更 |
|------|------|
| `Utils.cs` | 新增 FileUtils.GetDirectories() |
| `Platform/Content/AppContents.cs` | File.Exists → FileUtils.Exists |
| `Platform/Config/Config.cs` | Directory.GetDirectories → FileUtils.GetDirectories |
| `Models/GameItemModel.cs` | Directory.GetDirectories → FileUtils.GetDirectories |
| `Platform/Program.cs` | using XEmuera + WorkingDir 委托 |
| `Platform/GlobalStatic.cs` | Reset() 添加 Sound/Bgm + SqlManager.CloseAll() + SpriteDisposeAll |
| `DesktopHost.cs` | FormClosing 事件 |
| `Platform/GameView/EmueraConsole.cs` | 旧枚举清理 + getGraphics/stTimer 注释块清理 |
| `Platform/Content/GraphicsImage.cs` | LockGraphics/UnlockGraphics 注释块清理 |
| 5 组死对话框文件 | 删除（15 个文件） |

### 知识库更新

| 文件 | 变更 |
|------|------|
| [android-migration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-migration.md) | 新建：三大阻塞问题 + 修正架构决策 + XEmuera 遗产继承表 |
| [porting-methodology.md](file:///d:/emuera/shared-trae/knowledge/xemuera/porting-methodology.md) | 核心洞见修正：APK 是唯一目标 |

### 关键参考文件（下次会话需要）

| 文件 | 用途 |
|------|------|
| `Emuera.Xamarin.csproj` | 当前 csproj，8A-P0 需要添加 Using Alias |
| `Drawing/ColorExtensions.cs` | 现有 Color 转换，8A-P0 创建 Color 垫片时参考 |
| `Drawing/DrawBitmapUtils.cs` | 使用 Rectangle/Color，8A-P0 验证目标 |
| `Platform/Stubs/SkiaPfc.cs` | 使用 System.Drawing.FontFamily，8A-P0 需修复 |
| `Platform/Stubs/InstalledFontCollection.cs` | 同上 |
| XEmuera-1 `XEmuera/Drawing/` | XEmuera 的 Skia 绘图实现，8A-P0 参考 |
