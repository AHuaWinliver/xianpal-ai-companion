# SPDX-License-Identifier: Apache-2.0
"""Prepare pinned OFL fonts on the build machine; no runtime network access."""
import hashlib
import pathlib
import sys
import urllib.request

FILES = (
    ("NotoSansCJKsc-Regular.otf", "https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf", "2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b"),
    ("NotoColorEmoji.ttf", "https://raw.githubusercontent.com/googlefonts/noto-emoji/main/2D/fonts/NotoColorEmoji.ttf", "15671215ab769fdc7162a045d56fd7d7e477c51b04e6b3c761d914d8fdd6cc44"),
    ("CJK-LICENSE.txt", "https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/LICENSE", "6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2"),
    ("EMOJI-LICENSE.txt", "https://raw.githubusercontent.com/googlefonts/noto-emoji/main/2D/fonts/LICENSE", "6a73f9541c2de74158c0e7cf6b0a58ef774f5a780bf191f2d7ec9cc53efe2bf2"),
)


def prepare(destination):
    destination.mkdir(parents=True, exist_ok=True)
    for name, url, expected in FILES:
        target = destination / name
        data = target.read_bytes() if target.exists() else b""
        if hashlib.sha256(data).hexdigest() != expected:
            with urllib.request.urlopen(url, timeout=90) as response:
                data = response.read()
        if hashlib.sha256(data).hexdigest() != expected:
            raise ValueError("Font source checksum changed: " + name)
        target.write_bytes(data)
    (destination / "SOURCES.txt").write_text(
        "Bundled Noto fonts: SIL Open Font License 1.1.\n\n" +
        "\n".join(name + "\n" + url + "\nSHA256 " + digest + "\n" for name, url, digest in FILES),
        encoding="utf-8")


if __name__ == "__main__":
    prepare(pathlib.Path(sys.argv[1]).resolve())
