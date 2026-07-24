#include "wbh_renderer_api.h"

#include "capture/capture_session.h"
#include "graphics/lens_renderer.h"
#include "overlay/overlay_window.h"

#include <algorithm>
#include <atomic>
#include <cmath>
#include <cstddef>
#include <cstdint>
#include <memory>

namespace
{
struct GpuMutex
{
    SRWLOCK value = SRWLOCK_INIT;
};

class GpuLockGuard final
{
public:
    explicit GpuLockGuard(GpuMutex& mutex) noexcept : mutex_(mutex)
    {
        AcquireSRWLockExclusive(&mutex_.value);
    }

    ~GpuLockGuard()
    {
        ReleaseSRWLockExclusive(&mutex_.value);
    }

    GpuLockGuard(const GpuLockGuard&) = delete;
    GpuLockGuard& operator=(const GpuLockGuard&) = delete;

private:
    GpuMutex& mutex_;
};
} // namespace

struct WbhRendererHandle
{
    wbh::OverlayWindow overlay;
    wbh::LensRenderer renderer;
    std::unique_ptr<GpuMutex> gpu_mutex{std::make_unique<GpuMutex>()};
    std::unique_ptr<wbh::CaptureSession> capture;
    std::atomic<WbhResult> last_result{WBH_OK};
    std::uint32_t width{};
    std::uint32_t height{};
    float dpi_scale{1.0F};
    float strength{1.0F};
    WbhRenderQuality quality{WBH_QUALITY_HIGH};
    bool capture_started{};
    bool diagnostics_frozen{};
};

namespace
{
constexpr std::uint32_t kAbiVersion = 1;
constexpr std::uint32_t kMaximumDimension = 16384;

bool IsValidDimension(const std::uint32_t value) noexcept
{
    return value > 0 && value <= kMaximumDimension;
}

bool IsValidConfig(const WbhRendererConfig* const config) noexcept
{
    return config != nullptr &&
           config->struct_size == sizeof(WbhRendererConfig) &&
           IsValidDimension(config->width) &&
           IsValidDimension(config->height) &&
           std::isfinite(config->dpi_scale) &&
           config->dpi_scale > 0.0F &&
           config->dpi_scale <= 8.0F;
}

WbhResult StoreResult(
    WbhRendererHandle* const handle,
    const WbhResult result) noexcept
{
    if (handle != nullptr)
    {
        handle->last_result.store(result, std::memory_order_release);
    }
    return result;
}

WbhResult SetBoundsCore(
    WbhRendererHandle* const handle,
    const std::int32_t x,
    const std::int32_t y,
    const std::uint32_t width,
    const std::uint32_t height) noexcept
{
    if (handle == nullptr ||
        !IsValidDimension(width) ||
        !IsValidDimension(height))
    {
        return StoreResult(handle, WBH_INVALID_ARGUMENT);
    }

    try
    {
        if (!handle->overlay.SetBounds(x, y, width, height))
        {
            return StoreResult(handle, WBH_WINDOW_CREATION_FAILED);
        }

        GpuLockGuard gpu_lock(*handle->gpu_mutex);
        handle->renderer.Resize(width, height);
        if (!handle->capture->SetCropSize(width, height))
        {
            return StoreResult(handle, WBH_GRAPHICS_INITIALIZATION_FAILED);
        }

        handle->width = width;
        handle->height = height;
        return StoreResult(handle, WBH_OK);
    }
    catch (...)
    {
        return StoreResult(handle, WBH_GRAPHICS_INITIALIZATION_FAILED);
    }
}
} // namespace

std::uint32_t wbh_renderer_abi_version() noexcept
{
    return kAbiVersion;
}

