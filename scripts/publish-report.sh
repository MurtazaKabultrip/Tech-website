#!/usr/bin/env bash
# Publishes a CI output folder to a throw-away branch (force-pushed each run) so reports
# can be read with plain git, without downloading Actions artifacts.
# Usage: scripts/publish-report.sh <folder> <branch>
set -euo pipefail
src="$1"; branch="$2"
[ -d "$src" ] || { echo "nothing to publish in $src"; exit 0; }
tmp="$(mktemp -d)"
cp -r "$src"/. "$tmp"/
cd "$tmp"
git init -q -b "$branch"
git add -A
git -c user.name="ci" -c user.email="ci@users.noreply.github.com" commit -q -m "CI report for ${GITHUB_SHA:-local}"
git push -q -f "https://x-access-token:${GH_TOKEN}@github.com/${GITHUB_REPOSITORY}.git" "$branch"
echo "published $src to $branch"
