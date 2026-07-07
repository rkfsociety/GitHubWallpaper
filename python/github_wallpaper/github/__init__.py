"""GitHub REST API, polling и OAuth."""

__all__ = ["GitHubSession", "RepoPoller"]


def __getattr__(name: str) -> object:
    if name == "GitHubSession":
        from github_wallpaper.github.github_session import GitHubSession

        return GitHubSession
    if name == "RepoPoller":
        from github_wallpaper.github.repo_poller import RepoPoller

        return RepoPoller
    raise AttributeError(f"module {__name__!r} has no attribute {name!r}")
