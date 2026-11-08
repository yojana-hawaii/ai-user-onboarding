FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:212fe4cb74627d5adf9817a4088906ae3edff1bd853bb27565d4b46731efe61e
# Apply the Brotli and APT security fixes until the pinned upstream SDK image includes them.
RUN apt-get update && apt-get install -y --no-install-recommends --only-upgrade libbrotli1=1.1.0-2ubuntu0.1 apt=2.8.4ubuntu0.1 libapt-pkg6.0t64=2.8.4ubuntu0.1 && rm -rf /var/lib/apt/lists/*
RUN install -d -o 1654 -g 1654 /artifacts /nuget /workspace /run/dahub
USER 1654:1654
WORKDIR /workspace
