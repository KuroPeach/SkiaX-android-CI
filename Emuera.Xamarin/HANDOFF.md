# Handoff: 8D 完成 + 5B-D 验证，准备 4A-Xamarin 框架开发

## 会话目标

执行切片 8D（Android 功能补全）和 5B-D（Desktop 音频验证），使 APK 达到可运行分界线。

## 已完成

- [x] Slice 8D-1：Android IPathProvider
  - `AndroidPathProvider.cs`：实现 `IPathProvider`，使用 `ExternalStorageDirectory/emuera/` 作为 ExeDir
  - `Context.CacheDir` 作为 TempDir，`PackageManager.GetPackageInfo` 获取 ExeName
  - 支持 `gamePath` 参数，游戏列表选择后传入子目录路径
- [x] Slice 8D-2+8D-3：触摸事件转发 + 文本输入
  - `AndroidMainWindow.cs`：继承 `MainWindow`，包含 SKCanvasView + Entry
  - SKCanvasView 触摸事件 → EmueraConsole 鼠标事件（Pressed/Moved/Released）
  - 长按模拟右键（500ms LongPressTimer）
  - 垂直拖动 → ScrollBar 滚动
  - Entry.Completed → PressEnterKey
  - extern alias `skviews` 解决 Core stub 与 SkiaSharp.Views.Forms NuGet 的类型冲突
- [x] Slice 8D-4：游戏列表 + 侧边菜单
  - `AndroidGameItemModel.cs`：扫描 ExeDir 下含 ERB/erb 子目录的文件夹
  - `AndroidGameListPage.cs`：ListView 显示游戏列表，下拉刷新，点击启动
  - `AndroidMainMenuPage.cs`：FlyoutPage 侧边菜单（Reboot/GotoTitle/Settings）
  - `AndroidApp.cs`：FlyoutPage 组合菜单+游戏列表+游戏页面
  - `AndroidHost.StartGame(gamePath)`：延迟初始化引擎，游戏选择后启动
- [x] Slice 8D-5：配置页面
  - `AndroidConfigPage.cs`：ListView 显示 ConfigData 所有配置项
  - bool 配置点击切换，string/int 配置点击弹出编辑页
  - Save & Close 按钮保存配置
  - MainWindow.ShowConfigDialog() override → 导航到配置页
- [x] Slice 8D-6：APK 打包验证
  - `dotnet publish -c Debug -f net8.0-android` 成功
  - 生成 `com.xemuera.app-Signed.apk`（~30.5 MB）
- [x] Slice 5B-D：Desktop 音频验证
  - NAudio NuGet 已集成（NAudio 2.2.1 + NAudio.Vorbis 1.5.0 + NVorbis 0.10.5）
  - NAudioSound.cs 已存在，继承 Sound 虚方法基类
  - DesktopHost.InitEmuera() 已初始化 16 通道 Sound + 1 BGM 通道
  - 编译验证：0 错误

## 未完成

- [ ] 切片 4A-Xamarin：SKCanvasView + 触摸 + 输入（回归框架开发）
- [ ] 切片 5B-E：音频 ERB 指令补全
- [ ] P2 优先级任务（7C-P1 字体缩放 / 7E 本地化 / 7F 日志查看器）

## 关键决策

1. **extern alias 解决类型冲突**（新增）：Core 的 `SkiaSharp.Views.Forms` stub 与 NuGet 包同名类型冲突，使用 `Aliases="skviews"` + `extern alias skviews;` + using alias 解决
2. **延迟引擎初始化**（新增）：`AndroidHost.InitEmuera()` 只初始化 PlatformInterop + Audio + MainWindow，引擎初始化推迟到 `StartGame(gamePath)` 调用时
3. **游戏列表驱动路径**（新增）：`AndroidPathProvider` 接受 `gamePath` 参数，游戏列表选择后传入子目录路径作为 ExeDir
4. **长按模拟右键**（新增）：500ms LongPressTimer，长按后 `MouseReleased` 中 `IsLongPressed → CoreSKMouseButton.Right`
5. **代码优先 Xamarin.Forms 页面**（新增）：不使用 XAML，所有页面（GameListPage/MainMenuPage/ConfigPage）纯 C# 代码构建

