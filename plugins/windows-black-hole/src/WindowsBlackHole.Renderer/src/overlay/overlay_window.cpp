#include "overlay/overlay_window.h"

#include <mutex>

#ifndef WDA_EXCLUDEFROMCAPTURE
#define WDA_EXCLUDEFROMCAPTURE 0x00000011
#endif

namespace
{
constexpr wchar_t kOverlayClassName[] = L"WindowsBlackHoleOverlay";
std::once_flag g_register_class;
bool g_class_registered = false;

void RegisterOverlayClass() noexcept
{
    WNDCLASSEXW window_class{};
    window_class.cbSize = sizeof(window_class);
    window_class.lpfnWndProc = &wbh::OverlayWindow::WindowProc;
    window_class.hInstance = GetModuleHandleW(nullptr);
    window_class.hCursor = LoadCursorW(nullptr, IDC_ARROW);
    window_class.lpszClassName = kOverlayClassName;

    const ATOM atom = RegisterClassExW(&window_class);
    g_class_registered = atom != 0 || GetLastError() == ERROR_CLASS_ALREADY_EXISTS;
}
} // namespace

namespace wbh
{
OverlayWindow::~OverlayWindow()
{
    if (hwnd_ != nullptr)
    {
        DestroyWindow(hwnd_);
        hwnd_ = nullptr;
    }
}

bool OverlayWindow::Create(
    const std::uint32_t width,
    const std::uint32_t height) noexcept
{
    if (width == 0 || height == 0)
    {
        return false;
    }

    std::call_once(g_register_class, RegisterOverlayClass);
    if (!g_class_registered)
    {
        return false;
    }

    hwnd_ = CreateWindowExW(
        WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_NOREDIRECTIONBITMAP,
        kOverlayClassName,
        L"Windows Black Hole Lens",
        WS_POPUP,
        0,
        0,
        static_cast<int>(width),
        static_cast<int>(height),
        nullptr,
        nullptr,
        GetModuleHandleW(nullptr),
        nullptr);
    if (hwnd_ == nullptr)
    {
        return false;
    }

    capture_exclusion_applied_ = SetCaptureExcluded(true);

    return SetWindowPos(
               hwnd_,
               HWND_TOPMOST,
               0,
               0,
               static_cast<int>(width),
               static_cast<int>(height),
               SWP_NOACTIVATE | SWP_SHOWWINDOW) != FALSE;
}

bool OverlayWindow::SetCaptureExcluded(const bool excluded) noexcept
{
    if (hwnd_ == nullptr)
    {
        capture_exclusion_applied_ = false;
        return false;
    }

    const DWORD affinity = excluded ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE;
    if (SetWindowDisplayAffinity(hwnd_, affinity) == FALSE)
    {
        return false;
    }
    capture_exclusion_applied_ = excluded;
    return true;
}

bool OverlayWindow::SetBounds(
    const std::int32_t x,
    const std::int32_t y,
    const std::uint32_t width,
    const std::uint32_t height) noexcept
{
    if (hwnd_ == nullptr || width == 0 || height == 0)
    {
        return false;
    }

    return SetWindowPos(
               hwnd_,
               nullptr,
               x,
               y,
               static_cast<int>(width),
               static_cast<int>(height),
               SWP_NOACTIVATE | SWP_NOZORDER | SWP_SHOWWINDOW) != FALSE;
}

LRESULT CALLBACK OverlayWindow::WindowProc(
    const HWND hwnd,
    const UINT message,
    const WPARAM wparam,
    const LPARAM lparam) noexcept
{
    if (message == WM_NCHITTEST)
    {
        return HTTRANSPARENT;
    }
    if (message == WM_MOUSEACTIVATE)
    {
        return MA_NOACTIVATE;
    }
    if (message == WM_ERASEBKGND)
    {
        return 1;
    }
    return DefWindowProcW(hwnd, message, wparam, lparam);
}
} // namespace wbh
