cmake_minimum_required(VERSION 3.11)

find_package(PkgConfig REQUIRED)
pkg_check_modules(CAIRO REQUIRED IMPORTED_TARGET cairo)
pkg_check_modules(PANGO REQUIRED IMPORTED_TARGET pango)
pkg_check_modules(PANGOCAIRO REQUIRED IMPORTED_TARGET pangocairo)
pkg_check_modules(GDK_PIXBUF REQUIRED IMPORTED_TARGET gdk-pixbuf-2.0)

# One self-contained shared object: litehtml + gumbo are linked in statically (both
# built as static libs by litehtml's own CMakeLists.txt by default) so the only runtime
# dependencies this .so has are system Cairo/Pango/gdk-pixbuf/glib - never a separately-
# installed litehtml package that could vanish out from under the app the way
# libwebkit2gtk-4.0 did (see the plan's Context section for why this matters here).
add_library(litehtml_shim SHARED
	src/shim_container.cpp
	src/litehtml_shim.cpp
)

target_include_directories(litehtml_shim PRIVATE
	include
	${CMAKE_CURRENT_SOURCE_DIR}/../third_party/litehtml/containers/cairo
)

target_link_libraries(litehtml_shim PRIVATE
	litehtml
	PkgConfig::CAIRO
	PkgConfig::PANGO
	PkgConfig::PANGOCAIRO
	PkgConfig::GDK_PIXBUF
)

set_target_properties(litehtml_shim PROPERTIES
	CXX_STANDARD 17
	CXX_STANDARD_REQUIRED ON
	POSITION_INDEPENDENT_CODE ON
)

# containers/cairo/*.cpp isn't part of litehtml's own CMake target (it's a separate
# reference-implementation folder meant for consumers to build themselves) - compile the
# two .cpp files this shim actually needs directly into litehtml_shim.
target_sources(litehtml_shim PRIVATE
	${CMAKE_CURRENT_SOURCE_DIR}/../third_party/litehtml/containers/cairo/container_cairo.cpp
	${CMAKE_CURRENT_SOURCE_DIR}/../third_party/litehtml/containers/cairo/container_cairo_pango.cpp
	${CMAKE_CURRENT_SOURCE_DIR}/../third_party/litehtml/containers/cairo/cairo_borders.cpp
	${CMAKE_CURRENT_SOURCE_DIR}/../third_party/litehtml/containers/cairo/conic_gradient.cpp
)

set_target_properties(litehtml_shim PROPERTIES
	LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
	RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
)

if (LITEHTML_SHIM_BUILD_TESTS)
	add_subdirectory(test)
endif()
