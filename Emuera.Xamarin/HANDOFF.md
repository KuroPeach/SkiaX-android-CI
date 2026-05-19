# Handoff: Xamarin 图片 LazyLoading + LRU 移植完成

## 会话目标

将 LazyLoading 内核的图片延迟加载和 LRU 缓存系统移植到 Xamarin 项目。

## 已完成

- [x] SharedBitmapCache 静态位图 LRU 缓存（max 200）
- [x] IFileBacked 可驱逐接口
- [x] ConstImage 延迟加载（CreateFrom 仅读尺寸，EnsureBitmapLoaded 按需解码）
- [x] AnimatedImageHelper GIF/WebP 多帧解码
- [x] SpriteAnimated 原生动画 Sprite（Evict/Reload 生命周期）
- [x] AnimSpriteCache 动画 LRU 缓存（max 6）
- [x] AppContents SQLite 元数据（DestW/DestH 列 + SpriteAnimated 创建）
- [x] SkiaSharp 2.88.x 兼容性修复（FrameRect 缺失退化处理）
- [x] 编译验证（0 错误 0 警告）
- [x] 知识库更新（graphics-rendering.md + lazyloading-features.md）
- [x] CHANGELOG.md 更新（v0.31.0）
- [x] TODO.md 更新（Phase 9I + 下一步计划）

## 未完成

- [ ] APK 构建验证 + 真机测试 LazyLoading/LRU 系统
- [ ] 动图渲染验证（GIF/WebP 在 Android 上是否正确播放）
- [ ] 内存占用对比测试（移植前后 SharedBitmapCache 效果）
- [ ] 修复 Android 特有错误（GETTEXTBOX user32.dll / .webp 图片缺失）
- [ ] CompatiErrorLine 仅在调试模式启用

## 关键决策

- **SkiaSharp 2.88.x FrameRect 缺失**：退化为清除整个画布，而非升级 SkiaSharp 版本。理由：升级 SkiaSharp 可能破坏现有渲染管线，且大多数 GIF 使用 Keep/RestoreToPrevious 处置方法，影响有限。
- **ConstImage.Dispose 不释放 Bitmap**：由 SharedBitmapCache 统一管理生命周期，避免重复释放或提前释放。
- **AnimSpriteCache 使用 WeakReference**：允许 GC 在内存压力下回收动画帧，同时 LRU 机制控制主动淘汰。

## 阻塞项

无。编译通过，等待真机验证。

## 下一步建议

1. **9J-P1：APK 构建验证** — `dotnet publish` 生成 APK，ADB 安装到模拟器，验证 LazyLoading/LRU 系统在 Android 上正常工作
2. **9J-P2：动图渲染验证** — 使用含 GIF/WebP 的游戏资源测试 SpriteAnimated 是否正确播放
3. **9J-P3：内存占用对比** — 对比移植前后的内存占用，验证 SharedBitmapCache 的 LRU 淘汰效果
4. **9E-NEXT：Android 特有错误修复** — GETTEXTBOX user32.dll P/Invoke 和 .webp 图片缺失问题

## 推荐技能

- **erabasic** — 涉及 .cs 中 ERABASIC 解析/运行时逻辑
- **powershell-git** — APK 构建、ADB 部署、Git 操作
- **knowledge-builder** — 真机测试发现新洞见时持久化

## 关键文件

- [AnimatedImageHelper.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/AnimatedImageHelper.cs) — GIF/WebP 多帧解码
- [ConstImage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/ConstImage.cs) — SharedBitmapCache + IFileBacked + ConstImage 延迟加载
- [CroppedImage.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/CroppedImage.cs) — SpriteAnimated + AnimSpriteCache
- [AppContents.cs](file:///d:/emuera/emuera_lazyloading_selfmodified_version/emuera.xamarin/Platform/Content/AppContents.cs) — SQLite 元数据 + SpriteAnimated 创建

## 知识库更新

- [graphics-rendering.md](file:///d:/emuera/shared-trae/knowledge/xemuera/graphics-rendering.md) — 新增 Xamarin LazyLoading + LRU 移植详情章节
- [lazyloading-features.md](file:///d:/emuera/shared-trae/knowledge/erabasic/lazyloading-features.md) — 标记 SpriteAnimated 已移植
