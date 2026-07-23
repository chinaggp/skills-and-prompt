# Windows Black Hole 桌面小组件设计

## 文档状态

- 日期：2026-07-23
- 状态：已批准
- 批准依据：用户明确授权剩余产品与技术细节由助手决定
- 目标目录：`plugins/windows-black-hole/`
- 产品名称：Windows Black Hole

## 1. 背景

用户需要一个 Windows 11 独立桌面小组件。它以写实黑洞为主体，始终显示在其他窗口上方。用户把文件拖入中央事件视界并松开后，小组件将文件移入 Windows 回收站。鼠标、文件和黑洞后方画面靠近黑洞时，应表现出类似真实黑洞引力透镜的扭曲；离开影响范围后平滑恢复。

用户提供的参考图只展示了部分黑洞。本产品采用相同的写实视觉方向，但补全为完整圆形事件视界、横向吸积盘和上下弯曲的完整引力透镜。参考图只作为美术方向，不直接随产品分发。

## 2. 目标

1. 提供一个用户手动启动、透明无边框、始终置顶的 Windows 11 x64 桌面小组件。
2. 支持普通文件、文件夹、快捷方式及多选对象拖放。
3. 只有中央事件视界是投放删除区，外围只产生视觉吸引和扭曲。
4. 投放成功后使用 Windows Shell 将对象移入回收站，永不降级为永久删除。
5. 实时渲染写实完整黑洞，并对背景、鼠标视觉替身和拖拽对象产生渐进式引力透镜效果。
6. 在 GPU 性能不足或捕获能力受限时自动降低视觉质量，同时保持拖放与回收站功能可用。
7. 支持 DPI 缩放、多显示器、位置持久化以及手动移动小组件。

## 3. 非目标

首个版本不包含：

- 开机启动或后台服务；
- Windows 10、ARM64、macOS 或 Linux 支持；
- Rainmeter、Wallpaper Engine 或浏览器宿主；
- 永久删除、文件粉碎或绕过回收站；
- 删除成功提示、撤销按钮或应用内回收站管理；
- 账户、云同步、遥测、广告或网络服务；
- 全局快捷键和主题市场；
- 对“此电脑”、磁盘分区、控制面板或回收站等 Shell 虚拟对象的删除。

## 4. 已确认的产品决策

| 项目 | 决策 |
| --- | --- |
| 运行形式 | 独立 Windows 11 桌面小组件 |
| 启动方式 | 用户手动启动，不注册开机启动项 |
| 窗口层级 | 始终置顶 |
| 移动方式 | 普通鼠标按住黑洞中心拖动 |
| 删除语义 | 默认无确认地移入回收站 |
| 支持对象 | 文件、文件夹、快捷方式、多选拖拽 |
| 删除命中区 | 仅中央事件视界 |
| 外围区域 | 只产生吸引、拉伸和扭曲 |
| 成功反馈 | 只播放吞噬动画，不显示提示或撤销 |
| 默认尺寸 | 电影感，黑洞视觉本体直径约 320 逻辑像素 |
| 影响范围 | 以事件视界中心为圆心，半径约 240 逻辑像素 |
| 视觉方向 | 写实完整黑洞、白金与橙色吸积盘、完整引力透镜 |
| 画质策略 | 自适应完整扭曲 |
| 技术路线 | C# WinUI 3 外壳 + C++ Direct3D 11/HLSL 渲染模块 |

## 5. 总体架构

系统由三个边界明确的模块组成。

### 5.1 Desktop Shell

技术：C#、.NET 8、WinUI 3、Windows App SDK。

职责：

- 启动并关闭原生覆盖窗与渲染模块；
- 管理窗口位置、DPI、多显示器和右键菜单命令；
- 在原生覆盖窗句柄上注册并处理 OLE Shell 拖放；
- 每帧提供鼠标屏幕坐标、拖拽状态和交互强度；
- 保存本地设置；
- 调用渲染接口和回收站接口；
- 保证渲染失败时不会改用其他删除方式。

### 5.2 Gravity Renderer

技术：C++20、Direct3D 11、Direct2D、Windows Graphics Capture、HLSL。

