FROM node:24-bookworm-slim@sha256:01d566e8d94d8c619591d78652babfccd3c6992a3dffa9ed5ab39e11cf6c70e9 AS node-runtime
FROM mcr.microsoft.com/playwright:v1.65.0-noble@sha256:6e3fb709361521eb27161606e6da6d2674e5e89fa16516cb37d6fdd91372273d
# Keep Angular's Node 24 toolchain while using the matching official browser binaries.
RUN apt-get purge -y nodejs && rm -rf /usr/lib/node_modules
COPY --from=node-runtime /usr/local/ /usr/local/
RUN npm install --global npm@11.21.1 --ignore-scripts --no-audit --no-fund && npm install --prefix /tmp/npm-security --no-save --ignore-scripts --omit=dev --package-lock=false --no-audit --no-fund undici@6.29.1 && rm -rf /usr/local/lib/node_modules/npm/node_modules/undici && mv /tmp/npm-security/node_modules/undici /usr/local/lib/node_modules/npm/node_modules/undici && rm -rf /tmp/npm-security && npm cache clean --force
RUN install -d -o 1000 -g 1000 /home/node/.npm /workspace/src/dahub-web/node_modules /workspace/src/dahub-web/.angular /workspace/src/dahub-web/dist /workspace/src/dahub-web/coverage /reports
ENV HOME=/home/pwuser NPM_CONFIG_CACHE=/home/node/.npm
USER 1000:1000
WORKDIR /workspace/src/dahub-web
