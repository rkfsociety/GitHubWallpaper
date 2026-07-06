"""Разбор строк owner/repo и URL GitHub (минимальный порт RepoUrlParser.cs)."""

from __future__ import annotations

import re
from dataclasses import dataclass
from urllib.parse import urlparse

_VALID_NAME = re.compile(r"^[A-Za-z0-9][A-Za-z0-9._-]*$")


@dataclass(frozen=True, slots=True)
class RepoReference:
    owner: str
    repo: str


def try_parse(input_value: str | None) -> RepoReference | None:
    """Разбирает owner/repo или https://github.com/owner/repo."""
    if not input_value or not input_value.strip():
        return None

    trimmed = input_value.strip()
    reference = _try_parse_from_uri(trimmed)
    if reference is not None:
        return reference

    return _try_parse_from_slug(trimmed)


def _try_parse_from_uri(input_value: str) -> RepoReference | None:
    candidate: str | None
    if "://" in input_value:
        candidate = input_value
    elif input_value.lower().startswith(("github.com/", "www.github.com/")):
        candidate = f"https://{input_value.lstrip('/')}"
    else:
        candidate = None

    if candidate is None:
        return None

    parsed = urlparse(candidate)
    if parsed.scheme not in ("http", "https"):
        return None

    host = parsed.hostname or ""
    if host.lower() not in ("github.com", "www.github.com"):
        return None

    return _try_create_from_path(parsed.path)


def _try_parse_from_slug(input_value: str) -> RepoReference | None:
    path = input_value.split("#", 1)[0].split("?", 1)[0]
    return _try_create_from_path(path)


def _try_create_from_path(path: str) -> RepoReference | None:
    segments = [segment.strip() for segment in path.split("/") if segment.strip()]
    if len(segments) < 2:
        return None

    owner = segments[0]
    repo = segments[1]
    if repo.lower().endswith(".git"):
        repo = repo[:-4]

    if not _is_valid_name(owner) or not _is_valid_name(repo):
        return None

    return RepoReference(owner=owner, repo=repo)


def _is_valid_name(name: str) -> bool:
    return bool(name) and _VALID_NAME.match(name) is not None
