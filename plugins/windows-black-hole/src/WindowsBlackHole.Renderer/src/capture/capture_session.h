#pragma once

#include <Windows.h>
#include <d3d11.h>
#include <wrl/client.h>

#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Graphics.Capture.h>
#include <winrt/Windows.Graphics.DirectX.Direct3D11.h>

#include <atomic>
#include <cstdint>

namespace wbh
{
enum class CaptureStatus : std::uint32_t
{
    Stopped,
    Starting,
    Active,
    Fallback
};

class CaptureSession final
{
public:
    CaptureSession(
        ID3D11Device* device,
        ID3D11DeviceContext* context);
    ~CaptureSession();

    CaptureSession(const CaptureSession&) = delete;
    CaptureSession& operator=(const CaptureSession&) = delete;

    bool Start(HWND overlay_hwnd) noexcept;
    void Stop() noexcept;
    bool RefreshMonitor(HWND overlay_hwnd) noexcept;
    bool SetCropSize(std::uint32_t width, std::uint32_t height) noexcept;
    // Polls the free-threaded frame pool and copies only the latest overlay
    // crop. ABI calls are serialized by the managed render loop.
    bool TryAcquireFrame() noexcept;

    [[nodiscard]] CaptureStatus status() const noexcept
    {
        return status_.load(std::memory_order_acquire);
    }

    [[nodiscard]] ID3D11ShaderResourceView* frame_view() const noexcept
    {
        return crop_view_.Get();
    }
    [[nodiscard]] bool has_frame() const noexcept
    {
        return status() == CaptureStatus::Active && crop_view_ != nullptr;
    }

private:
    bool StartForMonitor(HMONITOR monitor, HWND overlay_hwnd);
    bool CreateCropTexture(std::uint32_t width, std::uint32_t height);
    void SetFallback() noexcept;

    Microsoft::WRL::ComPtr<ID3D11Device> device_;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> context_;
    winrt::Windows::Graphics::DirectX::Direct3D11::IDirect3DDevice
        winrt_device_{nullptr};
    winrt::Windows::Graphics::Capture::GraphicsCaptureItem item_{nullptr};
    winrt::Windows::Graphics::Capture::Direct3D11CaptureFramePool
        frame_pool_{nullptr};
    winrt::Windows::Graphics::Capture::GraphicsCaptureSession session_{nullptr};
    Microsoft::WRL::ComPtr<ID3D11Texture2D> crop_texture_;
    Microsoft::WRL::ComPtr<ID3D11ShaderResourceView> crop_view_;
    Microsoft::WRL::ComPtr<ID3D11RenderTargetView> crop_render_target_;
    std::atomic<CaptureStatus> status_{CaptureStatus::Stopped};
    HMONITOR monitor_{};
    HWND overlay_hwnd_{};
    RECT monitor_rect_{};
    std::uint32_t crop_width_{};
    std::uint32_t crop_height_{};
    winrt::Windows::Graphics::SizeInt32 content_size_{};
};
} // namespace wbh
