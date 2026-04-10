#!/usr/bin/env bash
# Release TypeIt4Me: bump version, build on VM, tag, push, create GitHub Release.
# Usage: ./vm-release.sh 1.7.0 [--shutdown]
set -euo pipefail

REPO_DIR="$(cd "$(dirname "$0")" && pwd)"

# === Parse arguments ===
VERSION="${1:-}"
SHUTDOWN=false
for arg in "$@"; do
    [[ "$arg" == "--shutdown" ]] && SHUTDOWN=true
done

if [[ -z "$VERSION" || "$VERSION" == "--shutdown" ]]; then
    echo "Usage: $0 VERSION [--shutdown]"
    echo "  VERSION: semver like 1.7.0"
    echo "  --shutdown: shut down the VM after release"
    exit 1
fi

if ! [[ "$VERSION" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "Error: VERSION must be in X.Y.Z format (got: $VERSION)"
    exit 1
fi

TAG="v${VERSION}"

# === Colors ===
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[0;33m'; NC='\033[0m'
step() { echo -e "${YELLOW}[$(date +%H:%M:%S)] $1${NC}"; }
ok()   { echo -e "${GREEN}[$(date +%H:%M:%S)] $1${NC}"; }
fail() { echo -e "${RED}[$(date +%H:%M:%S)] $1${NC}"; exit 1; }

# === Preflight checks ===
if git -C "$REPO_DIR" tag -l "$TAG" | grep -q "$TAG"; then
    fail "Tag $TAG already exists. Aborting."
fi

# === Step 1: Update version ===
step "Bumping version to $VERSION..."

# .csproj has <Version>, <AssemblyVersion>, <FileVersion>
sed -i "s/<Version>[0-9]\+\.[0-9]\+\.[0-9]\+<\/Version>/<Version>${VERSION}<\/Version>/" \
    "$REPO_DIR/TypeIt4Me.csproj"
sed -i "s/<AssemblyVersion>[0-9]\+\.[0-9]\+\.[0-9]\+\.[0-9]\+<\/AssemblyVersion>/<AssemblyVersion>${VERSION}.0<\/AssemblyVersion>/" \
    "$REPO_DIR/TypeIt4Me.csproj"
sed -i "s/<FileVersion>[0-9]\+\.[0-9]\+\.[0-9]\+\.[0-9]\+<\/FileVersion>/<FileVersion>${VERSION}.0<\/FileVersion>/" \
    "$REPO_DIR/TypeIt4Me.csproj"

ok "Version updated."

# === Step 2: Build on VM ===
step "Building on VM..."
"$REPO_DIR/vm-build.sh"

# === Step 3: Commit version bump ===
step "Committing version bump..."
git -C "$REPO_DIR" add TypeIt4Me.csproj
git -C "$REPO_DIR" commit -m "release: bump version to ${VERSION}"

# === Step 4: Tag ===
step "Creating tag $TAG..."
git -C "$REPO_DIR" tag -a "$TAG" -m "Release ${VERSION}"

# === Step 5: Push ===
step "Pushing commits and tag..."
git -C "$REPO_DIR" push origin main
git -C "$REPO_DIR" push origin "$TAG"

# === Step 6: Create GitHub Release via API ===
step "Creating GitHub Release..."
TOKEN=$(grep -o 'ghp_[A-Za-z0-9]*' "$HOME/github/github.sh" | head -1)
REPO_SLUG=$(git -C "$REPO_DIR" remote get-url origin | sed 's/.*github.com[:/]\(.*\)\.git/\1/')

RESPONSE=$(curl -s -X POST \
    -H "Authorization: token $TOKEN" \
    -H "Accept: application/vnd.github+json" \
    "https://api.github.com/repos/$REPO_SLUG/releases" \
    -d "{\"tag_name\":\"$TAG\",\"name\":\"$TAG\",\"body\":\"Release ${VERSION}\"}")

UPLOAD_URL=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['upload_url'].split('{')[0])" 2>/dev/null)
RELEASE_URL=$(echo "$RESPONSE" | python3 -c "import sys,json; print(json.load(sys.stdin)['html_url'])" 2>/dev/null)

if [ -z "$UPLOAD_URL" ]; then
    fail "Failed to create release. Response: $RESPONSE"
fi

# Upload exe
curl -s -X POST \
    -H "Authorization: token $TOKEN" \
    -H "Content-Type: application/octet-stream" \
    "${UPLOAD_URL}?name=TypeIt4Me.exe" \
    --data-binary @"$REPO_DIR/TypeIt4Me.exe" | python3 -c "import sys,json; d=json.load(sys.stdin); print(f'  Uploaded: {d[\"name\"]} ({d[\"size\"]//1048576}MB)')"

ok "GitHub Release created: $RELEASE_URL"

# === Step 7: Optional shutdown ===
if [[ "$SHUTDOWN" == "true" ]]; then
    step "Shutting down VM..."
    virsh shutdown win11
    ok "VM shutdown initiated."
fi

echo ""
ok "Release $VERSION complete!"
echo "  Tag: $TAG"
echo "  $RELEASE_URL"
