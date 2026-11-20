#!/usr/bin/env bash
# System packages of Playwright's Chromium on the Linux CI runner (#302), without the Ubuntu mirror on every run.
#
# `playwright install --with-deps` ran `apt-get update` and downloaded the missing packages on every run; on a slow
# mirror that took over 7 minutes. Most of the libraries are already in the runner image, so this script:
#   plan    - asks Playwright for its package list (dry run), keeps only the packages that are not installed and
#             writes a cache key for exactly that set to $GITHUB_OUTPUT (`key`, and `missing=true|false`);
#   install - installs the missing packages from the cached .deb files with dpkg, without apt and the mirror; only
#             when there is no cache, or the cached files do not cover the set, it falls back to apt-get and keeps
#             the downloaded .deb files in the cache directory for the next run.
# The Playwright version comes from frontend/package.json, so a Renovate bump changes the list and the key.
set -euo pipefail
trap 'echo "::error::playwright-deps.sh: line $LINENO failed (exit code $?)"' ERR

cache_dir="${PLAYWRIGHT_DEPS_CACHE:-$HOME/.cache/playwright-deps}"
state="${RUNNER_TEMP:-/tmp}/playwright-deps"
mkdir -p "$state"

missing_packages() {
    local pkg
    for pkg in $(cat "$state/packages"); do
        [ "$(dpkg-query -W -f='${Status}' "$pkg" 2>/dev/null)" = 'install ok installed' ] || echo "$pkg"
    done
}

plan() {
    local version
    version=$(node -p "require('./frontend/package.json').devDependencies['@playwright/test']")
    # Dry run prints: sudo -- sh -c "apt-get update&& apt-get install -y --no-install-recommends <packages>"
    npx --yes "playwright@$version" install-deps --dry-run chromium > "$state/dry-run.txt"
    sed -n 's/.*--no-install-recommends \([^"]*\)".*/\1/p' "$state/dry-run.txt" | tr ' ' '\n' | sed '/^$/d' | sort -u \
        > "$state/packages"
    if [ ! -s "$state/packages" ]; then
        echo "::error::Could not read the package list from 'playwright install-deps --dry-run':"
        cat "$state/dry-run.txt"
        exit 1
    fi
    missing_packages > "$state/missing"
    echo "Playwright $version needs $(wc -l < "$state/packages") packages; not installed: $(tr '\n' ' ' < "$state/missing")"
    {
        echo "key=playwright-deps-${ImageOS:-linux}-$(sha256sum "$state/missing" | cut -c1-16)"
        if [ -s "$state/missing" ]; then echo "missing=true"; else echo "missing=false"; fi
    } >> "${GITHUB_OUTPUT:-/dev/null}"
}

install() {
    [ -s "$state/missing" ] || { echo "All Playwright system packages are installed."; return; }
    if compgen -G "$cache_dir/*.deb" > /dev/null; then
        echo "Installing from the cache: $(ls "$cache_dir"/*.deb | wc -l) .deb files"
        sudo dpkg -i --skip-same-version "$cache_dir"/*.deb || echo "::warning::dpkg could not install the cached packages"
        missing_packages > "$state/missing"
        if [ ! -s "$state/missing" ]; then echo "save=false" >> "${GITHUB_OUTPUT:-/dev/null}"; return; fi
        echo "::warning::The cache does not cover: $(tr '\n' ' ' < "$state/missing"); falling back to apt-get"
    fi
    rm -rf "$cache_dir"
    mkdir -p "$cache_dir/partial"
    sudo apt-get update -q
    # shellcheck disable=SC2046
    sudo apt-get install -y -q --no-install-recommends \
        -o Dir::Cache::Archives="$cache_dir" -o APT::Keep-Downloaded-Packages=true $(cat "$state/missing")
    sudo rm -rf "$cache_dir/partial" "$cache_dir/lock"
    sudo chown -R "$(id -u):$(id -g)" "$cache_dir"
    echo "save=true" >> "${GITHUB_OUTPUT:-/dev/null}"
}

case "${1:-}" in
    plan) plan ;;
    install) install ;;
    *) echo "usage: $0 plan|install" >&2; exit 2 ;;
esac
