FROM mcr.microsoft.com/dotnet/sdk:10.0-noble@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb
# Apply the Brotli and APT security fixes until the pinned upstream SDK image includes them.
RUN apt-get update && apt-get install -y --no-install-recommends --only-upgrade libbrotli1=1.1.0-2ubuntu0.1 apt=2.8.4ubuntu0.1 libapt-pkg6.0t64=2.8.4ubuntu0.1 && rm -rf /var/lib/apt/lists/*
RUN install -d -o 1654 -g 1654 /artifacts /nuget /workspace /run/dahub
USER 1654:1654
WORKDIR /workspace
