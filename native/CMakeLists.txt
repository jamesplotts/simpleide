cmake_minimum_required(VERSION 3.11)

project(simpleide_html_native LANGUAGES C CXX)

# litehtml itself (vendored as a git submodule, pinned to v0.10 - see
# third_party/litehtml). Its own CMakeLists.txt builds a static "litehtml" target and
# vendors gumbo-parser (HTML5 parser) automatically since EXTERNAL_GUMBO defaults OFF.
add_subdirectory(third_party/litehtml)

# The C-ABI shim SimpleIDE's VB.NET side P/Invokes into - see shim/CMakeLists.txt
add_subdirectory(shim)