职责：

- 创建逐像素透明、无边框、非激活、始终置顶的原生 Win32 覆盖窗；
- 为覆盖窗创建 DirectComposition Target 和预乘 Alpha Composition Swap Chain；
- 捕获黑洞后方当前显示器的局部画面；
- 生成事件视界、光子环、吸积盘、粒子和辉光；
- 使用 HLSL 对背景采样坐标实施径向引力透镜映射；
- 保持真实系统光标可见，并绘制受扭曲的光标视觉回声和拖拽对象替身；
- 根据交互距离更新吸引、拉伸、旋转和亮度；
- 收集 CPU/GPU 帧耗时并选择画质等级；
- 处理 Direct3D 设备丢失并重建资源。

渲染模块拥有覆盖窗句柄、D3D11 Device、Capture Frame Pool、纹理和合成目标。创建成功后把覆盖窗句柄返回给 Desktop Shell，使后者能注册 OLE Drop Target、更新位置和处理菜单命令。Desktop Shell 不直接操作 GPU 资源。

### 5.3 Recycle Service

技术：Windows Shell COM `IFileOperation`。

职责：

- 从拖放数据对象提取真实文件系统路径；
- 规范化路径并拒绝 Shell 虚拟对象和不保证回收语义的网络位置；
- 对整批对象执行回收站删除；
- 通过进度回调报告每个对象的成功或失败；
- 永不在回收站操作失败后尝试永久删除。

三个模块是逻辑隔离，不在首个版本中拆分为多个进程。删除操作不在渲染线程执行，渲染模块也不持有待删除路径。

## 6. 建议工程结构

实施阶段在当前仓库创建：

```text
plugins/windows-black-hole/
├── WindowsBlackHole.sln
├── Directory.Build.props
├── README.md
├── assets/
│   ├── shaders/
│   ├── textures/
│   └── sounds/
├── src/
│   ├── WindowsBlackHole.App/
│   ├── WindowsBlackHole.Contracts/
│   └── WindowsBlackHole.Renderer/
├── tests/
│   ├── WindowsBlackHole.App.Tests/
│   ├── WindowsBlackHole.Renderer.Tests/
│   └── WindowsBlackHole.IntegrationTests/
└── packaging/
    └── WindowsBlackHole.Package/
```

边界定义：

- `WindowsBlackHole.App`：窗口、拖放、设置、状态机和回收站服务；
- `WindowsBlackHole.Contracts`：C# 与原生模块共享的结构、枚举和生命周期接口；
- `WindowsBlackHole.Renderer`：屏幕捕获、D3D11 资源和 HLSL；
- `App.Tests`：纯逻辑、状态机、路径验证和设置；
- `Renderer.Tests`：数学函数、质量控制器和离屏渲染；
- `IntegrationTests`：真实 HWND、DPI、拖放与受控回收站测试；
- `Package`：不包含开机启动扩展的 x64 安装包。

## 7. 窗口与输入设计

### 7.1 窗口

- 三档尺寸使用下列固定逻辑尺寸：

| 档位 | 视觉本体直径 | 影响半径 | 事件视界/投放区直径 |
| --- | ---: | ---: | ---: |
| Compact | 160 | 120 | 64 |
| Balanced | 240 | 180 | 96 |
| Cinematic | 320 | 240 | 128 |

- 影响半径从事件视界中心计算，不是从视觉本体边缘计算。
- 顶层透明窗口覆盖影响区，并在四周增加 16 逻辑像素的 Shader 采样安全边距；默认窗口因此为 512 × 512 逻辑像素。
- 原生模块使用 `WS_POPUP | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP` 创建覆盖窗，并通过 DirectComposition 预乘 Alpha Swap Chain 输出逐像素透明内容。
- Desktop Shell 使用返回的 HWND 管理位置，并以 `SetWindowPos(HWND_TOPMOST, ...)` 保持始终置顶。
- 窗口不出现在任务切换器中，不抢夺前台焦点。
- 正常鼠标事件在事件视界外采用透明命中；事件视界同时是普通鼠标的移动手柄和文件拖拽的投放区。
- OLE `IDropTarget` 注册在完整影响窗口上，使文件进入外围时即可获得拖拽坐标和数据对象。
- 鼠标靠近使用 `GetCursorPos` 获取坐标，不安装全局键鼠钩子。
- 小组件位置限制在当前显示器可见工作区内，避免重启后完全落在屏幕外。

