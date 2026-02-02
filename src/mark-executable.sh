#!/bin/bash
TARGET_DIR=${1:-.}

echo "Scanning for .sh files in: $TARGET_DIR"
echo "--------------------------------------"

# -print displays the path, and -exec performs the action
find "$TARGET_DIR" -type f -name "*.sh" -print -exec chmod +x {} \;

echo "--------------------------------------"
echo "Permissions updated."
