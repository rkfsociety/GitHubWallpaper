"""Генерация PKCE-пары для OAuth (GitHubPkce.cs)."""

from __future__ import annotations

import base64
import hashlib
import secrets


def create_authorization_request() -> tuple[str, str, str]:
    verifier = _generate_code_verifier()
    challenge = _generate_code_challenge(verifier)
    state = _generate_state()
    return verifier, challenge, state


def _generate_code_verifier() -> str:
    return _base64_url_encode(secrets.token_bytes(32))


def _generate_code_challenge(verifier: str) -> str:
    digest = hashlib.sha256(verifier.encode("ascii")).digest()
    return _base64_url_encode(digest)


def _generate_state() -> str:
    return _base64_url_encode(secrets.token_bytes(16))


def _base64_url_encode(data: bytes) -> str:
    return base64.urlsafe_b64encode(data).rstrip(b"=").decode("ascii")
