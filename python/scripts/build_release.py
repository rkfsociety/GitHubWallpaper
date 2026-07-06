"""Сборка portable-дистрибутива GitHub Wallpaper v2.0."""

from __future__ import annotations

import argparse
import os
import re
import shutil
import subprocess
import sys
import tarfile
import zipfile
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser(description="Build GitHub Wallpaper v2.0 release artifacts.")
    parser.add_argument(
        "--output-dir",
        type=Path,
        default=Path("publish"),
        help="Directory for release archives (default: publish)",
    )
    args = parser.parse_args()

    python_dir = Path(__file__).resolve().parent.parent
    publish_dir = args.output_dir
    if not publish_dir.is_absolute():
        publish_dir = python_dir / publish_dir

    _write_build_version(python_dir)
    _write_build_flavor(python_dir, "runtime")
    _run_pyinstaller(python_dir, "GitHubWallpaper.spec")

    dist_dir = python_dir / "dist" / "GitHubWallpaper"
    if not dist_dir.is_dir():
        raise SystemExit(f"PyInstaller runtime output not found: {dist_dir}")

    executable = dist_dir / ("GitHubWallpaper.exe" if sys.platform == "win32" else "GitHubWallpaper")
    if not executable.is_file():
        raise SystemExit(f"Runtime executable not found: {executable}")

    publish_dir.mkdir(parents=True, exist_ok=True)
    if sys.platform == "win32":
        runtime_archive = publish_dir / "GitHubWallpaper-Win-x64.zip"
        _create_zip(dist_dir, runtime_archive)
        print(f"Built runtime archive {runtime_archive} ({runtime_archive.stat().st_size:,} bytes)")
    else:
        runtime_archive = publish_dir / "GitHubWallpaper-linux-x64.tar.gz"
        _create_tarball(dist_dir, runtime_archive)
        print(f"Built runtime archive {runtime_archive} ({runtime_archive.stat().st_size:,} bytes)")

    _write_build_flavor(python_dir, "bootstrap")
    _run_pyinstaller(python_dir, "GitHubWallpaperBootstrap.spec")

    bootstrap_exe = python_dir / "dist" / ("GitHubWallpaper.exe" if sys.platform == "win32" else "GitHubWallpaper")
    if not bootstrap_exe.is_file():
        raise SystemExit(f"Bootstrap executable not found: {bootstrap_exe}")

    installer_path = publish_dir / bootstrap_exe.name
    shutil.copy2(bootstrap_exe, installer_path)
    print(f"Built installer {installer_path} ({installer_path.stat().st_size:,} bytes)")
    return 0


def _write_build_version(python_dir: Path) -> None:
    version = os.environ.get("GITHUB_WALLPAPER_VERSION")
    if not version:
        pyproject = (python_dir / "pyproject.toml").read_text(encoding="utf-8")
        match = re.search(r'^version\s*=\s*"(?P<version>[^"]+)"', pyproject, re.MULTILINE)
        version = match.group("version") if match else "2.0.0"

    target = python_dir / "github_wallpaper" / "_build_version.py"
    target.write_text(f'__version__ = "{version}"\n', encoding="utf-8")


def _write_build_flavor(python_dir: Path, flavor: str) -> None:
    target = python_dir / "github_wallpaper" / "_build_flavor.py"
    target.write_text(f'BUILD_FLAVOR = "{flavor}"\n', encoding="utf-8")


def _run_pyinstaller(python_dir: Path, spec_name: str) -> None:
    spec_path = python_dir / spec_name
    build_dir = python_dir / "build"
    dist_dir = python_dir / "dist"

    if build_dir.exists():
        shutil.rmtree(build_dir)
    if dist_dir.exists():
        shutil.rmtree(dist_dir)

    command = [sys.executable, "-m", "PyInstaller", "--noconfirm", "--clean", str(spec_path)]
    subprocess.run(command, cwd=python_dir, check=True)


def _create_zip(source_dir: Path, archive_path: Path) -> None:
    if archive_path.exists():
        archive_path.unlink()

    with zipfile.ZipFile(archive_path, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for path in source_dir.rglob("*"):
            if path.is_file():
                archive.write(path, path.relative_to(source_dir.parent).as_posix())


def _create_tarball(source_dir: Path, archive_path: Path) -> None:
    if archive_path.exists():
        archive_path.unlink()

    with tarfile.open(archive_path, "w:gz") as archive:
        archive.add(source_dir, arcname=source_dir.name)


if __name__ == "__main__":
    raise SystemExit(main())
