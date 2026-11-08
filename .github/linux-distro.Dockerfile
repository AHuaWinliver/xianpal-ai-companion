# SPDX-License-Identifier: Apache-2.0
# Build-only validation image. The application never installs these packages.
ARG XIANPAL_IMAGE=debian@sha256:268fb24c39bca92d326dd83ea3abebe4a3f7751c804ffef23bb6d857dd343ff8
FROM ${XIANPAL_IMAGE}
ARG XIANPAL_FAMILY
RUN set -eu; \
    case "$XIANPAL_FAMILY" in \
      debian) \
        export DEBIAN_FRONTEND=noninteractive; \
        if grep -q 'VERSION_CODENAME=bullseye' /etc/os-release; then \
          printf 'deb http://archive.debian.org/debian bullseye main\ndeb http://archive.debian.org/debian-security bullseye-security main\n' > /etc/apt/sources.list; \
          printf 'Acquire::Check-Valid-Until "false";\n' > /etc/apt/apt.conf.d/99xianpal-archive; \
        fi; \
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
          Mesa-libGL1 Mesa-libEGL1 xorg-x11-server-Xvfb openbox dbus-1-daemon dbus-1-x11 \
          dejavu-fonts python3; \
        zypper clean --all ;; \
      *) echo 'Unknown validation image family' >&2; exit 1 ;; \
    esac
