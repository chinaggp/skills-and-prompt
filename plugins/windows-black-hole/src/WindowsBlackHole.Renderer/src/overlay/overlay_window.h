#pragma once

#include <Windows.h>
#include <cstdint>

namespace wbh
{
class OverlayWindow final
{
public:
    OverlayWindow() = default;
    ~OverlayWindow();

    OverlayWindow(const OverlayWindow&) = delete;
    OverlayWindow& operator=(const OverlayWindow&) = delete;

    bool Create(std::uint32_t width, std::uint32_t height) noexcept;
    bool SetBounds(
        std::int32_t x,
        std::int32_t y,
        std::uint32_t width,
        std::uint32_t height) noexcept;
    bool SetCaptureExcluded(bool excluded) noexcept;

    [[nodiscard]] HWND hwnd() const noexcept { return hwnd_; }
    [[nodiscard]] bool capture_exclusion_applied() const noexcept
    {
        return capture_exclusion_applied_;
    }

    static LRESULT CALLBACK WindowProc(
        HWND hwnd,
        UINT message,
        WPARAM wparam,
        LPARAM lparam) noexcept;

private:
    HWND hwnd_{};
    bool capture_exclusion_applied_{};
};
} // namespace wbh
