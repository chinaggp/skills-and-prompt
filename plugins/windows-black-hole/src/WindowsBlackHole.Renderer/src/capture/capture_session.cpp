#include "capture/capture_session.h"

#include <windows.graphics.capture.interop.h>
#include <windows.graphics.directx.direct3d11.interop.h>

#include <algorithm>

using Microsoft::WRL::ComPtr;
namespace wf = winrt::Windows::Foundation;
namespace wg = winrt::Windows::Graphics;
namespace wgc = winrt::Windows::Graphics::Capture;
namespace wgdx = winrt::Windows::Graphics::DirectX;
namespace wgd3d = winrt::Windows::Graphics::DirectX::Direct3D11;

namespace wbh
{
CaptureSession::CaptureSession(
    ID3D11Device* const device,
    ID3D11DeviceContext* const context)
    : device_(device), context_(context)
{
}

CaptureSession::~CaptureSession()
{
    Stop();
}

bool CaptureSession::Start(const HWND overlay_hwnd) noexcept
{
    if (overlay_hwnd == nullptr)
    {
        SetFallback();
        return false;
    }

    const HMONITOR monitor =
        MonitorFromWindow(overlay_hwnd, MONITOR_DEFAULTTONEAREST);
    if (monitor == nullptr)
    {
        SetFallback();
        return false;
    }

    Stop();
    try
    {
        return StartForMonitor(monitor, overlay_hwnd);
    }
    catch (...)
    {
        SetFallback();
        return false;
    }
}

bool CaptureSession::StartForMonitor(
    const HMONITOR monitor,
    const HWND overlay_hwnd)
{
    status_.store(CaptureStatus::Starting, std::memory_order_release);
    overlay_hwnd_ = overlay_hwnd;
    monitor_ = monitor;

    MONITORINFO monitor_info{};
    monitor_info.cbSize = sizeof(monitor_info);
    if (!GetMonitorInfoW(monitor, &monitor_info))
    {
        throw winrt::hresult_error(HRESULT_FROM_WIN32(GetLastError()));
    }
    monitor_rect_ = monitor_info.rcMonitor;

    ComPtr<IDXGIDevice> dxgi_device;
    winrt::check_hresult(device_.As(&dxgi_device));
    winrt::com_ptr<::IInspectable> inspectable;
    winrt::check_hresult(CreateDirect3D11DeviceFromDXGIDevice(
        dxgi_device.Get(),
        inspectable.put()));
    winrt_device_ = inspectable.as<wgd3d::IDirect3DDevice>();

    const auto activation_factory =
        winrt::get_activation_factory<wgc::GraphicsCaptureItem>();
    const auto interop = activation_factory.as<IGraphicsCaptureItemInterop>();
    winrt::check_hresult(interop->CreateForMonitor(
        monitor,
        __uuidof(ABI::Windows::Graphics::Capture::IGraphicsCaptureItem),
        winrt::put_abi(item_)));

    content_size_ = item_.Size();
    frame_pool_ = wgc::Direct3D11CaptureFramePool::CreateFreeThreaded(
        winrt_device_,
        wgdx::DirectXPixelFormat::B8G8R8A8UIntNormalized,
        2,
        content_size_);
    session_ = frame_pool_.CreateCaptureSession(item_);
    session_.IsCursorCaptureEnabled(false);
    session_.StartCapture();
    return true;
}

void CaptureSession::Stop() noexcept
{
    try
    {
        if (session_ != nullptr)
        {
            session_.Close();
        }
        if (frame_pool_ != nullptr)
        {
            frame_pool_.Close();
        }
    }
    catch (...)
    {
    }

    session_ = nullptr;
    frame_pool_ = nullptr;
    item_ = nullptr;
    winrt_device_ = nullptr;
    monitor_ = nullptr;
    overlay_hwnd_ = nullptr;
    if (status_.load(std::memory_order_acquire) != CaptureStatus::Fallback)
    {
        status_.store(CaptureStatus::Stopped, std::memory_order_release);
    }
}

bool CaptureSession::RefreshMonitor(const HWND overlay_hwnd) noexcept
{
    const HMONITOR current =
        MonitorFromWindow(overlay_hwnd, MONITOR_DEFAULTTONEAREST);
    if (current == nullptr)
    {
        SetFallback();
        return false;
    }
    if (current == monitor_ && frame_pool_ != nullptr)
    {
        return true;
    }
    return Start(overlay_hwnd);
}

bool CaptureSession::SetCropSize(
    const std::uint32_t width,
    const std::uint32_t height) noexcept
{
    if (width == 0 || height == 0)
    {
        return false;
    }
    return CreateCropTexture(width, height);
}

bool CaptureSession::CreateCropTexture(
    const std::uint32_t width,
    const std::uint32_t height)
{
    if (crop_texture_ != nullptr &&
        width == crop_width_ &&
        height == crop_height_)
    {
        return true;
    }

    D3D11_TEXTURE2D_DESC description{};
    description.Width = width;
    description.Height = height;
    description.MipLevels = 1;
    description.ArraySize = 1;
    description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    description.SampleDesc.Count = 1;
    description.Usage = D3D11_USAGE_DEFAULT;
    description.BindFlags =
        D3D11_BIND_SHADER_RESOURCE | D3D11_BIND_RENDER_TARGET;

    ComPtr<ID3D11Texture2D> texture;
    if (FAILED(device_->CreateTexture2D(
            &description,
            nullptr,
            texture.GetAddressOf())))
    {
        return false;
    }
    ComPtr<ID3D11ShaderResourceView> view;
    if (FAILED(device_->CreateShaderResourceView(
            texture.Get(),
            nullptr,
            view.GetAddressOf())))
    {
        return false;
    }
    ComPtr<ID3D11RenderTargetView> render_target;
    if (FAILED(device_->CreateRenderTargetView(
            texture.Get(),
            nullptr,
            render_target.GetAddressOf())))
    {
        return false;
    }

    crop_texture_ = std::move(texture);
    crop_view_ = std::move(view);
    crop_render_target_ = std::move(render_target);
    crop_width_ = width;
    crop_height_ = height;
    return true;
}

bool CaptureSession::TryAcquireFrame() noexcept
{
    if (frame_pool_ == nullptr ||
        status_.load(std::memory_order_acquire) == CaptureStatus::Fallback)
    {
        return false;
    }

    try
    {
        const wgc::Direct3D11CaptureFrame frame =
            frame_pool_.TryGetNextFrame();
        if (frame == nullptr)
        {
            return false;
        }

        const wg::SizeInt32 frame_size = frame.ContentSize();
        if (frame_size.Width <= 0 || frame_size.Height <= 0)
        {
            return false;
        }
        if (frame_size.Width != content_size_.Width ||
            frame_size.Height != content_size_.Height)
        {
            content_size_ = frame_size;
            frame_pool_.Recreate(
                winrt_device_,
                wgdx::DirectXPixelFormat::B8G8R8A8UIntNormalized,
                2,
                content_size_);
        }

        const auto surface_access =
            frame.Surface().as<
                ::Windows::Graphics::DirectX::Direct3D11::
                    IDirect3DDxgiInterfaceAccess>();
        ComPtr<ID3D11Texture2D> source;
        winrt::check_hresult(surface_access->GetInterface(
            IID_PPV_ARGS(source.GetAddressOf())));

        RECT overlay_rect{};
        if (!GetWindowRect(overlay_hwnd_, &overlay_rect))
        {
            SetFallback();
            return false;
        }

        if (!CreateCropTexture(
                static_cast<std::uint32_t>(
                    std::max<LONG>(overlay_rect.right - overlay_rect.left, 1)),
                static_cast<std::uint32_t>(
                    std::max<LONG>(overlay_rect.bottom - overlay_rect.top, 1))))
        {
            SetFallback();
            return false;
        }

        constexpr float transparent[4]{0.0F, 0.0F, 0.0F, 0.0F};
        context_->ClearRenderTargetView(
            crop_render_target_.Get(),
            transparent);

        const LONG local_left = overlay_rect.left - monitor_rect_.left;
        const LONG local_top = overlay_rect.top - monitor_rect_.top;
        const LONG source_left = std::max<LONG>(local_left, 0);
        const LONG source_top = std::max<LONG>(local_top, 0);
        const LONG source_right = std::min<LONG>(
            local_left + static_cast<LONG>(crop_width_),
            frame_size.Width);
        const LONG source_bottom = std::min<LONG>(
            local_top + static_cast<LONG>(crop_height_),
            frame_size.Height);
        if (source_right <= source_left || source_bottom <= source_top)
        {
            status_.store(CaptureStatus::Active, std::memory_order_release);
            return true;
        }

        const D3D11_BOX source_box{
            static_cast<UINT>(source_left),
            static_cast<UINT>(source_top),
            0,
            static_cast<UINT>(source_right),
            static_cast<UINT>(source_bottom),
            1};
        const UINT destination_x =
            static_cast<UINT>(source_left - local_left);
        const UINT destination_y =
            static_cast<UINT>(source_top - local_top);
        context_->CopySubresourceRegion(
            crop_texture_.Get(),
            0,
            destination_x,
            destination_y,
            0,
            source.Get(),
            0,
            &source_box);
        status_.store(CaptureStatus::Active, std::memory_order_release);
        return true;
    }
    catch (...)
    {
        SetFallback();
        return false;
    }
}

void CaptureSession::SetFallback() noexcept
{
    status_.store(CaptureStatus::Fallback, std::memory_order_release);
}
} // namespace wbh