### 7.2 多显示器与 DPI

- 内部距离计算统一使用物理像素。
- UI 配置以逻辑像素表达，再使用当前显示器 DPI 转换。
- 跨屏拖动完成后重建捕获目标并更新缩放。
- 持久化显示器设备标识、归一化位置和上次 DPI。
- 原显示器不存在时，把小组件恢复到主显示器右下安全区域。

## 8. 视觉设计

### 8.1 静态构图

- 中央为无纹理纯黑事件视界。
- 事件视界外沿存在细窄白金色光子环。
- 吸积盘以约 8° 倾角横穿黑洞前方。
- 后方吸积盘光线经引力透镜后向黑洞上方和下方弯曲，构成完整 360° 轮廓。
- 主色为白金、暖橙和少量冷灰，不使用霓虹紫蓝作为主色。
- 黑洞外围完全透明，不绘制矩形面板或窗口边框。

### 8.2 动画

空闲状态：

- 吸积盘缓慢旋转；
- 粒子沿盘面流动；
- 光子环进行低幅度呼吸；
- 背景扭曲保持稳定，不主动吸引远处对象。

靠近状态：

- 影响强度由对象到事件视界中心的距离连续计算；
- 背景采样位置朝切向和径向偏移；
- 真实系统光标保持可见以保证指向精度，其周围的视觉回声出现轻微弯曲和拖尾；
- 拖拽对象视觉替身逐渐拉伸并朝盘面切向偏转；
- 对象离开后在 180 毫秒内使用缓出曲线恢复。

待投放状态：

- 仅当拖拽热点进入事件视界时激活；
- 光子环收紧并提高亮度；
- 吸积盘速度与粒子密度提升；
- Drop Effect 显示为允许移动到回收站。

吞噬状态：

- 回收操作确认成功后播放约 350 毫秒收束动画；
- 对象视觉替身沿螺旋曲线缩小并消失；
- 黑洞短暂增强亮度后恢复空闲状态。

## 9. 交互状态机

```text
Idle
 ├─ mouse enters influence radius ─> PointerInfluenced
 ├─ valid shell drag enters ───────> DragInfluenced
 └─ center mouse down ─────────────> MovingWidget

PointerInfluenced
 └─ pointer leaves ────────────────> Idle

DragInfluenced
 ├─ hotspot enters event horizon ──> DropArmed
 ├─ drag leaves ───────────────────> Idle
 └─ validation fails ──────────────> DragRejected

DropArmed
 ├─ hotspot leaves center ─────────> DragInfluenced
 └─ drop ──────────────────────────> Recycling

Recycling
 ├─ all items succeeded ───────────> Consuming -> Idle
 ├─ partial success ───────────────> PartialFailure -> Idle
 └─ all items failed ──────────────> Failure -> Idle
```

规则：

- 文件拖拽和移动小组件由不同输入通道触发，不互相抢占。
- `Drop` 发生时先执行回收操作，成功后再播放成功吞噬动画。
- 回收操作执行中不接受第二批投放。
- 批量操作允许 Windows Shell 出现部分成功；只为成功对象播放吞噬，失败对象保留原位置。
- 用户要求的不显示成功提示和撤销提示不影响失败反馈。失败使用短暂红色光子环和低强度排斥动画表达，不弹成功类消息。

## 10. 回收站操作

### 10.1 接受对象

- 本地普通文件；
- 本地目录；
- `.lnk` 快捷方式本身；
- Windows Shell 提供的多个文件系统路径；
- Shell 能以回收语义处理的本地同步盘占位文件。

### 10.2 拒绝对象

