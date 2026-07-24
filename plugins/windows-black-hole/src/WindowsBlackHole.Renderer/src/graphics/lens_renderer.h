#pragma once

#include <Windows.h>
#include <d3d11.h>
#include <dcomp.h>
#include <dxgi1_2.h>
#include <wrl/client.h>

#include <cstdint>
#include <filesystem>

namespace wbh
{
class LensRenderer final
{
public:
    LensRenderer() = default;
    ~LensRenderer() = default;

    LensRenderer(const LensRenderer&) = delete;
    LensRenderer& operator=(const LensRenderer&) = delete;

    void Initialize(HWND hwnd, std::uint32_t width, std::uint32_t height);
    void Resize(std::uint32_t width, std::uint32_t height);
    HRESULT Render(
        ID3D11ShaderResourceView* captured_desktop,
        float strength,
        float dpi_scale,
        float delta_seconds) noexcept;

    [[nodiscard]] ID3D11Device* device() const noexcept
    {
        return device_.Get();
    }

    [[nodiscard]] ID3D11DeviceContext* context() const noexcept
    {
        return context_.Get();
    }

private:
    struct alignas(16) LensConstants
    {
        float center[2];
        float influence_radius;
        float strength;
        float texture_size[2];
        float dpi_scale;
        float delta_seconds;
    };

    void CreateDevice();
    void CreateSwapChain(HWND hwnd, std::uint32_t width, std::uint32_t height);
    void CreateComposition(HWND hwnd);
    void CreateRenderTarget();
    void CreatePipeline();
    [[nodiscard]] static std::filesystem::path ShaderPath();

    Microsoft::WRL::ComPtr<ID3D11Device> device_;
    Microsoft::WRL::ComPtr<ID3D11DeviceContext> context_;
    Microsoft::WRL::ComPtr<IDXGISwapChain1> swap_chain_;
    Microsoft::WRL::ComPtr<ID3D11RenderTargetView> render_target_;
    Microsoft::WRL::ComPtr<ID3D11VertexShader> vertex_shader_;
    Microsoft::WRL::ComPtr<ID3D11PixelShader> pixel_shader_;
    Microsoft::WRL::ComPtr<ID3D11SamplerState> sampler_;
    Microsoft::WRL::ComPtr<ID3D11Buffer> constants_;
    Microsoft::WRL::ComPtr<ID3D11BlendState> blend_state_;
    Microsoft::WRL::ComPtr<IDCompositionDevice> composition_device_;
    Microsoft::WRL::ComPtr<IDCompositionTarget> composition_target_;
    Microsoft::WRL::ComPtr<IDCompositionVisual> composition_visual_;
    std::uint32_t width_{};
    std::uint32_t height_{};
};
} // namespace wbh
