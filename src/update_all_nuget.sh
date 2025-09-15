#!/bin/bash

TARGETS_FILE="Directory.Build.targets"
NUGET_CONFIG="nuget.config"
BACKUP_FILE="${TARGETS_FILE}.bak.$(date +%Y%m%d%H%M%S)"

# Backup the original file
cp "$TARGETS_FILE" "$BACKUP_FILE"
echo "Created backup at $BACKUP_FILE"

# Function to compare versions
version_compare() {
    if [[ $1 == $2 ]]; then
        echo "equal"
        return
    fi

    local IFS=.
    local i ver1=($1) ver2=($2)

    # Fill empty fields with zeros
    for ((i=${#ver1[@]}; i<${#ver2[@]}; i++))
    do
        ver1[i]=0
    done

    for ((i=0; i<${#ver1[@]}; i++))
    do
        if [[ -z ${ver2[i]} ]]; then
            ver2[i]=0
        fi

        if ((10#${ver1[i]} > 10#${ver2[i]})); then
            echo "greater"
            return
        fi

        if ((10#${ver1[i]} < 10#${ver2[i]})); then
            echo "less"
            return
        fi
    done

    echo "equal"
}

# Function to get latest minor version from NuGet (excluding preview versions)
get_latest_minor_version() {
    local PACKAGE_NAME=$1
    local CURRENT_VERSION=$2

    # Extract major and minor from current version (e.g., from 9.0.6 get 9.0)
    local MAJOR_MINOR=$(echo "$CURRENT_VERSION" | grep -o "^[0-9]*\.[0-9]*")

    if [ -z "$MAJOR_MINOR" ]; then
        echo "Error extracting major.minor from $CURRENT_VERSION"
        return
    fi

    local MAJOR=$(echo "$MAJOR_MINOR" | cut -d. -f1)

    # Get all versions from NuGet
    local RESPONSE=$(curl -s "https://api.nuget.org/v3-flatcontainer/$PACKAGE_NAME/index.json")
    local VERSIONS=""

    if [[ "$RESPONSE" == *"\"versions\""* ]]; then
        if command -v jq &> /dev/null; then
            VERSIONS=$(echo "$RESPONSE" | jq -r '.versions[]')
        else
            VERSIONS=$(echo "$RESPONSE" | grep -o '"versions":\[[^]]*\]' | grep -o '"[^"]*"' | sed 's/"//g')
        fi
    else
        # If first API fails, try the registration API
        RESPONSE=$(curl -s "https://api.nuget.org/v3/registration5-semver1/$PACKAGE_NAME/index.json")

        if [[ "$RESPONSE" == *"\"items\""* ]]; then
            if command -v jq &> /dev/null; then
                # Extract all versions from the registration API
                VERSIONS=$(echo "$RESPONSE" | jq -r '.items[].items[].catalogEntry.version')
            else
                VERSIONS=$(echo "$RESPONSE" | grep -o '"version":"[^"]*"' | sed 's/"version":"//g' | sed 's/"//g')
            fi
        fi
    fi

    if [ -z "$VERSIONS" ]; then
        echo ""
        return
    fi

    local LATEST_VERSION=""
    local LATEST_VERSION_SORTED=""

    # Filter versions to only include those with the same major version and are not previews
    while IFS= read -r VERSION; do
        # Skip empty lines
        if [ -z "$VERSION" ]; then
            continue
        fi

        # Skip preview/beta versions
        if [[ "$VERSION" == *"-"* ]]; then
            continue
        fi

        # Extract major version
        local VERSION_MAJOR=$(echo "$VERSION" | cut -d. -f1)

        # Only consider versions with the same major version
        if [ "$VERSION_MAJOR" = "$MAJOR" ]; then
            if [ -z "$LATEST_VERSION" ]; then
                LATEST_VERSION="$VERSION"
            else
                # Compare versions and keep the higher one
                local COMPARE=$(version_compare "$VERSION" "$LATEST_VERSION")
                if [ "$COMPARE" = "greater" ]; then
                    LATEST_VERSION="$VERSION"
                fi
            fi
        fi
    done <<< "$VERSIONS"

    echo "$LATEST_VERSION"
}

# Extract all package references that have an Update attribute
PACKAGES=$(grep -o '<PackageReference Update="[^"]*"' "$TARGETS_FILE" | sed 's/<PackageReference Update="//g' | sed 's/"//g')

# Counter for successful updates
UPDATED_COUNT=0
FAILED_COUNT=0
SKIPPED_COUNT=0

echo "Starting update of all packages..."

# Process each package
for PACKAGE_NAME in $PACKAGES; do
    echo "Checking for updates to $PACKAGE_NAME..."

    # Get the current version from the file
    CURRENT_VERSION=$(grep -o "<PackageReference Update=\"$PACKAGE_NAME\" Version=\"[^\"]*\"" "$TARGETS_FILE" | sed "s/<PackageReference Update=\"$PACKAGE_NAME\" Version=\"//g" | sed 's/"//g')

    if [ -z "$CURRENT_VERSION" ]; then
        echo "  ❌ Could not find current version for $PACKAGE_NAME"
        FAILED_COUNT=$((FAILED_COUNT+1))
        continue
    fi

    echo "  Current version: $CURRENT_VERSION"

    # Get the latest minor version (excluding preview versions)
    LATEST_VERSION=$(get_latest_minor_version "$PACKAGE_NAME" "$CURRENT_VERSION")

    if [ -z "$LATEST_VERSION" ]; then
        echo "  ❌ Could not find latest version for $PACKAGE_NAME"
        FAILED_COUNT=$((FAILED_COUNT+1))
        continue
    fi

    echo "  Latest stable minor version: $LATEST_VERSION"

    # Compare versions
    COMPARE=$(version_compare "$LATEST_VERSION" "$CURRENT_VERSION")

    if [ "$COMPARE" = "greater" ]; then
        # Update the version in the file for macOS (sed behavior is different)
        sed -i '' "s|<PackageReference Update=\"$PACKAGE_NAME\" Version=\"$CURRENT_VERSION\"|<PackageReference Update=\"$PACKAGE_NAME\" Version=\"$LATEST_VERSION\"|g" "$TARGETS_FILE"

        if [ $? -eq 0 ]; then
            echo "  ✅ Updated $PACKAGE_NAME from $CURRENT_VERSION to $LATEST_VERSION"
            UPDATED_COUNT=$((UPDATED_COUNT+1))
        else
            echo "  ❌ Failed to update $PACKAGE_NAME"
            FAILED_COUNT=$((FAILED_COUNT+1))
        fi
    else
        echo "  ✓ $PACKAGE_NAME already at latest stable minor version $CURRENT_VERSION"
        SKIPPED_COUNT=$((SKIPPED_COUNT+1))
    fi
done

echo "Update complete: $UPDATED_COUNT packages updated, $SKIPPED_COUNT packages already at latest minor version, $FAILED_COUNT failures"
echo "Original file backed up at $BACKUP_FILE"