WbhResult wbh_renderer_create(
    const WbhRendererConfig* const config,
    WbhRendererHandle** const handle) noexcept
{
    if (!IsValidConfig(config) || handle == nullptr)
    {
        return WBH_INVALID_ARGUMENT;
    }
    *handle = nullptr;

    try
    {
        auto state = std::make_unique<WbhRendererHandle>();
        state->width = config->width;
        state->height = config->height;
        state->dpi_scale = config->dpi_scale;

        if (!state->overlay.Create(config->width, config->height))
        {
            return WBH_WINDOW_CREATION_FAILED;
        }

        state->renderer.Initialize(
            state->overlay.hwnd(),
            config->width,
            config->height);
        state->capture = std::make_unique<wbh::CaptureSession>(
            state->renderer.device(),
            state->renderer.context());
        if (!state->capture->SetCropSize(config->width, config->height))
        {
            return WBH_GRAPHICS_INITIALIZATION_FAILED;
        }

        *handle = state.release();
        return WBH_OK;
    }
    catch (...)
    {
        return WBH_GRAPHICS_INITIALIZATION_FAILED;
    }
}

HWND wbh_renderer_get_hwnd(WbhRendererHandle* const handle) noexcept
{
    return handle != nullptr ? handle->overlay.hwnd() : nullptr;
}

WbhResult wbh_renderer_start(WbhRendererHandle* const handle) noexcept
{
    if (handle == nullptr)
    {
        return WBH_INVALID_ARGUMENT;
    }
    if (!handle->overlay.SetCaptureExcluded(true))
    {
        handle->capture_started = false;
        handle->capture->Stop();
        return StoreResult(handle, WBH_CAPTURE_UNAVAILABLE);
    }
    handle->diagnostics_frozen = false;
    if (!handle->overlay.capture_exclusion_applied() ||
        handle->quality == WBH_QUALITY_FALLBACK)
    {
        handle->capture_started = false;
        handle->capture->Stop();
        return StoreResult(handle, WBH_CAPTURE_UNAVAILABLE);
    }

    if (!handle->capture->Start(handle->overlay.hwnd()))
    {
        handle->capture_started = false;
        return StoreResult(handle, WBH_CAPTURE_UNAVAILABLE);
    }
    handle->capture_started = true;
    return StoreResult(handle, WBH_OK);
}

WbhResult wbh_renderer_set_bounds(
    WbhRendererHandle* const handle,
    const std::int32_t x,
    const std::int32_t y,
    const std::uint32_t width,
    const std::uint32_t height) noexcept
{
    return SetBoundsCore(handle, x, y, width, height);
}

WbhResult wbh_renderer_set_strength(
    WbhRendererHandle* const handle,
    const float strength) noexcept
{
    if (handle == nullptr || !std::isfinite(strength))
    {
        return StoreResult(handle, WBH_INVALID_ARGUMENT);
    }
    handle->strength = std::clamp(strength, 0.0F, 1.0F);
    return StoreResult(handle, WBH_OK);
}

WbhResult wbh_renderer_tick(
    WbhRendererHandle* const handle,
    const float delta_seconds) noexcept
{
    if (handle == nullptr ||
        !std::isfinite(delta_seconds) ||
        delta_seconds < 0.0F)
    {
        return StoreResult(handle, WBH_INVALID_ARGUMENT);
    }
    if (handle->diagnostics_frozen)
    {
        return StoreResult(handle, WBH_OK);
    }

    if (handle->capture_started &&
        !handle->capture->RefreshMonitor(handle->overlay.hwnd()))
    {
        handle->capture_started = false;
    }

    GpuLockGuard gpu_lock(*handle->gpu_mutex);
    handle->capture->TryAcquireFrame();
    ID3D11ShaderResourceView* frame_view = nullptr;
    if (handle->capture->status() == wbh::CaptureStatus::Active)
    {
        frame_view = handle->capture->frame_view();
    }
    const HRESULT render_result = handle->renderer.Render(
        frame_view,
        handle->strength,
        handle->dpi_scale,
        delta_seconds);
    if (FAILED(render_result))
    {
        return StoreResult(handle, WBH_DEVICE_LOST);
    }
    return StoreResult(handle, WBH_OK);
}

