# Handoff: Phase 4A-D22/D23 完成

## 会话目标

继续推进 Phase 4 移植任务，包括一至两次完整提交。

## 已完成

- [x] 勘测桌面端剩余差距：8 项空壳/不完整实现（P1-P3 优先级排序）
- [x] 切片 4A-D22：P1-P2 快速修复
  - AnimeTimer：从硬编码 0 改为 redrawTimer.Interval
  - OutputLog(string, bool)：从空壳改为委托 OutputLog(string)
  - pointingStrings：添加 HashSet<ConsoleButtonString>，ButtonIsPointing 改为 Contains，MoveMouse/LeaveMouse 同步更新
  - SetTextBoxPos：从空壳改为 DesktopMainWindow override + 坐标裁剪
  - WinmmTimer：恢复 winmm.dll P/Invoke + 跨平台 fallback
- [x] 切片 4A-D23：GotoTitleAndLoadAndRepeatInput
  - 完整 Ctrl+Z 回退重放实现（BeginTitle → callEmueraProgram → LoadSilent → PressEnterKey 循环）
  - DesktopHostForm Ctrl+Z 键盘快捷键
  - Config.Ctrl_Z_Enabled 委托到 Runtime Config
- [x] 编译验证：0 错误，64 警告（均为既有）
- [x] 第一次 Git 提交：`feat(Desktop): Phase 4A-D22 P1-P2 fixes`
- [x] 第二次 Git 提交：`feat(Desktop): Phase 4A-D23 GotoTitleAndLoadAndRepeatInput + Ctrl_Z`
- [x] 文档更新：TODO.md + CHANGELOG.md + HANDOFF.md

## 未完成

- [ ] Phase 4A-Xamarin：启用 ContentPage MainWindow（未来 TFM 切换）
- [ ] Phase 4B-Xamarin：配置页面体系
- [ ] Phase 5：平台特定功能（IO/音频/SQL）
- [ ] Phase 7C-P1：静态字体缩放（Config 属性已存在，DesktopConfigDialog 已有编辑入口，但 LazyLoading Config.cs 缺少 FontScale 属性）
- [ ] Phase 7D：外部字体加载
- [ ] Phase 7E-G：本地化/日志查看器/游戏列表
- [ ] ToolTip OwnerDraw（Draw/Popup 事件）：需要 SKBitmap→Bitmap 转换，高难度
- [ ] G5：MainThread (Xamarin.Essentials) 残留引用（6 处文件，仅影响 Xamarin 层）
- [ ] G6：旧 GDI 注释清理

## 关键决策

- **pointingStrings 升级策略**：保留原有 `pointingString`（用于 rikaichan 等单按钮引用），同时添加 `pointingStrings` HashSet（用于 ButtonIsPointing 多按钮高亮）。MoveMouse 中 Clear + Add，LeaveMouse 中 Clear
- **SetTextBoxPos 实现策略**：在 DesktopHostForm 中直接操作 inputTextBox.Location/Width，坐标裁剪到 pictureBox 范围内
- **WinmmTimer P/Invoke 策略**：使用 `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` 条件判断，Windows 桌面端用 winmm.dll（1ms 精度），其他平台 fallback 到 Environment.TickCount64（15ms 精度）
- **GotoTitleAndLoadAndRepeatInput**：直接移植 LazyLoading 实现，使用 `callEmueraProgram`（xamarin 中的方法名，非 `RunEmueraProgram`）
- **Config.Ctrl_Z_Enabled 委托**：与 DefaultFont 相同模式，在 Platform Config 添加委托属性到 Runtime Config

## 阻塞项

- 无代码阻塞。Phase 4A-Desktop 所有切片已完成

## 下一步建议

1. **Phase 4A-Xamarin**：TFM 切换到 net8.0-android 或多目标框架，启用 XAML 页面
2. **Phase 5A**：IO 跨平台适配（FileUtils + IPathProvider）
3. **Phase 5B**：音频系统（NAudio → IPlayer 扩展）
4. **Phase 7C-P1**：静态字体缩放（需在 LazyLoading Config.cs 添加 FontScale 属性）
5. **Phase 7D-P1**：外部字体加载（扩展目录扫描 + 别名映射）

## 推荐技能

- `erabasic [XEmuera-1]`：修改 EmueraConsole 逻辑时需理解 ERABASIC 语义
- `knowledge-builder [XEmuera-1]`：更新知识库
- `powershell-git [XEmuera-1]`：Git 提交操作

## 关键文件

### 本次修改的文件（Phase 4A-D22/D23）

| 文件 | 变更说明 |
|------|---------|
| `DesktopHost.cs` | SetTextBoxPos override + SetTextBoxPosition + Ctrl+Z KeyDown |
| `Platform/GameView/EmueraConsole.cs` | AnimeTimer 修复 + OutputLog 委托 + pointingStrings HashSet + GotoTitleAndLoadAndRepeatInput |
| `Platform/Stubs/MainWindow.cs` | SetTextBoxPos 改为 virtual |
| `Platform/Stubs/WinmmTimer.cs` | winmm.dll P/Invoke + 跨平台 fallback |
| `Platform/Config/Config.cs` | Ctrl_Z_Enabled 委托属性 |
| `TODO.md` | 4A-D22/D23 标记 ✅ |
| `CHANGELOG.md` | v0.10.0 |
