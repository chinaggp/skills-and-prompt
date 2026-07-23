# Windows Black Hole Desktop Widget Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a manually launched Windows 11 x64 desktop black-hole widget that distorts nearby desktop content and moves dropped local filesystem objects to the Windows Recycle Bin without any permanent-delete fallback.

**Architecture:** A C#/.NET 8 WinUI 3 controller owns application lifecycle, settings, interaction state, OLE drop registration, and recycle orchestration. A C++20 DLL owns the transparent Win32 overlay HWND, DirectComposition/D3D11 rendering, Windows Graphics Capture, HLSL effects, and the native `IFileOperation` bridge; managed/native communication is limited to fixed-width C ABI structs and callbacks.

**Tech Stack:** .NET SDK 8.0.423, C# 12, Windows App SDK 2.3.1, C++20/MSVC, Windows SDK, Direct3D 11, DirectComposition, Windows Graphics Capture, HLSL Shader Model 5.0, MSTest 4.3.2, CMake/CTest, MSIX.

## Global Constraints

- Target Windows 11 x64 only; target framework is `net8.0-windows10.0.22621.0`.
- The app is launched manually and must not create a Startup Task, Run registry value, Startup-folder shortcut, scheduled task, service, driver, Shell Extension, or global input hook.
- The overlay is always topmost, non-activating, absent from Alt+Tab, and movable by left-dragging the event horizon.
- Cinematic is the default size: visual body diameter 320 logical pixels, influence radius 240 logical pixels, event-horizon/drop-zone diameter 128 logical pixels, and overlay bounds 512 × 512 logical pixels.
- Accept local files, folders, `.lnk` shortcuts, and multi-selection; reject drive roots, UNC/network paths, virtual Shell objects, missing paths, and duplicate in-flight batches.
- Recycle through `IFileOperation` using `FOFX_RECYCLEONDELETE | FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI`; never fall back to permanent deletion.
- Do not show success, undo, or confirmation UI; failures use only the red rejection animation and local diagnostics.
- Default quality is Auto: idle 30 FPS, active target 60 FPS, degrade after 120 over-budget frames, and upgrade after 600 frames with at least 25% headroom.
- Do not request administrator rights, access the network at runtime, save capture frames, or collect telemetry.
- Preserve unrelated repository changes and never stage `skills/spec-master.zip`.
- Design source: `docs/superpowers/specs/2026-07-23-windows-black-hole-desktop-widget-design.md`.

## Current Environment Evidence

As of 2026-07-23, `dotnet --info` reports only .NET 6 runtimes and no SDK; `vswhere.exe`, Visual Studio 2022, and the Windows SDK include directory are absent. Task 1 installs and verifies the required local toolchain before any project file is generated.

## Primary References

- .NET 8.0.423 SDK: <https://dotnet.microsoft.com/en-us/download/dotnet/8.0>
- Windows App SDK stable downloads: <https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads>
- Unpackaged self-contained WinUI 3: <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app>
- DirectComposition HWND target: <https://learn.microsoft.com/en-us/windows/win32/api/dcomp/nf-dcomp-idcompositiondesktopdevice-createtargetforhwnd>
- Composition swap chain: <https://learn.microsoft.com/en-us/windows/win32/api/dxgi1_2/nf-dxgi1_2-idxgifactory2-createswapchainforcomposition>
- Windows Graphics Capture desktop interop: <https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nn-windows-graphics-capture-interop-igraphicscaptureiteminterop>
- Capture exclusion: <https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity>
- Recycle flags: <https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags>

## Locked File Structure

```text
plugins/windows-black-hole/
├── WindowsBlackHole.sln
├── global.json
├── Directory.Build.props
├── Directory.Packages.props
├── build.ps1
├── README.md
├── assets/
│   ├── shaders/
│   │   ├── BlackHole.hlsl
│   │   └── GravitationalLens.hlsl
│   └── sounds/
│       ├── generate-consume.ps1
│       └── consume.wav
├── src/
│   ├── WindowsBlackHole.Core/
│   │   ├── Geometry/
│   │   ├── Interaction/
│   │   ├── Quality/
│   │   ├── Recycle/
│   │   └── Settings/
│   ├── WindowsBlackHole.Contracts/
│   │   └── Native/
│   ├── WindowsBlackHole.App/
│   │   ├── DragDrop/
│   │   ├── Native/
│   │   ├── Recycle/
│   │   ├── Shell/
│   │   └── ViewModels/
│   └── WindowsBlackHole.Renderer/
│       ├── CMakeLists.txt
│       ├── include/
│       ├── src/
│       │   ├── capture/
│       │   ├── graphics/
│       │   ├── overlay/
│       │   └── shell/
│       └── tests/
├── tests/
│   ├── WindowsBlackHole.Core.Tests/
│   ├── WindowsBlackHole.App.Tests/
│   └── WindowsBlackHole.IntegrationTests/
└── packaging/
    └── WindowsBlackHole.Package/
        ├── Assets/
        ├── build-package.ps1
        ├── generate-assets.ps1
        ├── Package.appxmanifest
        └── WindowsBlackHole.Package.wapproj
```

---

### Task 1: Install the Windows toolchain and bootstrap a reproducible solution

**Files:**
- Create: `plugins/windows-black-hole/global.json`
- Create: `plugins/windows-black-hole/Directory.Build.props`
- Create: `plugins/windows-black-hole/Directory.Packages.props`
- Create: `plugins/windows-black-hole/build.ps1`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/CMakeLists.txt`
- Create: generated `plugins/windows-black-hole/WindowsBlackHole.sln`
- Create: generated Core, Contracts, App, and test project files

**Interfaces:**
- Consumes: no earlier task.
- Produces: `build.ps1 -Configuration <Debug|Release> [-RunTests]`, central package versions, solution/project graph, native CMake build directory.

- [ ] **Step 1: Install the exact .NET SDK and Visual Studio workloads**

Run from an elevated PowerShell after approval:

```powershell
winget install --exact --id Microsoft.DotNet.SDK.8 --version 8.0.423 --accept-package-agreements --accept-source-agreements
winget install --exact --id Microsoft.VisualStudio.2022.Community --accept-package-agreements --accept-source-agreements --override "--wait --passive --add Microsoft.VisualStudio.Workload.ManagedDesktop --add Microsoft.VisualStudio.Workload.NativeDesktop --add Microsoft.VisualStudio.Component.Windows11SDK.26100 --includeRecommended"
```

Expected: both commands exit `0`; Visual Studio Installer lists “.NET desktop development”, “Desktop development with C++”, CMake tools, and a Windows 11 SDK.

- [ ] **Step 2: Verify the toolchain in a Developer PowerShell**

Run:

```powershell
dotnet --version
& "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\Common7\Tools\VsDevCmd.bat" -arch=x64 -host_arch=x64
cl
cmake --version
Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\Include" -Directory
```

Expected: `dotnet --version` prints `8.0.423`; `cl` reports an x64 MSVC compiler; CMake and at least one Windows SDK include directory are present.

- [ ] **Step 3: Create the solution and project skeleton**

Run:

```powershell
$root = "plugins\windows-black-hole"
New-Item -ItemType Directory -Force "$root\src", "$root\tests", "$root\assets\shaders", "$root\assets\textures", "$root\assets\sounds", "$root\packaging" | Out-Null
dotnet new sln -n WindowsBlackHole -o $root
dotnet new classlib -n WindowsBlackHole.Core -f net8.0 -o "$root\src\WindowsBlackHole.Core"
dotnet new classlib -n WindowsBlackHole.Contracts -f net8.0 -o "$root\src\WindowsBlackHole.Contracts"
dotnet new mstest -n WindowsBlackHole.Core.Tests -f net8.0 -o "$root\tests\WindowsBlackHole.Core.Tests"
dotnet new mstest -n WindowsBlackHole.App.Tests -f net8.0 -o "$root\tests\WindowsBlackHole.App.Tests"
dotnet new mstest -n WindowsBlackHole.IntegrationTests -f net8.0-windows10.0.22621.0 -o "$root\tests\WindowsBlackHole.IntegrationTests"
dotnet sln "$root\WindowsBlackHole.sln" add "$root\src\WindowsBlackHole.Core\WindowsBlackHole.Core.csproj" "$root\src\WindowsBlackHole.Contracts\WindowsBlackHole.Contracts.csproj" "$root\tests\WindowsBlackHole.Core.Tests\WindowsBlackHole.Core.Tests.csproj" "$root\tests\WindowsBlackHole.App.Tests\WindowsBlackHole.App.Tests.csproj" "$root\tests\WindowsBlackHole.IntegrationTests\WindowsBlackHole.IntegrationTests.csproj"
dotnet add "$root\tests\WindowsBlackHole.Core.Tests\WindowsBlackHole.Core.Tests.csproj" reference "$root\src\WindowsBlackHole.Core\WindowsBlackHole.Core.csproj"
dotnet add "$root\tests\WindowsBlackHole.App.Tests\WindowsBlackHole.App.Tests.csproj" reference "$root\src\WindowsBlackHole.Core\WindowsBlackHole.Core.csproj" "$root\src\WindowsBlackHole.Contracts\WindowsBlackHole.Contracts.csproj"
```

Expected: the solution contains five projects and all commands exit `0`.

- [ ] **Step 4: Pin SDK, compiler policy, and packages**

Create `global.json`:

```json
{
  "sdk": {
    "version": "8.0.423",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  }
}
```

Create `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>12.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Platforms>x64</Platforms>
    <PlatformTarget>x64</PlatformTarget>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  </PropertyGroup>
