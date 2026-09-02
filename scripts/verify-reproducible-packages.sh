#!/usr/bin/env bash
set -euo pipefail

version="${1:-}"
if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+([.-][0-9A-Za-z.-]+)?$ ]]; then
  echo "Usage: $0 <NuGet-version>" >&2
  exit 2
fi

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
work_dir="$(mktemp -d)"
first="$work_dir/first"
second="$work_dir/second"
release_dir="$root_dir/artifacts/nuget"

cleanup() {
  rm -rf "$work_dir"
}
trap cleanup EXIT

mkdir -p "$first" "$second" "$release_dir"
find "$release_dir" -maxdepth 1 -type f \
  \( -name 'VertexBPMN.Sdk.*.nupkg' -o -name 'VertexBPMN.Cli.*.nupkg' -o -name 'SHA256SUMS' \) \
  -delete

pack() {
  local output="$1"
  dotnet pack "$root_dir/src/VertexBPMN.Sdk/VertexBPMN.Sdk.csproj" \
    --configuration Release --no-build --no-restore --output "$output" \
    -p:PackageVersion="$version" -p:ContinuousIntegrationBuild=true -p:NuGetAudit=false
  dotnet pack "$root_dir/src/VertexBPMN.Cli/VertexBPMN.Cli.csproj" \
    --configuration Release --no-build --no-restore --output "$output" \
    -p:PackageVersion="$version" -p:ContinuousIntegrationBuild=true -p:NuGetAudit=false
}

normalize_package() {
  local package="$1"
  local unpacked
  local normalized
  local core_file
  unpacked="$(mktemp -d "$work_dir/package.XXXXXX")"
  normalized="$package.normalized"

  unzip -q "$package" -d "$unpacked"
  core_file="$(find "$unpacked/package/services/metadata/core-properties" -type f -name '*.psmdcp' -print -quit)"
  if [[ -n "$core_file" ]]; then
    mv "$core_file" "$unpacked/package/services/metadata/core-properties/core.psmdcp"
    sed -i \
      -e '/2010\/07\/manifest/ s/Id="[^"]*"/Id="RManifest"/' \
      -e '/metadata\/core-properties/ { s#Target="[^"]*"#Target="/package/services/metadata/core-properties/core.psmdcp"#; s/Id="[^"]*"/Id="RCoreProperties"/; }' \
      "$unpacked/_rels/.rels"
  fi
  find "$unpacked" -exec touch -h -t 200001010000.00 {} +
  (
    cd "$unpacked"
    find . -type f -print0 | sort -z | xargs -0 zip -X -q "$normalized"
  )
  mv "$normalized" "$package"
}

pack "$first"
pack "$second"

for package in "$first"/*.nupkg "$second"/*.nupkg; do
  normalize_package "$package"
done

for package in "$first"/*.nupkg; do
  name="$(basename "$package")"
  if [[ ! -f "$second/$name" ]]; then
    echo "Reproducibility gate failed: second build did not produce $name." >&2
    exit 1
  fi
  if ! cmp --silent "$package" "$second/$name"; then
    echo "Reproducibility gate failed: $name is not byte-identical across two pack runs." >&2
    exit 1
  fi
done

cp "$first"/*.nupkg "$release_dir/"
(
  cd "$release_dir"
  sha256sum ./*.nupkg >SHA256SUMS
  sha256sum --check SHA256SUMS
)

echo "Reproducibility gate passed for version $version. Packages and SHA256SUMS are in $release_dir."
