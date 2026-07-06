# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller spec: bootstrap one-file launcher (без Qt WebEngine)."""

from pathlib import Path

spec_dir = Path(SPECPATH)

a = Analysis(
    [str(spec_dir / "github_wallpaper" / "bootstrap" / "main.py")],
    pathex=[str(spec_dir)],
    binaries=[],
    datas=[],
    hiddenimports=[
        "httpx",
        "httpcore",
        "certifi",
        "h11",
        "anyio",
        "sniffio",
        "idna",
    ],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[
        "PySide6",
        "keyring",
        "pydantic",
        "pytest",
    ],
    noarchive=False,
    optimize=0,
)

pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    a.binaries,
    a.datas,
    [],
    name="GitHubWallpaper",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    upx_exclude=[],
    runtime_tmpdir=None,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)
