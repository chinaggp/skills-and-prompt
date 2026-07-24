#include "graphics/lens_math.h"

#include <cstdlib>
#include <cmath>

namespace
{
bool nearly_equal(const float left, const float right) noexcept
{
    return std::abs(left - right) <= 0.00001F;
}

void require(const bool condition) noexcept
{
    if (!condition)
    {
        std::abort();
    }
}
} // namespace

int main()
{
    using wbh::Float2;
    using wbh::map_lens;

    require((map_lens(
                {0.5F, 0.5F},
                {0.5F, 0.5F},
                0.47F,
                0.0F) == Float2{0.5F, 0.5F}));
    require(nearly_equal(
        map_lens({0.97F, 0.5F}, {0.5F, 0.5F}, 0.47F, 1.0F).x,
        0.97F));
    require(std::isfinite(
        map_lens({0.5001F, 0.5F}, {0.5F, 0.5F}, 0.47F, 1.0F).x));

    const Float2 center =
        map_lens({0.5F, 0.5F}, {0.5F, 0.5F}, 0.47F, 1.0F);
    require(std::isfinite(center.x));
    require(std::isfinite(center.y));

    const Float2 clamped =
        map_lens({0.999F, 0.999F}, {0.999F, 0.999F}, 0.47F, 2.0F);
    require(clamped.x >= 0.0F && clamped.x <= 1.0F);
    require(clamped.y >= 0.0F && clamped.y <= 1.0F);
    return 0;
}
