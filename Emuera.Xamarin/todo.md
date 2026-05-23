# Emuera.Xamarin 音频系统移植 TODO

> 知识库参考：[audio-system.md](file:///d:/emuera/shared-trae/knowledge/xemuera/audio-system.md)
> 回流审计：[kernel-audit.md](file:///d:/emuera/shared-trae/knowledge/lazyloading/kernel-audit.md)

## 已完成（归档至 CHANGELOG）

- Phase 1 (v0.53.0)：Sound 虚方法 + Factory + NAudioSound/AndroidSound + 7 基础指令
- Phase 1.5 (v0.53.1)：审查修复 + 内核解耦
- 回流合并 (v0.54.0)：PlatformInterop/Program.MusicDir/Sound 基类回流 develop-skiasharp
- Phase 2-3 (v0.55.0)：扩展 API + 编译修复 + A33-A34 bug 修复

## 待实施

### Phase 4：APK 构建验证

- [ ] **4A** 完整 APK 构建 + 真机测试
- [ ] **4B** 音频功能端到端测试

## 风险

| # | 风险 | 缓解 |
|---|------|------|
| 1 | Android MediaPlayer 变速不可靠 | PlaybackParams + 设备测试 |
| 2 | NAudioSound pause/resume 缓冲区状态 | 需端到端测试验证 |