std::int32_t wbh_renderer_is_capture_active(
    WbhRendererHandle* const handle) noexcept
{
    if (handle == nullptr)
    {
        return 0;
    }
    return handle->capture->status() == wbh::CaptureStatus::Active ? 1 : 0;
}

WbhResult wbh_renderer_last_result(
    WbhRendererHandle* const handle) noexcept
{
    return handle != nullptr
        ? handle->last_result.load(std::memory_order_acquire)
        : WBH_INVALID_ARGUMENT;
}

WbhResult wbh_renderer_freeze_for_diagnostics(
    WbhRendererHandle* const handle) noexcept
{
    if (handle == nullptr)
    {
        return WBH_INVALID_ARGUMENT;
    }

    try
    {
        GpuLockGuard gpu_lock(*handle->gpu_mutex);
        if (!handle->capture_started || !handle->capture->has_frame())
        {
            return StoreResult(handle, WBH_CAPTURE_UNAVAILABLE);
        }

        const HRESULT render_result = handle->renderer.Render(
            handle->capture->frame_view(),
            handle->strength,
            handle->dpi_scale,
            0.0F);
        if (FAILED(render_result))
        {
            return StoreResult(handle, WBH_DEVICE_LOST);
        }

        handle->capture->Stop();
        handle->capture_started = false;
        if (!handle->overlay.SetCaptureExcluded(false))
        {
            return StoreResult(handle, WBH_WINDOW_CREATION_FAILED);
        }
        handle->diagnostics_frozen = true;
        return StoreResult(handle, WBH_OK);
    }
    catch (...)
    {
        return StoreResult(handle, WBH_GRAPHICS_INITIALIZATION_FAILED);
    }
}

void wbh_renderer_shutdown(WbhRendererHandle* const handle) noexcept
{
    try
    {
        delete handle;
    }
    catch (...)
    {
    }
}

WbhResult wbh_renderer_move(
    WbhRendererHandle* const handle,
    const std::int32_t x,
    const std::int32_t y) noexcept
{
    if (handle == nullptr)
    {
        return WBH_INVALID_ARGUMENT;
    }
    return SetBoundsCore(handle, x, y, handle->width, handle->height);
}

WbhResult wbh_renderer_resize(
    WbhRendererHandle* const handle,
    const WbhRendererConfig* const config) noexcept
{
    if (handle == nullptr || !IsValidConfig(config))
    {
        return StoreResult(handle, WBH_INVALID_ARGUMENT);
    }

    RECT bounds{};
    if (!GetWindowRect(handle->overlay.hwnd(), &bounds))
    {
        return StoreResult(handle, WBH_WINDOW_CREATION_FAILED);
    }
    handle->dpi_scale = config->dpi_scale;
    return SetBoundsCore(
        handle,
        bounds.left,
        bounds.top,
        config->width,
        config->height);
}

WbhResult wbh_renderer_set_quality(
    WbhRendererHandle* const handle,
    const WbhRenderQuality quality) noexcept
{
    if (handle == nullptr || quality > WBH_QUALITY_HIGH)
    {
        return StoreResult(handle, WBH_INVALID_ARGUMENT);
    }

    const WbhRenderQuality previous = handle->quality;
    handle->quality = quality;
    if (quality == WBH_QUALITY_FALLBACK)
    {
        handle->diagnostics_frozen = false;
        handle->capture_started = false;
        handle->capture->Stop();
        return StoreResult(handle, WBH_OK);
    }
    if (previous == WBH_QUALITY_FALLBACK)
    {
        return wbh_renderer_start(handle);
    }
    return StoreResult(handle, WBH_OK);
}

void wbh_renderer_destroy(WbhRendererHandle* const handle) noexcept
{
    wbh_renderer_shutdown(handle);
}
