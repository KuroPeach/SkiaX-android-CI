# Handoff: Phase 9A+9B 完成，准备真机测试

## 会话目标

修复 P0 阻塞项（KeyNotFoundException + eratw-chs 闪退）+ 实现 P1 UI 功能（悬浮工具栏 + 输入条 + 沉浸式全屏）

## 已完成

- [x] Phase 9A：Preload 缓存 miss 修复 + eratw-chs 闪退诊断
  - 添加 `Preload.TryGetFileLines()` 方法
  - `EraStreamReader.OpenOnCache` 缓存 miss 降级到磁盘读取
  - 修复 `Lang.LoadLanguageFiles()` + `Lang.SetLanguage()` 未调用
  - 修复 `engineInitialized = true` 提前设置（移到 Task 完成后）
  - 替换所有 `Application.DoEvents()` 为 `PlatformInterop.DoEvents()`
  - 修复 `console.Initialize()` fire-and-forget 异常吞没
- [x] Phase 9B：悬浮工具栏 + 输入条修复 + 沉浸式全屏
  - 悬浮工具栏：☰ 菜单 + ⌨ 编辑 + A+/A- 缩放按钮（InputTransparent 叠加）
  - 输入条：`FillAndExpand` + 发送按钮 ▶ + `Completed` 事件
  - 沉浸式全屏：`mainLayout` 黑色背景
  - 缩放功能：`AdjustFontScale()` 通过 `ConfigData` + `Config.SetConfig` 运行时修改
- [x] 对比调研 m-emuera 平台实现
  - m-emuera 用空 stub 替代 DoEvents（我们用 PlatformInterop 更灵活）
  - m-emuera 用 ConsoleStateMachine + AsyncInputProvider + Task.Delay（长期目标）
  - m-emuera 也没调用 Lang（说明 Lang 缺失不是闪退主因）
  - m-emuera 有完整 SQLite 支持（我们 NuGet 已引用）
- [x] 更新知识库：[android-engine-init.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-engine-init.md)
  - 新增 4 个知识条目 + 5 个反模式 + 三方对比表（XEmuera vs m-emuera vs LazyLoading）

## 未完成

- [ ] 真机测试：安装 APK 到 Android 设备验证渲染/触摸/输入/音频
- [ ] 7C-Phase2：动态缩放（运行时，高难度 — ReflowDisplayLines）
- [ ] 7E：本地化系统（LanguagePage + CultureInfo 联动）
- [ ] 7F：解释器日志查看器（TxtFilePage + 侧边菜单入口）
- [ ] 7G：游戏列表+侧边菜单完善（GameUtils 游戏管理）
- [ ] 5A-5/5A-6：IPathProvider iOS 版本 + Android 资源解压
- [ ] 清理调试日志 + 修复编译警告

## 编译验证

| 项目 | 错误 | 警告 |
|------|------|------|
| Emuera.Xamarin.Core | 0 | 0 |
| Emuera.Xamarin.Desktop | 0 | 4 |
| Emuera.Xamarin.Android | 0 | 8 |

## 阻塞项

- 无代码阻塞。真机测试需要 Android 设备。

## 下一步建议

1. **真机测试**：安装 APK 到 Android 设备，验证游戏画面渲染 / 触摸交互 / 文本输入 / 音频播放
2. **7C-P2**：动态缩放（高难度，需 ReflowDisplayLines）
3. **7E**：本地化系统（LanguagePage + CultureInfo 联动）
4. **7F**：解释器日志查看器（TxtFilePage + 侧边菜单入口）

## 关键文件

### 本次修改

| 文件 | 修改内容 |
|------|---------|
| `Emuera/Runtime/Utils/Preload.cs` | 添加 `TryGetFileLines` 方法 |
| `Emuera/Runtime/Utils/EraStreamReader.cs` | `OpenOnCache` 缓存 miss 降级到磁盘读取 |
| `Emuera/UI/Game/EmueraConsole.cs` | `Application.DoEvents` → `PlatformInterop.DoEvents`（3 处） |
| `Emuera/UI/Game/EmueraConsole.Print.cs` | `Application.DoEvents` → `PlatformInterop.DoEvents`（1 处） |
| `Emuera/UI/Framework/Forms/MainWindow.cs` | `Application.DoEvents` → `PlatformInterop.DoEvents`（1 处） |
| `Emuera.Xamarin/Platform/Program.cs` | 添加 `Lang.LoadLanguageFiles()` + `Lang.SetLanguage()` |
| `Emuera.Xamarin.Android/AndroidHost.cs` | engineInitialized 移到 Task 完成后 + 异常捕获改进 |
| `Emuera.Xamarin.Android/AndroidMainWindow.cs` | 悬浮工具栏 + 发送按钮 + 缩放功能 + 黑色背景 |

### 项目结构

```
Emuera.Xamarin.sln
├── Emuera.Xamarin/          (net8.0-windows, WinExe)     — 原项目
├── Emuera.Xamarin.Core/     (net8.0;net8.0-windows, Library) — 共享代码
├── Emuera.Xamarin.Desktop/  (net8.0-windows, WinExe)     — 桌面验证
└── Emuera.Xamarin.Android/  (net8.0-android, Exe)        — APK 产出
    ├── MainActivity.cs          Activity + 沉浸式模式
    ├── AndroidHost.cs           PlatformInterop + Audio + 延迟引擎初始化
    ├── AndroidMainWindow.cs     ContentPage + SKCanvasView + 触摸 + Entry + QuickButton + 悬浮工具栏
    ├── AndroidSound.cs          MediaPlayer 音频
    ├── AndroidPathProvider.cs   IPathProvider（ExternalStorage + CacheDir）
    ├── AndroidApp.cs            FlyoutPage（菜单+游戏列表+游戏页面）
    ├── AndroidGameItemModel.cs  游戏目录扫描
    ├── AndroidGameListPage.cs   游戏选择列表
    ├── AndroidMainMenuPage.cs   侧边菜单
    └── AndroidConfigPage.cs     配置编辑页（含 FontScale float 编辑）
```
