from pathlib import Path

import pytest

from app.core.config import get_settings


@pytest.fixture(autouse=True)
def clear_settings_cache():
    get_settings.cache_clear()
    yield
    get_settings.cache_clear()


def test_internal_secret_file_overrides_plain_env(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    secret_file = tmp_path / "internal_secret.txt"
    secret_file.write_text("from-file-secret\n", encoding="utf-8")

    monkeypatch.setenv("INTERNAL_SECRET", "plain-env-secret")
    monkeypatch.setenv("INTERNAL_SECRET_FILE", str(secret_file))

    settings = get_settings()
    assert settings.internal_secret == "from-file-secret"


def test_missing_secret_file_raises(monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setenv("INTERNAL_SECRET_FILE", "/tmp/definitely-missing-secret-file")

    with pytest.raises(RuntimeError):
        get_settings()
