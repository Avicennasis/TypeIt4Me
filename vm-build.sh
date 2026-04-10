#!/usr/bin/env bash
# Build TypeIt4Me on the Win11 KVM VM via SSH.
# Usage: ./vm-build.sh
set -euo pipefail

# === Configuration ===
VM_NAME="win11"
SSH_HOST="win11-build"
REMOTE_DIR_WIN="C:\\build\\typeit4me"
REPO_DIR="$(cd "$(dirname "$0")" && pwd)"
SSH_TIMEOUT=120

# === Colors ===
RED='\033[0;31m'; GREEN='\033[0;32m'; YELLOW='\033[0;33m'; NC='\033[0m'
step() { echo -e "${YELLOW}[$(date +%H:%M:%S)] $1${NC}"; }
ok()   { echo -e "${GREEN}[$(date +%H:%M:%S)] $1${NC}"; }
fail() { echo -e "${RED}[$(date +%H:%M:%S)] $1${NC}"; exit 1; }

# === Step 1: Ensure VM is running ===
step "Checking VM state..."
VM_STATE=$(virsh domstate "$VM_NAME" 2>/dev/null || echo "unknown")
case "$VM_STATE" in
    running)
        ok "VM already running."
        ;;
    "shut off")
        step "Starting VM..."
        virsh start "$VM_NAME" >/dev/null
        ok "VM start initiated."
        ;;
    *)
        fail "VM is in unexpected state: $VM_STATE"
        ;;
esac

# === Step 2: Wait for SSH ===
step "Waiting for SSH (up to ${SSH_TIMEOUT}s)..."
elapsed=0
while ! ssh -o ConnectTimeout=5 -o BatchMode=yes "$SSH_HOST" "echo ready" &>/dev/null; do
    elapsed=$((elapsed + 5))
    if [ "$elapsed" -ge "$SSH_TIMEOUT" ]; then
        fail "SSH not available after ${SSH_TIMEOUT}s. Is the VM fully booted?"
    fi
    sleep 5
done
ok "SSH connected."

# === Step 3: Sync repo to VM ===
step "Syncing repo to VM..."
ssh "$SSH_HOST" "if (Test-Path $REMOTE_DIR_WIN) { Remove-Item $REMOTE_DIR_WIN -Recurse -Force }; New-Item -Path $REMOTE_DIR_WIN -ItemType Directory -Force | Out-Null"
tar cf - -C "$REPO_DIR" \
    --exclude='.git' \
    --exclude='*/bin' \
    --exclude='*/obj' \
    --exclude='TestResults' \
    --exclude='publish' \
    --exclude='*.exe' \
    --exclude='*.dll' \
    --exclude='*.pdb' \
    --exclude='BenchmarkDotNet.Artifacts' \
    --exclude='.vs' \
    --exclude='IconGen' \
    . | ssh "$SSH_HOST" "tar xf - -C $REMOTE_DIR_WIN"
ok "Repo synced."

# === Step 4: Restore, build, test ===
step "Restoring NuGet packages..."
ssh "$SSH_HOST" "cd $REMOTE_DIR_WIN; dotnet restore TypeIt4Me.sln"

step "Building solution (Release)..."
ssh "$SSH_HOST" "cd $REMOTE_DIR_WIN; dotnet build TypeIt4Me.sln --no-restore -c Release"

step "Running tests..."
ssh "$SSH_HOST" "cd $REMOTE_DIR_WIN; dotnet test TypeIt4Me.sln --no-build -c Release --logger 'console;verbosity=normal'"
ok "All tests passed."

# === Step 5: Publish single-file exe ===
step "Publishing single-file exe..."
ssh "$SSH_HOST" "cd $REMOTE_DIR_WIN; dotnet publish TypeIt4Me.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish"
ok "Publish complete."

# === Step 6: Copy exe back ===
step "Copying TypeIt4Me.exe to repo..."
scp "$SSH_HOST:C:/build/typeit4me/publish/TypeIt4Me.exe" "$REPO_DIR/"

# === Step 7: Report ===
echo ""
ok "Build complete!"
SIZE=$(du -h "$REPO_DIR/TypeIt4Me.exe" | cut -f1)
echo "  TypeIt4Me.exe: $SIZE"
