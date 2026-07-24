#pragma once

namespace wbh
{
struct Float2
{
    float x;
    float y;

    friend constexpr bool operator==(const Float2&, const Float2&) = default;
};

[[nodiscard]] Float2 map_lens(
    Float2 uv,
    Float2 center,
    float influence_radius,
    float strength) noexcept;
} // namespace wbh
