FROM node:24-alpine@sha256:8a32c8cf649a49915a66e81e7a609223c708d0adabf288ee3f4077d765542f85
# npm 11.21.1 fixes brace-expansion; apply the compatible Undici security patch until npm bundles it.
RUN npm install --global npm@11.21.1 --ignore-scripts && npm install --prefix /tmp/npm-security --no-save --ignore-scripts --omit=dev --package-lock=false undici@6.29.1 && rm -rf /usr/local/lib/node_modules/npm/node_modules/undici && mv /tmp/npm-security/node_modules/undici /usr/local/lib/node_modules/npm/node_modules/undici && rm -rf /tmp/npm-security && npm cache clean --force
RUN install -d -o 1000 -g 1000 /home/node/.npm /workspace/src/dahub-web/node_modules /workspace/src/dahub-web/.angular /workspace/src/dahub-web/dist /workspace/src/dahub-web/coverage
USER 1000:1000
WORKDIR /workspace/src/dahub-web
