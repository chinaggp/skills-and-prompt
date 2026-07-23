# Windows Black Hole MVP

Windows 11 桌面黑洞小组件的最小可运行版本。

## 已实现

- 透明、无边框、始终置顶的黑洞窗口
- 鼠标或文件靠近时，吸积盘产生旋转、拉伸与偏斜效果
- 按住黑洞中心可拖动小组件
- 将本地文件或文件夹放进黑洞中心后，通过 Windows Shell 移入回收站
- 不注册开机启动
- 禁止磁盘根目录、系统顶层目录、用户顶层目录、网络路径和目录链接

## 运行

```powershell
dotnet run --project .\src\WindowsBlackHole.App\WindowsBlackHole.App.csproj
```

## 验证

```powershell
dotnet build .\src\WindowsBlackHole.App\WindowsBlackHole.App.csproj -c Release
dotnet run --project .\tests\WindowsBlackHole.Core.Tests\WindowsBlackHole.Core.Tests.csproj -c Release
```

显式验证 Windows 回收站集成（会在回收站中留下一个名称以
`WindowsBlackHole-Smoke-` 开头的临时测试目录）：

```powershell
dotnet run --project .\tests\WindowsBlackHole.Core.Tests\WindowsBlackHole.Core.Tests.csproj -c Release -- --recycle-smoke
```

## MVP 边界

当前的“扭曲”作用于黑洞自身的吸积盘视觉。真实桌面背景采集、DirectX
着色器引力透镜、托盘设置、位置持久化和安装包属于后续增强版。
