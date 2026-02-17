#!/bin/bash
# Ensure FlareSolverr container is running. Idempotent — safe to call repeatedly.
set -e

CONTAINER_NAME="flaresolverr"
IMAGE="ghcr.io/flaresolverr/flaresolverr:latest"
PORT=8191

# Check Docker is available
if ! command -v docker &>/dev/null; then
    echo "Error: docker not found. Install Docker or enable WSL integration." >&2
    exit 1
fi

# Check if container exists and is running
if docker ps --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    # Already running — verify it responds
    if curl -s "http://localhost:${PORT}/" | grep -q "FlareSolverr is ready"; then
        echo "FlareSolverr is already running on port ${PORT}"
        exit 0
    fi
fi

# Check if container exists but is stopped
if docker ps -a --format '{{.Names}}' | grep -q "^${CONTAINER_NAME}$"; then
    echo "Starting existing FlareSolverr container..."
    docker start "$CONTAINER_NAME"
else
    echo "Creating FlareSolverr container..."
    docker run -d \
        --name="$CONTAINER_NAME" \
        -p "${PORT}:${PORT}" \
        -e LOG_LEVEL=info \
        --restart unless-stopped \
        "$IMAGE"
fi

# Wait for it to be ready
echo -n "Waiting for FlareSolverr to start"
for i in $(seq 1 30); do
    if curl -s "http://localhost:${PORT}/" | grep -q "FlareSolverr is ready" 2>/dev/null; then
        echo " ready!"
        exit 0
    fi
    echo -n "."
    sleep 1
done

echo " timeout waiting for FlareSolverr to start" >&2
exit 1