## 编译验证

| 项目 | 错误 | 警告 |
|------|------|------|
| Emuera.Xamarin.Core | 0 | 52 |
| Emuera.Xamarin.Desktop | 0 | 56 |
| Emuera.Xamarin.Android | 0 | 4 |

## APK 打包验证

| 指标 | 结果 |
|------|------|
| dotnet publish | ✅ 成功 |
| Signed APK | ✅ com.xemuera.app-Signed.apk (~30.5 MB) |

## 阻塞项

- 无代码阻塞。4A-Xamarin 可立即开始。

## 下一步建议

1. **切片 4A-X3**：SKCanvasView 渲染管线连接（AndroidMainWindow 已有 OnPaintSurface，需验证 OnPaint 完整渲染）
2. **切片 4A-X4**：ScrollBar + QuickButton 完善
3. **切片 5B-E**：音频 ERB 指令补全（PLAY/STOP 等）
4. **真机测试**：安装 APK 到 Android 设备，验证游戏列表 + 触摸 + 输入

## 推荐技能

- `powershell-git`：终端命令
- `knowledge-builder`：4A-Xamarin 完成后更新知识库
- `erabasic`：如需确认 Runtime/ 中 ERB 相关代码

## 关键文件

### 项目结构

```
Emuera.Xamarin.sln
├── Emuera.Xamarin/          (net8.0-windows, WinExe)     — 原项目
├── Emuera.Xamarin.Core/     (net8.0;net8.0-windows, Library) — 共享代码
├── Emuera.Xamarin.Desktop/  (net8.0-windows, WinExe)     — 桌面验证
└── Emuera.Xamarin.Android/  (net8.0-android, Exe)        — APK 产出
    ├── MainActivity.cs          Activity + 沉浸式模式
    ├── AndroidHost.cs           PlatformInterop + Audio + 延迟引擎初始化
    ├── AndroidMainWindow.cs     SKCanvasView + 触摸 + Entry + MainWindow override
    ├── AndroidSound.cs          MediaPlayer 音频
    ├── AndroidPathProvider.cs   IPathProvider（ExternalStorage + CacheDir）
    ├── AndroidApp.cs            FlyoutPage（菜单+游戏列表+游戏页面）
    ├── AndroidGameItemModel.cs  游戏目录扫描
    ├── AndroidGameListPage.cs   游戏选择列表
    ├── AndroidMainMenuPage.cs   侧边菜单
    └── AndroidConfigPage.cs     配置编辑页
```

### 关键参考文件（下次会话需要）

| 文件 | 用途 |
|------|------|
| `Emuera.Xamarin.Android/AndroidMainWindow.cs` | SKCanvasView + 触摸 + Entry，4A-X3 需验证渲染 |
| `Emuera.Xamarin.Android/AndroidHost.cs` | 延迟引擎初始化，4A-X3 需扩展 |
| `Emuera.Xamarin.Android/AndroidPathProvider.cs` | Android 路径系统 |
| `Emuera.Xamarin.Android/Emuera.Xamarin.Android.csproj` | Android 项目配置，extern alias |
| `Emuera.Xamarin.Core/Stubs/SoundStub.cs` | Sound 虚方法基类 |
| `Emuera.Xamarin/DesktopHost.cs` | Desktop 委托初始化，NAudio 音频 |
| `Emuera.Xamarin.Desktop/NAudioSound.cs` | Desktop NAudio 实现 |
| XEmuera-1 `XEmuera/Emuera/Forms/MainWindow.xaml.cs` | XEmuera 触摸+渲染参考 |

### 知识库

| 条目 | 核心洞见 |
|------|---------|
| [android-migration.md](file:///d:/emuera/shared-trae/knowledge/xemuera/android-migration.md) | 8C+5B-A+8D 已完成；extern alias 解决类型冲突；延迟引擎初始化 |
| [audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md) | SoundStub 虚方法继承：NAudioSound:Sound(Desktop) + AndroidSound:Sound(Android) |
