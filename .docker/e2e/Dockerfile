FROM node:24-bookworm-slim@sha256:01d566e8d94d8c619591d78652babfccd3c6992a3dffa9ed5ab39e11cf6c70e9 AS node-runtime
FROM mcr.microsoft.com/playwright:v1.65.1-resolute@sha256:dcf385f7a31aa1cbb0a7a0c97a539142bf70480b661f89f26a774ec1fe2edd1d
# Keep Angular's Node 24 toolchain while using the matching official browser binaries.
RUN apt-get purge -y nodejs && rm -rf /usr/lib/node_modules
COPY --from=node-runtime /usr/local/ /usr/local/
# Keep the reviewed Undici and picomatch fixes until npm bundles verified remediations.
RUN npm install --global npm@11.21.1 --ignore-scripts --no-audit --no-fund && npm install --prefix /tmp/npm-security --ignore-scripts --omit=dev --no-audit --no-fund undici@6.29.1 picomatch@npm:@ryotsu/picomatch@4.0.7-a && node -e "require('node:assert/strict').equal(require('/tmp/npm-security/package-lock.json').packages['node_modules/picomatch'].integrity, 'sha512-JroBFEmaejC7szGSfqOhF1QZHLKvX2wTA1+fWyfkpy3BjzdQKNPLKa2FtgeKVUDy5/UPUNkXEeCHJ5ZRBMLTVA==')" && rm -rf /usr/local/lib/node_modules/npm/node_modules/undici /usr/local/lib/node_modules/npm/node_modules/tinyglobby/node_modules/picomatch && mv /tmp/npm-security/node_modules/undici /usr/local/lib/node_modules/npm/node_modules/undici && mv /tmp/npm-security/node_modules/picomatch /usr/local/lib/node_modules/npm/node_modules/tinyglobby/node_modules/picomatch && rm -rf /tmp/npm-security && npm cache clean --force
RUN install -d -o 1000 -g 1000 /home/node/.npm /workspace/src/dahub-web/node_modules /workspace/src/dahub-web/.angular /workspace/src/dahub-web/dist /workspace/src/dahub-web/coverage /reports
# Apply the available Ubuntu APT fix for CVE-2026-109518.
RUN apt-get update && apt-get install -y --no-install-recommends --only-upgrade apt=3.2.1ubuntu0.1 libapt-pkg7.0=3.2.1ubuntu0.1 && rm -rf /var/lib/apt/lists/*
ENV HOME=/home/pwuser NPM_CONFIG_CACHE=/home/node/.npm
USER 1000:1000
WORKDIR /workspace/src/dahub-web
