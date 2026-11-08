# SPDX-License-Identifier: Apache-2.0
# Build-only validation image. The application never installs these packages.
ARG XIANPAL_IMAGE
FROM ${XIANPAL_IMAGE}
ARG XIANPAL_FAMILY
RUN set -eu; \
    case "$XIANPAL_FAMILY" in \
      debian) \
        export DEBIAN_FRONTEND=noninteractive; \
        apt-get update; \
        apt-get install -y --no-install-recommends libicu-dev libssl-dev ca-certificates \
          libfontconfig1 libx11-6 libxext6 libxrender1 libice6 libsm6 libgl1 libegl1 \
          xvfb openbox dbus-x11 fonts-dejavu-core python3; \
        apt-get clean ;; \
      rhel) \
        dnf install -y epel-release; \
        dnf install -y libicu openssl-libs ca-certificates fontconfig libX11 libXext \
          libXrender libICE libSM mesa-libGL mesa-libEGL xorg-x11-server-Xvfb \
          openbox dbus-x11 dejavu-sans-fonts python3; \
        dnf clean all ;; \
      suse) \
        zypper --non-interactive install --no-recommends libicu libopenssl3 \
          ca-certificates fontconfig libX11-6 libXext6 libXrender1 libICE6 libSM6 \
          Mesa-libGL1 Mesa-libEGL1 xorg-x11-server-Xvfb openbox dbus-1-x11 \
          dejavu-fonts python3; \
        zypper clean --all ;; \
      *) echo 'Unknown validation image family' >&2; exit 1 ;; \
    esac
