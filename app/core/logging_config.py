import logging.config
from pathlib import Path


def configure_logging(log_level: str = "INFO", log_file: str = "logs/app.log") -> None:
    log_path = Path(log_file)
    log_path.parent.mkdir(parents=True, exist_ok=True)

    logging_config = {
        "version": 1,
        "disable_existing_loggers": False,
        "formatters": {
            "default": {
                "format": "%(asctime)s %(levelname)s [%(name)s] %(message)s",
            },
        },
        "handlers": {
            "console": {
                "class": "logging.StreamHandler",
                "formatter": "default",
                "level": log_level,
            },
            "file": {
                "class": "logging.handlers.RotatingFileHandler",
                "formatter": "default",
                "level": log_level,
                "filename": str(log_path),
                "maxBytes": 5 * 1024 * 1024,
                "backupCount": 3,
            },
        },
        "root": {
            "handlers": ["console", "file"],
            "level": log_level,
        },
    }

    logging.config.dictConfig(logging_config)
