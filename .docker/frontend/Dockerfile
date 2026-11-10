FROM node:26-alpine@sha256:8996bb5821d62a8fd9bb27c1cb07462f3fdce6e47d640c6a881ead0b064ce216
# npm 11.21.1 fixes brace-expansion; apply the compatible Undici security patch until npm bundles it.
RUN npm install --global npm@11.21.1 --ignore-scripts && npm install --prefix /tmp/npm-security --no-save --ignore-scripts --omit=dev --package-lock=false undici@6.29.1 && rm -rf /usr/local/lib/node_modules/npm/node_modules/undici && mv /tmp/npm-security/node_modules/undici /usr/local/lib/node_modules/npm/node_modules/undici && rm -rf /tmp/npm-security && npm cache clean --force
RUN install -d -o 1000 -g 1000 /home/node/.npm /workspace/src/dahub-web/node_modules /workspace/src/dahub-web/.angular /workspace/src/dahub-web/dist
USER 1000:1000
WORKDIR /workspace/src/dahub-web