- “此电脑”、控制面板、库、回收站等虚拟 Shell 对象；
- 驱动器根目录和卷对象；
- UNC 网络路径及无法保证进入回收站的远程位置；
- 空数据对象；
- 在回收操作开始前已不存在的路径；
- 当前正处于回收操作中的重复批次。

### 10.3 执行规则

调用 `IFileOperation`，启用：

- `FOFX_RECYCLEONDELETE`；
- `FOF_ALLOWUNDO`；
- `FOF_NOCONFIRMATION`；
- `FOF_NOERRORUI`。

使用 `IFileOperationProgressSink` 获取逐项结果。只有 `PerformOperations` 完成且 `GetAnyOperationsAborted` 为 false、对应项目回调成功时，项目才进入成功动画。任何 HRESULT 失败均记录到本地诊断日志，但不展示完整文件路径以外的敏感内容，也不发往网络。

## 11. 背景捕获与防递归

- 使用 Windows Graphics Capture 捕获当前显示器。
- 每帧只裁剪黑洞窗口对应区域，不处理全屏输出纹理。
- 对小组件窗口设置捕获排除策略，避免捕获到自身后形成递归画面。
- 捕获排除不可用时，关闭背景捕获并降级为透明背景上的黑洞渲染。
- DRM、受保护内容、远程桌面和系统安全桌面可能不可捕获；这些区域不承诺扭曲，但不影响鼠标交互和回收功能。
- 应用不保存、编码或上传任何捕获帧。

## 12. 自适应画质

画质控制器使用滚动窗口统计 CPU 与 GPU 帧耗时。

| 等级 | 捕获比例 | 活跃刷新率 | 粒子与采样 |
| --- | --- | --- | --- |
| High | 100% | 60 FPS | 完整 |
| Medium | 75% | 45 FPS | 中等 |
| Low | 50% | 30 FPS | 精简 |
| Fallback | 关闭背景捕获 | 30 FPS | 仅黑洞与交互替身 |

默认使用 `Auto`：

- 空闲动画目标 30 FPS；
- 鼠标或文件进入影响区时目标 60 FPS；
- 连续 120 帧无法达到当前目标时降低一级；
- 连续 600 帧留有至少 25% 帧预算时提高一级；
- 升降级使用滞回，避免频繁切换；
- 用户手动选择固定等级时不自动升级，但设备丢失仍可进入 Fallback。

## 13. 设置与右键菜单

右键菜单只包含：

- Visual Quality：Auto、Low、Medium、High；
- Size：Compact、Balanced、Cinematic；
- Sound：On/Off；
- Open Recycle Bin；
- Reset Position；
- Exit。

默认值：

- Visual Quality：Auto；
- Size：Cinematic；
- Sound：On，使用低音量、短时吞噬音效；
- Always On Top：固定开启，不暴露关闭选项；
- Auto Start：不存在该选项。

设置保存在 `%LOCALAPPDATA%\WindowsBlackHole\settings.json`。文件损坏时使用默认值并保留损坏文件的备份，不阻止应用启动。

## 14. 错误处理

| 场景 | 行为 |
| --- | --- |
| D3D 设备丢失 | 暂停动画，重建资源；失败则进入 Fallback |
| 屏幕捕获失败 | 关闭背景扭曲，保留黑洞与交互 |
| 无效拖拽对象 | 红色排斥效果，Drop Effect 为禁止 |
| 权限不足 | 不请求管理员权限；回收失败并显示失败动画 |
| 文件被占用 | 以 Shell 返回结果为准，不永久删除 |
| 批次部分成功 | 成功对象吞噬，失败对象排斥，写入诊断日志 |
| 设置文件损坏 | 备份后恢复默认设置 |
| 显示器移除 | 移到主显示器安全区域并重建捕获 |
| 原生模块初始化失败 | 显示静态黑洞错误状态并允许退出，不启用删除区 |

关键安全约束：渲染模块未达到可用状态时不得启用删除命中区，避免用户在没有明确视觉反馈时投放文件。

## 15. 安全与隐私

