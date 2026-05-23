# Emuera.Xamarin 音频系统移植 TODO

> 版本对应：Phase 1 = v0.53.0，Phase 1.5 = v0.53.1，回流合并 = v0.54.0，Phase 2-3 = v0.55.0
> 知识库参考：[audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md)
> 回流审计：[kernel-audit.md](file:///d:/emuera/shared-trae/knowledge/lazyloading/kernel-audit.md)

## 已完成

### Phase 1：AndroidSound 基础实现 ✅ v0.53.0

- [x] Sound 虚方法继承方案 + Factory 工厂模式
- [x] NAudioSound : Sound（桌面端）
- [x] AndroidSound : Sound（Android 端，MediaPlayer）
- [x] csproj 条件编译（Sound.NAudio.cs 仅 windows，AndroidSound.cs 仅 android）
- [x] DesktopHost / AndroidHost 初始化 Factory
- [x] 7 个基础指令：PLAYSOUND/PLAYBGM/STOPSOUND/STOPBGM/SETSOUNDVOLUME/SETBGMVOLUME/EXISTSOUND

### Phase 1.5：审查修复 + 内核解耦 ✅ v0.53.1

- [x] GlobalStatic.cs 去除 NAudioSound 直接依赖
- [x] WinForms Program.cs 设置 Sound.Factory
- [x] AndroidSound OnCompletion repeat>1 修复
- [x] AndroidSound play() 改 PrepareAsync
- [x] GraphicsImage 补 PenColorArgb/PenWidth/BrushColorArgb
- [x] GlobalStatic.Reset() 音量重置（stop+close+Factory 重建）
- [x] AndroidSound.play 签名修正（long→int 匹配基类）
- [x] AndroidSound.cs 重复 play 方法删除

### 回流合并 develop-skiasharp ✅ v0.54.0

- [x] **RF1** PlatformInterop.cs（委托模式）— 内核 29 处引用
- [x] **RF2** Program.MusicDir / FileExists — 6 处音频路径引用
- [x] **RF3** SKColor→Color 转换 — Creator.Method.cs ToArgb()
- [x] **RF4** Sound 基类 + Factory 在内核中实现（develop-skiasharp v4.1.0）
- [x] NAudioSound 重构为 Sound 子类 + override
- [x] B 类回流修复（跨平台适配）
- [x] 合并冲突解决（9 文件）

### Phase 2-3：扩展音频 API + 编译修复 ✅ v0.55.0

- [x] **2A** PLAYSOUND ArgumentBuilder 升级为 SP_HTML_PRINT（支持 repeat 参数）
- [x] **2B** AndroidSound repeat=N Completion 计数
- [x] **3A** 移植 GETSOUNDORBGMINFO（channelId, infoType）
- [x] **3B** 移植 ISPLAYINGSOUND / ISPLAYINGBGM
- [x] **3C** 移植 SOUNDCONTROL / BGMCONTROL（暂停/恢复/停止/变速）
- [x] **3D** 变速支持（NAudioSound: VarispeedSampleProvider, AndroidSound: PlaybackParams）
- [x] 编译错误修复：Config.Config→Config（6处）、AppContents.LoadContents 返回值（1处）、NAudioSound override 签名（1处）
- [x] Bug 修复：IsPlayingSoundMethod 死代码+无限循环、AndroidSound.ApplySpeed OnPrepared 不生效

## 待实施

### Phase 4：APK 构建验证

- [ ] **4A** 完整 APK 构建 + 真机测试
- [ ] **4B** 音频功能端到端测试

## 风险

| # | 风险 | 缓解 |
|---|------|------|
| 1 | Android MediaPlayer 变速不可靠 | PlaybackParams + 设备测试 |
| 2 | PLAYSOUND SP_HTML_PRINT 参数构建器复杂 | 参考 LazyLoading 已有实现 |
| 3 | Droid 版 AndroidSound 无变速实现 | Phase 4 补充 |
| 4 | NAudioSound pause/resume 缓冲区状态 | 需端到端测试验证 |
