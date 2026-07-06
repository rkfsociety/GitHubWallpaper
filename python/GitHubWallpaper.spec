# -*- mode: python ; coding: utf-8 -*-
"""PyInstaller spec: GitHub Wallpaper v2.0 (one-folder, Qt WebEngine)."""

from pathlib import Path

from PyInstaller.utils.hooks import collect_all

spec_dir = Path(SPECPATH)
repo_root = spec_dir.parent
wwwroot = repo_root / "wwwroot" / "wallpaper"

pyside_datas, pyside_binaries, pyside_hiddenimports = collect_all("PySide6")

assets_dir = spec_dir / "github_wallpaper" / "wallpaper" / "assets"

datas = [
    (str(wwwroot), "wwwroot/wallpaper"),
    (str(assets_dir), "github_wallpaper/wallpaper/assets"),
    *pyside_datas,
]
hiddenimports = [
    *pyside_hiddenimports,
    "PySide6.QtWebEngineWidgets",
    "PySide6.QtWebEngineCore",
    "keyring.backends.Windows",
    "keyring.backends.SecretService",
]

a = Analysis(
    [str(spec_dir / "github_wallpaper" / "main.py")],
    pathex=[str(spec_dir)],
    binaries=pyside_binaries,
    datas=datas,
    hiddenimports=hiddenimports,
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=[],
    noarchive=False,
    optimize=0,
)

pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="GitHubWallpaper",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=False,
    console=False,
    disable_windowed_traceback=False,
    argv_emulation=False,
    target_arch=None,
    codesign_identity=None,
    entitlements_file=None,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=False,
    upx_exclude=[],
    name="GitHubWallpaper",
)
