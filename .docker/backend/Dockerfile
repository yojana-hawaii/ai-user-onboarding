FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b
# Apply the Brotli and APT security fixes until the pinned upstream SDK image includes them.
RUN apt-get update && apt-get install -y --no-install-recommends --only-upgrade libbrotli1=1.1.0-2ubuntu0.1 apt=2.8.4ubuntu0.1 libapt-pkg6.0t64=2.8.4ubuntu0.1 && rm -rf /var/lib/apt/lists/*
RUN install -d -o 1654 -g 1654 /artifacts /nuget /workspace /run/dahub
USER 1654:1654
WORKDIR /workspace
