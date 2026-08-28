# PUBG 地图标记叠加层 — 项目交接文档

本文档供后续会话 / 协作者快速接手项目。包含项目现状、技术要点、构建方法、OpenSpec 状态与待办事项。

---

## 1. 项目概览

PUBG（绝地求生）地图标记辅助工具：在游戏画面上叠加透明图层，显示地图关键地点标记（密室 / 撬棍房 / 熊洞等）。单文件 C# WinForms 程序，csc 直接编译，无第三方依赖，便携 exe。

**当前功能**（最新版本）：

- 7 张地图标记叠加（艾伦格 / 米拉玛 / 泰戈 / 维寒迪 / 帕拉莫 / 帝斯顿 / 荣都）
- 维寒迪 39 点三类型区分（密室 / 撬棍房 / 熊洞），其他地图各点
- 全局热键：切换显隐（`` ` `` / `F2`）、上一张 / 下一张地图（`←` / `→`）——**可在设置中自定义**
- 托盘图标 + 右键菜单：地图池筛选、显示/隐藏、设置、退出
- **设置窗口（两页签）**：
  - 「快捷键」：三个动作重新绑定，保存即生效，冲突检测，恢复默认
  - 「标记外观」：4 项独立颜色（常规 / 密室 / 撬棍房 / 熊洞）、大小（4~20px）、形状（圆/方/三角/菱）、实时预览
- **现代化浅色苹果风界面**：托盘菜单与设置窗口统一主题（`UiTheme` 令牌 + `CustomMenuRenderer`），苹果蓝强调、键帽式快捷键展示、等宽数据字体；仅换视觉，交互与布局不变
- 配置持久化：exe 同目录 `hotkeys.ini`（缺失/损坏逐项回退默认，首次运行不落盘）

## 2. 状态速览

| 项 | 状态 |
|---|---|
| 编译 | ✅ 通过（.NET Framework v4.0.30319 csc，C# 5，含 /win32icon） |
| 启动冒烟 | ✅ 进程正常、托盘图标出现 |
| 自动化验证 | ✅ 临时单测全过（快捷键 21 项 + 外观 53 项 + 图标 8 项 + UI 主题 13 项） |
| 手工验收 | ✅ 快捷键/外观/图标三个变更通过；⚠️ **modern-dark-ui（浅色界面）待实机验收**（见 §6） |
| OpenSpec | ✅ 4 个变更已归档，4 个主 capability spec，无进行中变更 |

## 3. 技术要点

| 组件 | 方案 |
|------|------|
| 语言 / 框架 | C#，.NET Framework 4.0（C# 5 编译器） |
| 窗口 | 无边框全屏、`TopMost`、`TransparencyKey=Black`、`WS_EX_LAYERED\|WS_EX_TRANSPARENT` 点击穿透 |
| 显隐控制 | `SetLayeredWindowAttributes`（alpha 0/255） |
| 全局热键 | `WH_KEYBOARD_LL` 低层钩子；设置窗口打开期间钩子放行全部按键（`settingsOpen` 标志） |
| 钩子分发 | `HotkeyConfig.Actions` → `Dictionary<Keys, Action>` 映射（`RebuildKeyActions`） |
| 绘制 | GDI+；`MarkerRenderer.Draw` 单函数四形状（中心对齐、包围盒一致），`PickColor` 类型取色 |
| 配置 | `HotkeyConfig`（快捷键 3 动作多键 + 外观 6 字段），文件 `hotkeys.ini` |
| 设置窗口 | `SettingsForm`：`TabControl` 两页，深拷贝编辑副本，确定时校验写回，取消零影响 |
| UI 主题 | `UiTheme`（浅色苹果风令牌 + `StyleButton`/字体）、`CustomMenuRenderer`（`ToolStripProfessionalRenderer` 子类，hover/勾选蓝点）、`TabControl` OwnerDraw 页签 |

**代码结构**（单文件 [Crosshair.cs](Crosshair.cs)，约 1180 行）：

```
OverlayForm        → 主窗口/钩子/托盘/绘制
HotkeyNames        → Keys ↔ 配置/显示名转换
MarkerShape        → 形状枚举（Circle/Square/Triangle/Diamond）
HotkeyAction       → 快捷键动作数据
HotkeyConfig       → 配置加载/保存/恢复默认（含外观 6 字段）
MarkerRenderer     → PickColor + Draw 四形状
DotIcon            → 红点图标生成（托盘/窗口/exe 同源）
UiTheme            → 浅色苹果风颜色/字体令牌 + 样式助手
CustomMenuRenderer → 托盘菜单深渲染（hover/勾选蓝点）
SettingsForm       → 设置窗口（两页签）
Program            → Main 入口
```

## 4. 构建与运行

```powershell
# Windows PowerShell（注意 --% 停止参数解析，避免 /target 被当除法）
& "$env:SystemRoot\Microsoft.NET\Framework\v4.0.30319\csc.exe" --% /target:winexe `
  /win32icon:pubg.ico `
  /reference:System.dll /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Crosshair.cs