</Project>
```

Create `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.WindowsAppSDK" Version="2.3.1" />
    <PackageVersion Include="Microsoft.Windows.CsWin32" Version="0.3.298" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="MSTest.TestAdapter" Version="4.3.2" />
    <PackageVersion Include="MSTest.TestFramework" Version="4.3.2" />
  </ItemGroup>
</Project>
```

Update every test project to use central versions:

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.NET.Test.Sdk" />
  <PackageReference Include="MSTest.TestAdapter" />
  <PackageReference Include="MSTest.TestFramework" />
</ItemGroup>
```

- [ ] **Step 5: Add the native build root and unified build command**

Create `src/WindowsBlackHole.Renderer/CMakeLists.txt`:

```cmake
cmake_minimum_required(VERSION 3.25)
project(WindowsBlackHoleRenderer LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
enable_testing()
add_library(WindowsBlackHole.Renderer SHARED src/bootstrap.cpp)
target_compile_definitions(WindowsBlackHole.Renderer PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX)
target_compile_options(WindowsBlackHole.Renderer PRIVATE /W4 /WX /permissive-)
```

Create `src/WindowsBlackHole.Renderer/src/bootstrap.cpp`:

```cpp
#include <cstdint>

extern "C" __declspec(dllexport) std::uint32_t wbh_renderer_abi_version() noexcept
{
    return 1U;
}
```

Create `build.ps1`:

```powershell
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$RunTests
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$nativeSource = Join-Path $root "src\WindowsBlackHole.Renderer"
$nativeBuild = Join-Path $root "artifacts\native"

cmake -S $nativeSource -B $nativeBuild -A x64
cmake --build $nativeBuild --config $Configuration
dotnet build (Join-Path $root "WindowsBlackHole.sln") -c $Configuration

if ($RunTests) {
    ctest --test-dir $nativeBuild -C $Configuration --output-on-failure
    dotnet test (Join-Path $root "WindowsBlackHole.sln") -c $Configuration --no-build
}
```

- [ ] **Step 6: Verify the empty solution**

Run:

```powershell
.\plugins\windows-black-hole\build.ps1 -Configuration Debug -RunTests
```

Expected: native DLL builds, CTest reports no failing tests, and all three MSTest projects pass their generated smoke tests.

- [ ] **Step 7: Commit the bootstrap**

```powershell
git add plugins/windows-black-hole
git commit -m "feat: bootstrap Windows black hole solution"
```

---

### Task 2: Implement size metrics and influence geometry with TDD

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Geometry/BlackHoleMetrics.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Geometry/InfluenceMath.cs`
- Create: `plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/Geometry/InfluenceMathTests.cs`

**Interfaces:**
- Consumes: .NET project graph from Task 1.
- Produces: `BlackHoleMetrics.For(BlackHoleSize)`, `InfluenceMath.NormalizedStrength(PointD point, PointD center, double influenceRadius)`, and `InfluenceMath.IsInsideDropZone(PointD point, PointD center, double dropZoneDiameter)`.

- [ ] **Step 1: Write failing geometry tests**

Create `InfluenceMathTests.cs`:

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsBlackHole.Core.Geometry;

namespace WindowsBlackHole.Core.Tests.Geometry;

[TestClass]
public sealed class InfluenceMathTests
{
    [TestMethod]
    public void CinematicMetricsMatchApprovedSpec()
    {
        BlackHoleMetrics metrics = BlackHoleMetrics.For(BlackHoleSize.Cinematic);
        Assert.AreEqual(320d, metrics.VisualDiameter);
        Assert.AreEqual(240d, metrics.InfluenceRadius);
        Assert.AreEqual(128d, metrics.DropZoneDiameter);
        Assert.AreEqual(512d, metrics.WindowDiameter);
    }

    [TestMethod]
    public void StrengthIsOneAtCenterZeroAtBoundaryAndClampedOutside()
    {
        PointD center = new(0, 0);
        Assert.AreEqual(1d, InfluenceMath.NormalizedStrength(center, center, 240d), 0.0001d);
        Assert.AreEqual(0d, InfluenceMath.NormalizedStrength(new PointD(240, 0), center, 240d), 0.0001d);
        Assert.AreEqual(0d, InfluenceMath.NormalizedStrength(new PointD(300, 0), center, 240d), 0.0001d);
    }

    [TestMethod]
    public void DropZoneUsesEventHorizonRadius()
    {
        PointD center = new(100, 100);
        Assert.IsTrue(InfluenceMath.IsInsideDropZone(new PointD(164, 100), center, 128d));
        Assert.IsFalse(InfluenceMath.IsInsideDropZone(new PointD(164.01, 100), center, 128d));
    }
}
```

- [ ] **Step 2: Run the tests and verify failure**

Run:

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/WindowsBlackHole.Core.Tests.csproj --filter FullyQualifiedName~InfluenceMathTests
```

Expected: build fails because `WindowsBlackHole.Core.Geometry` types do not exist.

- [ ] **Step 3: Implement exact metrics and geometry**

Create `BlackHoleMetrics.cs`:

```csharp
namespace WindowsBlackHole.Core.Geometry;

public enum BlackHoleSize
{
    Compact,
    Balanced,
    Cinematic
}

public readonly record struct PointD(double X, double Y);

public readonly record struct BlackHoleMetrics(
    double VisualDiameter,
    double InfluenceRadius,
    double DropZoneDiameter,
    double SamplingMargin)
{
    public double WindowDiameter => (InfluenceRadius * 2d) + (SamplingMargin * 2d);

    public static BlackHoleMetrics For(BlackHoleSize size) => size switch
    {
        BlackHoleSize.Compact => new(160d, 120d, 64d, 16d),
        BlackHoleSize.Balanced => new(240d, 180d, 96d, 16d),
        BlackHoleSize.Cinematic => new(320d, 240d, 128d, 16d),
        _ => throw new ArgumentOutOfRangeException(nameof(size))
    };
}
```

Create `InfluenceMath.cs`:

```csharp
namespace WindowsBlackHole.Core.Geometry;

public static class InfluenceMath
{
    public static double NormalizedStrength(PointD point, PointD center, double influenceRadius)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(influenceRadius);
        double distance = Distance(point, center);
        double linear = Math.Clamp(1d - (distance / influenceRadius), 0d, 1d);
        return linear * linear * (3d - (2d * linear));
    }

    public static bool IsInsideDropZone(PointD point, PointD center, double dropZoneDiameter)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dropZoneDiameter);
        return Distance(point, center) <= dropZoneDiameter / 2d;
    }

    private static double Distance(PointD a, PointD b)
    {
        double x = a.X - b.X;
        double y = a.Y - b.Y;
        return Math.Sqrt((x * x) + (y * y));
    }
}
```

- [ ] **Step 4: Run tests**

Run:

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/WindowsBlackHole.Core.Tests.csproj --filter FullyQualifiedName~InfluenceMathTests
```

Expected: 3 tests pass.

- [ ] **Step 5: Commit**

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Core/Geometry plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/Geometry
git commit -m "feat: add black hole geometry metrics"
```

---

### Task 3: Implement the interaction state machine

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Interaction/InteractionState.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Interaction/InteractionStateMachine.cs`
- Create: `plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/Interaction/InteractionStateMachineTests.cs`

**Interfaces:**
- Consumes: approved interaction states from the spec.
- Produces: `InteractionStateMachine.Handle(InteractionEvent)` and immutable `InteractionTransition`.

- [ ] **Step 1: Write failing state transition tests**

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsBlackHole.Core.Interaction;

namespace WindowsBlackHole.Core.Tests.Interaction;

[TestClass]
public sealed class InteractionStateMachineTests
{
    [TestMethod]
    public void ValidDropRecyclesBeforeConsuming()
    {
        InteractionStateMachine machine = new();
        machine.Handle(InteractionEvent.ValidDragEntered);
        machine.Handle(InteractionEvent.DropZoneEntered);
        Assert.AreEqual(InteractionState.Recycling, machine.Handle(InteractionEvent.DropRequested).Current);
        Assert.AreEqual(InteractionState.Consuming, machine.Handle(InteractionEvent.RecycleAllSucceeded).Current);
        Assert.AreEqual(InteractionState.Idle, machine.Handle(InteractionEvent.AnimationCompleted).Current);
    }

