## Why

程序窗口与 `Crosshair.exe` 文件当前使用系统默认图标（csc 编译未附带任何图标资源），仅系统托盘有红点图标，应用整体视觉不统一。为程序窗口与 exe 文件应用与托盘一致的红点图标，提升辨识度与完成度。

## What Changes

- 新增红点样式图标资源文件（`.ico`，透明背景 + 红色圆点，与托盘图标 `(255,60,60)` 样式一致）
- 编译命令加入 `/win32icon`，使 `Crosshair.exe` 文件图标（资源管理器/任务栏）显示为红点
- 运行时窗口 `Form.Icon` 设置为红点图标（与 exe 图标一致，不依赖系统默认）

## Capabilities

### New Capabilities
- `app-icon`: 应用程序图标（exe 文件与程序窗口）显示为红点样式

### Modified Capabilities
<!-- 无现有 capability 行为变化 -->

## Impact

- **代码**：[Crosshair.cs](Crosshair.cs) — `OverlayForm` 构造中设置 `this.Icon`，复用红点绘制逻辑（与托盘图标同源）
- **资源**：新增 `pubg.ico` 图标文件（编译资源，随仓库提交）
- **文档**：README / HANDOFF 中编译命令同步加入 `/win32icon:pubg.ico`
- **依赖**：无新增（System.Drawing 自带 Icon/Bitmap 生成能力）