cmake_minimum_required(VERSION 3.24)

project(WindowsBlackHoleRenderer LANGUAGES CXX)

if(NOT WIN32)
    message(FATAL_ERROR "WindowsBlackHole.Renderer requires Windows 10 or later.")
endif()

add_library(WindowsBlackHole.Renderer SHARED
    src/capture/capture_session.cpp
    src/graphics/lens_math.cpp
    src/graphics/lens_renderer.cpp
    src/overlay/overlay_window.cpp
    src/renderer_api.cpp
)

target_compile_features(WindowsBlackHole.Renderer PRIVATE cxx_std_20)
target_compile_definitions(WindowsBlackHole.Renderer PRIVATE
    WBH_RENDERER_EXPORTS
    UNICODE
    _UNICODE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
    _WIN32_WINNT=0x0A00
)
target_include_directories(WindowsBlackHole.Renderer
    PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}/include"
    PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src"
)
target_compile_options(WindowsBlackHole.Renderer PRIVATE
    $<$<CXX_COMPILER_ID:MSVC>:/W4 /WX /permissive- /await>
)
target_link_libraries(WindowsBlackHole.Renderer PRIVATE
    d3d11
    d3dcompiler
    dcomp
    dxgi
    dwmapi
    runtimeobject
    user32
    windowsapp
)

set(WBH_SHADER_SOURCE
    "${CMAKE_CURRENT_SOURCE_DIR}/../../assets/shaders/GravitationalLens.hlsl")
set(WBH_SHADER_OUTPUT
    "${CMAKE_CURRENT_BINARY_DIR}/shaders/GravitationalLens.cso")

find_program(WBH_FXC_EXECUTABLE
    NAMES fxc.exe fxc
    HINTS
        "$ENV{WindowsSdkDir}/bin/$ENV{WindowsSDKVersion}/x64"
        "C:/Program Files (x86)/Windows Kits/10/bin/${CMAKE_VS_WINDOWS_TARGET_PLATFORM_VERSION}/x64"
)

if(NOT WBH_FXC_EXECUTABLE)
    message(FATAL_ERROR "fxc.exe was not found. Install the Windows SDK DirectX shader tools.")
endif()

add_custom_command(
    OUTPUT "${WBH_SHADER_OUTPUT}"
    COMMAND "${CMAKE_COMMAND}" -E make_directory "${CMAKE_CURRENT_BINARY_DIR}/shaders"
    COMMAND "${WBH_FXC_EXECUTABLE}" /nologo /T ps_5_0 /E PSMain
            /Fo "${WBH_SHADER_OUTPUT}" "${WBH_SHADER_SOURCE}"
    DEPENDS "${WBH_SHADER_SOURCE}"
    VERBATIM
)
add_custom_target(WindowsBlackHole.Shaders ALL DEPENDS "${WBH_SHADER_OUTPUT}")
add_dependencies(WindowsBlackHole.Renderer WindowsBlackHole.Shaders)
add_custom_command(TARGET WindowsBlackHole.Renderer POST_BUILD
    COMMAND "${CMAKE_COMMAND}" -E make_directory
            "$<TARGET_FILE_DIR:WindowsBlackHole.Renderer>/shaders"
    COMMAND "${CMAKE_COMMAND}" -E copy_if_different
            "${WBH_SHADER_OUTPUT}"
            "$<TARGET_FILE_DIR:WindowsBlackHole.Renderer>/shaders/GravitationalLens.cso"
    VERBATIM
)

include(CTest)
if(BUILD_TESTING)
    add_executable(lens_math_tests
        tests/lens_math_tests.cpp
        src/graphics/lens_math.cpp
    )
    target_compile_features(lens_math_tests PRIVATE cxx_std_20)
    target_compile_options(lens_math_tests PRIVATE
        $<$<CXX_COMPILER_ID:MSVC>:/W4 /WX /permissive->
    )
    target_include_directories(lens_math_tests PRIVATE "${CMAKE_CURRENT_SOURCE_DIR}/src")
    add_test(NAME lens_math_tests COMMAND lens_math_tests)

    add_executable(renderer_smoke_tests tests/renderer_smoke_tests.cpp)
    target_compile_features(renderer_smoke_tests PRIVATE cxx_std_20)
    target_compile_definitions(renderer_smoke_tests PRIVATE
        UNICODE
        _UNICODE
        WIN32_LEAN_AND_MEAN
        NOMINMAX
        _WIN32_WINNT=0x0A00
    )
    target_compile_options(renderer_smoke_tests PRIVATE
        $<$<CXX_COMPILER_ID:MSVC>:/W4 /WX /permissive->
    )
    target_link_libraries(renderer_smoke_tests PRIVATE
        WindowsBlackHole.Renderer
        runtimeobject
        user32
    )
    add_test(NAME renderer_smoke_tests COMMAND renderer_smoke_tests)

    find_program(WBH_DOTNET_EXECUTABLE NAMES dotnet)
    if(WBH_DOTNET_EXECUTABLE)
        set(WBH_MANAGED_SMOKE_PROJECT
            "${CMAKE_CURRENT_SOURCE_DIR}/tests/ManagedInteropSmoke/ManagedInteropSmoke.csproj")
        add_test(
            NAME managed_interop_sta
            COMMAND "${WBH_DOTNET_EXECUTABLE}" run
                    --project "${WBH_MANAGED_SMOKE_PROJECT}"
                    --configuration Release
                    --
                    "$<TARGET_FILE:WindowsBlackHole.Renderer>"
                    sta
        )
        add_test(
            NAME managed_interop_mta
            COMMAND "${WBH_DOTNET_EXECUTABLE}" run
                    --project "${WBH_MANAGED_SMOKE_PROJECT}"
                    --configuration Release
                    --
                    "$<TARGET_FILE:WindowsBlackHole.Renderer>"
                    mta
        )
        set_tests_properties(
            managed_interop_sta
            managed_interop_mta
            PROPERTIES TIMEOUT 30
        )
    endif()
endif()