    [TestMethod]
    public void InvalidDragCannotBecomeArmed()
    {
        InteractionStateMachine machine = new();
        Assert.AreEqual(InteractionState.DragRejected, machine.Handle(InteractionEvent.InvalidDragEntered).Current);
        Assert.ThrowsException<InvalidOperationException>(() => machine.Handle(InteractionEvent.DropZoneEntered));
    }

    [TestMethod]
    public void SecondDropIsRejectedWhileRecycling()
    {
        InteractionStateMachine machine = new();
        machine.Handle(InteractionEvent.ValidDragEntered);
        machine.Handle(InteractionEvent.DropZoneEntered);
        machine.Handle(InteractionEvent.DropRequested);
        Assert.ThrowsException<InvalidOperationException>(() => machine.Handle(InteractionEvent.DropRequested));
    }
}
```

- [ ] **Step 2: Verify tests fail**

Run:

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/WindowsBlackHole.Core.Tests.csproj --filter FullyQualifiedName~InteractionStateMachineTests
```

Expected: build fails because interaction types do not exist.

- [ ] **Step 3: Implement the complete transition table**

Create `InteractionState.cs`:

```csharp
namespace WindowsBlackHole.Core.Interaction;

public enum InteractionState
{
    Idle,
    PointerInfluenced,
    DragInfluenced,
    DropArmed,
    DragRejected,
    MovingWidget,
    Recycling,
    Consuming,
    PartialFailure,
    Failure
}

public enum InteractionEvent
{
    PointerEntered,
    PointerLeft,
    ValidDragEntered,
    InvalidDragEntered,
    DragLeft,
    DropZoneEntered,
    DropZoneLeft,
    DropRequested,
    RecycleAllSucceeded,
    RecyclePartiallySucceeded,
    RecycleFailed,
    CenterPointerPressed,
    PointerReleased,
    AnimationCompleted
}

public readonly record struct InteractionTransition(InteractionState Previous, InteractionState Current);
```

Create `InteractionStateMachine.cs`:

```csharp
namespace WindowsBlackHole.Core.Interaction;

public sealed class InteractionStateMachine
{
    public InteractionState State { get; private set; } = InteractionState.Idle;

    public InteractionTransition Handle(InteractionEvent interactionEvent)
    {
        InteractionState previous = State;
        State = (State, interactionEvent) switch
        {
            (InteractionState.Idle, InteractionEvent.PointerEntered) => InteractionState.PointerInfluenced,
            (InteractionState.PointerInfluenced, InteractionEvent.PointerLeft) => InteractionState.Idle,
            (InteractionState.Idle, InteractionEvent.ValidDragEntered) => InteractionState.DragInfluenced,
            (InteractionState.PointerInfluenced, InteractionEvent.ValidDragEntered) => InteractionState.DragInfluenced,
            (InteractionState.Idle, InteractionEvent.InvalidDragEntered) => InteractionState.DragRejected,
            (InteractionState.PointerInfluenced, InteractionEvent.InvalidDragEntered) => InteractionState.DragRejected,
            (InteractionState.DragInfluenced, InteractionEvent.DropZoneEntered) => InteractionState.DropArmed,
            (InteractionState.DropArmed, InteractionEvent.DropZoneLeft) => InteractionState.DragInfluenced,
            (InteractionState.DragInfluenced, InteractionEvent.DragLeft) => InteractionState.Idle,
            (InteractionState.DropArmed, InteractionEvent.DragLeft) => InteractionState.Idle,
            (InteractionState.DragRejected, InteractionEvent.DragLeft) => InteractionState.Idle,
            (InteractionState.DropArmed, InteractionEvent.DropRequested) => InteractionState.Recycling,
            (InteractionState.Recycling, InteractionEvent.RecycleAllSucceeded) => InteractionState.Consuming,
            (InteractionState.Recycling, InteractionEvent.RecyclePartiallySucceeded) => InteractionState.PartialFailure,
            (InteractionState.Recycling, InteractionEvent.RecycleFailed) => InteractionState.Failure,
            (InteractionState.Consuming, InteractionEvent.AnimationCompleted) => InteractionState.Idle,
            (InteractionState.PartialFailure, InteractionEvent.AnimationCompleted) => InteractionState.Idle,
            (InteractionState.Failure, InteractionEvent.AnimationCompleted) => InteractionState.Idle,
            (InteractionState.Idle, InteractionEvent.CenterPointerPressed) => InteractionState.MovingWidget,
            (InteractionState.PointerInfluenced, InteractionEvent.CenterPointerPressed) => InteractionState.MovingWidget,
            (InteractionState.MovingWidget, InteractionEvent.PointerReleased) => InteractionState.Idle,
            _ => throw new InvalidOperationException($"Event {interactionEvent} is invalid while state is {State}.")
        };
        return new(previous, State);
    }
}
```

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/WindowsBlackHole.Core.Tests.csproj --filter FullyQualifiedName~InteractionStateMachineTests
```

Expected: 3 tests pass.

Commit:

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Core/Interaction plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/Interaction
git commit -m "feat: add black hole interaction state machine"
```

---

### Task 4: Implement settings, monitor-safe placement, and adaptive quality

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Settings/WidgetSettings.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Settings/SettingsStore.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Settings/PlacementPolicy.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Quality/QualityController.cs`
- Create: matching tests under `plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/Settings/` and `Quality/`

**Interfaces:**
- Consumes: `BlackHoleSize`.
- Produces: JSON settings schema version 1, `PlacementPolicy.Clamp`, and `QualityController.RecordFrame`.

- [ ] **Step 1: Write failing tests for defaults, corrupt JSON, clamping, and hysteresis**

```csharp
[TestMethod]
public async Task MissingSettingsUseApprovedDefaults()
{
    InMemorySettingsFile file = new(null);
    WidgetSettings settings = await new SettingsStore(file).LoadAsync(CancellationToken.None);
    Assert.AreEqual(BlackHoleSize.Cinematic, settings.Size);
    Assert.AreEqual(QualityPreference.Auto, settings.Quality);
    Assert.IsTrue(settings.SoundEnabled);
}

[TestMethod]
public void PlacementIsClampedInsideWorkArea()
{
    RectD result = PlacementPolicy.Clamp(new RectD(1900, 1050, 512, 512), new RectD(0, 0, 1920, 1040));
    Assert.AreEqual(new RectD(1408, 528, 512, 512), result);
}

[TestMethod]
public void AutoQualityDegradesAt120AndUpgradesAt600()
{
    QualityController controller = new(RenderQuality.High);
    for (int i = 0; i < 119; i++) controller.RecordFrame(TimeSpan.FromMilliseconds(20), true);
    Assert.AreEqual(RenderQuality.High, controller.Current);
    controller.RecordFrame(TimeSpan.FromMilliseconds(20), true);
    Assert.AreEqual(RenderQuality.Medium, controller.Current);
    for (int i = 0; i < 600; i++) controller.RecordFrame(TimeSpan.FromMilliseconds(10), true);
    Assert.AreEqual(RenderQuality.High, controller.Current);
}

private sealed class InMemorySettingsFile(string? content) : ISettingsFile
{
    public string? Content { get; private set; } = content;
    public string? Backup { get; private set; }

