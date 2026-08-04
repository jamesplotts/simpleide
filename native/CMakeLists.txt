cmake_minimum_required(VERSION 3.11)

project(simpleide_html_native LANGUAGES C CXX)

# litehtml/gumbo build as static libs (their own CMakeLists.txt default), but the shim
# links them into a shared library (liblitehtml_shim.so) - every object involved needs
# position-independent code or the final link fails ("recompile with -fPIC"). Must be set
# before add_subdirectory(third_party/litehtml) so it applies to those targets too.
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# litehtml itself (vendored as a git submodule, pinned to v0.10 - see
# third_party/litehtml). Its own CMakeLists.txt builds a static "litehtml" target and
# vendors gumbo-parser (HTML5 parser) automatically since EXTERNAL_GUMBO defaults OFF.
add_subdirectory(third_party/litehtml)

# The C-ABI shim SimpleIDE's VB.NET side P/Invokes into - see shim/CMakeLists.txt
add_subdirectory(shim)
