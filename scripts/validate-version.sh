#!/bin/bash
set -e

CURRENT_VERSION=$(cat version | tr -d '[:space:]')
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "0.0.0")
LAST_VERSION=${LAST_TAG#v}

version_gt() {
    test "$(printf '%s\n' "$@" | sort -V | head -n 1)" != "$1"
}

# Validate semver format
if ! [[ "$CURRENT_VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[a-zA-Z0-9.-]+)?(\+[a-zA-Z0-9.-]+)?$ ]]; then
    echo "Error: Invalid version format. Expected: MAJOR.MINOR.PATCH"
    exit 1
fi

# Check if version changed
if git diff --cached --name-only | grep -q "^version$"; then
    if ! version_gt "$CURRENT_VERSION" "$LAST_VERSION"; then
        echo "Error: Version must be greater than $LAST_VERSION"
        exit 1
    fi
fi
