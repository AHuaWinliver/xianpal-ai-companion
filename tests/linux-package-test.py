# SPDX-License-Identifier: Apache-2.0
"""Offline archive contract tests; generated fixtures are never distributable assets."""
import argparse
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tarfile
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[1] / "src/DesktopTasks/XianPal.Linux/package-linux.py"
SPEC = importlib.util.spec_from_file_location("linux_package", SOURCE)
PACKAGE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(PACKAGE)


class ArchiveTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="xianpal-package-test-")
        self.addCleanup(self.temporary.cleanup)
        self.base = Path(self.temporary.name)
        self.root = self.base / "published"
        self.root.mkdir()
        self.output = self.base / "candidate.tar.gz"
        self.args = argparse.Namespace(folder=str(self.root), output=str(self.output), compat=False,
            source_commit="a" * 40,
            validation_url="https://github.com/example/example/actions/runs/1")
        for name in ("xianpal.dll", "launch.sh", "install.sh", "diagnose-linux.sh", "xianpal.png",
                     "LICENSE", "NOTICE", "ASSET-LICENSE.md", "libhostfxr.so", "libhostpolicy.so",
                     "libcoreclr.so", "System.Private.CoreLib.dll", "scenes/sect-hall-v2.png",
                     "fonts/NotoSansCJKsc-Regular.otf", "fonts/NotoColorEmoji.ttf",
                     "fonts/CJK-LICENSE.txt", "fonts/EMOJI-LICENSE.txt"):
            self.write(name, b"fixture")
        self.write("xianpal", b"\x7fELF\x02\x01" + b"\0" * 12 + (62).to_bytes(2, "little"))
        self.write("xianpal.deps.json", json.dumps({"libraries": {"xianpal/0.6.12": {}}}).encode())
        self.write("xianpal.runtimeconfig.json", b'{"runtimeOptions":{"tfm":"net10.0"}}')
        header = b"\x89PNG\r\n\x1a\n" + b"\0" * 8 + (384).to_bytes(4, "big") + (416).to_bytes(4, "big") + b"\x08\x06"
        for companion in ("orbit", "spark", "moss", "pippin"):
            frames = "assets" if companion == "orbit" else "assets/characters/" + companion
            for state, count in PACKAGE.STATES.items():
                for frame in range(count):
                    self.write("%s/%s/%02d.png" % (frames, state, frame), header)
            for frame in range(1, 7):
                self.write("%s/profile-expressions/%02d.png" % (frames, frame), b"fixture")
            for frame in range(157):
                self.write("performances/%s/%03d.png" % (companion, frame), b"fixture")

    def write(self, name, data):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)

    def test_complete_archive_and_exclusive_publication(self):
        PACKAGE.build(self.args)
        PACKAGE.verify_archive(self.output)
        before = self.output.read_bytes()
        checksum = hashlib.sha256(before).hexdigest()
        self.assertTrue(Path(str(self.output) + ".sha256").read_text().startswith(checksum))
        with tarfile.open(self.output) as archive:
            for entry in archive:
                if entry.name.endswith("/xianpal") or entry.name.endswith(".sh"):
                    self.assertEqual(entry.mode, 0o755)
            metadata = next(e for e in archive.getmembers() if e.name.endswith("/VALIDATION.json"))
            self.assertEqual(json.load(archive.extractfile(metadata))["targetDesktopAcceptance"], "pending")
        with self.assertRaises(FileExistsError):
            PACKAGE.build(self.args)
        self.assertEqual(self.output.read_bytes(), before)

    def test_missing_and_corrupt_resources(self):
        frame = self.root / "assets/running-right/23.png"
        frame.unlink()
        with self.assertRaises(FileNotFoundError):
            PACKAGE.build(self.args)
        self.write("assets/running-right/23.png", b"broken")
        with self.assertRaisesRegex(ValueError, "RGBA"):
            PACKAGE.build(self.args)
        self.assertFalse(self.output.exists())

    def test_personal_state_and_directory_escape(self):
        self.write("claude-integration.json", b"private")
        with self.assertRaisesRegex(ValueError, "personal state"):
            PACKAGE.build(self.args)
        for name in ("../outside", "/absolute", "root/../outside", "root\\outside", "line\nbreak"):
            with self.assertRaises(ValueError):
                PACKAGE.safe_name(name)

    def test_family_and_runtime_libraries(self):
        self.args.compat = True
        with self.assertRaisesRegex(ValueError, "runtime"):
            PACKAGE.build(self.args)
        self.write("xianpal.runtimeconfig.json", b'{"runtimeOptions":{"tfm":"net8.0"}}')
        with self.assertRaisesRegex(ValueError, "private runtime"):
            PACKAGE.build(self.args)
        self.write("xianpal", b"\x7fELF\x02\x01" + b"\0" * 12 + (183).to_bytes(2, "little"))
        with self.assertRaisesRegex(ValueError, "x64 only"):
            PACKAGE.build(self.args)

    def test_stream_verification_rejects_changed_bytes_and_links(self):
        checksum = hashlib.sha256(b"expected").hexdigest()
        for kind in ("changed", "link"):
            path = self.base / (kind + ".tar.gz")
            with tarfile.open(path, "w:gz") as archive:
                manifest = (checksum + "  xianpal\n").encode()
                entry = tarfile.TarInfo("candidate/SHA256SUMS.txt"); entry.size = len(manifest)
                archive.addfile(entry, io.BytesIO(manifest))
                entry = tarfile.TarInfo("candidate/xianpal"); entry.mode = 0o755
                if kind == "link":
                    entry.type = tarfile.SYMTYPE; entry.linkname = "outside"
                    archive.addfile(entry)
                else:
                    entry.size = 7
                    archive.addfile(entry, io.BytesIO(b"changed"))
            with self.assertRaises(ValueError):
                PACKAGE.verify_archive(path)


if __name__ == "__main__":
    unittest.main()
