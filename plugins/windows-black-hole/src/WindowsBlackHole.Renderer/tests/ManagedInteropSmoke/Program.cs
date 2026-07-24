using System.Reflection;
using System.Runtime.InteropServices;

internal static class Program
{
    private const string RendererLibrary = "WindowsBlackHole.Renderer.dll";
    private static string _rendererPath = string.Empty;

    private static int Main(string[] args)
    {
        if (args.Length != 2 ||
            !Path.IsPathFullyQualified(args[0]) ||
            (args[1] != "sta" && args[1] != "mta"))
        {
            Console.Error.WriteLine(
                "Usage: ManagedInteropSmoke <absolute-renderer-dll> <sta|mta>");
            return 2;
        }

        _rendererPath = args[0];
        NativeLibrary.SetDllImportResolver(
            Assembly.GetExecutingAssembly(),
            ResolveNativeLibrary);

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                RunInterop();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });
        thread.SetApartmentState(
            args[1] == "sta" ? ApartmentState.STA : ApartmentState.MTA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            Console.Error.WriteLine(failure);
            return 1;
        }
        return 0;
    }

    private static nint ResolveNativeLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        _ = assembly;
        _ = searchPath;
        return libraryName == RendererLibrary
            ? NativeLibrary.Load(_rendererPath)
            : nint.Zero;
    }

    private static void RunInterop()
    {
        var config = new RendererConfig
        {
            StructSize = (uint)Marshal.SizeOf<RendererConfig>(),
            Width = 320,
            Height = 320,
            DpiScale = 1,
        };
        Console.WriteLine(
            $"apartment={Thread.CurrentThread.GetApartmentState()} " +
            $"abi={NativeMethods.GetAbiVersion()} config={config.StructSize}");

        var holder = new RendererHandleHolder();
        var result = holder.Create(in config);
        var handle = holder.Handle;
        Require(result == RendererResult.Ok, $"create failed: {result}");
        Require(handle != nint.Zero, "create returned a null handle");

        try
        {
            Require(NativeMethods.GetWindow(handle) != nint.Zero, "null HWND");
            result = NativeMethods.SetBounds(handle, 80, 80, 320, 320);
            Require(result == RendererResult.Ok, $"set_bounds failed: {result}");
            result = NativeMethods.SetStrength(handle, 0.8f);
            Require(result == RendererResult.Ok, $"set_strength failed: {result}");

            result = NativeMethods.Start(handle);
            Require(
                result is RendererResult.Ok or RendererResult.CaptureUnavailable,
                $"start failed: {result}");
            var startResult = result;
            var captureBecameActive = false;
            for (var frame = 0; frame < 120; frame++)
            {
                result = NativeMethods.Tick(handle, 1f / 60f);
                Require(result == RendererResult.Ok, $"tick failed: {result}");
                captureBecameActive |= NativeMethods.IsCaptureActive(handle) != 0;
                Thread.Sleep(8);
            }
            if (startResult == RendererResult.Ok)
            {
                Require(captureBecameActive, "capture did not become active");
                result = NativeMethods.FreezeForDiagnostics(handle);
                Require(result == RendererResult.Ok, $"freeze failed: {result}");
                Require(
                    NativeMethods.IsCaptureActive(handle) == 0,
                    "capture remained active after freeze");
                result = NativeMethods.Tick(handle, 1f / 60f);
                Require(result == RendererResult.Ok, $"frozen tick failed: {result}");
            }
        }
        finally
        {
            NativeMethods.Shutdown(handle);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RendererConfig
    {
        public uint StructSize;
        public uint Width;
        public uint Height;
        public float DpiScale;
    }

    private enum RendererResult : int
    {
        Ok = 0,
        CaptureUnavailable = 5,
    }

    private sealed class RendererHandleHolder
    {
        internal nint Handle;

        internal RendererResult Create(in RendererConfig config) =>
            NativeMethods.Create(in config, out Handle);
    }

    private static class NativeMethods
    {
        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_abi_version",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetAbiVersion();

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_create",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern RendererResult Create(
            in RendererConfig config,
            out nint renderer);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_get_hwnd",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetWindow(nint renderer);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_start",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern RendererResult Start(nint renderer);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_set_bounds",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern RendererResult SetBounds(
            nint renderer,
            int x,
            int y,
            uint width,
            uint height);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_set_strength",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern RendererResult SetStrength(
            nint renderer,
            float strength);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_tick",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern RendererResult Tick(
            nint renderer,
            float deltaSeconds);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_is_capture_active",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int IsCaptureActive(nint renderer);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_freeze_for_diagnostics",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern RendererResult FreezeForDiagnostics(
            nint renderer);

        [DllImport(
            RendererLibrary,
            EntryPoint = "wbh_renderer_shutdown",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Shutdown(nint renderer);
    }
}
