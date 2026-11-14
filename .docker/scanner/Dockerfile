FROM ghcr.io/aquasecurity/trivy:0.76.2@sha256:8f6bb947dbc528902e11f2f5be52f7b519f6a39d5d148f41166f2040499768fb
USER 0
RUN mkdir -p /cache/tmp && chown -R 65532:65532 /cache
USER 65532:65532
