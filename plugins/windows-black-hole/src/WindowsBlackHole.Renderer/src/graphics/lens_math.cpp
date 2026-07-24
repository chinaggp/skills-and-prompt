#include "graphics/lens_math.h"

#include <algorithm>
#include <cmath>

namespace wbh
{
Float2 map_lens(
    const Float2 uv,
    const Float2 center,
    const float influence_radius,
    const float strength) noexcept
{
    if (strength <= 0.0F || influence_radius <= 0.0F)
    {
        return uv;
    }

    const float delta_x = uv.x - center.x;
    const float delta_y = uv.y - center.y;
    const float distance = std::sqrt(delta_x * delta_x + delta_y * delta_y);
    const float safe_distance = std::max(distance, 0.0001F);
    const float influence =
        std::clamp(1.0F - safe_distance / influence_radius, 0.0F, 1.0F);
    const float eased = influence * influence * (3.0F - 2.0F * influence);
    const float clamped_strength = std::clamp(strength, 0.0F, 1.0F);
    const float radial_offset =
        0.055F * eased * eased * clamped_strength;
    const float tangential_offset =
        0.018F * eased * clamped_strength;

    const float radial_x = distance > 0.0001F ? delta_x / safe_distance : 0.0F;
    const float radial_y = distance > 0.0001F ? delta_y / safe_distance : 0.0F;
    const Float2 mapped{
        uv.x + radial_x * radial_offset - radial_y * tangential_offset,
        uv.y + radial_y * radial_offset + radial_x * tangential_offset};

    return {
        std::clamp(mapped.x, 0.0F, 1.0F),
        std::clamp(mapped.y, 0.0F, 1.0F)};
}
} // namespace wbh
