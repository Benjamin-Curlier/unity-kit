#!/usr/bin/env bash
# Lists installed Unity editors as JSON: [{version, channel, exe}], newest first.
# macOS/Linux port of find-unity.ps1. Override the Hub root with --search-root <dir>.
set -euo pipefail

SEARCH_ROOT=""
if [[ "${1:-}" == "--search-root" && -n "${2:-}" ]]; then
  SEARCH_ROOT="$2"
elif [[ "$(uname -s)" == "Darwin" ]]; then
  SEARCH_ROOT="/Applications/Unity/Hub/Editor"
else
  SEARCH_ROOT="$HOME/Unity/Hub/Editor"
fi

if [[ ! -d "$SEARCH_ROOT" ]]; then
  echo "No Unity Hub editor directory at $SEARCH_ROOT (use --search-root for custom locations)" >&2
  exit 1
fi

entries=()
for dir in "$SEARCH_ROOT"/*/; do
  v="$(basename "$dir")"
  if [[ "$(uname -s)" == "Darwin" ]]; then
    exe="$dir/Unity.app/Contents/MacOS/Unity"
  else
    exe="$dir/Editor/Unity"
  fi
  [[ -x "$exe" ]] || continue
  case "$v" in
    *f*) channel="stable" ;;
    *b*) channel="beta" ;;
    *a*) channel="alpha" ;;
    *)   channel="unknown" ;;
  esac
  entries+=("{\"version\":\"$v\",\"channel\":\"$channel\",\"exe\":\"$exe\"}")
done

if [[ ${#entries[@]} -eq 0 ]]; then
  echo "No Unity editors found under $SEARCH_ROOT" >&2
  exit 1
fi

printf '%s\n' "${entries[@]}" | sort -r -t'"' -k4 | paste -sd',' - | sed 's/^/[/; s/$/]/'
