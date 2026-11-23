#!/usr/bin/env bash
# Fetch a pinned snapshot of the OMG DMN TCK test cases.
# The suite is deliberately pinned: a TCK change should be a reviewed commit,
# not something that shifts under CI.
set -euo pipefail

commit="${TCK_COMMIT:-a428da2f9c5ff2a4b8566ab511144e9022d743fe}"
destination="${1:-artifacts/tck}"

mkdir -p "$destination"

archive="$(mktemp)"
trap 'rm -f "$archive"' EXIT

echo "Fetching DMN TCK $commit"
curl -fsSL "https://codeload.github.com/dmn-tck/tck/tar.gz/$commit" -o "$archive"
tar -xzf "$archive" -C "$destination" --strip-components=1 "tck-$commit/TestCases"
printf '%s' "$commit" > "$destination/.commit"

echo "Test cases: $destination/TestCases"
echo "Commit:     $commit"
