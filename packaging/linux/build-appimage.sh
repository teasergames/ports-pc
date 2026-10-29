#!/usr/bin/env bash
set -euo pipefail
project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$project_root"
if [[ "$(uname -s)" != Linux || "$(uname -m)" != x86_64 ]]; then
  echo 'Este empacotamento exige Linux x86_64.' >&2; exit 1
fi
dotnet_bin="${DOTNET_BIN:-dotnet}"
output="$project_root/dist/linux"
appdir="$output/Nexus.AppDir"
mkdir -p "$output"
# The staging directory is generated; never remove application/user data.
rm -rf -- "$appdir"
mkdir -p "$appdir/usr/bin"
"$dotnet_bin" restore desktop/Nexus.Desktop/Nexus.Desktop.csproj -r linux-x64 --locked-mode
"$dotnet_bin" publish desktop/Nexus.Desktop/Nexus.Desktop.csproj -c Release -r linux-x64 --no-restore --self-contained true -p:PublishSingleFile=false -o "$appdir/usr/bin"
cp assets/nexus.png "$appdir/nexus.png"
cp packaging/linux/nexus.desktop "$appdir/nexus.desktop"
cp LICENSE "$appdir/usr/bin/LICENSE-NEXUS"
python3 packaging/linux/collect-licenses.py desktop/Nexus.Desktop/obj/project.assets.json "$appdir/usr/share/licenses"
cp packaging/linux/*-LICENSE.txt packaging/linux/Inter-OFL.txt "$appdir/usr/share/licenses/"
cp README.md "$appdir/usr/bin/README.md"
cat > "$appdir/AppRun" <<'APPRUN'
#!/bin/sh
appdir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
exec "$appdir/usr/bin/Nexus" "$@"
APPRUN
chmod +x "$appdir/AppRun"
tool="$output/appimagetool-1.9.1-x86_64.AppImage"
if [[ ! -f "$tool" ]]; then
  curl --fail --location --retry 3 https://github.com/AppImage/appimagetool/releases/download/1.9.1/appimagetool-x86_64.AppImage -o "$tool.tmp"
  mv "$tool.tmp" "$tool"
fi
printf '%s  %s\n' ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0 "$tool" | sha256sum --check
chmod +x "$tool"
runtime="$output/runtime-x86_64"
if [[ ! -f "$runtime" ]]; then
  curl --fail --location --retry 3 https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-x86_64 -o "$runtime.tmp"
  mv "$runtime.tmp" "$runtime"
fi
printf '%s  %s\n' 617aa77314d57c8b8e4fae9c3f0b35af7b30d5529cb69e98f64f92510ed98a46 "$runtime" | sha256sum --check
# Extraction mode works on CI/container hosts without FUSE. Limit compression threads.
ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" --runtime-file "$runtime" --mksquashfs-opt -processors --mksquashfs-opt 2 "$appdir" "$output/Nexus-Linux-x86_64.AppImage"
(cd "$output" && sha256sum Nexus-Linux-x86_64.AppImage > Nexus-Linux-x86_64.AppImage.sha256)
echo "AppImage: $output/Nexus-Linux-x86_64.AppImage"
