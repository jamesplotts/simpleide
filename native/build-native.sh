#!/bin/bash

# Builds the litehtml native shim (liblitehtml_shim.so) used by
# Widgets/CustomDrawHtmlView.vb. Deliberately NOT wired into `dotnet build` - a plain
# .NET-only build/contributor never needs cmake/g++ installed. Run this manually whenever
# native/ changes, then `dotnet build` picks up the resulting .so via SimpleIDE.vbproj's
# Exists()-conditioned <None> item.
echo "Building litehtml native shim..."
echo "================================="

cd "$(dirname "$0")"

if ! command -v cmake &> /dev/null; then
    echo "cmake not found - install with: sudo apt install cmake pkg-config libcairo2-dev libpango1.0-dev"
    exit 1
fi

cmake -S . -B build -DCMAKE_BUILD_TYPE=Release -DLITEHTML_SHIM_BUILD_TESTS=ON
cmake --build build --parallel

if [ $? -eq 0 ]; then
    echo ""
    echo "Build succeeded!"
    echo "Shim: $(pwd)/build/lib/liblitehtml_shim.so"
else
    echo ""
    echo "Build failed."
    exit 1
fi