```

运行 `Crosshair.exe`，托盘出现红色圆点图标。

**应用图标说明**：`pubg.ico`（32×32，透明背景 + 红点 `(255,60,60)`）已入库并在编译时通过 `/win32icon` 嵌入 exe；窗口与托盘图标在运行时由 `DotIcon.Create(size)` 同源生成（16/32px）。如需重新生成 `pubg.ico`（如调整配色），用临时程序调用 `PubgCrosshair.DotIcon.Create(32)` 并 `Icon.Save` 为 `.ico`。

**临时单测方法**（修改后回归）：

```powershell
# 1) 编译为 library
# 2) 编写临时测试 .cs（引用 PubgCrosshair 命名空间）
# 3) /reference:CrosshairLib.dll 编译测试 exe 并运行，断言全部 PASS
```

## 5. 配置文件格式（hotkeys.ini）

```
Toggle=F2,Oemtilde          # 快捷键行，逗号分隔多键
PrevMap=Left
NextMap=Right
Appearance.Normal=255,40,40       # 外观：常规色（非维寒迪地图）
Appearance.Room=255,40,40         # 密室
Appearance.Crowbar=160,40,200     # 撬棍房
Appearance.Bear=40,200,40         # 熊洞
Appearance.Size=8                 # 直径 4~20
Appearance.Shape=Circle           # Circle/Square/Triangle/Diamond
```

- 缺失行 / 非法值 → 该项回退默认；首启不生成文件，保存时才落盘
- 删除 `hotkeys.ini` 即恢复全部出厂默认
- 键名用 `Keys` 枚举名（大小写不敏感），颜色为 `R,G,B`（0~255）

## 6. 验收状态

| 变更 | 验收内容 | 状态 |
|---|---|---|
| `add-hotkey-settings` | 设置打开、改键生效/旧键失效、冲突提示、重启持久、恢复默认、默认键与损坏 ini 回退 | ✅ 通过 |
| `add-marker-appearance` | 外观页预览、4 色独立生效、大小 4~20、四形状切换、保存立即生效、与快捷键设置并存 | ✅ 通过 |
| `add-app-icon` | 资源管理器 / 窗口 / 任务栏红点图标，与托盘一致 | ✅ 通过 |
| `modern-dark-ui` | 菜单浅色+hover+勾选样式、窗口浅色、页签签名、键帽、等宽数据、**行为回归**（改键/冲突/保存/恢复默认/立即生效） | ⚠️ 待实机验收 |

验收场景与规范详见 `openspec/specs/` 下对应 capability spec。

## 7. OpenSpec 状态

- **规范根**：`openspec/`（schema: spec-driven，CLI v1.10.0）
- **主 specs**：
  - `openspec/specs/hotkey-settings/spec.md`（6 条 Requirement）
  - `openspec/specs/marker-appearance/spec.md`（6 条 Requirement）
  - `openspec/specs/app-icon/spec.md`（2 条 Requirement）
  - `openspec/specs/ui-theme/spec.md`（4 条 Requirement）
- **已归档变更**：`2026-08-28-add-hotkey-settings/`、`2026-08-28-add-marker-appearance/`、`2026-08-28-add-app-icon/`、`2026-08-28-modern-dark-ui/`（均在 `openspec/changes/archive/`）
- **进行中变更**：无
- **新需求流程**：`openspec propose` → 产物审核 → `/opsx:apply` 实现 → `/opsx:archive` 归档（归档自动同步主 specs）

## 8. 已知事项与注意事项

- **全局拦截**：绑定后的热键不会传递给游戏（与现状一致），设置界面与 README 已提示用户避开游戏操作键
- **WinForms 标题栏**：标题文字由系统绘制不可染色，「● 设置」的蓝色签名落实在页签指示条与主按钮
- **ToolStripRenderer**：无 `OnRenderItemCheck` 虚方法，勾选样式并入 `OnRenderMenuItemBackground`（Checked 菜单项画蓝点）
- **PowerShell 编码坑**：`Get-Content` 默认按 ANSI 读 UTF-8 文件会中文乱码，校验含中文内容时加 `-Encoding UTF8`
- **PowerShell 调用 csc**：`/target:...` 需用 `--%` 或 `&` 运算符包裹路径，否则被解析为除法
- **GDI+ 边界像素**：`FillPolygon`/`FillRectangle` 不填充右/下边界与尖锐顶点像素，形状单测取点应选内部像素
- README 为使用文档，LOG.md 为迭代开发日志，本文件为交接索引