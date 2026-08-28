## Context

现状：标记外观全部硬编码在 `OverlayForm.OnPaint` —— `dotColors[]` 三色数组（红/紫/绿）、`FillEllipse(px-4, py-4, 8, 8)` 固定 8px 圆形。现有配置体系为 `HotkeyConfig` + `hotkeys.ini`（快捷键），设置窗口 `SettingsForm` 为单页快捷键编辑。动机见 proposal.md - Why，行为要求见 specs/marker-appearance/spec.md 与 specs/hotkey-settings/spec.md。

约束：
- 单文件 `Crosshair.cs`、csc 直接编译、.NET 4.0 无第三方依赖
- 渲染是逐点高频路径（7 地图最多 39 点），形状计算要轻量
- 默认外观必须与现状逐像素一致（现有用户升级无感知）

## Goals / Non-Goals

**Goals:**
- 配置模型扩展：4 项颜色（常规/密室/撬棍房/熊洞）+ 大小 + 形状，缺失回退默认
- 绘制逻辑单点重构：类型取色 + 统一大小 + 四形状绘制
- 设置窗口 TabControl 两页，外观页含实时预览，确认后立即生效
- 与快捷键共享同一配置文件的读写与「恢复默认」

**Non-Goals:**
- 每类型/每地图独立的大小与形状（仅颜色按类型分；大小、形状全局统一）
- 标记描边、透明度、阴影、图标化
- 自定义任意形状
- ColorDialog 之外的取色交互（如吸管）

## Decisions

### D1: 外观配置并入 `HotkeyConfig`，文件仍为 `hotkeys.ini`

新增字段：`Color NormalColor/RoomColor/CrowbarColor/BearColor`、`int MarkerSize`（4~20，默认 8）、`MarkerShape Shape`（枚举 Circle/Square/Triangle/Diamond）。ini 新增 6 行：
```
Appearance.Normal=255,40,40
Appearance.Room=255,40,40
Appearance.Crowbar=160,40,200
Appearance.Bear=40,200,40
Appearance.Size=8
Appearance.Shape=Circle
```
缺失/非法行逐项回退默认（与快捷键 `Load` 同一容错模式）。

- **理由**：单配置单文件，读写、恢复默认、生命周期管理一处完成；旧版 `hotkeys.ini` 无外观行自动按默认渲染，升级兼容。
- **替代方案**：独立 `appearance.ini`（双文件冗余、双份容错代码）；注册表（不便携）。

### D2: 绘制逻辑收敛到单一函数 `DrawMarker`

`OnPaint` 循环中每点调用：按地图类型取色（`DotTypes == null` → `NormalColor`，否则 type 0/1/2 → Room/Crowbar/Bear），统一 `size` 以「中心点 + 半径」语义绘制：
- 圆形：`FillEllipse`
- 方形：`FillRectangle`
- 三角形：`FillPolygon`（顶点向上的等腰三角形，中心对齐）
- 菱形：`FillPolygon`（45° 旋转的方形，中心对齐）

形状均为 `size × size` 外接包围盒，保证切换形状时视觉尺寸一致。

- **理由**：现有代码中非维寒迪地图复用 `dotColors[0]`（红=密室红）——新模型将其拆为独立「常规色」与「密室色」，语义清晰且默认值不变；单函数使新增形状成本最低。
- **替代方案**：三处内联渲染（重复代码）；保持按 `dotColors` 索引渲染（无法表达 4 色独立配置）。

### D3: 设置窗口改为 `TabControl` 两页

- 「快捷键」页：现有三动作编辑行原样迁入第一个 TabPage。
- 「标记外观」页：
  - 颜色：4 行 = 名称 + 当前色块 Button（`ColorDialog` 选色）+ RGB 文本
  - 大小：`TrackBar`（4~20）+ 当前像素值 Label
  - 形状：4 个 `RadioButton`（圆形/方形/三角形/菱形）
  - 预览：约 90×60 `Panel`，`Paint` 中按当前编辑值绘制一个大号样例点
- 底部「恢复默认」「确定」「取消」作用于两页；「恢复默认」同时还原外观默认值。
- 编辑仍走「深拷贝副本 → 确定时校验 → 写回 config」模式；切换 TabPage 时自动 `StopListening()`（避免快捷键监听态残留误捕获）。

- **理由**：两页共用一个保存流程与编辑副本，复用现有 `SaveAndClose` 的校验/写回骨架；`KeyPreview` 全局监听只需在页切换时终止监听态。
- **替代方案**：单页堆叠（窗口过高）；两个独立窗口（入口分裂、配置写入分散）。

### D4: 保存即生效

`OnPaint` 直接读取 `hotkeys` 当前字段渲染（无缓存副本，天然单真相）；`OpenSettings` 保存成功后除 `RebuildKeyActions()` 外追加 `this.Invalidate()` 触发立即重绘。

- **理由**：渲染路径无额外状态，避免缓存失同步。
- **替代方案**：保存后重建渲染快照（多余状态，收益为零）。

## Risks / Trade-offs

- **旧配置文件兼容**：老 `hotkeys.ini` 无外观行 → 逐项回退默认，渲染与现状一致 → 由 D1 的缺失回退保证。
- **形状中心对齐误差**：三角形/菱形需要几何计算，坐标取整可能产生 1px 偏差 → 统一以中心点计算顶点并用浮点坐标传给 GDI+（`FillPolygon` 接受 PointF）。
- **ColorDialog 抢焦点**：调色对话框打开期间全局钩子仍会响应设置外的快捷键 → 复用现有 `settingsOpen` 标志（窗口未关闭，钩子已暂停），无需额外处理。
- **Tab 页切换丢失监听**：监听中切页导致交互困惑 → TabPage 激活变化时 `StopListening()`。

## Migration Plan

- 无数据迁移：旧 `hotkeys.ini` 直接可用（外观行缺失按默认）。
- 回滚：删除 `hotkeys.ini` 恢复出厂默认外观与快捷键；或重新编译旧源码。

## Open Questions

无。外观范围（4 色/统一大小/4 形状）已由用户确认，落在 specs 中。