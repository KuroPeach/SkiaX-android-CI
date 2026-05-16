# Handoff: Slice 5A-D1 + 5C-D1 完成

## 会话目标

继续推进移植任务，包括一至两次完整提交。

## 已完成

- [x] Slice 5A-D1：IO 跨平台适配
  - `FileUtils.GetDirectories()`：新增方法，为跨平台目录枚举预留扩展点
  - `AppContents.CreateSpriteFromFileDynamic()`：`System.IO.File.Exists` → `FileUtils.Exists(ref filepath)`
  - `Config.getFiles()`：`Directory.GetDirectories` → `FileUtils.GetDirectories`
  - `GameItemModel.Load()`：`Directory.GetDirectories` → `FileUtils.GetDirectories`
  - `Program.cs`：添加 `using XEmuera;` + `WorkingDir` 委托到 `GlobalStatic.PathProvider?.ExeDir`
- [x] Slice 5C-D1：SqlManager + 资源清理
  - `GlobalStatic.Reset()`：添加 Sound/Bgm 停止 + `SqlManager.CloseAll()` + `AppContents.SpriteDisposeAll(true)`
  - `DesktopHostForm`：添加 `FormClosing` 事件，退出时调用 `GlobalStatic.Reset()`
  - SqlManager 已通过 `**\*.cs` 通配符自动编译（611行，20个API），`Microsoft.Data.Sqlite` NuGet 已存在
  - `IPathProvider.SqlDir` 已定义，`Config.SavDir` 路径链完整
- [x] 编译验证：0 错误，72 警告
- [x] 第一次 Git 提交：`feat(IO): Slice 5A-D1 cross-platform IO adaptation - FileUtils`
- [x] 第二次 Git 提交：待执行（包含 5C-D1 + 文档更新）
- [x] 文档更新：TODO.md + CHANGELOG.md + HANDOFF.md

## 未完成

- [ ] Phase 5A-4/5/6：音频路径构建 + IPathProvider Android/iOS + 资源解压（需 TFM 切换）
- [ ] Phase 4A-Xamarin：启用 ContentPage MainWindow（未来 TFM 切换）
- [ ] Phase 4B-Xamarin：配置页面体系
- [ ] Phase 7C-P1：静态字体缩放
- [ ] Phase 7E-G：本地化/日志查看器/游戏列表
- [ ] ToolTip OwnerDraw（Draw/Popup 事件）：需要 SKBitmap→Bitmap 转换
- [ ] G5：MainThread (Xamarin.Essentials) 残留引用（6 处文件，仅影响 Xamarin 层）
- [ ] G6：旧 GDI 注释清理

## 关键决策

- **FileUtils.GetDirectories**：当前仅封装 `Directory.GetDirectories`，为将来跨平台大小写不敏感预留扩展点
- **SqlManager 零改动移植**：SqlManager.cs 已通过 `**\*.cs` 通配符自动编译，无需修改源码。路径依赖（`Config.SavDir`/`Program.WorkingDir`/`Program.ExeDir`）在 xamarin 中均已正确定义
- **GlobalStatic.Reset() 资源清理**：对齐 LazyLoading 原版，添加 Sound/Bgm 停止 + SqlManager.CloseAll() + SpriteDisposeAll
- **DesktopHostForm.FormClosing**：确保退出时正确释放 SQLite 连接和 WAL 锁

## 阻塞项

- 无代码阻塞

## 下一步建议

1. **Phase 5A-4**：替换音频路径构建（`IPathProvider.MusicDir`）
2. **Phase 4A-Xamarin**：TFM 切换到 net8.0-android 或多目标框架
3. **Phase 7C-P1**：静态字体缩放
4. **Phase 7E-G**：本地化/日志查看器/游戏列表

## 关键文件

### 本次修改的文件（Slice 5A-D1 + 5C-D1）

| 文件 | 变更说明 |
|------|---------|
| `Utils.cs` | 新增 `FileUtils.GetDirectories()` 方法 |
| `Platform/Content/AppContents.cs` | `System.IO.File.Exists` → `FileUtils.Exists(ref filepath)` |
| `Platform/Config/Config.cs` | `Directory.GetDirectories` → `FileUtils.GetDirectories` |
| `Models/GameItemModel.cs` | `Directory.GetDirectories` → `FileUtils.GetDirectories` + 添加 `using XEmuera;` |
| `Platform/Program.cs` | 添加 `using XEmuera;` + `WorkingDir` 委托到 PathProvider |
| `Platform/GlobalStatic.cs` | Reset() 添加 Sound/Bgm 清理 + SqlManager.CloseAll() + SpriteDisposeAll |
| `DesktopHost.cs` | 添加 FormClosing 事件处理 |
