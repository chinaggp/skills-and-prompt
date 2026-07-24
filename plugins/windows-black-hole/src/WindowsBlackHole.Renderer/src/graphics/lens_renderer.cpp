#include "graphics/lens_renderer.h"

#include <d3dcompiler.h>

#include <array>
#include <fstream>
#include <stdexcept>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace
{
void ThrowIfFailed(const HRESULT result, const char* operation)
{
    if (FAILED(result))
    {
        throw std::runtime_error(operation);
    }
}

std::vector<std::uint8_t> ReadBinary(const std::filesystem::path& path)
{
    std::ifstream stream(path, std::ios::binary | std::ios::ate);
    if (!stream)
    {
        throw std::runtime_error("GravitationalLens.cso was not found");
    }

    const std::streampos end = stream.tellg();
    if (end <= 0)
    {
        throw std::runtime_error("GravitationalLens.cso is empty");
    }

    std::vector<std::uint8_t> bytes(static_cast<std::size_t>(end));
    stream.seekg(0, std::ios::beg);
    stream.read(
        reinterpret_cast<char*>(bytes.data()),
        static_cast<std::streamsize>(bytes.size()));
    if (!stream)
    {
        throw std::runtime_error("GravitationalLens.cso could not be read");
    }
    return bytes;
}

constexpr char kFullscreenVertexShader[] = R"(
struct VertexOutput
{
    float4 position : SV_Position;
    float2 uv : TEXCOORD0;
};

VertexOutput VSMain(uint vertexId : SV_VertexID)
{
    VertexOutput output;
    float2 position = float2(
        (vertexId == 2) ? 3.0 : -1.0,
        (vertexId == 1) ? 3.0 : -1.0);
    output.position = float4(position, 0.0, 1.0);
    output.uv = float2(
        (vertexId == 2) ? 2.0 : 0.0,
        (vertexId == 1) ? -1.0 : 1.0);
    return output;
}
)";
} // namespace

