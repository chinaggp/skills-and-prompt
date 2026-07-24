using System.Runtime.InteropServices;

namespace WindowsBlackHole.App.Rendering;

internal sealed class NativeRendererHost : IDisposable
{
    private const uint ExpectedAbiVersion = 1;
    private nint _handle;
    private DateTime _lastTickUtc;
    private int _lastX = int.MinValue;
    private int _lastY = int.MinValue;
    private uint _lastWidth;
    private uint _lastHeight;

    public bool IsInitialized => _handle != nint.Zero;

    public bool IsCaptureActive =>
        IsInitialized && NativeMethods.IsCaptureActive(_handle) != 0;

    public nint OverlayWindow =>
        IsInitialized ? NativeMethods.GetWindow(_handle) : nint.Zero;

    public bool TryInitialize(uint width, uint height, float dpiScale)
    {
        if (IsInitialized)
        {
            return true;
        }

        try
        {
            if (NativeMethods.GetAbiVersion() != ExpectedAbiVersion)
            {
                return false;
            }

            var config = new RendererConfig
            {
                StructSize = (uint)Marshal.SizeOf<RendererConfig>(),
                Width = width,
                Height = height,
                DpiScale = dpiScale,
            };

            if (NativeMethods.Create(ref config, out _handle) != (int)RendererResult.Ok ||
                _handle == nint.Zero)
            {
                _handle = nint.Zero;
                return false;
            }

            if (NativeMethods.Start(_handle) != (int)RendererResult.Ok)
            {
                Dispose();
                return false;
            }

            _lastTickUtc = DateTime.UtcNow;
            return true;
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or
            EntryPointNotFoundException or
            BadImageFormatException)
        {
            _handle = nint.Zero;
            return false;
        }
    }

    public bool Update(int x, int y, uint width, uint height, float strength)
    {
        if (!IsInitialized)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var deltaSeconds = Math.Clamp(
            (float)(now - _lastTickUtc).TotalSeconds,
            0,
            0.1f);
        _lastTickUtc = now;

        var boundsChanged =
            x != _lastX ||
            y != _lastY ||
            width != _lastWidth ||
            height != _lastHeight;
        if (boundsChanged)
        {
            _ = NativeMethods.SetBounds(_handle, x, y, width, height);
            _lastX = x;
            _lastY = y;
            _lastWidth = width;
            _lastHeight = height;
        }

        _ = NativeMethods.SetStrength(_handle, Math.Clamp(strength, 0, 1));
        _ = NativeMethods.Tick(_handle, deltaSeconds);
        return boundsChanged;
    }

    public bool FreezeForDiagnostics() =>
        IsInitialized &&
        NativeMethods.FreezeForDiagnostics(_handle) == (int)RendererResult.Ok;

    public void Dispose()
    {
        if (_handle == nint.Zero)
        {
            return;
        }

        NativeMethods.Shutdown(_handle);
        _handle = nint.Zero;
        _lastX = int.MinValue;
        _lastY = int.MinValue;
        _lastWidth = 0;
        _lastHeight = 0;
        GC.SuppressFinalize(this);
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
    }

    private static class NativeMethods
    {
        private const string LibraryName = "WindowsBlackHole.Renderer.dll";

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_abi_version", CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetAbiVersion();

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_create", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Create(
            ref RendererConfig config,
            out nint renderer);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_get_hwnd", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetWindow(nint renderer);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_start", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Start(nint renderer);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_set_bounds", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetBounds(
            nint renderer,
            int x,
            int y,
            uint width,
            uint height);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_set_strength", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetStrength(nint renderer, float strength);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_tick", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Tick(nint renderer, float deltaSeconds);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_is_capture_active", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int IsCaptureActive(nint renderer);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_freeze_for_diagnostics", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int FreezeForDiagnostics(nint renderer);

        [DllImport(LibraryName, EntryPoint = "wbh_renderer_shutdown", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Shutdown(nint renderer);
    }
}
