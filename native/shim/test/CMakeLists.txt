add_executable(render_to_png render_to_png.cpp)
target_include_directories(render_to_png PRIVATE ${CMAKE_CURRENT_SOURCE_DIR}/../include)
target_link_libraries(render_to_png PRIVATE litehtml_shim PkgConfig::CAIRO)
set_target_properties(render_to_png PROPERTIES
	CXX_STANDARD 17
	RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib
)
