#!/usr/bin/env bash

resolve_dotnet_bin() {
  if command -v dotnet >/dev/null 2>&1; then
    printf '%s\n' "dotnet"
    return 0
  fi

  if command -v dotnet.exe >/dev/null 2>&1; then
    printf '%s\n' "dotnet.exe"
    return 0
  fi

  local windows_dotnet="/c/Program Files/dotnet/dotnet.exe"
  if [[ -x "$windows_dotnet" ]]; then
    printf '%s\n' "$windows_dotnet"
    return 0
  fi

  printf '%s\n' "dotnet executable not found. Set DOTNET_BIN explicitly if dotnet is installed outside PATH." >&2
  return 1
}
