#include "wbh_renderer_api.h"

#include <roapi.h>

#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <thread>

#ifndef WDA_EXCLUDEFROMCAPTURE
#define WDA_EXCLUDEFROMCAPTURE 0x00000011
#endif

namespace
{
void require(const bool condition) noexcept
{
    if (!condition)
    {
        std::abort();
    }
}

bool is_window_above(const HWND candidate, const HWND target) noexcept
{
    for (HWND current = GetTopWindow(nullptr);
         current != nullptr;
         current = GetWindow(current, GW_HWNDNEXT))
    {
        if (current == candidate)
        {
            return true;
        }
        if (current == target)
        {
            return false;
        }
    }
    return false;
}
} // namespace

int main()
{
    std::fputs("runtime-init\n", stderr);
    const HRESULT runtime_result = RoInitialize(RO_INIT_MULTITHREADED);
    require(SUCCEEDED(runtime_result) || runtime_result == RPC_E_CHANGED_MODE);

    const WbhRendererConfig config{
        sizeof(WbhRendererConfig),
        320U,
        320U,
        1.0F};
    WbhRendererHandle* handle = nullptr;
    std::fputs("renderer-create\n", stderr);
    require(wbh_renderer_abi_version() == 1U);
    require(wbh_renderer_create(&config, &handle) == WBH_OK);
    require(handle != nullptr);

    const HWND hwnd = wbh_renderer_get_hwnd(handle);
    require(hwnd != nullptr);
    require((GetWindowLongPtrW(hwnd, GWL_STYLE) & WS_POPUP) != 0);
    const LONG_PTR extended_style = GetWindowLongPtrW(hwnd, GWL_EXSTYLE);
    require((extended_style & WS_EX_TOOLWINDOW) != 0);
    require((extended_style & WS_EX_NOACTIVATE) != 0);
    require((extended_style & WS_EX_NOREDIRECTIONBITMAP) != 0);

    DWORD affinity{};
    require(GetWindowDisplayAffinity(hwnd, &affinity) != FALSE);
    require(affinity == WDA_EXCLUDEFROMCAPTURE);

    const HWND z_order_sentinel = CreateWindowExW(
        WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST,
        L"STATIC",
        L"Windows Black Hole Z-Order Sentinel",
        WS_POPUP,
        0,
        0,
        8,
        8,
        nullptr,
        nullptr,
        GetModuleHandleW(nullptr),
        nullptr);
    require(z_order_sentinel != nullptr);
    require(SetWindowPos(
                z_order_sentinel,
                HWND_TOPMOST,
                0,
                0,
                8,
                8,
                SWP_NOACTIVATE | SWP_SHOWWINDOW) != FALSE);
    require(is_window_above(z_order_sentinel, hwnd));

    require(wbh_renderer_set_bounds(handle, 80, 80, 320U, 320U) == WBH_OK);
    require(is_window_above(z_order_sentinel, hwnd));
    require(DestroyWindow(z_order_sentinel) != FALSE);
    require(wbh_renderer_set_strength(handle, 0.85F) == WBH_OK);
    std::fputs("capture-start\n", stderr);
    const WbhResult start_result = wbh_renderer_start(handle);
    require(start_result == WBH_OK || start_result == WBH_CAPTURE_UNAVAILABLE);

    bool capture_became_active = false;
    std::fputs("render-loop\n", stderr);
    for (int frame = 0; frame < 120; ++frame)
    {
        MSG message{};
        while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
        {
            TranslateMessage(&message);
            DispatchMessageW(&message);
        }
        require(wbh_renderer_tick(handle, 1.0F / 60.0F) == WBH_OK);
        capture_became_active =
            capture_became_active ||
            wbh_renderer_is_capture_active(handle) != 0;
        std::this_thread::sleep_for(std::chrono::milliseconds(8));
    }

    if (start_result == WBH_OK)
    {
        require(capture_became_active);
        require(wbh_renderer_freeze_for_diagnostics(handle) == WBH_OK);
        require(wbh_renderer_is_capture_active(handle) == 0);
        affinity = WDA_EXCLUDEFROMCAPTURE;
        require(GetWindowDisplayAffinity(hwnd, &affinity) != FALSE);
        require(affinity == WDA_NONE);
        require(wbh_renderer_tick(handle, 1.0F / 60.0F) == WBH_OK);
    }
    std::fputs("renderer-shutdown\n", stderr);
    wbh_renderer_shutdown(handle);
    std::fputs("renderer-stopped\n", stderr);

    if (SUCCEEDED(runtime_result))
    {
        RoUninitialize();
    }
    std::fputs("test-complete\n", stderr);
    return 0;
}
