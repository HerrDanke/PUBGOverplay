## 1. 设计令牌

- [x] 1.1 新增 `UiTheme` 静态类：颜色令牌（Bg=#F5F5F7/Surface=#FFFFFF/Elevated=#E8E8ED/Line=#D1D1D6/Text=#1D1D1F/TextDim=#86868B/Accent=#007AFF/AccentPressed=#0A5CD6 + 标记三色）、字体（微软雅黑 9F / Consolas 9F）、样式助手（`StyleButton`），验证方式：csc 编译通过；临时单测断言令牌值、字体对象可创建
- [x] 1.2 新增 `CustomMenuRenderer`（`ToolStripProfessionalRenderer` 子类）：浅色背景、`Line` 细分隔线、hover `Elevated` 背景 + 左侧 `Accent` 蓝点指示、勾选 `Accent` 蓝点、`Text`/`TextDim` 文字渲染，验证方式：csc 编译通过；实机确认托盘菜单浅色渲染

## 2. 托盘菜单

- [x] 2.1 `SetupTrayIcon` 中 `ContextMenuStrip.Renderer = CustomMenuRenderer`（子菜单自动继承），验证方式：右键托盘图标菜单为浅色主题，hover 出现蓝色指示、地图池勾选为蓝色（实机逐项确认）

## 3. 设置窗口

- [x] 3.1 窗口与基础控件令牌化：`BackColor=Bg`/`ForeColor=Text`、Label/状态栏/说明文字颜色（`Text`/`TextDim`）、标题「● 设置」用 `Accent`，验证方式：打开窗口整体浅色渲染、文字对比清晰（实机）
- [x] 3.2 `TabControl` OwnerDraw（`DrawMode=OwnerDrawFixed` + `DrawItem`）：未选中 `Surface`+`TextDim`、选中 `Elevated`+`Text`+`Accent` 底部指示条，验证方式：两页签选中/未选中态样式正确（实机）
- [x] 3.3 按钮扁平化：修改/恢复默认/确定/取消 `FlatStyle.Flat` + `FlatAppearance`（`BorderColor=Line`、`MouseOverBackColor=Elevated`），「确定」用 `Accent` 蓝底白字（hover `AccentPressed`）；标准控件（RadioButton/TrackBar/色块）统一浅色背景，验证方式：按钮浅色扁平、hover 变色、确定按钮为蓝色（实机）
- [x] 3.4 键帽与等宽数据：快捷键绑定值键帽样式（白底 `Surface` + `Line` 细边框 + `Consolas` 文本），RGB 与像素值 Label 用 `Consolas`，验证方式：设置窗口键帽视觉与 RGB 等宽显示（实机）
- [x] 3.5 预览面板与外观页细节：预览 `Panel` 白底 `Surface` + `Line` 边框，色块按钮与「标记外观」页整体令牌统一，验证方式：外观页浅色观感与预览清晰（实机）

## 4. 编译与整体验收

- [x] 4.1 csc 重新编译 `Crosshair.exe` 无错误、启动冒烟，验证方式：编译退出码 0、进程存活、托盘图标正常
- [x] 4.2 行为回归：快捷键修改/冲突检测/外观保存/恢复默认/立即生效等原有交互全部正常（仅观感变化），验证方式：逐项回归勾选（实机）
- [x] 4.3 按 spec 场景手工验收（菜单浅色+hover+勾选样式、窗口浅色、页签签名、键帽、等宽数据、交互不变），验证方式：验收清单逐项勾选（需实机环境）