namespace wbh
{
void LensRenderer::Initialize(
    const HWND hwnd,
    const std::uint32_t width,
    const std::uint32_t height)
{
    if (hwnd == nullptr || width == 0 || height == 0)
    {
        throw std::invalid_argument("Invalid renderer dimensions");
    }

    CreateDevice();
    CreateSwapChain(hwnd, width, height);
    CreateComposition(hwnd);
    CreateRenderTarget();
    CreatePipeline();
    width_ = width;
    height_ = height;
}

void LensRenderer::CreateDevice()
{
    UINT flags = D3D11_CREATE_DEVICE_BGRA_SUPPORT;
#if defined(_DEBUG)
    flags |= D3D11_CREATE_DEVICE_DEBUG;
#endif

    constexpr std::array feature_levels{
        D3D_FEATURE_LEVEL_11_1,
        D3D_FEATURE_LEVEL_11_0};
    D3D_FEATURE_LEVEL selected_level{};
    HRESULT result = D3D11CreateDevice(
        nullptr,
        D3D_DRIVER_TYPE_HARDWARE,
        nullptr,
        flags,
        feature_levels.data(),
        static_cast<UINT>(feature_levels.size()),
        D3D11_SDK_VERSION,
        device_.ReleaseAndGetAddressOf(),
        &selected_level,
        context_.ReleaseAndGetAddressOf());

#if defined(_DEBUG)
    if (FAILED(result))
    {
        flags &= ~D3D11_CREATE_DEVICE_DEBUG;
        result = D3D11CreateDevice(
            nullptr,
            D3D_DRIVER_TYPE_HARDWARE,
            nullptr,
            flags,
            feature_levels.data(),
            static_cast<UINT>(feature_levels.size()),
            D3D11_SDK_VERSION,
            device_.ReleaseAndGetAddressOf(),
            &selected_level,
            context_.ReleaseAndGetAddressOf());
    }
#endif

    ThrowIfFailed(result, "D3D11CreateDevice failed");
}

void LensRenderer::CreateSwapChain(
    const HWND,
    const std::uint32_t width,
    const std::uint32_t height)
{
    ComPtr<IDXGIDevice> dxgi_device;
    ThrowIfFailed(
        device_.As(&dxgi_device),
        "ID3D11Device does not implement IDXGIDevice");
    ComPtr<IDXGIAdapter> adapter;
    ThrowIfFailed(dxgi_device->GetAdapter(&adapter), "GetAdapter failed");
    ComPtr<IDXGIFactory2> factory;
    ThrowIfFailed(
        adapter->GetParent(IID_PPV_ARGS(&factory)),
        "GetParent IDXGIFactory2 failed");

    DXGI_SWAP_CHAIN_DESC1 description{};
    description.Width = width;
    description.Height = height;
    description.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
    description.SampleDesc.Count = 1;
    description.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
    description.BufferCount = 2;
    description.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
    description.AlphaMode = DXGI_ALPHA_MODE_PREMULTIPLIED;

    ThrowIfFailed(
        factory->CreateSwapChainForComposition(
            device_.Get(),
            &description,
            nullptr,
            swap_chain_.ReleaseAndGetAddressOf()),
        "CreateSwapChainForComposition failed");
}

void LensRenderer::CreateComposition(const HWND hwnd)
{
    ComPtr<IDXGIDevice> dxgi_device;
    ThrowIfFailed(device_.As(&dxgi_device), "IDXGIDevice query failed");
    ThrowIfFailed(
        DCompositionCreateDevice(
            dxgi_device.Get(),
            IID_PPV_ARGS(composition_device_.ReleaseAndGetAddressOf())),
        "DCompositionCreateDevice failed");
    ThrowIfFailed(
        composition_device_->CreateTargetForHwnd(
            hwnd,
            TRUE,
            composition_target_.ReleaseAndGetAddressOf()),
        "CreateTargetForHwnd failed");
    ThrowIfFailed(
        composition_device_->CreateVisual(
            composition_visual_.ReleaseAndGetAddressOf()),
        "CreateVisual failed");
    ThrowIfFailed(
        composition_visual_->SetContent(swap_chain_.Get()),
        "SetContent failed");
    ThrowIfFailed(
        composition_target_->SetRoot(composition_visual_.Get()),
        "SetRoot failed");
    ThrowIfFailed(composition_device_->Commit(), "DirectComposition commit failed");
}

void LensRenderer::CreateRenderTarget()
{
    ComPtr<ID3D11Texture2D> back_buffer;
    ThrowIfFailed(
        swap_chain_->GetBuffer(0, IID_PPV_ARGS(&back_buffer)),
        "Swap-chain GetBuffer failed");
    ThrowIfFailed(
        device_->CreateRenderTargetView(
            back_buffer.Get(),
            nullptr,
            render_target_.ReleaseAndGetAddressOf()),
        "CreateRenderTargetView failed");
}

void LensRenderer::CreatePipeline()
{
    ComPtr<ID3DBlob> vertex_bytecode;
    ComPtr<ID3DBlob> compile_errors;
    ThrowIfFailed(
        D3DCompile(
            kFullscreenVertexShader,
            sizeof(kFullscreenVertexShader) - 1,
            "FullscreenTriangle",
            nullptr,
            nullptr,
            "VSMain",
            "vs_5_0",
            D3DCOMPILE_ENABLE_STRICTNESS,
            0,
            vertex_bytecode.ReleaseAndGetAddressOf(),
            compile_errors.ReleaseAndGetAddressOf()),
        "Fullscreen vertex shader compilation failed");
    ThrowIfFailed(
        device_->CreateVertexShader(
            vertex_bytecode->GetBufferPointer(),
            vertex_bytecode->GetBufferSize(),
            nullptr,
            vertex_shader_.ReleaseAndGetAddressOf()),
        "CreateVertexShader failed");

    const std::vector<std::uint8_t> pixel_bytecode = ReadBinary(ShaderPath());
    ThrowIfFailed(
        device_->CreatePixelShader(
            pixel_bytecode.data(),
            pixel_bytecode.size(),
            nullptr,
            pixel_shader_.ReleaseAndGetAddressOf()),
        "CreatePixelShader failed");

    D3D11_SAMPLER_DESC sampler_description{};
    sampler_description.Filter = D3D11_FILTER_MIN_MAG_MIP_LINEAR;
    sampler_description.AddressU = D3D11_TEXTURE_ADDRESS_CLAMP;
    sampler_description.AddressV = D3D11_TEXTURE_ADDRESS_CLAMP;
    sampler_description.AddressW = D3D11_TEXTURE_ADDRESS_CLAMP;
    sampler_description.MaxLOD = D3D11_FLOAT32_MAX;
    ThrowIfFailed(
        device_->CreateSamplerState(
            &sampler_description,
            sampler_.ReleaseAndGetAddressOf()),
        "CreateSamplerState failed");

    D3D11_BUFFER_DESC buffer_description{};
    buffer_description.ByteWidth = sizeof(LensConstants);
    buffer_description.Usage = D3D11_USAGE_DEFAULT;
    buffer_description.BindFlags = D3D11_BIND_CONSTANT_BUFFER;
    ThrowIfFailed(
        device_->CreateBuffer(
            &buffer_description,
            nullptr,
            constants_.ReleaseAndGetAddressOf()),
        "CreateBuffer failed");

    D3D11_BLEND_DESC blend_description{};
    auto& target = blend_description.RenderTarget[0];
    target.BlendEnable = TRUE;
    target.SrcBlend = D3D11_BLEND_ONE;
    target.DestBlend = D3D11_BLEND_INV_SRC_ALPHA;
    target.BlendOp = D3D11_BLEND_OP_ADD;
    target.SrcBlendAlpha = D3D11_BLEND_ONE;
    target.DestBlendAlpha = D3D11_BLEND_INV_SRC_ALPHA;
    target.BlendOpAlpha = D3D11_BLEND_OP_ADD;
    target.RenderTargetWriteMask = D3D11_COLOR_WRITE_ENABLE_ALL;
    ThrowIfFailed(
        device_->CreateBlendState(
            &blend_description,
            blend_state_.ReleaseAndGetAddressOf()),
        "CreateBlendState failed");
}

void LensRenderer::Resize(
    const std::uint32_t width,
    const std::uint32_t height)
{
    if (width == 0 || height == 0 || (width == width_ && height == height_))
    {
        return;
    }

    context_->OMSetRenderTargets(0, nullptr, nullptr);
    render_target_.Reset();
    ThrowIfFailed(
        swap_chain_->ResizeBuffers(
            0,
            width,
            height,
            DXGI_FORMAT_UNKNOWN,
            0),
        "ResizeBuffers failed");
    CreateRenderTarget();
    width_ = width;
    height_ = height;
    ThrowIfFailed(composition_device_->Commit(), "DirectComposition commit failed");
}

HRESULT LensRenderer::Render(
    ID3D11ShaderResourceView* const captured_desktop,
    const float strength,
    const float dpi_scale,
    const float delta_seconds) noexcept
{
    if (context_ == nullptr || swap_chain_ == nullptr || render_target_ == nullptr)
    {
        return E_UNEXPECTED;
    }

    constexpr float clear_color[4]{0.0F, 0.0F, 0.0F, 0.0F};
    context_->ClearRenderTargetView(render_target_.Get(), clear_color);

    if (captured_desktop != nullptr)
    {
        const LensConstants values{
            {0.5F, 0.5F},
            0.47F,
            strength,
            {static_cast<float>(width_), static_cast<float>(height_)},
            dpi_scale,
            delta_seconds};
        context_->UpdateSubresource(constants_.Get(), 0, nullptr, &values, 0, 0);

        D3D11_VIEWPORT viewport{};
        viewport.Width = static_cast<float>(width_);
        viewport.Height = static_cast<float>(height_);
        viewport.MaxDepth = 1.0F;
        context_->RSSetViewports(1, &viewport);
        context_->OMSetRenderTargets(1, render_target_.GetAddressOf(), nullptr);
        constexpr float blend_factor[4]{};
        context_->OMSetBlendState(
            blend_state_.Get(),
            blend_factor,
            0xFFFFFFFFU);
        context_->IASetInputLayout(nullptr);
        context_->IASetPrimitiveTopology(D3D11_PRIMITIVE_TOPOLOGY_TRIANGLELIST);
        context_->VSSetShader(vertex_shader_.Get(), nullptr, 0);
        context_->PSSetShader(pixel_shader_.Get(), nullptr, 0);
        context_->PSSetShaderResources(0, 1, &captured_desktop);
        context_->PSSetSamplers(0, 1, sampler_.GetAddressOf());
        context_->PSSetConstantBuffers(0, 1, constants_.GetAddressOf());
        context_->Draw(3, 0);

        ID3D11ShaderResourceView* null_view = nullptr;
        context_->PSSetShaderResources(0, 1, &null_view);
    }

    const HRESULT result = swap_chain_->Present(1, 0);
    if (SUCCEEDED(result))
    {
        return device_->GetDeviceRemovedReason();
    }
    return result;
}

std::filesystem::path LensRenderer::ShaderPath()
{
    static int module_anchor = 0;
    HMODULE module{};
    if (!GetModuleHandleExW(
            GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
                GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&module_anchor),
            &module))
    {
        throw std::runtime_error("Renderer module path could not be resolved");
    }

    std::wstring path(32768, L'\0');
    const DWORD length =
        GetModuleFileNameW(module, path.data(), static_cast<DWORD>(path.size()));
    if (length == 0 || length == path.size())
    {
        throw std::runtime_error("Renderer module path could not be read");
    }
    path.resize(length);
    return std::filesystem::path(path).parent_path() /
           L"shaders" /
           L"GravitationalLens.cso";
}
} // namespace wbh
