#!/usr/bin/env python3
# SPDX-License-Identifier: Apache-2.0
"""Build/verify an offline Linux candidate without running it or using the network."""
import argparse
import hashlib
import io
import json
import os
from pathlib import Path, PurePosixPath
import re
import tarfile
import tempfile

GENERATED = {"SHA256SUMS.txt", "VALIDATION.json", "README-FIRST.md", "README-FIRST.zh-CN.md"}
STATES = {name: (8 if name in ("idle", "waiting", "failed") else 24) for name in (
    "idle", "waiting", "failed", "running", "running-left", "running-right", "waving",
    "review", "jumping", "punching", "eating", "wiping-mouth")}


def digest(stream):
    value = hashlib.sha256()
    for block in iter(lambda: stream.read(1024 * 1024), b""):
        value.update(block)
    return value.hexdigest()


def regular_path(path):
    for item in (path, *path.parents):
        if item.is_symlink() or (hasattr(item, "is_junction") and item.is_junction()):
            raise ValueError("Package paths cannot contain links: " + str(item))


def safe_name(name):
    path = PurePosixPath(name)
    if path.is_absolute() or ".." in path.parts or "\\" in name or any(c in name for c in "\r\n\0"):
        raise ValueError("Unsafe archive path: " + name)
    return path


def elf_arch(header):
    if len(header) < 20 or header[:6] != b"\x7fELF\x02\x01":
        raise ValueError("Expected a little-endian 64-bit Linux executable")
    machine = int.from_bytes(header[18:20], "little")
    if machine not in (62, 183):
        raise ValueError("Unsupported ELF architecture")
    return "x64" if machine == 62 else "arm64"


def describe(root, compat):
    for name in ("xianpal", "xianpal.dll", "xianpal.deps.json", "xianpal.runtimeconfig.json",
                 "launch.sh", "install.sh", "diagnose-linux.sh", "xianpal.png", "LICENSE", "NOTICE", "ASSET-LICENSE.md",
                 "libhostfxr.so", "libhostpolicy.so", "libcoreclr.so", "System.Private.CoreLib.dll", "scenes/sect-hall-v2.png",
                 "fonts/NotoSansCJKsc-Regular.otf", "fonts/NotoColorEmoji.ttf", "fonts/CJK-LICENSE.txt", "fonts/EMOJI-LICENSE.txt"):
        if not (root / name).is_file():
            raise ValueError("Incomplete published folder: " + name)
    with (root / "xianpal").open("rb") as stream:
        arch = elf_arch(stream.read(20))
    libraries = json.loads((root / "xianpal.deps.json").read_text(encoding="utf-8-sig"))["libraries"]
    versions = [key.split("/", 1)[1] for key in libraries if key.startswith("xianpal/")]
    if len(versions) != 1 or not re.fullmatch(r"\d+\.\d+\.\d+", versions[0]):
        raise ValueError("Application version was not found in dependency metadata")
    runtime = json.loads((root / "xianpal.runtimeconfig.json").read_text(encoding="utf-8-sig"))["runtimeOptions"]["tfm"]
    if runtime != ("net8.0" if compat else "net10.0"):
        raise ValueError("Package family disagrees with the published runtime")
    if compat:
        if arch != "x64":
            raise ValueError("The compatibility runtime is currently x64 only")
        for name in ("runtime/libstdc++.so.6", "runtime/libgcc_s.so.1", "licenses/gcc-runtime/COPYING3", "licenses/gcc-runtime/COPYING.RUNTIME", "licenses/gcc-runtime/SOURCES.txt"):
            if not (root / name).is_file():
                raise ValueError("Missing private runtime/license: " + name)
    for companion in ("orbit", "spark", "moss", "pippin"):
        frames = root / "assets" if companion == "orbit" else root / "assets" / "characters" / companion
        for expression in range(1, 7):
            if not (frames / "profile-expressions" / ("%02d.png" % expression)).is_file():
                raise ValueError("Missing walking expression: " + companion)
        for frame in range(157):
            if not (root / "performances" / companion / ("%03d.png" % frame)).is_file():
                raise ValueError("Missing reminder performance: " + companion)
        for state, count in STATES.items():
            for frame in range(count):
                path = frames / state / ("%02d.png" % frame)
                with path.open("rb") as stream:
                    header = stream.read(26)
                if header[:8] != b"\x89PNG\r\n\x1a\n" or header[16:24] != (384).to_bytes(4, "big") + (416).to_bytes(4, "big") or header[24:26] != b"\x08\x06":
                    raise ValueError("Invalid RGBA character frame: " + str(path))
    return versions[0], arch, runtime


