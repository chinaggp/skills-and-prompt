#pragma once

#include <Windows.h>
#include <cstdint>

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
    WBH_DEVICE_LOST = 4,
    WBH_CAPTURE_UNAVAILABLE = 5,
    WBH_SHADER_LOAD_FAILED = 6
};

enum WbhRenderQuality : std::uint32_t
{
    WBH_QUALITY_FALLBACK = 0,
    WBH_QUALITY_LOW = 1,
    WBH_QUALITY_MEDIUM = 2,
    WBH_QUALITY_HIGH = 3
};

struct WbhRendererConfig
{
    std::uint32_t struct_size;
    std::uint32_t width;
    std::uint32_t height;
    float dpi_scale;
};
static_assert(sizeof(WbhRendererConfig) == 16);

struct WbhRendererHandle;

WBH_API std::uint32_t wbh_renderer_abi_version() noexcept;
WBH_API WbhResult wbh_renderer_create(
    const WbhRendererConfig* config,
    WbhRendererHandle** handle) noexcept;
WBH_API HWND wbh_renderer_get_hwnd(WbhRendererHandle* handle) noexcept;
WBH_API WbhResult wbh_renderer_start(WbhRendererHandle* handle) noexcept;
WBH_API WbhResult wbh_renderer_set_bounds(
    WbhRendererHandle* handle,
    std::int32_t x,
    std::int32_t y,
    std::uint32_t width,
    std::uint32_t height) noexcept;
WBH_API WbhResult wbh_renderer_set_strength(
    WbhRendererHandle* handle,
    float strength) noexcept;
WBH_API WbhResult wbh_renderer_tick(
    WbhRendererHandle* handle,
    float delta_seconds) noexcept;
WBH_API std::int32_t wbh_renderer_is_capture_active(
    WbhRendererHandle* handle) noexcept;
WBH_API WbhResult wbh_renderer_last_result(
    WbhRendererHandle* handle) noexcept;
// Diagnostic-only: presents the latest captured crop, stops capture, and makes
// the frozen overlay visible to external capture tools. Normal application
// flow must not call this export.
WBH_API WbhResult wbh_renderer_freeze_for_diagnostics(
    WbhRendererHandle* handle) noexcept;
WBH_API void wbh_renderer_shutdown(WbhRendererHandle* handle) noexcept;

// ABI v1 compatibility exports. shutdown and destroy are aliases; callers must
// invoke exactly one of them for a handle.
WBH_API WbhResult wbh_renderer_move(
    WbhRendererHandle* handle,
    std::int32_t x,
    std::int32_t y) noexcept;
WBH_API WbhResult wbh_renderer_resize(
    WbhRendererHandle* handle,
    const WbhRendererConfig* config) noexcept;
WBH_API WbhResult wbh_renderer_set_quality(
    WbhRendererHandle* handle,
    WbhRenderQuality quality) noexcept;
WBH_API void wbh_renderer_destroy(WbhRendererHandle* handle) noexcept;
