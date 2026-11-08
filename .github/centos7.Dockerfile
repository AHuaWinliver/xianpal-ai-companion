FROM quay.io/centos/centos@sha256:e4ca2ed0202e76be184e75fb26d14bf974193579039d5573fb2348664deef76e

# Pin the CentOS 7 userspace used for the glibc 2.17 compatibility checks.
RUN printf '[base]\nname=CentOS 7.9 base\nbaseurl=https://vault.centos.org/7.9.2009/os/$basearch/\nenabled=1\ngpgcheck=1\ngpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-CentOS-7\n[updates]\nname=CentOS 7.9 updates\nbaseurl=https://vault.centos.org/7.9.2009/updates/$basearch/\nenabled=1\ngpgcheck=1\ngpgkey=file:///etc/pki/rpm-gpg/RPM-GPG-KEY-CentOS-7\n' > /etc/yum.repos.d/xianpal-validation.repo \
    && yum --disablerepo='*' --enablerepo=base,updates install -y \
       libicu openssl-libs fontconfig libX11 libXext libXrender libICE libSM \
       mesa-libGL mesa-libEGL xorg-x11-server-Xvfb metacity dbus-x11 \
       dejavu-sans-fonts cjkuni-uming-fonts python3 \
    && yum clean all
