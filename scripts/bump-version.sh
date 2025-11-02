#!/bin/bash
# Version bumping script for Conduit Framework
# Usage: ./scripts/bump-version.sh [major|minor|patch|version]
#   ./scripts/bump-version.sh minor      # Bump minor version (0.1.0 -> 0.2.0)
#   ./scripts/bump-version.sh patch      # Bump patch version (0.1.0 -> 0.1.1)
#   ./scripts/bump-version.sh 0.3.0      # Set specific version

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Get current version
CURRENT_VERSION=$(cat VERSION)
echo -e "${BLUE}Current version: ${CURRENT_VERSION}${NC}"

# Parse version
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT_VERSION"

# Determine new version
if [ $# -eq 0 ]; then
    echo -e "${RED}Error: No version argument provided${NC}"
    echo "Usage: $0 [major|minor|patch|X.Y.Z]"
    echo "  major - Bump major version (0.1.0 -> 1.0.0)"
    echo "  minor - Bump minor version (0.1.0 -> 0.2.0)"
    echo "  patch - Bump patch version (0.1.0 -> 0.1.1)"
    echo "  X.Y.Z - Set specific version (e.g., 0.3.0)"
    exit 1
fi

case "$1" in
    major)
        if [ "$MAJOR" -eq 0 ]; then
            echo -e "${YELLOW}Warning: In 0.x development, major changes bump MINOR${NC}"
            echo -e "${YELLOW}To release 1.0.0, use: $0 1.0.0${NC}"
            exit 1
        fi
        NEW_VERSION="$((MAJOR + 1)).0.0"
        CHANGE_TYPE="major"
        ;;
    minor)
        NEW_VERSION="${MAJOR}.$((MINOR + 1)).0"
        CHANGE_TYPE="minor"
        ;;
    patch)
        NEW_VERSION="${MAJOR}.${MINOR}.$((PATCH + 1))"
        CHANGE_TYPE="patch"
        ;;
    *)
        # Validate version format
        if [[ ! $1 =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
            echo -e "${RED}Error: Invalid version format '$1'${NC}"
            echo "Version must be in format X.Y.Z (e.g., 0.2.0)"
            exit 1
        fi
        NEW_VERSION="$1"
        CHANGE_TYPE="specific"
        ;;
esac

echo -e "${GREEN}New version: ${NEW_VERSION}${NC}"

# Confirm
read -p "Continue with version bump? (y/n) " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo -e "${YELLOW}Aborted${NC}"
    exit 0
fi

# Check for uncommitted changes
if [ -n "$(git status --porcelain)" ]; then
    echo -e "${RED}Error: Working directory has uncommitted changes${NC}"
    echo "Please commit or stash changes before bumping version"
    exit 1
fi

# Check if on develop branch
CURRENT_BRANCH=$(git branch --show-current)
if [ "$CURRENT_BRANCH" != "develop" ] && [ "$CURRENT_BRANCH" != "master" ]; then
    echo -e "${YELLOW}Warning: Not on develop or master branch${NC}"
    read -p "Continue anyway? (y/n) " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Aborted${NC}"
        exit 0
    fi
fi

echo ""
echo -e "${BLUE}Updating version files...${NC}"

# Update VERSION file
echo "$NEW_VERSION" > VERSION
echo -e "${GREEN}✓${NC} Updated VERSION"

# Update all .csproj files
CSPROJ_COUNT=0
for file in src/*/Conduit.*.csproj; do
    if [ -f "$file" ]; then
        sed -i "s/<Version>${CURRENT_VERSION}<\/Version>/<Version>${NEW_VERSION}<\/Version>/g" "$file"
        CSPROJ_COUNT=$((CSPROJ_COUNT + 1))
    fi
done
echo -e "${GREEN}✓${NC} Updated $CSPROJ_COUNT .csproj files"

# Update README.md badge
if [ -f "README.md" ]; then
    sed -i "s/version-${CURRENT_VERSION}/version-${NEW_VERSION}/g" README.md
    echo -e "${GREEN}✓${NC} Updated README.md"
fi

# Show git diff
echo ""
echo -e "${BLUE}Changes to be committed:${NC}"
git diff --stat

echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "1. Update CHANGELOG.md with release notes"
echo "2. Review changes: git diff"
echo "3. Commit: git add VERSION README.md CHANGELOG.md src/*/Conduit.*.csproj"
echo "4. Commit: git commit -m 'chore(release): Bump version to ${NEW_VERSION}'"
echo "5. Tag: git tag -a v${NEW_VERSION} -m 'Release version ${NEW_VERSION}'"
echo "6. Push: git push origin develop --tags"

# Optionally auto-commit
echo ""
read -p "Create commit automatically? (y/n) " -n 1 -r
echo
if [[ $REPLY =~ ^[Yy]$ ]]; then
    git add VERSION README.md src/*/Conduit.*.csproj

    # Generate commit message
    COMMIT_MSG="chore(release): Bump version to ${NEW_VERSION}"

    # Try to auto-generate release notes from commits
    if [ -n "$(git tag -l)" ]; then
        LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "")
        if [ -n "$LAST_TAG" ]; then
            echo ""
            echo -e "${BLUE}Commits since ${LAST_TAG}:${NC}"
            git log ${LAST_TAG}..HEAD --oneline --no-decorate

            COMMIT_MSG="${COMMIT_MSG}

Changes since ${LAST_TAG}:
$(git log ${LAST_TAG}..HEAD --pretty=format:'- %s' | head -20)"
        fi
    fi

    git commit -m "$COMMIT_MSG"
    echo -e "${GREEN}✓${NC} Version bump committed"

    echo ""
    read -p "Create git tag v${NEW_VERSION}? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        git tag -a "v${NEW_VERSION}" -m "Release version ${NEW_VERSION}"
        echo -e "${GREEN}✓${NC} Tag v${NEW_VERSION} created"

        echo ""
        echo -e "${YELLOW}Don't forget to:${NC}"
        echo "1. Update CHANGELOG.md"
        echo "2. Push: git push origin develop --tags"
    fi
fi

echo ""
echo -e "${GREEN}Version bump complete!${NC}"