    public Task<string?> ReadAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Content);

    public Task WriteAsync(string newContent, CancellationToken cancellationToken)
    {
        Content = newContent;
        return Task.CompletedTask;
    }

    public Task BackupCorruptAsync(string corruptContent, CancellationToken cancellationToken)
    {
        Backup = corruptContent;
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 2: Verify the tests fail**

Run:

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/WindowsBlackHole.Core.Tests.csproj --filter "FullyQualifiedName~Settings|FullyQualifiedName~Quality"
```

Expected: build fails for missing settings and quality types.

- [ ] **Step 3: Implement the public models and exact policies**

Use these signatures:

```csharp
public enum QualityPreference { Auto, Low, Medium, High }
public enum RenderQuality { Fallback, Low, Medium, High }
public readonly record struct RectD(double X, double Y, double Width, double Height);
public sealed record WidgetSettings(
    int SchemaVersion,
    BlackHoleSize Size,
    QualityPreference Quality,
    bool SoundEnabled,
    string? MonitorDeviceName,
    double NormalizedX,
    double NormalizedY)
{
    public static WidgetSettings Default { get; } =
        new(1, BlackHoleSize.Cinematic, QualityPreference.Auto, true, null, 0.9d, 0.85d);
}
```

Implement `PlacementPolicy.Clamp` using:

```csharp
public static RectD Clamp(RectD requested, RectD workArea)
{
    double x = Math.Clamp(requested.X, workArea.X, workArea.X + workArea.Width - requested.Width);
    double y = Math.Clamp(requested.Y, workArea.Y, workArea.Y + workArea.Height - requested.Height);
    return new(x, y, requested.Width, requested.Height);
}
```

Implement `QualityController` with counters `overBudgetFrames` and `headroomFrames`; active budget is `16.6667 ms`, idle budget is `33.3333 ms`; degrade after exactly 120 consecutive over-budget frames and upgrade after exactly 600 consecutive frames at or below 75% of budget. A quality change resets both counters.

Implement `SettingsStore` over:

```csharp
public interface ISettingsFile
{
    Task<string?> ReadAsync(CancellationToken cancellationToken);
    Task WriteAsync(string content, CancellationToken cancellationToken);
    Task BackupCorruptAsync(string content, CancellationToken cancellationToken);
}
```

Use `System.Text.Json` with camel-case property names. On `JsonException`, call `BackupCorruptAsync`, return `WidgetSettings.Default`, and write the default JSON.

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/WindowsBlackHole.Core.Tests.csproj
```

Expected: all Core tests pass.

Commit:

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Core/Settings plugins/windows-black-hole/src/WindowsBlackHole.Core/Quality plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests
git commit -m "feat: add widget settings and adaptive quality policy"
```

---

### Task 5: Create the native overlay HWND and stable renderer ABI

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/include/wbh_renderer_api.h`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/overlay/overlay_window.h`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/overlay/overlay_window.cpp`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/renderer_api.cpp`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/tests/overlay_window_tests.cpp`
- Modify: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/CMakeLists.txt`

**Interfaces:**
- Consumes: size metrics passed as physical pixels.
- Produces: ABI version 1, opaque `WbhRendererHandle`, overlay HWND, move/resize/tick/destroy exports.

- [ ] **Step 1: Write the native overlay contract test**

```cpp
#include "wbh_renderer_api.h"
#include <Windows.h>
#include <cassert>

int wmain()
{
    WbhRendererConfig config{512U, 512U, 1.0F};
    WbhRendererHandle* handle = nullptr;
    assert(wbh_renderer_create(&config, &handle) == WBH_OK);
    HWND hwnd = wbh_renderer_get_hwnd(handle);
    assert(hwnd != nullptr);
    assert((GetWindowLongPtrW(hwnd, GWL_STYLE) & WS_POPUP) != 0);
    const LONG_PTR ex = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
    assert((ex & WS_EX_TOOLWINDOW) != 0);
    assert((ex & WS_EX_NOACTIVATE) != 0);
    assert((ex & WS_EX_NOREDIRECTIONBITMAP) != 0);
    assert(GetWindow(hwnd, GW_HWNDPREV) == nullptr || IsWindow(hwnd));
    wbh_renderer_destroy(handle);
    return 0;
}
```

- [ ] **Step 2: Add the ABI header**

```cpp
#pragma once
#include <cstdint>
#include <Windows.h>

#if defined(WBH_RENDERER_EXPORTS)
#define WBH_API extern "C" __declspec(dllexport)
#else
#define WBH_API extern "C" __declspec(dllimport)
#endif

enum WbhResult : std::int32_t
{
    WBH_OK = 0,
    WBH_INVALID_ARGUMENT = 1,
    WBH_WINDOW_CREATION_FAILED = 2,
    WBH_GRAPHICS_INITIALIZATION_FAILED = 3,
    WBH_DEVICE_LOST = 4
};

enum WbhRenderQuality : std::uint32_t
{
    WBH_QUALITY_FALLBACK = 0,
    WBH_QUALITY_LOW = 1,
    WBH_QUALITY_MEDIUM = 2,
    WBH_QUALITY_HIGH = 3
};

enum WbhInteractionFlags : std::uint32_t
{
    WBH_POINTER_INFLUENCED = 1U << 0U,
    WBH_DRAG_INFLUENCED = 1U << 1U,
    WBH_DROP_ARMED = 1U << 2U,
    WBH_REJECTED = 1U << 3U,
    WBH_CONSUMING = 1U << 4U,
    WBH_FAILURE = 1U << 5U
};

struct WbhRendererConfig
{
    std::uint32_t width;
    std::uint32_t height;
    float dpi_scale;
};

struct WbhInteractionFrame
{
    float pointer_x;
    float pointer_y;
    float drag_x;
    float drag_y;
    float pointer_strength;
    float drag_strength;
    float delta_seconds;
    std::uint32_t flags;
};

struct WbhRendererHandle;

WBH_API std::uint32_t wbh_renderer_abi_version() noexcept;
WBH_API WbhResult wbh_renderer_create(const WbhRendererConfig*, WbhRendererHandle**) noexcept;
WBH_API HWND wbh_renderer_get_hwnd(WbhRendererHandle*) noexcept;
WBH_API WbhResult wbh_renderer_move(WbhRendererHandle*, std::int32_t x, std::int32_t y) noexcept;
WBH_API WbhResult wbh_renderer_resize(WbhRendererHandle*, const WbhRendererConfig*) noexcept;
WBH_API WbhResult wbh_renderer_set_quality(WbhRendererHandle*, WbhRenderQuality) noexcept;
WBH_API WbhResult wbh_renderer_tick(WbhRendererHandle*, const WbhInteractionFrame*) noexcept;
WBH_API void wbh_renderer_destroy(WbhRendererHandle*) noexcept;
```

- [ ] **Step 3: Implement the overlay window**

`OverlayWindow::Create` must register class name `WindowsBlackHoleOverlay`, call `CreateWindowExW` with `WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP`, use `WS_POPUP`, call `SetWindowPos(hwnd, HWND_TOPMOST, x, y, width, height, SWP_NOACTIVATE | SWP_SHOWWINDOW)`, and call `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)`.

Use this window procedure:

```cpp
LRESULT CALLBACK OverlayWindow::WindowProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    if (message == WM_NCHITTEST)
    {
        RECT rect{};
        GetClientRect(hwnd, &rect);
        POINT point{GET_X_LPARAM(lparam), GET_Y_LPARAM(lparam)};
        ScreenToClient(hwnd, &point);
        const float cx = static_cast<float>(rect.right) * 0.5F;
        const float cy = static_cast<float>(rect.bottom) * 0.5F;
        const float dx = static_cast<float>(point.x) - cx;
        const float dy = static_cast<float>(point.y) - cy;
        const float dpi = static_cast<float>(GetDpiForWindow(hwnd)) / 96.0F;
        const float radius = 64.0F * dpi;
        return ((dx * dx) + (dy * dy) <= radius * radius) ? HTCLIENT : HTTRANSPARENT;
    }
    if (message == WM_MOUSEACTIVATE)
    {
        return MA_NOACTIVATE;
    }
    return DefWindowProcW(hwnd, message, wparam, lparam);
}
```

All exports catch exceptions and return a `WbhResult`; no C++ exception may cross the ABI.

- [ ] **Step 4: Build and run the native test**

Update CMake to link `user32`, `dwmapi`, `dcomp`, `d3d11`, `dxgi`, and add `overlay_window_tests` to CTest.

Run:

```powershell
cmake -S plugins/windows-black-hole/src/WindowsBlackHole.Renderer -B plugins/windows-black-hole/artifacts/native -A x64
cmake --build plugins/windows-black-hole/artifacts/native --config Debug
ctest --test-dir plugins/windows-black-hole/artifacts/native -C Debug --output-on-failure
```

Expected: `overlay_window_tests` passes and no visible taskbar/Alt+Tab entry remains after the process exits.

- [ ] **Step 5: Commit**

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Renderer
git commit -m "feat: add native transparent overlay host"
```

---

### Task 6: Add the managed controller and native interop

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/WindowsBlackHole.App.csproj`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/app.manifest`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/App.xaml`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/App.xaml.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/ControllerWindow.xaml`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/ControllerWindow.xaml.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Contracts/Native/RendererContracts.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Native/NativeRenderer.cs`
- Create: `plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests/Native/NativeLayoutTests.cs`

**Interfaces:**
- Consumes: renderer ABI v1.
- Produces: `INativeRenderer`, `NativeRenderer.Create`, and a hidden WinUI controller that keeps the process message loop alive.

- [ ] **Step 1: Write struct layout tests**

```csharp
[TestMethod]
public void RendererInteropStructsHaveStableAbiSizes()
{
    Assert.AreEqual(12, Marshal.SizeOf<RendererConfig>());
    Assert.AreEqual(32, Marshal.SizeOf<InteractionFrame>());
}
```

Expected initial result: fail because the structs do not exist.

- [ ] **Step 2: Define exact managed contracts**

```csharp
[StructLayout(LayoutKind.Sequential)]
public readonly struct RendererConfig
{
    public RendererConfig(uint width, uint height, float dpiScale) =>
        (Width, Height, DpiScale) = (width, height, dpiScale);
    public uint Width { get; }
    public uint Height { get; }
    public float DpiScale { get; }
}

