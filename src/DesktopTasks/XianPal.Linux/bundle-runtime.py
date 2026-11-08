#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Stage pinned, application-private C++ libraries for the glibc 2.17 build."""
import hashlib
import pathlib
import shutil
import subprocess
import sys
import tempfile
import urllib.request
import zipfile


PACKAGES = (
    ("libstdcxx-16.2.0-h934c35e_7.conda",
     "fa7018298629fc429971fffd6ecba3d27a7b02e4e890d6a93ea01ffebe8cb745"),
    ("libgcc-16.2.0-ha9f2e26_7.conda",
     "e031634c3a928f9594eba868bc46ef5230d6571a364a0c63215b06c4563bfcea"),
)

LICENSES = (
    ("COPYING3", "8ceb4b9ee5adedde47b31e975c1d90c73ad27b6b165a1dcd80c7c545eb65b903"),
    ("COPYING.RUNTIME", "9d6b43ce4d8de0c878bf16b54d8e7a10d9bd42b75178153e3af6a815bdc90f74"),
)


def main(destination):
    destination = pathlib.Path(destination).resolve()
    runtime = destination / "runtime"
    runtime.mkdir(parents=True, exist_ok=True)
    notices = destination / "licenses" / "gcc-runtime"
    notices.mkdir(parents=True, exist_ok=True)
    sources = []
    for filename, expected in PACKAGES:
        url = "https://conda.anaconda.org/conda-forge/linux-64/" + filename
        with tempfile.TemporaryDirectory(prefix="xianpal-runtime-") as temporary:
            temporary = pathlib.Path(temporary)
            archive = temporary / filename
            with urllib.request.urlopen(url, timeout=60) as response, archive.open("wb") as output:
                shutil.copyfileobj(response, output)
            if hashlib.sha256(archive.read_bytes()).hexdigest() != expected:
                raise RuntimeError("Package checksum mismatch: " + filename)
            with zipfile.ZipFile(archive) as package:
                for member in package.namelist():
                    if not member.endswith(".tar.zst"):
                        continue
                    compressed = temporary / pathlib.PurePosixPath(member).name
                    with package.open(member) as source, compressed.open("wb") as output:
                        shutil.copyfileobj(source, output)
                    extracted = temporary / compressed.stem
                    extracted.mkdir()
                    subprocess.run(["tar", "--zstd", "-xf", str(compressed), "-C", str(extracted)], check=True)
                    for library in (extracted / "lib").glob("lib*.so*"):
                        if library.name.startswith(("libstdc++.", "libgcc_s.")):
                            shutil.copyfile(library.resolve(), runtime / library.name)
                    license_root = extracted / "info" / "licenses"
                    if license_root.is_dir():
                        shutil.copytree(license_root, notices / filename, dirs_exist_ok=True)
                    recipe_root = extracted / "info" / "recipe"
                    if recipe_root.is_dir():
                        shutil.copytree(recipe_root, notices / (filename + "-recipe"), dirs_exist_ok=True)
            sources.append(url + "\nSHA256 " + expected + "\nLicense GPL-3.0-only WITH GCC-exception-3.1\n")
    if not (runtime / "libstdc++.so.6").is_file() or not (runtime / "libgcc_s.so.1").is_file():
        raise RuntimeError("Incomplete private C++ runtime.")
    for filename, expected in LICENSES:
        url = "https://raw.githubusercontent.com/gcc-mirror/gcc/releases/gcc-16.2.0/" + filename
        with urllib.request.urlopen(url, timeout=60) as response:
            license_text = response.read()
        if hashlib.sha256(license_text).hexdigest() != expected:
            raise RuntimeError("License checksum mismatch: " + filename)
        (notices / filename).write_bytes(license_text)
        sources.append(url + "\nSHA256 " + expected + "\n")
    (notices / "SOURCES.txt").write_text("\n".join(sources), encoding="utf-8")
    shutil.copyfile(pathlib.Path(__file__).with_name("launch.sh"), destination / "launch.sh")
    (destination / "launch.sh").chmod(0o755)


if __name__ == "__main__":
    main(sys.argv[1])
