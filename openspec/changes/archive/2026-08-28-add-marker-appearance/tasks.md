## 1. 外观配置模型

- [x] 1.1 新增 `MarkerShape` 枚举（Circle/Square/Triangle/Diamond），`HotkeyConfig` 新增字段 `NormalColor/RoomColor/CrowbarColor/BearColor`、`MarkerSize`（默认 8）、`Shape`（默认 Circle），默认颜色为 常规/密室红(255,40,40)、撬棍房紫(160,40,200)、熊洞绿(40,200,40)，验证方式：csc 编译通过、临时单测断言默认字段值
- [x] 1.2 实现外观配置字符串双向转换：颜色 `R,G,B` ↔ `Color`、形状枚举名 ↔ `MarkerShape`（非法回退默认），验证方式：临时单测覆盖格式正确/大小写/非法输入
- [x] 1.3 扩展 `Load()`/`Save()`：读写 `Appearance.Normal/Room/Crowbar/Bear/Size/Shape` 六行，缺失或非法逐项回退默认；旧版仅含快捷键行的 `hotkeys.ini` 加载后外观全默认，验证方式：临时单测分别构造旧配置/新配置/损坏配置断言加载结果，Save 后文件含全部外观行
- [x] 1.4 扩展 `RestoreDefaults()` 同时还原外观默认值（颜色/大小/形状），验证方式：临时单测修改外观后调用 RestoreDefaults 断言全部还原

## 2. 绘制逻辑

- [x] 2.1 实现 `DrawMarker(Graphics g, SolidBrush brush, float cx, float cy, float size, MarkerShape shape)`：圆形 FillEllipse、方形 FillRectangle、三角形/菱形 FillPolygon（中心对齐、外接包围盒一致），验证方式：临时单测离屏渲染 4 形状到小 Bitmap，断言包围盒边界像素分布与预期形状一致（圆/方形四角对比、三角/菱形顶点分布）
- [x] 2.2 改造 `OnPaint` 标记循环：删除硬编码 `dotColors`，按 `DotTypes == null ? NormalColor : 类型色(Room/Crowbar/Bear)` 取色，统一按 `MarkerSize` 与 `Shape` 绘制，验证方式：默认配置下离屏渲染艾伦格/维寒迪各一点，颜色与旧逻辑输出一致（单测断言）

## 3. 设置窗口

- [x] 3.1 `SettingsForm` 改为 `TabControl` 两页：现有三动作快捷键编辑行迁入「快捷键」TabPage，底部「恢复默认」「确定」「取消」保留作用于整窗，验证方式：打开设置窗口显示两个页签，快捷键页功能与修改监听行为不变
- [x] 3.2 实现「标记外观」页：4 行颜色（名称 + 色块 Button 点击弹 `ColorDialog` + RGB 文本）、大小 `TrackBar`(4~20) + 像素值 Label、形状 4 个 `RadioButton`、预览 `Panel`，验证方式：切换到外观页各控件初始值与配置一致，色块点击弹出选色框
- [x] 3.3 预览 `Panel` 的 `Paint` 按当前编辑值绘制样例点（取常规色或集合色），颜色/大小/形状任一修改触发预览 `Invalidate` 实时更新，验证方式：拖动大小滑条、切换形状、改色后预览立即反映新外观
- [x] 3.4 保存/取消/恢复默认扩展：确定时把两页编辑数据写回 config 并 `Save()`（快捷键冲突校验逻辑保留）；「恢复默认」同时还原外观；切换 TabPage 时 `StopListening()`，验证方式：修改外观确定后 `hotkeys.ini` 含新 Appearance 行、取消后文件不变、恢复默认后界面回显默认外观
- [x] 3.5 `OpenSettings` 保存成功后追加 `this.Invalidate()` 触发立即重绘，验证方式：保存外观修改关窗后叠加层标记立即按新外观显示

## 4. 编译与整体验收

- [x] 4.1 csc 重新编译 `Crosshair.exe` 无错误、启动冒烟（托盘图标出现、无 `hotkeys.ini` 生成），验证方式：编译退出码 0、进程存活
- [x] 4.2 临时单测全量通过（1.1~2.2 涉及的转换、加载、保存、形状渲染断言），验证方式：单测程序退出码 0 输出 ALL PASS
- [ ] 4.3 按 spec 场景手工验收（外观页预览、4 色独立生效、大小 4~20、四形状切换、重启持久、保存立即生效、与快捷键设置并存），验证方式：验收清单逐项勾选（需实机环境）