[StructLayout(LayoutKind.Sequential)]
public readonly struct InteractionFrame
{
    public InteractionFrame(
        float pointerX,
        float pointerY,
        float dragX,
        float dragY,
        float pointerStrength,
        float dragStrength,
        float deltaSeconds,
        uint flags) =>
        (PointerX, PointerY, DragX, DragY, PointerStrength, DragStrength, DeltaSeconds, Flags) =
        (pointerX, pointerY, dragX, dragY, pointerStrength, dragStrength, deltaSeconds, flags);

    public readonly float PointerX;
    public readonly float PointerY;
    public readonly float DragX;
    public readonly float DragY;
    public readonly float PointerStrength;
    public readonly float DragStrength;
    public readonly float DeltaSeconds;
    public readonly uint Flags;
}
```

`internal sealed partial class NativeRenderer` uses `[LibraryImport("WindowsBlackHole.Renderer.dll")]` for every export, verifies `wbh_renderer_abi_version() == 1`, owns a `SafeHandle`, and exposes:

```csharp
public interface INativeRenderer : IDisposable
{
    nint Hwnd { get; }
    void Move(int x, int y);
    void Resize(RendererConfig config);
    void SetQuality(RenderQuality quality);
    void Tick(in InteractionFrame frame);
}
```

- [ ] **Step 3: Create the unpackaged self-contained WinUI project**

Use:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.22621.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.22621.0</TargetPlatformMinVersion>
    <UseWinUI>true</UseWinUI>
    <WindowsPackageType>None</WindowsPackageType>
    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" />
    <ProjectReference Include="..\WindowsBlackHole.Core\WindowsBlackHole.Core.csproj" />
    <ProjectReference Include="..\WindowsBlackHole.Contracts\WindowsBlackHole.Contracts.csproj" />
  </ItemGroup>
</Project>
```

`App.OnLaunched` creates a `ControllerWindow`, activates it, obtains its `AppWindow`, calls `Hide()`, then creates the native renderer with Cinematic metrics. `ControllerWindow.Closed` disposes the renderer. Do not add startup registration code.

Create `app.manifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
    <security>
      <requestedPrivileges>
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <longPathAware xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">true</longPathAware>
    </windowsSettings>
  </application>
</assembly>
```

Create `App.xaml`:

```xml
<Application
    x:Class="WindowsBlackHole.App.App"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Application.Resources />
</Application>
```

Create `ControllerWindow.xaml`:

```xml
<Window
    x:Class="WindowsBlackHole.App.Shell.ControllerWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Background="Transparent" />
</Window>
```

- [ ] **Step 4: Copy the native DLL and add the app to the solution**

Add an MSBuild target to the App project:

```xml
<Target Name="CopyNativeRenderer" AfterTargets="Build">
  <Copy SourceFiles="$(MSBuildThisFileDirectory)..\..\artifacts\native\$(Configuration)\WindowsBlackHole.Renderer.dll"
        DestinationFolder="$(OutDir)" />
</Target>
```

Run:

```powershell
dotnet sln plugins/windows-black-hole/WindowsBlackHole.sln add plugins/windows-black-hole/src/WindowsBlackHole.App/WindowsBlackHole.App.csproj
dotnet add plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests/WindowsBlackHole.App.Tests.csproj reference plugins/windows-black-hole/src/WindowsBlackHole.App/WindowsBlackHole.App.csproj
dotnet add plugins/windows-black-hole/tests/WindowsBlackHole.IntegrationTests/WindowsBlackHole.IntegrationTests.csproj reference plugins/windows-black-hole/src/WindowsBlackHole.App/WindowsBlackHole.App.csproj
.\plugins\windows-black-hole\build.ps1 -Configuration Debug -RunTests
```

Expected: all tests pass and the app output contains `WindowsBlackHole.Renderer.dll`.

- [ ] **Step 5: Run a process/window smoke check**

```powershell
$exe = Resolve-Path "plugins\windows-black-hole\src\WindowsBlackHole.App\bin\Debug\net8.0-windows10.0.22621.0\win-x64\WindowsBlackHole.App.exe"
$process = Start-Process -FilePath $exe -PassThru
Start-Sleep -Milliseconds 800
Get-Process -Id $process.Id
Stop-Process -Id $process.Id
```

Expected: process remains alive until stopped and the overlay is visible without a taskbar button.

- [ ] **Step 6: Commit**

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.App plugins/windows-black-hole/src/WindowsBlackHole.Contracts plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests plugins/windows-black-hole/WindowsBlackHole.sln
git commit -m "feat: connect WinUI controller to native overlay"
```

---

### Task 7: Implement recycle path policy and batch orchestration

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Recycle/RecyclePathPolicy.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Core/Recycle/RecycleModels.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Recycle/RecycleCoordinator.cs`
- Create: `plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/Recycle/RecyclePathPolicyTests.cs`
- Create: `plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests/Recycle/RecycleCoordinatorTests.cs`

**Interfaces:**
- Consumes: interaction state machine.
- Produces: `RecyclePathPolicy.Validate`, `IRecycleBackend.RecycleAsync`, and single-flight `RecycleCoordinator`.

- [ ] **Step 1: Write policy and single-flight failing tests**

Tests must assert:

```csharp
Assert.AreEqual(RecycleRejection.None, policy.Validate(@"C:\work\file.txt", pathExists: true).Reason);
Assert.AreEqual(RecycleRejection.NetworkPath, policy.Validate(@"\\server\share\file.txt", true).Reason);
Assert.AreEqual(RecycleRejection.DriveRoot, policy.Validate(@"C:\", true).Reason);
Assert.AreEqual(RecycleRejection.Missing, policy.Validate(@"C:\missing.txt", false).Reason);
await Assert.ThrowsExceptionAsync<InvalidOperationException>(
    () => coordinator.RecycleAsync(secondBatch, CancellationToken.None));
```

- [ ] **Step 2: Implement exact models**

```csharp
public enum RecycleRejection { None, Missing, DriveRoot, NetworkPath, VirtualObject, DuplicateBatch }
public readonly record struct RecycleValidation(string Path, RecycleRejection Reason)
{
    public bool IsAccepted => Reason == RecycleRejection.None;
}
public readonly record struct RecycleItemResult(string Path, bool Succeeded, int HResult);
public sealed record RecycleBatchResult(IReadOnlyList<RecycleItemResult> Items)
{
    public int Succeeded => Items.Count(static item => item.Succeeded);
    public int Failed => Items.Count - Succeeded;
}
public interface IRecycleBackend
{
    Task<RecycleBatchResult> RecycleAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken);
}
```

`RecyclePathPolicy.Validate` first rejects paths beginning with `\\`, then requires `Path.IsPathFullyQualified`. Normalize with `Path.GetFullPath`; obtain the root with `Path.GetPathRoot`; reject the item when `string.Equals(fullPath.TrimEnd(Path.DirectorySeparatorChar), root?.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)`. Accept only when `File.Exists` or `Directory.Exists` is true.

`RecycleCoordinator` uses `Interlocked.CompareExchange(ref active, 1, 0)`; it resets `active` in `finally`, validates the full batch before calling the backend, and returns rejected items without invoking the backend.

- [ ] **Step 3: Run tests and commit**

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.Core.Tests/WindowsBlackHole.Core.Tests.csproj --filter FullyQualifiedName~Recycle
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests/WindowsBlackHole.App.Tests.csproj --filter FullyQualifiedName~Recycle
git add plugins/windows-black-hole/src/WindowsBlackHole.Core/Recycle plugins/windows-black-hole/src/WindowsBlackHole.App/Recycle plugins/windows-black-hole/tests
git commit -m "feat: validate recycle batches and enforce single flight"
```

Expected: recycle policy and coordinator tests pass.

---

### Task 8: Implement native IFileOperation recycling and controlled integration tests

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/include/wbh_shell_api.h`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/shell/recycle_progress_sink.h`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/shell/recycle_progress_sink.cpp`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/shell/recycle_service.cpp`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Recycle/NativeRecycleBackend.cs`
- Create: `plugins/windows-black-hole/tests/WindowsBlackHole.IntegrationTests/Recycle/RecycleIntegrationTests.cs`

**Interfaces:**
- Consumes: validated local paths from Task 7.
- Produces: `wbh_recycle_paths` and `NativeRecycleBackend`.

- [ ] **Step 1: Define the native shell ABI**

```cpp
#pragma once
#include <cstdint>

struct WbhRecycleItemResult
{
    std::int32_t hresult;
    std::uint32_t succeeded;
};

extern "C" __declspec(dllexport) std::int32_t wbh_recycle_paths(
    const wchar_t* const* paths,
    std::uint32_t count,
    WbhRecycleItemResult* results) noexcept;
```

- [ ] **Step 2: Implement the progress sink and IFileOperation flags**

`RecycleProgressSink` implements every `IFileOperationProgressSink` method. `PostDeleteItem` records `hrDelete` by normalized source path. All other callbacks return `S_OK` without side effects.

`wbh_recycle_paths` must:

```cpp
const HRESULT initializeResult = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
if (FAILED(initializeResult) && initializeResult != RPC_E_CHANGED_MODE)
{
    return initializeResult;
}
Microsoft::WRL::ComPtr<IFileOperation> operation;
const HRESULT createResult = CoCreateInstance(
    CLSID_FileOperation,
    nullptr,
    CLSCTX_INPROC_SERVER,
    IID_PPV_ARGS(&operation));
if (FAILED(createResult))
{
    if (SUCCEEDED(initializeResult)) CoUninitialize();
    return createResult;
}
constexpr DWORD flags = FOFX_RECYCLEONDELETE | FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_NOERRORUI;
const HRESULT flagResult = operation->SetOperationFlags(flags);
if (FAILED(flagResult))
{
    if (SUCCEEDED(initializeResult)) CoUninitialize();
    return flagResult;
}
```

For each path, call `SHCreateItemFromParsingName`, then `DeleteItem`. Advise one `RecycleProgressSink`, call `PerformOperations`, call `GetAnyOperationsAborted`, unadvise, and fill one result per original index. An aborted or missing callback is failure. The function returns an HRESULT and never calls `DeleteFile`, `RemoveDirectory`, `std::filesystem::remove`, or any permanent-delete API.

Update CMake to link `ole32`, `shell32`, and `shlwapi` for the shell bridge.

- [ ] **Step 3: Implement the managed STA backend**

`NativeRecycleBackend` marshals:

```csharp
[StructLayout(LayoutKind.Sequential)]
private readonly struct NativeRecycleItemResult
{
    public readonly int HResult;
    public readonly uint Succeeded;
}

[DllImport("WindowsBlackHole.Renderer.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
private static extern int wbh_recycle_paths(
    [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] string[] paths,
    uint count,
    [Out] NativeRecycleItemResult[] results);
```

Run the call on a dedicated thread configured with `SetApartmentState(ApartmentState.STA)`. Convert each native result to `RecycleItemResult` and propagate cancellation only before native execution begins.

- [ ] **Step 4: Write gated integration tests**

The test creates `%TEMP%\WindowsBlackHoleTests\<guid>\recycle-me.txt`. It runs only when `WBH_RUN_RECYCLE_TESTS=1`. After success, assert the original path no longer exists and use `Shell.Application.NameSpace(10)` to find the unique filename and invoke the canonical `undelete` verb; assert the original path exists again, then remove the test directory.

Run:

```powershell
$env:WBH_RUN_RECYCLE_TESTS="1"
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.IntegrationTests/WindowsBlackHole.IntegrationTests.csproj --filter FullyQualifiedName~RecycleIntegrationTests
Remove-Item Env:WBH_RUN_RECYCLE_TESTS
```

Expected: the unique test file enters the Recycle Bin, is restored, and the test directory is removed. No pre-existing user file is touched.

- [ ] **Step 5: Add a permanent-delete guard and commit**

Run:

```powershell
rg -n "DeleteFile|RemoveDirectory|filesystem::remove|File\\.Delete|Directory\\.Delete" plugins/windows-black-hole/src
```

Expected: no match in production recycle code. Cleanup calls are allowed only in the gated integration test.

Commit:

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Renderer plugins/windows-black-hole/src/WindowsBlackHole.App/Recycle plugins/windows-black-hole/tests/WindowsBlackHole.IntegrationTests
git commit -m "feat: recycle dropped paths through IFileOperation"
```

---

### Task 9: Register OLE drag/drop and map it to the interaction state

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/DragDrop/OverlayDropTarget.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/DragDrop/ShellDataObjectReader.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/DragDrop/DropRegistration.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/InteractionCoordinator.cs`
- Create: corresponding App tests

**Interfaces:**
- Consumes: overlay HWND, `RecycleCoordinator`, `InteractionStateMachine`, geometry metrics.
- Produces: COM `IDropTarget`, `DragSnapshot`, and renderer interaction frames.

- [ ] **Step 1: Write data extraction and state mapping tests**

Tests use a fake `IShellDataObjectReader` and assert:

- multi-select paths remain in source order;
- invalid virtual data yields `DROPEFFECT_NONE`;
- valid influence-area drag yields `DROPEFFECT_NONE` until the hotspot enters the event horizon;
- the armed zone yields `DROPEFFECT_MOVE`;
- `Drop` calls recycle once and transitions to `Recycling`;
- `DragLeave` returns to `Idle`.

- [ ] **Step 2: Implement ShellDataObjectReader**

Use `System.Runtime.InteropServices.ComTypes.IDataObject`, request `CF_HDROP` with `TYMED_HGLOBAL`, lock the returned `HGLOBAL`, call `DragQueryFileW` once for count and once per index, then call `ReleaseStgMedium` in `finally`.

Expose:

```csharp
public interface IShellDataObjectReader
{
    IReadOnlyList<string> ReadFileSystemPaths(System.Runtime.InteropServices.ComTypes.IDataObject dataObject);
}
```

- [ ] **Step 3: Implement COM IDropTarget and registration**

Define `IDropTarget` with the official COM GUID `00000122-0000-0000-C000-000000000046`. `DropRegistration` calls `OleInitialize`, `RegisterDragDrop(renderer.Hwnd, dropTarget)`, and on disposal calls `RevokeDragDrop` and `OleUninitialize` on the same UI thread.

`OverlayDropTarget` converts screen coordinates to client coordinates with `ScreenToClient`, calculates strengths through `InfluenceMath`, and delegates state changes to `InteractionCoordinator`.

- [ ] **Step 4: Hide the standard drag image only inside the influence radius**

Create `IDropTargetHelper` using `CLSID_DragDropHelper`. Call `Show(false)` when strength is greater than zero and `Show(true)` in `DragLeave`, after a rejected drag, and after `Drop`. This allows the renderer to draw the distorted proxy without permanently hiding Explorer’s drag image.

- [ ] **Step 5: Run tests and manual drag verification**

```powershell
dotnet test plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests/WindowsBlackHole.App.Tests.csproj --filter FullyQualifiedName~DragDrop
```

Expected: all drag/drop tests pass.

Manual check with throwaway files:

- dragging through the outer region does not delete;
- center reports Move;
- leaving restores the normal Shell drag image;
- dropping a throwaway file sends it to Recycle Bin.

- [ ] **Step 6: Commit**

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.App/DragDrop plugins/windows-black-hole/src/WindowsBlackHole.App/Shell plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests
git commit -m "feat: add black hole shell drop target"
```

---

### Task 10: Build the DirectComposition surface and procedural black-hole shader

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/graphics/graphics_device.*`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/graphics/composition_surface.*`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/graphics/shader_loader.*`
- Create: `plugins/windows-black-hole/assets/shaders/BlackHole.hlsl`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/tests/render_smoke_tests.cpp`

**Interfaces:**
- Consumes: overlay HWND and renderer config.
- Produces: premultiplied-alpha composition swap chain and `RenderBlackHole(RenderConstants)`.

- [ ] **Step 1: Write an off-screen render smoke test**

Render one 512 × 512 frame into a staging texture and assert:

- corner alpha at `(0, 0)` is `0`;
- event-horizon RGB at `(256, 256)` is below `0.01`;
- photon-ring alpha at radius `64` is above `0.7`;
- no channel is NaN or outside `[0, 1]`.

- [ ] **Step 2: Create D3D11 and DirectComposition resources**

Create a hardware D3D11 device with `D3D11_CREATE_DEVICE_BGRA_SUPPORT`; in Debug add `D3D11_CREATE_DEVICE_DEBUG`. Create a flip-sequential composition swap chain with:

```cpp
DXGI_SWAP_CHAIN_DESC1 desc{};
desc.Width = width;
desc.Height = height;
desc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
desc.SampleDesc.Count = 1;
desc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
desc.BufferCount = 2;
desc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
desc.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;
```

Attach it to an `IDCompositionVisual`, bind the visual with `CreateTargetForHwnd`, set it as root, and commit.

- [ ] **Step 3: Implement the complete pixel shader formula**

`BlackHole.hlsl` uses normalized centered coordinates. It returns transparent outside radius `0.47`; returns opaque black inside event-horizon radius `0.125`; builds a white-gold photon ring with `smoothstep`; builds the foreground disk from a thin rotated band; builds upper/lower lensed arcs by mirroring disk coordinates around the horizon; adds deterministic procedural value noise from a hash of pixel coordinates and time; and returns premultiplied RGBA.

Use these fixed colors:

```hlsl
static const float3 PHOTON = float3(1.0, 0.94, 0.84);
static const float3 DISK_HOT = float3(1.0, 0.78, 0.48);
static const float3 DISK_COOL = float3(0.42, 0.20, 0.08);
```

Compile with:

```powershell
fxc /T ps_5_0 /E PSMain /Fo plugins/windows-black-hole/artifacts/shaders/BlackHole.cso plugins/windows-black-hole/assets/shaders/BlackHole.hlsl
```

Expected: shader compilation has no warnings.

- [ ] **Step 4: Run native render tests and inspect a debug frame**

```powershell
cmake --build plugins/windows-black-hole/artifacts/native --config Debug
ctest --test-dir plugins/windows-black-hole/artifacts/native -C Debug --output-on-failure
```

Expected: render smoke test passes. A debug-only WIC export shows a complete circular event horizon, foreground disk, and upper/lower lens arcs; the export function is excluded from Release builds.

- [ ] **Step 5: Commit**

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Renderer plugins/windows-black-hole/assets/shaders
git commit -m "feat: render procedural cinematic black hole"
```

