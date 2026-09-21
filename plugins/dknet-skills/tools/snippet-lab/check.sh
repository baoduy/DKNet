#!/usr/bin/env bash
# check.sh <skill-dir> [run-label]
# Compiles every C# fence in <skill-dir>/SKILL.md and <skill-dir>/references/*.md against the DKNet packages
# AS PUBLISHED ON NUGET.ORG (version in Directory.Packages.props) — the surface a reader actually installs.
# No local solution build is needed. Each run gets its own copy of the lab under ../lab-runs/<label> so runs
# can go in parallel.
set -uo pipefail
LAB="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$(cd "$LAB/../../../../src" && pwd)"
# The only DKNet project that is IsPackable=false, so the only one that still needs a local build.
ASPIRE_DLL="$SRC/Aspire/Aspire.Hosting.ServiceBus/bin/Debug/net10.0/Aspire.Hosting.ServiceBus.dll"
SKILL="$(cd "${1:?usage: check.sh <skill-dir> [run-label]}" && pwd)"
LABEL="${2:-$(basename "$SKILL")}"
RUN="$LAB/../lab-runs/$LABEL"
rm -rf "$RUN"; mkdir -p "$RUN"
cp "$LAB/SnippetLab.csproj" "$LAB/Directory.Packages.props" "$RUN/"
mapfile -t MD < <(ls "$SKILL"/SKILL.md "$SKILL"/references/*.md "$SKILL"/*.md 2>/dev/null | sort -u)
node "$LAB/extract.mjs" "$RUN/Snippets" "${MD[@]}" || exit 2
[ -f "$ASPIRE_DLL" ] || echo "note: $ASPIRE_DLL not built — Aspire.Hosting.ServiceBus is IsPackable=false, so" \
  "dknet-slimbus-cqrs' AddServiceBus fences will fail until you run: dotnet build $SRC/DKNet.FW.sln -c Debug"
cd "$RUN" && dotnet build SnippetLab.csproj -nologo -v q -p:AspireServiceBusDll="$ASPIRE_DLL" 2>&1 |
  grep -E "error|warning CS|Build succeeded|Build FAILED" | grep -v "warning CS8" | sort -u | head -80
echo "--- snippets in $RUN/Snippets (header comment names the source fence); generated code in $RUN/obj/Generated ---"