def build(args):
    root = Path(args.folder).absolute()
    regular_path(root)
    output = Path(args.output).absolute()
    regular_path(output)
    if root == output or root in output.parents:
        raise ValueError("Write the archive outside the published application folder")
    if output.exists() or Path(str(output) + ".sha256").exists():
        raise FileExistsError("Refusing to replace an existing package or checksum")
    if not re.fullmatch(r"[0-9a-f]{40}", args.source_commit):
        raise ValueError("Use the full tested source commit SHA")
    if not re.fullmatch(r"https://github\.com/[\w.-]+/[\w.-]+/actions/runs/\d+", args.validation_url):
        raise ValueError("Use the corresponding GitHub Actions run URL")
    payload = {}
    private_names = {"desktop.json", "claude-integration.json", "workshop.json", ".env", ".xianpal-install", "installation.json"}
    for file in sorted(root.rglob("*")):
        regular_path(file)
        if not file.is_file():
            continue
        name = file.relative_to(root).as_posix()
        safe_name(name)
        if name in GENERATED:
            continue
        if name in private_names or any(part in (".git", "obj", "build", "scheduled-task-runs") for part in PurePosixPath(name).parts) or name.endswith(".log"):
            raise ValueError("Published folder contains build or personal state: " + name)
        payload[name] = file
    version, arch, runtime = describe(root, args.compat)
    family = "Compat-" if args.compat else ""
    prefix = "XianPal-%s-Linux-%s%s-Candidate" % (version, family, arch)
    metadata = {"version": version, "architecture": arch, "runtime": runtime, "status": "candidate",
                "sourceCommit": args.source_commit, "validationUrl": args.validation_url,
                "targetKernelAcceptance": "pending", "targetDesktopAcceptance": "pending", "realClaudeAcceptance": "pending"}
    extras = {"VALIDATION.json": (json.dumps(metadata, indent=2) + "\n").encode("utf-8")}
    for suffix, name in (("en", "README-FIRST.md"), ("zh-CN", "README-FIRST.zh-CN.md")):
        value = Path(__file__).with_name("PACKAGE-README." + suffix + ".md").read_text(encoding="utf-8")
        for key, replacement in {"VERSION": version, "ARCH": arch, "RUNTIME": runtime, "SOURCE_COMMIT": args.source_commit, "VALIDATION_URL": args.validation_url}.items():
            value = value.replace("@" + key + "@", replacement)
        extras[name] = value.encode("utf-8")
    hashes = {}
    for name, path in payload.items():
        with path.open("rb") as stream:
            hashes[name] = digest(stream)
    hashes.update({name: hashlib.sha256(data).hexdigest() for name, data in extras.items()})
    extras["SHA256SUMS.txt"] = "".join(hashes[name] + "  " + name + "\n" for name in sorted(hashes)).encode("utf-8")
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = None
    try:
        with tempfile.NamedTemporaryFile(prefix=".xianpal-package-", dir=str(output.parent), delete=False) as stream:
            temporary = Path(stream.name)
        with tarfile.open(temporary, "w:gz", compresslevel=6) as archive:
            header = tarfile.TarInfo(prefix); header.type = tarfile.DIRTYPE; header.mode = 0o755
            archive.addfile(header)
            for name in ("SHA256SUMS.txt", "VALIDATION.json", "README-FIRST.md", "README-FIRST.zh-CN.md"):
                data = extras[name]; entry = tarfile.TarInfo(prefix + "/" + name); entry.size = len(data); entry.mode = 0o644
                archive.addfile(entry, io.BytesIO(data))
            for name, path in payload.items():
                entry = tarfile.TarInfo(prefix + "/" + name)
                entry.size = path.stat().st_size; entry.mode = 0o755 if name == "xianpal" or name.endswith(".sh") else 0o644
                with path.open("rb") as stream:
                    archive.addfile(entry, stream)
        verify_archive(temporary)
        # Exclusive publication: concurrent or pre-existing outputs are retained.
        os.link(temporary, output)
        with output.open("rb") as stream:
            checksum = digest(stream)
        with Path(str(output) + ".sha256").open("x", encoding="ascii") as stream:
            stream.write(checksum + "  " + output.name + "\n")
        print("Built candidate:", output)
        print("SHA256:", checksum)
    finally:
        if temporary is not None:
            temporary.unlink(missing_ok=True)


def verify_archive(path):
    expected = None
    seen = set()
    prefix = None
    with tarfile.open(path, "r|gz") as archive:
        for entry in archive:
            name = safe_name(entry.name)
            if not name.parts:
                raise ValueError("Empty archive path")
            if prefix is None:
                prefix = name.parts[0]
            if name.parts[0] != prefix or entry.issym() or entry.islnk() or not (entry.isfile() or entry.isdir()):
                raise ValueError("Unexpected archive member: " + entry.name)
            if entry.isdir():
                continue
            relative = PurePosixPath(*name.parts[1:]).as_posix()
            if relative in seen:
                raise ValueError("Duplicate archive file: " + relative)
            seen.add(relative)
            stream = archive.extractfile(entry)
            if relative == "SHA256SUMS.txt":
                if expected is not None or entry.size > 4 * 1024 * 1024:
                    raise ValueError("Invalid checksum manifest")
                expected = {}
                for line in stream.read().decode("utf-8").splitlines():
                    checksum, filename = line.split("  ", 1)
                    safe_name(filename)
                    if not re.fullmatch(r"[0-9a-f]{64}", checksum) or filename in expected:
                        raise ValueError("Invalid checksum entry")
                    expected[filename] = checksum
            else:
                if expected is None or relative not in expected or digest(stream) != expected[relative]:
                    raise ValueError("Package checksum mismatch: " + relative)
                if (relative == "xianpal" or relative.endswith(".sh")) and entry.mode & 0o111 == 0:
                    raise ValueError("Executable permissions missing: " + relative)
    if expected is None or seen != set(expected) | {"SHA256SUMS.txt"}:
        raise ValueError("Package files disagree with the checksum manifest")
    print("PASS archive: %d files, checksums, paths and executable modes" % len(expected))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    create = commands.add_parser("build")
    create.add_argument("folder"); create.add_argument("--output", required=True)
    create.add_argument("--source-commit", required=True); create.add_argument("--validation-url", required=True)
    create.add_argument("--compat", action="store_true")
    inspect = commands.add_parser("verify"); inspect.add_argument("archive")
    args = parser.parse_args()
    if args.command == "build":
        build(args)
    else:
        verify_archive(args.archive)


if __name__ == "__main__":
    main()
