# Handoff: Xamarin Android 兼容性修复 + UI 功能补全

## 会话目标

在 LazyLoading Xamarin 项目中修复 Android 兼容性问题、补全 UI 功能、完善动态缩放和字体加载。

## 已完成

### Phase 9J (v0.32.0)
- [x] GETTEXTBOX 修复 — ChangeTextBox 同步 TextBox.Text（原"user32.dll"为误报）
- [x] WebP 图片回退 — PlatformInterop 图片解码回退委托 + Android BitmapFactory 实现
- [x] CompatiErrorLine 条件启用 — 改为 `Program.DebugMode || config值`，清理调试日志
- [x] AndroidLanguagePage — 语言选择（日/中/英）+ 菜单入口
- [x] AndroidLogPage — 解释器日志查看器 + 菜单入口

### Phase 9K (v0.33.0)
- [x] GSAVE 指令 Android 实现 — `SKBitmap.Encode(SKEncodedImageFormat.Png)`
- [x] ShowQuestion AlertDialog — ManualResetEvent 同步等待
- [x] 跨平台编译保护 — WinmmTimer/GraphicsImage/ImgUtils/EmueraConsole/ConsoleStyledString `#if WINDOWS`

### Phase 9L (v0.34.0)
- [x] PrintHtml 存根修复 — 返回空数组（实际 HTMLPRINT 走 EmueraConsole.PrintHtml）
- [x] 运行时崩溃保护 — catch(Exception)/Process.Start/WorkingSet64/useGDI
- [x] 7C-P2 动态缩放完善 — 方向变化 reflow + ToLogicalTouchPoint 触摸坐标转换
- [x] FontModel 字体扫描修复 — font/ + fonts/ 目录 + .ttc 格式 + 别名注册
- [x] Save Log 菜单 — 手动保存日志带时间戳（yyyyMMdd-HHmmss.log）
- [x] ServerGC + android:largeHeap 启用

### APK 构建验证
- [x] Debug APK 170.7MB 构建成功（0 错误）
- [x] Release APK 构建脚本已存在（`发布-Android.bat`）

## 未完成

- [ ] 真机测试 — 需要 ADB 安装 APK 到模拟器/真机
- [ ] 动图渲染验证 — GIF/WebP 在 Android 上是否正确播放
- [ ] 内存占用对比测试
- [ ] AndroidSound 变速 — setSpeed/getSpeed/SetPreservePitch 为 stub，需 ExoPlayer

## 关键决策

- **SkiaSharp 保持 2.88.8**：升级到 3.x 的成本远大于收益。`SkiaSharp.Views.Forms` 在 3.x 中被移除（替换为 MAUI 专用），Android 项目依赖此包的 `SKCanvasView`/`SKTouchEventArgs`，升级需自建渲染器或迁移 MAUI。WebP 问题通过 BitmapFactory 回退解决。
- **GETTEXTBOX 不依赖 user32.dll**：HANDOFF/TODO 中"GETTEXTBOX user32.dll"为误报，真正问题是 ChangeTextBox 未同步 TextBox.Text
- **WebP 回退策略**：SkiaSharp 解码失败时通过 PlatformInterop 委托回退到 Android BitmapFactory，通过 PNG 中间格式转换确保像素格式正确
- **FontModel 双轨管线**：FontModel 和 FontFactory 各自维护字体注册，FontModel 扫描 font/+fonts/，FontFactory 扫描 font/+FONT/+fonts/
- **ServerGC**：启用多线程并行 GC 提高吞吐量，配合 android:largeHeap 允许更多内存分配

## 阻塞项

- ADB 不可用，无法进行真机安装测试

## 下一步建议

1. **10A-P1：ADB 真机安装测试** — 安装 Release APK，验证 LazyLoading/LRU/字体/缩放/对话框/日志保存
2. **10A-P2：动图渲染验证** — 使用含 GIF/WebP 的游戏资源测试
3. **10A-P3：内存占用对比** — 对比 SharedBitmapCache 效果
4. **10B-P1：AndroidSound 变速** — ExoPlayer 替代 MediaPlayer
5. **10B-P2：APK 体积优化** — 尝试 `AndroidLinkMode=SdkOnly`（需测试反射依赖）
6. **10B-P3：Release 签名构建** — 配置正式签名密钥

## 推荐技能

- **erabasic** — 涉及 .cs 中 ERABASIC 解析/运行时逻辑
- **powershell-git** — APK 构建、ADB 部署、Git 操作
- **knowledge-builder** — 真机测试发现新洞见时持久化

## 关键文件

- [PlatformInterop.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/Platform/Stubs/PlatformInterop.cs) — 图片解码回退委托
- [AndroidHost.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidHost.cs) — BitmapFactory 回退 + ShowQuestion + Console 属性
- [AndroidMainWindow.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidMainWindow.cs) — 方向变化 reflow + ToLogicalTouchPoint
- [AndroidApp.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidApp.cs) — Save Log + Language/Log 页面导航
- [AndroidLanguagePage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidLanguagePage.cs) — 语言选择页面
- [AndroidLogPage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidLogPage.cs) — 日志查看器
- [FontModel.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/Models/FontModel.cs) — font/+fonts/ 扫描 + 别名注册
- [CHANGELOG.md](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/CHANGELOG.md) — v0.31.0~0.34.0 变更记录
- [TODO.md](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/TODO.md) — Phase 10 规划

## 知识库更新

- [graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md) — 新增 Android 兼容性修复章节（编译保护、运行时保护、图片回退、动态缩放、ShowQuestion、GSAVE）
- [font-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/font-rendering.md) — 新增外部字体加载章节（双轨管线、内嵌字体、别名系统、移动端方案）
