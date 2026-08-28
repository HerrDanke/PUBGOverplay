## Context

现状：托盘菜单为系统默认 `ContextMenuStrip`（浅色 3D 外观），设置窗口 `SettingsForm` 为默认控件配色（TabControl/Button/Label 系统样式），仅预览面板自绘。动机与行为要求见 proposal.md - Why 与 specs/ui-theme/spec.md。范围已确认：**只换视觉，保留交互与布局结构**（窗口 430×358、两页签、三行快捷键、4 色/大小/形状/预览、底部三按钮、菜单项组）。视觉方向经用户确认：**浅色苹果风**（明亮浅色、柔和边框、苹果蓝强调），否决了初版的深色战术系。

约束：单文件 csc 编译、.NET 4.0、无第三方控件库；经典 WinForms 无法原生实现圆角/毛玻璃，全部视觉基于标准控件 + `FlatStyle.Flat` + OwnerDraw 实现。

## Goals / Non-Goals

**Goals:**
- 统一浅色调色板（6 基色 + 苹果蓝强调 + 标记三色），一处定义全局引用
- 托盘菜单与设置窗口共用主题，苹果蓝作为「签名元素」贯穿（指示/勾选/页签选中/标题）
- 实现全部基于 .NET 4.0 内置能力，无新增依赖
- 交互零变化：热键监听、冲突校验、预览刷新、配置持久化均为纯视觉叠加

**Non-Goals:**
- 改变窗口尺寸、控件几何、菜单项结构、页签行为
- 圆角/阴影/毛玻璃等非 WinForms 原生效果（避免大段自绘成本与稳定性风险）
- 跟随系统主题/深浅色切换
- 修改叠加层的绘制与配置逻辑

## Decisions

### D1: 设计令牌 — `UiTheme` 静态类

色彩（浅色苹果风，明亮基底 + 苹果蓝强调）：

| 令牌 | Hex | 用途 |
|---|---|---|
| `Bg` | `#F5F5F7` | 窗口/菜单基底（macOS 面板浅灰） |
| `Surface` | `#FFFFFF` | 白面（键帽、按钮、预览面板） |
| `Elevated` | `#E8E8ED` | 提升面（hover、选中、滑条槽） |
| `Line` | `#D1D1D6` | 分隔线、细边框 |
| `Text` | `#1D1D1F` | 主文本（近黑） |
| `TextDim` | `#86868B` | 次文本/说明（灰） |
| `Accent` | `#007AFF` | 苹果蓝：签名/选中/hover 指示/主按钮 |
| `AccentPressed` | `#0A5CD6` | 苹果蓝按下态 |

标记三色（外观页色块）沿用：红 `#FF2828` / 紫 `#A028C8` / 绿 `#28C828`。

字体：界面沿用「微软雅黑 9F」（中文）；数据角色用 `Consolas`（键帽字符、RGB 值）。

- **理由**：苹果风语义明确（`#F5F5F7` 面板、`#007AFF` 强调是 macOS 系统色惯例）；令牌集中避免散落魔法色值；与红点图标（品牌标识）在托盘/exe 端保持，界面内不再引入第二强调色。
- **替代方案**：每控件内联颜色（难维护）；保留品牌红作强调（与苹果蓝冲突，浅色界面红过于强烈，否决）。

### D2: 托盘菜单 — `CustomMenuRenderer`（ToolStripRenderer 子类）

继承 `ToolStripProfessionalRenderer`，重写背景（`Bg` 浅色）、分隔线（`Line` 细分隔）、hover 项背景（`Elevated`）+ 左侧 `Accent` 蓝色指示点、勾选（`Checked` 项）用 `Accent` 蓝色实心圆点、文字 `Text`/`TextDim`。`ContextMenuStrip` 设置 `Renderer = new CustomMenuRenderer()`，子菜单（地图池）继承同一渲染器。

- **理由**：`ToolStripProfessionalRenderer` 覆盖所需绘制点，无需触碰现有菜单构建逻辑与 `Checked` 状态机。
- **替代方案**：OwnerDraw 全自绘（易碎）；换用第三方库（违反零依赖）。

### D3: 设置窗口浅色化 — 控件级令牌应用

- 窗口：`BackColor = Bg`、`ForeColor = Text`，标题「● 设置」（整个标题文字用 `Accent` 蓝）
- `TabControl`：`DrawMode = OwnerDrawFixed` + `DrawItem` 自绘页签条（未选中 `Surface`+`TextDim`，选中 `Elevated`+`Text`+底部 `Accent` 2px 指示条）
- 按钮（修改/恢复默认/确定/取消）：`FlatStyle.Flat`、`FlatAppearance.BorderColor=Line`、`MouseOverBackColor=Elevated`；「确定」用 `Accent` 蓝底白字（`MouseOver` 用 `AccentPressed`）
- Label/状态栏：显式 `ForeColor`（`Text`/`TextDim`）
- 键帽：快捷键值 Label 白底（`Surface`）+ `Line` 细边框 + `Consolas` 等宽文本
- RGB/像素值 Label：`Consolas` + `TextDim`/`Text`
- 色块按钮、`TrackBar`、`RadioButton`、预览 `Panel`：`Surface`/`Bg` 统一背景；`RadioButton` 文字 `Text`
- 预览 `Panel`：白底 + `Line` 边框（`PreviewPaint` 内绘制）

- **理由**：全部为稳定的小改动（属性赋值 + 一个 `DrawItem` 回调），不重构控件布局；等宽/键帽保留为该应用的"按键/数据"语义呈现，构成主题记忆点。
- **替代方案**：自绘控件全部重写（成本高、回归风险大，违反「只换视觉」范围）。

### D4: 实现边界 —「系统样式兜底」规则

标准控件无法用属性达成的细节（`RadioButton` 选中圆点、`TrackBar` 滑块的系统绘制色），保留系统绘制但统一其背景。验收标准为"观感整体浅色统一"，不为个别系统控件像素死磕。

- **理由**：控制改动面，保证本次变更的风险可控、可回归。
- **替代方案**：全部控件自绘（高复杂度、高回归风险，超出「只换视觉」）。

## Risks / Trade-offs

- **浅色下可读性** → `Text/#1D1D1F` 在 `Bg/#F5F5F7` 上对比度高；说明性文字用 `TextDim` 保持层级。
- **OwnerDraw 页签在 DPI/缩放下的位偏移** → `DrawItem` 内使用 `e.TabBounds` 相对坐标，不手算像素。
- **菜单渲染器与菜单现有 `Opening` 动态文字** → 渲染器只画不读写状态，与现有事件无耦合。
- **验收主观性** → 以 spec 场景为验收清单（浅色、hover 指示、勾选样式、键帽、RBG 等宽、行为不变），逐条勾选。

## Migration Plan

- 纯视觉变更，无需配置迁移；重新编译即生效。
- 回滚：恢复系统默认外观 = 重新编译旧源码（主题集中在 `UiTheme`/`CustomMenuRenderer`，若需逐步回退可仅在 `SettingsForm`/`SetupTrayIcon` 移除两处引用）。

## Open Questions

无（视觉方向与范围经用户确认，落在 specs）。