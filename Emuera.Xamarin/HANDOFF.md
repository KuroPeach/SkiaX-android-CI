# Handoff: Xamarin Android 真机测试反馈修复 + UI 完善

## 会话目标

修复真机测试中发现的 3 个问题（文字渲染全黑、Language 崩溃、菜单未中文化），并完成 UI 完善（SVG 图标、英文翻译、APK 标识）。

## 已完成

### Phase 9N (v0.35.0)
- [x] 文字渲染全黑修复 — DrawTextUtils 添加 fallback 绘制路径 + FontModel.AddFontFromResource 回退到 SKTypeface.Default
- [x] 游戏列表页 Language 崩溃修复 — SaveConfig try-catch + Preferences 回退 + ApplyLanguagePreference
- [x] 菜单栏中文化 — 所有 Android 页面硬编码英文→StringsText 本地化资源
- [x] 工具栏 SVG 图标 — Unicode 字符→TintImageButton + SVG 矢量图标（edit/gamepad/gallery_view/scroll_vertical/menu_show）
- [x] 汉堡菜单按钮 — 新增 ☰ 按钮打开 FlyoutPage 侧边菜单，原 ☰ 改为收起/展开工具栏
- [x] 英文翻译文件 — StringsText.en.resx（48 条翻译）
- [x] StringsText 扩展 — 新增 10 个本地化条目
- [x] SVG 嵌入资源 — Core csproj 添加 5 个 SVG + Views 编译引用
- [x] gamepad.svg — 从 XEmuera 项目复制
- [x] 应用名 — "Emuera" → "Emuera skiaX"
- [x] 版本 — 0.1.0 → 0.35.0 (versionCode=35)
- [x] APK 图标 — Emuera\icon.ico → 5 密度桶 mipmap PNG
- [x] 文字行向下偏移修复 — Config.FontSize/LineHeight/QuickButtonFontSize 乘以 ScreenDensity 统一物理像素坐标系
- [x] 按钮点击位置偏移修复 — ToLogicalTouchPoint 移除 density 除法，触摸坐标与 ClientWidth/ClientHeight 一致
- [x] 字体回退缺字修复 — FontModel 新增 RegisterMissingChineseFontFallbacks()，外部字体缺失时中文别名映射到内嵌 Microsoft YaHei

### 之前已完成

#### Phase 9J (v0.32.0)
- [x] GETTEXTBOX 修复 — ChangeTextBox 同步 TextBox.Text
- [x] WebP 图片回退 — PlatformInterop 图片解码回退委托 + Android BitmapFactory 实现
- [x] CompatiErrorLine 条件启用
- [x] AndroidLanguagePage — 语言选择 + 菜单入口
- [x] AndroidLogPage — 解释器日志查看器 + 菜单入口

#### Phase 9K (v0.33.0)
- [x] GSAVE 指令 Android 实现
- [x] ShowQuestion AlertDialog
- [x] 跨平台编译保护

#### Phase 9L (v0.34.0)
- [x] PrintHtml 存根修复
- [x] 运行时崩溃保护
- [x] 7C-P2 动态缩放完善
- [x] FontModel 字体扫描修复
- [x] Save Log 菜单
- [x] ServerGC + android:largeHeap 启用

### APK 构建验证
- [x] Debug APK 构建成功（0 错误）

## 未完成

- [ ] 真机测试 — 验证文字渲染修复效果 + SVG 图标显示 + 语言切换
- [ ] 动图渲染验证 — GIF/WebP 在 Android 上是否正确播放
- [ ] 内存占用对比测试
- [ ] AndroidSound 变速 — setSpeed/getSpeed/SetPreservePitch 为 stub，需 ExoPlayer

## 关键决策

- **DrawTextUtils fallback 路径**：Paint==null 或 FontCaches 为空时，创建临时 SKPaint 用 SKTypeface.Default 直接绘制
- **FontModel.Load 健壮性**：嵌入字体加载失败时回退到 SKTypeface.Default，不再抛异常
- **Language 页面双轨保存**：引擎已初始化→ConfigData.SaveConfig()，未初始化→Preferences 保存 + ApplyLanguagePreference
- **SVG 图标方案**：复用 XEmuera 的 TintImageButton + SVG 嵌入资源，统一工具栏图标风格
- **汉堡菜单分离**：☰ 打开 FlyoutPage 侧边菜单，menu_show.svg（三横线+左箭头）收起/展开工具栏
- **APK 标识**：应用名 "Emuera skiaX"（桌面版叫 "Emuera skia"），图标使用桌面版 icon.ico

## 阻塞项

无（ADB 可用，MuMu 模拟器端口 16384）

## 下一步建议

1. **9N-验证：ADB 真机测试** — 安装新 APK，验证文字渲染/SVG 图标/语言切换/菜单中文化
2. **10A-P2：动图渲染验证** — 使用含 GIF/WebP 的游戏资源测试
3. **10A-P3：内存占用对比** — 对比 SharedBitmapCache 效果
4. **10B-P1：AndroidSound 变速** — ExoPlayer 替代 MediaPlayer

## 推荐技能

- **erabasic** — 涉及 .cs 中 ERABASIC 解析/运行时逻辑
- **powershell-git** — APK 构建、ADB 部署、Git 操作
- **knowledge-builder** — 真机测试发现新洞见时持久化

## 关键文件

- [DrawTextUtils.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/Drawing/DrawTextUtils.cs) — fallback 绘制路径
- [FontModel.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/Models/FontModel.cs) — 字体加载回退
- [AndroidMainWindow.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidMainWindow.cs) — SVG 图标 + 汉堡菜单
- [AndroidLanguagePage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidLanguagePage.cs) — 语言选择 + SaveConfig 保护
- [AndroidHost.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidHost.cs) — ApplyLanguagePreference
- [AndroidMainMenuPage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidMainMenuPage.cs) — StringsText 中文化
- [AndroidApp.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/AndroidApp.cs) — StringsText 中文化
- [AndroidManifest.xml](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/Properties/AndroidManifest.xml) — 应用名 + 版本
- [MainActivity.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin.Android/MainActivity.cs) — Label = "Emuera skiaX"
- [StringsText.resx](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/Resources/StringsText.resx) — 新增 10 个本地化条目
- [StringsText.en.resx](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/Resources/StringsText.en.resx) — 英文翻译（48 条）
- [CHANGELOG.md](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/CHANGELOG.md) — v0.35.0 变更记录
- [TODO.md](file:///d:/emuera/emuera_lazyloading_selfmodified_version/Emuera.Xamarin/TODO.md) — Phase 9N 记录

## 知识库更新

- [graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md) — 新增 DrawTextUtils fallback 路径和 FontModel.Load 回退说明