---

### Task 11: Capture the desktop and apply gravitational lensing

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/capture/capture_session.*`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/graphics/lens_math.*`
- Create: `plugins/windows-black-hole/assets/shaders/GravitationalLens.hlsl`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/tests/lens_math_tests.cpp`

**Interfaces:**
- Consumes: current monitor, overlay physical bounds, quality level.
- Produces: cropped capture texture, lens shader output, capture fallback status.

- [ ] **Step 1: Write CPU lens-mapping tests**

Assert exact invariants:

```cpp
assert(map_lens({0.5F, 0.5F}, {0.5F, 0.5F}, 0.47F, 0.0F) == Float2{0.5F, 0.5F});
assert(nearly_equal(map_lens({0.97F, 0.5F}, {0.5F, 0.5F}, 0.47F, 1.0F).x, 0.97F));
assert(std::isfinite(map_lens({0.5001F, 0.5F}, {0.5F, 0.5F}, 0.47F, 1.0F).x));
```

- [ ] **Step 2: Implement monitor capture**

Use `IGraphicsCaptureItemInterop::CreateForMonitor`, a free-threaded `Direct3D11CaptureFramePool`, and `GraphicsCaptureSession`. Recreate the frame pool when content size changes. Copy only the physical overlay crop into a reusable texture. Never encode or map the capture texture to CPU in Release.

Update CMake to compile the capture sources with `/await`, include C++/WinRT headers from the Windows SDK, and link `windowsapp`. Set `GraphicsCaptureSession.IsCursorCaptureEnabled(false)` so the captured background never contains a second cursor.

- [ ] **Step 3: Exclude the overlay and prove recursion is absent**

Keep `WDA_EXCLUDEFROMCAPTURE` on the overlay. Add a debug test pattern to the overlay, capture five frames, and assert the pattern is absent from the crop. If exclusion or capture initialization fails, set capture status to Fallback and render only the procedural black hole.

- [ ] **Step 4: Implement gravitational lens sampling**

`GravitationalLens.hlsl` computes:

```hlsl
float2 delta = uv - center;
float distanceToCenter = max(length(delta), 0.0001);
float influence = saturate(1.0 - distanceToCenter / influenceRadius);
float eased = influence * influence * (3.0 - 2.0 * influence);
float radialOffset = 0.055 * eased * eased;
float tangentialOffset = 0.018 * eased;
float2 radial = delta / distanceToCenter;
float2 tangent = float2(-radial.y, radial.x);
float2 sampleUv = uv + radial * radialOffset + tangent * tangentialOffset;
```

Clamp sampling to the capture crop, then composite the procedural black hole over the distorted background using premultiplied alpha.

- [ ] **Step 5: Verify and commit**

Run native tests, then move a high-contrast window behind the black hole and verify lines bend continuously and recover after the pointer leaves.

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Renderer plugins/windows-black-hole/assets/shaders/GravitationalLens.hlsl
git commit -m "feat: add captured desktop gravitational lensing"
```

---

### Task 12: Add pointer/file proxies, consume/reject animations, and quality telemetry

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/graphics/interaction_renderer.*`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.Renderer/src/graphics/frame_timing.*`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/RenderLoop.cs`
- Create: matching Core/App/native tests

**Interfaces:**
- Consumes: `InteractionFrame`, hidden standard drag image state, quality controller.
- Produces: 180 ms restore, 350 ms consume animation, red rejection pulse, frame timing samples.

- [ ] **Step 1: Write animation boundary tests**

Tests assert:

- restore progress is `0` at start and `1` at 180 ms;
- consume progress is `0` at start and `1` at 350 ms;
- consume spiral radius decreases monotonically;
- rejected state never sets the consume flag;
- quality mapping is High `100%/60`, Medium `75%/45`, Low `50%/30`, Fallback `capture off/30`.

- [ ] **Step 2: Implement the render loop**

`RenderLoop` uses `DispatcherQueueTimer` while idle and a high-resolution waitable timer while active. Each frame:

1. calls `GetCursorPos`;
2. converts to overlay client physical pixels;
3. calculates pointer and drag strength;
4. maps `InteractionState` to ABI flags;
5. calls `renderer.Tick`;
6. records elapsed time in `QualityController`;
7. calls `renderer.SetQuality` only when the quality changes.

The render loop never performs recycle I/O.

- [ ] **Step 3: Implement proxy and animations**

- Normal pointer: keep the real system cursor visible for precision and draw a warped white echo plus gravitational trail around it; do not modify the global cursor or install a hook.
- Shell drag: `IDropTargetHelper.Show(false)` hides the standard drag image; draw a proxy card containing the first extension and `+N` for additional items.
- Consume: use `radius = startRadius * (1 - eased)` and `angle = startAngle + 4π * eased`; scale and alpha both reach zero at 350 ms.
- Restore: interpolate the warped coordinate back to the true coordinate over 180 ms with cubic ease-out.
- Reject: keep object scale at `1`, apply outward offset, and pulse photon ring color to `(1, 0.12, 0.06)` for 220 ms.

- [ ] **Step 4: Verify quality downgrade and device loss**

Add a debug command that injects a 25 ms render delay. Verify Auto changes High → Medium after 120 active frames. Trigger device removal through the D3D debug interface, verify resource recreation, and verify persistent failure enters Fallback without enabling a visually invisible drop zone.

- [ ] **Step 5: Commit**

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.Renderer plugins/windows-black-hole/src/WindowsBlackHole.App/Shell plugins/windows-black-hole/src/WindowsBlackHole.Core/Quality plugins/windows-black-hole/tests
git commit -m "feat: animate gravitational drag interactions"
```

---

### Task 13: Add movement, multi-monitor/DPI recovery, menu, sound, and diagnostics

