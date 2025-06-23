#!/bin/bash
set -e

NUGET_API_KEY="your-key" # Replace with your actual NuGet API key
SOURCE="https://api.nuget.org/v3/index.json"
SEARCH="HBDStack"

while :; do
  result=$(curl -s "https://api-v2v3search-0.nuget.org/query?q=$SEARCH&prerelease=true&semVerLevel=2.0.0")
  count=$(echo "$result" | tr -d '\000-\031' | jq '.data | length')
  if [ -z "$result" ] || [ "$count" -eq 0 ]; then
    break
  fi

  echo "$result" | tr -d '\000-\031' | jq -r --arg SEARCH "$SEARCH" '.data[] | select(.id | startswith($SEARCH)) | [.id, .version] | @tsv' |
  while IFS=$'\t' read -r package version; do
    echo "Unlisting $package $version"
    if ! dotnet nuget delete "$package" "$version" --source "$SOURCE" --api-key "$NUGET_API_KEY" --non-interactive; then
        echo "Failed to delete $package $version. Stopping."
        exit 1
    fi
  done
  sleep 20
done