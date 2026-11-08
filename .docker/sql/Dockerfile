FROM mcr.microsoft.com/mssql/server:2022-CU27-GDR1-ubuntu-22.04@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718
USER 0
# Apply vendor OS fixes until the upstream image incorporates them.
RUN apt-get update && apt-get install -y --no-install-recommends --only-upgrade apt=2.4.15ubuntu0.1 apt-transport-https=2.4.15ubuntu0.1 libapt-pkg6.0=2.4.15ubuntu0.1 libbrotli1=1.0.9-2ubuntu0.1 && rm -rf /var/lib/apt/lists/*
# This development database does not support external R/Python scripts. Remove
# their unused, vulnerable Go helpers; never enable external scripts here.
RUN rm /opt/mssql-extensibility/bin/launchpad /opt/mssql/bin/launchpadd /opt/mssql/bin/setnetbr
USER 10001:10001