**Files:**
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/WidgetMover.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/MonitorService.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/ContextMenuService.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/SoundService.cs`
- Create: `plugins/windows-black-hole/src/WindowsBlackHole.App/Shell/DiagnosticLog.cs`
- Create: `plugins/windows-black-hole/assets/sounds/consume.wav`
- Create: matching App tests

**Interfaces:**
- Consumes: overlay HWND, settings store, renderer, interaction state.
- Produces: exact right-click menu, position persistence, local rolling log, opt-out sound.

- [ ] **Step 1: Write tests for menu commands and placement recovery**

Assert the menu contains exactly:

```text
Visual Quality > Auto, Low, Medium, High
Size > Compact, Balanced, Cinematic
Sound > On
Open Recycle Bin
Reset Position
Exit
```

Assert no menu item or settings key contains `AutoStart`, `Startup`, or `AlwaysOnTop=false`. Simulate a missing saved monitor and assert the default is the primary monitor’s bottom-right safe area.

- [ ] **Step 2: Implement movement and monitor changes**

The native window sends center `WM_LBUTTONDOWN` as a callback. `WidgetMover` records cursor/window origin, calls `SetCapture`, updates with `SetWindowPos(hwnd, HWND_TOPMOST, x, y, 0, 0, SWP_NOACTIVATE | SWP_NOSIZE)`, and releases on button-up. On `WM_DPICHANGED` or monitor transition, recalculate physical metrics, call `renderer.Resize`, recreate capture for the new monitor, clamp placement, and persist normalized coordinates.

- [ ] **Step 3: Implement the exact native context menu**

Use `CreatePopupMenu`, `AppendMenuW`, and `TrackPopupMenuEx` at `WM_CONTEXTMENU`. Commands call typed methods; `Open Recycle Bin` starts `explorer.exe shell:RecycleBinFolder`; `Exit` shuts down render loop, revokes drag/drop, disposes native resources, and exits the app.

- [ ] **Step 4: Add sound and bounded local diagnostics**

Create `assets/sounds/generate-consume.ps1` to deterministically generate a 300 ms, 44.1 kHz, mono PCM WAV. The sample is `0.25 * sin(2π * frequency(t) * t) * (1 - t / 0.3)^2`, where `frequency(t) = 92 - 52 * (t / 0.3)`. Run the script once and commit `consume.wav`. Play it with `PlaySoundW` using `SND_FILENAME | SND_ASYNC | SND_NODEFAULT`; no third-party attribution is required.

`DiagnosticLog` writes `%LOCALAPPDATA%\WindowsBlackHole\logs\widget.log`, rotates at 1 MiB, retains three files, redacts capture data, and logs only timestamp, component, HRESULT, quality transition, monitor ID, and normalized local path when a recycle error requires diagnosis.

- [ ] **Step 5: Run tests and manual DPI checks**

Run App tests, then verify at Windows scale 100%, 150%, and 200% that event-horizon hit testing matches the visible 128 logical-pixel diameter. Move across two monitors and unplug the saved monitor; verify recovery to the primary display.

- [ ] **Step 6: Commit**

```powershell
git add plugins/windows-black-hole/src/WindowsBlackHole.App plugins/windows-black-hole/assets/sounds plugins/windows-black-hole/tests/WindowsBlackHole.App.Tests
git commit -m "feat: finish widget controls and desktop placement"
```

---

### Task 14: Package, audit, document, and run the full acceptance suite

**Files:**
- Create: `plugins/windows-black-hole/packaging/WindowsBlackHole.Package/Package.appxmanifest`
- Create: `plugins/windows-black-hole/packaging/WindowsBlackHole.Package/WindowsBlackHole.Package.wapproj`
- Create: `plugins/windows-black-hole/packaging/WindowsBlackHole.Package/build-package.ps1`
- Create: `plugins/windows-black-hole/packaging/WindowsBlackHole.Package/generate-assets.ps1`
- Create: package PNG files under `plugins/windows-black-hole/packaging/WindowsBlackHole.Package/Assets/`
- Create: `plugins/windows-black-hole/README.md`
- Create: `plugins/windows-black-hole/tests/acceptance.ps1`
- Modify: `plugins/windows-black-hole/WindowsBlackHole.sln`

**Interfaces:**
- Consumes: complete app.
- Produces: signed local-test x64 MSIX, Release publish folder, evidence-backed acceptance report.

- [ ] **Step 1: Create the package without startup capabilities**

The manifest declares x64 desktop full-trust entry point only. It must not contain `windows.startupTask`, background tasks, services, firewall rules, broad file capabilities, or network capabilities.

Use this application declaration:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">
  <Identity Name="WindowsBlackHole" Publisher="CN=WindowsBlackHole.Dev" Version="1.0.0.0" ProcessorArchitecture="x64" />
  <Properties>
    <DisplayName>Windows Black Hole</DisplayName>
    <PublisherDisplayName>Windows Black Hole</PublisherDisplayName>
    <Logo>Assets\StoreLogo.png</Logo>
  </Properties>
  <Resources>
    <Resource Language="zh-CN" />
    <Resource Language="en-US" />
  </Resources>
  <Dependencies>
    <TargetDeviceFamily Name="Windows.Desktop" MinVersion="10.0.22621.0" MaxVersionTested="10.0.26100.0" />
  </Dependencies>
  <Applications>
    <Application Id="App" Executable="WindowsBlackHole.App.exe" EntryPoint="Windows.FullTrustApplication">
      <uap:VisualElements
        AppListEntry="default"
        DisplayName="Windows Black Hole"
        Description="Cinematic desktop recycle black hole"
        BackgroundColor="transparent"
        Square44x44Logo="Assets\Square44x44Logo.png"
        Square150x150Logo="Assets\Square150x150Logo.png" />
    </Application>
  </Applications>
  <Capabilities>
    <rescap:Capability Name="runFullTrust" />
  </Capabilities>
</Package>
```

`generate-assets.ps1` creates `44 × 44`, `150 × 150`, and `50 × 50` transparent PNGs using `System.Drawing`: fill a centered black circle occupying 72% of the canvas, draw a 6%-width white-gold ring, and draw an orange horizontal ellipse. The script must be deterministic and must not download assets.

`build-package.ps1` performs these exact stages:

```powershell
dotnet publish ..\..\src\WindowsBlackHole.App\WindowsBlackHole.App.csproj -c Release -r win-x64 --self-contained -o .\artifacts\package-root
Copy-Item .\Package.appxmanifest .\artifacts\package-root\AppxManifest.xml
Copy-Item .\Assets .\artifacts\package-root\Assets -Recurse
makeappx.exe pack /d .\artifacts\package-root /p .\artifacts\WindowsBlackHole.msix /o
signtool.exe sign /fd SHA256 /f $CertificatePath /p $CertificatePassword .\artifacts\WindowsBlackHole.msix
```

The `.wapproj` is a one-target MSBuild wrapper that invokes `build-package.ps1` with `CertificatePath` and `CertificatePassword` properties.

Create a local-only test certificate:

```powershell
$password = ConvertTo-SecureString "WindowsBlackHole-LocalTest" -AsPlainText -Force
$certificate = New-SelfSignedCertificate -Type Custom -Subject "CN=WindowsBlackHole.Dev" -KeyUsage DigitalSignature -FriendlyName "Windows Black Hole Local Test" -CertStoreLocation "Cert:\CurrentUser\My"
Export-PfxCertificate -Cert $certificate -FilePath "$env:TEMP\WindowsBlackHole.Dev.pfx" -Password $password
```

Build:

```powershell
msbuild plugins/windows-black-hole/packaging/WindowsBlackHole.Package/WindowsBlackHole.Package.wapproj /p:Configuration=Release /p:Platform=x64 /p:CertificatePath="$env:TEMP\WindowsBlackHole.Dev.pfx" /p:CertificatePassword="WindowsBlackHole-LocalTest"
```

Expected: one x64 `.msix` is produced and installs under a local test certificate.

- [ ] **Step 2: Add automated security and autostart audit**

`tests/acceptance.ps1` must fail if:

```powershell
rg -n "DeleteFile|RemoveDirectory|filesystem::remove|File\\.Delete|Directory\\.Delete" src
rg -n "startupTask|CurrentVersion\\\\Run|StartupFolder|TaskScheduler|CreateService|SetWindowsHookEx" src packaging
rg -n "HttpClient|WinHttp|curl|WebRequest|Telemetry" src
```

matches production code. It then runs:

```powershell
.\build.ps1 -Configuration Release -RunTests
dotnet publish src\WindowsBlackHole.App\WindowsBlackHole.App.csproj -c Release -r win-x64 --self-contained
```

Expected: clean searches, all unit/native/integration tests pass, and publish succeeds.

- [ ] **Step 3: Execute requirement-by-requirement manual acceptance**

Use only temporary uniquely named objects:

1. Manual launch shows a full cinematic black hole, topmost, with no taskbar or Alt+Tab entry.
2. Center drag moves it; restart restores position.
3. Pointer and background distort continuously inside 240 logical pixels and recover in 180 ms.
4. Single file, folder, `.lnk`, and multi-select show attraction.
5. Outer-region release does nothing.
6. Center drop places every successful object in Recycle Bin.
7. UNC, drive root, virtual objects, missing paths, access denied, and occupied files do not play success.
8. Partial batch success matches actual filesystem state.
9. Capture failure and injected GPU failure enter Fallback while preserving visible interaction.
10. 100%, 150%, and 200% DPI hit regions match the rendered event horizon.
11. Multi-monitor movement and disconnected-monitor recovery work.
12. Registry Run keys, Startup folders, scheduled tasks, and services contain no Windows Black Hole entry.
13. Resource Monitor shows no runtime network connection.
14. `%LOCALAPPDATA%\WindowsBlackHole` contains settings/logs only and no capture frames.

Record command output and observations in `artifacts/acceptance/acceptance-report.md`; keep that report uncommitted if it contains local machine identifiers.

- [ ] **Step 4: Write the operator README**

README must contain:

- supported platform and manual-start behavior;
- build prerequisites and exact build/test/package commands;
- drag/drop semantics and Recycle Bin guarantee;
- controls and right-click menu;
- performance fallback behavior;
- known capture limits for DRM, secure desktop, and Remote Desktop;
- privacy statement: no telemetry, no network, no frame persistence;
- troubleshooting for missing Windows App Runtime, device loss, and access-denied recycling;
- license/attribution for textures and sound.

- [ ] **Step 5: Final clean-tree verification**

Run:

```powershell
git diff --check
git status --short
.\plugins\windows-black-hole\build.ps1 -Configuration Release -RunTests
```

Expected: only deliberate task files are modified; `skills/spec-master.zip` remains untracked and unstaged; Release build and all automated tests pass.

- [ ] **Step 6: Commit**

```powershell
git add plugins/windows-black-hole/README.md plugins/windows-black-hole/packaging plugins/windows-black-hole/tests/acceptance.ps1 plugins/windows-black-hole/WindowsBlackHole.sln
git commit -m "feat: package and verify Windows black hole widget"
```

## Plan Self-Review Checklist

- Every design goal and all 13 acceptance requirements map to Tasks 2–14.
- No task introduces permanent delete, startup registration, administrator elevation, telemetry, or runtime network access.
- Managed/native ABI names remain `RendererConfig`, `InteractionFrame`, `wbh_renderer_*`, and ABI version 1 throughout.
- Cinematic metrics remain 320/240/128/512 logical pixels throughout.
- Recycle execution occurs before consume animation; failure never plays success.
- Toolchain installation is explicit because the current machine lacks the required SDKs.
- Each task ends with a focused test cycle and commit.
