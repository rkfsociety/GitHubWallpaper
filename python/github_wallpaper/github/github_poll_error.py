"""Описание ошибок опроса GitHub (GitHubPollError.cs)."""

from __future__ import annotations

import json
from dataclasses import dataclass

import httpx

from github_wallpaper.github.api_parsers import GitHubPollErrorCode
from github_wallpaper.github.github_models import GitHubApiException


@dataclass(frozen=True, slots=True)
class GitHubPollError:
    code: GitHubPollErrorCode
    message: str
    hint: str | None
    is_fatal_for_repo: bool

    @property
    def code_name(self) -> str:
        return self.code.value

    @classmethod
    def from_exception(cls, exception: BaseException) -> GitHubPollError:
        if isinstance(exception, GitHubApiException):
            return cls._from_api_exception(exception)
        if isinstance(exception, httpx.RequestError):
            return cls(
                GitHubPollErrorCode.NETWORK,
                "Не удалось связаться с GitHub.",
                "Проверьте подключение к интернету. Повторим при следующем опросе.",
                False,
            )
        if isinstance(exception, httpx.TimeoutException):
            return cls(
                GitHubPollErrorCode.NETWORK,
                "Превышено время ожидания ответа GitHub.",
                "Повторим при следующем опросе.",
                False,
            )
        return cls(GitHubPollErrorCode.UNKNOWN, str(exception), None, False)

    @classmethod
    def _from_api_exception(cls, api: GitHubApiException) -> GitHubPollError:
        status = api.status_code
        if status == 404:
            return cls(
                GitHubPollErrorCode.NOT_FOUND,
                f"Репозиторий не найден: {_extract_repo_path(api.path)}.",
                "Проверьте owner/repo в настройках. Для приватных репозиториев нужен PAT со scope repo.",
                True,
            )
        if status == 401:
            return cls(
                GitHubPollErrorCode.UNAUTHORIZED,
                "Токен GitHub недействителен или истёк.",
                "Откройте Настройки и обновите PAT.",
                False,
            )
        if status == 403 and _is_rate_limit_body(api.response_body):
            return cls(
                GitHubPollErrorCode.RATE_LIMITED,
                "Исчерпан лимит запросов GitHub API.",
                "Добавьте PAT или дождитесь сброса лимита.",
                False,
            )
        if status == 403:
            return cls(
                GitHubPollErrorCode.FORBIDDEN,
                f"Нет доступа к репозиторию: {_extract_repo_path(api.path)}.",
                "Для приватных репозиториев создайте PAT со scope repo.",
                True,
            )
        if status == 429:
            return cls(
                GitHubPollErrorCode.RATE_LIMITED,
                "Слишком много запросов к GitHub API.",
                "Опрос возобновится после паузы.",
                False,
            )
        if status >= 500:
            return cls(
                GitHubPollErrorCode.SERVER_ERROR,
                "Временная ошибка сервера GitHub.",
                "Повторим при следующем опросе.",
                False,
            )
        return cls(GitHubPollErrorCode.UNKNOWN, str(api), None, False)


def _is_rate_limit_body(body: str | None) -> bool:
    return body is not None and "rate limit" in body.lower()


def _extract_repo_path(path: str) -> str:
    prefix = "/repos/"
    if not path.lower().startswith(prefix):
        return path
    remainder = path[len(prefix) :]
    slash = remainder.find("/")
    return remainder if slash < 0 else remainder[:slash]


def try_read_github_message(json_text: str | None) -> str | None:
    if not json_text:
        return None
    try:
        data = json.loads(json_text)
        message = data.get("message")
        return message if isinstance(message, str) else None
    except json.JSONDecodeError:
        return None
