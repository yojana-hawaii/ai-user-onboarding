FROM ghcr.io/aquasecurity/trivy:0.76.1@sha256:790fd66d1697395d8693d1acaa1ea702406a20f109b6fc7bac8db623eca6c06c
USER 0
RUN mkdir -p /cache/tmp && chown -R 65532:65532 /cache
USER 65532:65532