- 不申请管理员权限；
- 不安装服务、驱动、Shell Extension 或全局输入钩子；
- 不创建开机启动注册表项、启动文件夹快捷方式或计划任务；
- 不访问网络；
- 不保存屏幕捕获内容；
- 不收集遥测；
- 日志保存在本机，并限制滚动大小；
- 删除只通过带回收语义的 `IFileOperation` 执行；
- 网络路径和虚拟对象在进入执行层前拒绝。

## 16. 打包与运行

- 目标：Windows 11 x64；
- 框架：.NET 8、Windows App SDK、C++20；
- 包含所需运行时的自包含 x64 MSIX 安装包；
- 安装包不声明 Startup Task；
- 应用安装后只能由开始菜单、桌面快捷方式或可执行文件手动启动；
- 首期不发布 Microsoft Store，先提供本地安装与测试包。

## 17. 测试策略

### 17.1 单元测试

- 距离、影响强度和缓动曲线；
- 状态机合法转换与并发投放拒绝；
- DPI/物理像素转换；
- 多显示器位置恢复；
- 路径规范化和对象分类；
- 画质升降级滞回；
- 设置损坏恢复；
- 回收服务通过可替换后端验证“永不永久删除”。

### 17.2 渲染测试

- 离屏渲染固定输入并与容差范围内的基准图比较；
- 验证完整事件视界、前后吸积盘和上下透镜结构；
- 验证距离为零、边界和范围外时 Shader 不产生 NaN；
- 模拟设备丢失和捕获尺寸变化；
- 使用 GPU 调试层检查资源泄漏和非法绑定。

### 17.3 集成测试

- 创建临时测试文件、目录和快捷方式，投放后验证原路径消失且回收站存在对应对象；
- 多选批次全部成功、全部失败和部分成功；
- 权限不足、文件消失、网络路径、驱动器根和虚拟对象；
- `100%`、`150%`、`200%` DPI；
- 单显示器和多显示器跨屏移动；
- 始终置顶、非激活和窗口拖动；
- 安装后检查注册表、启动目录和计划任务中不存在自动启动项。

真实回收站集成测试必须使用唯一前缀的临时对象，并在测试结束后恢复或清理这些对象；不得操作用户现有文件。

## 18. 验收标准

发布候选版本必须同时满足：

1. 用户手动启动后出现完整写实黑洞，默认电影感尺寸且始终置顶。
2. 黑洞可通过中央区域拖动并在重启后恢复位置。
3. 鼠标进入影响区时，背景和光标视觉回声连续扭曲；真实光标保持可见，离开后视觉回声平滑恢复。
4. 文件、目录、快捷方式及多选对象进入影响区时显示逐渐增强的吸引效果。
5. 只有中央事件视界允许投放，外围松开不执行删除。
6. 成功投放的对象可在 Windows 回收站中找到。
7. 无任何执行路径使用永久删除作为回收失败的替代方案。
8. 无效、网络、虚拟或权限不足对象不会播放成功动画。
9. 批量部分成功时，视觉结果与实际 Shell 结果一致。
10. 捕获或 GPU 失败时自动降级，删除功能只在视觉反馈可用时开放。
11. 在三种规定 DPI 和多显示器场景中命中范围正确。
12. 安装包和首次运行不创建任何开机启动机制。
13. 应用无网络请求、遥测或捕获帧落盘。

## 19. 官方技术依据

- Windows App SDK AppWindow：
  <https://learn.microsoft.com/en-us/windows/apps/develop/ui/manage-app-windows>
- Windows Graphics Capture：
  <https://learn.microsoft.com/en-us/uwp/api/windows.graphics.capture>
- Direct2D Effects 与自定义 Shader：
  <https://learn.microsoft.com/en-us/windows/win32/direct2d/effects-overview>
- DirectX 图形组合：
  <https://learn.microsoft.com/en-us/windows/win32/getting-started-with-directx-graphics>
- DirectComposition Window Target：
  <https://learn.microsoft.com/en-us/windows/win32/api/dcomp/nf-dcomp-idcompositiondesktopdevice-createtargetforhwnd>
- Composition Swap Chain：
  <https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgifactory2-createswapchainforcomposition>
- IFileOperation 回收站标志：
  <https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags>
