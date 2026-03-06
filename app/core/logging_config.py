import logging
import logging.config
import logging.handlers
from pathlib import Path


# ContextVar-backed request-ID is set by RequestIdMiddleware in app/main.py.
# This filter injects the current value into every log record.
class _RequestIdFilter(logging.Filter):
    def filter(self, record: logging.LogRecord) -> bool:
        from app.core.log_context import request_id_ctx, tenant_id_ctx

        record.request_id = request_id_ctx.get("")
        record.tenant_id = tenant_id_ctx.get("")
        return True


def configure_logging(log_level: str = "INFO", log_file: str = "logs/app.log") -> None:
    log_path = Path(log_file)
    log_path.parent.mkdir(parents=True, exist_ok=True)

    try:
        # python-json-logger >= 3.x uses pythonjsonlogger.json.
        # Earlier versions used pythonjsonlogger.jsonlogger.
        try:
            from pythonjsonlogger import json as _pjl_json  # noqa: F401 — check only

            json_fmt_class = "pythonjsonlogger.json.JsonFormatter"
        except ImportError:
            from pythonjsonlogger import jsonlogger  # noqa: F401

            json_fmt_class = "pythonjsonlogger.jsonlogger.JsonFormatter"

        json_fmt_args = {
            "fmt": "%(asctime)s %(levelname)s %(name)s %(message)s %(request_id)s %(tenant_id)s"
        }
        use_json = True
    except ImportError:
        use_json = False

    if use_json:
        formatters: dict = {
            "json": {
                "()": json_fmt_class,
                "fmt": json_fmt_args["fmt"],
            },
        }
        console_formatter = "json"
        file_formatter = "json"
    else:
        formatters = {
            "default": {
                "format": "%(asctime)s %(levelname)s [%(name)s] %(message)s",
            },
        }
        console_formatter = "default"
        file_formatter = "default"

    logging_config = {
        "version": 1,
        "disable_existing_loggers": False,
        "filters": {
            "request_id": {
                "()": _RequestIdFilter,
            },
        },
        "formatters": formatters,
        "handlers": {
            "console": {
                "class": "logging.StreamHandler",
                "formatter": console_formatter,
                "level": log_level,
                "filters": ["request_id"],
            },
            "file": {
                "class": "logging.handlers.RotatingFileHandler",
                "formatter": file_formatter,
                "level": log_level,
                "filename": str(log_path),
                "maxBytes": 5 * 1024 * 1024,
                "backupCount": 3,
                "filters": ["request_id"],
            },
        },
        "root": {
            "handlers": ["console", "file"],
            "level": log_level,
        },
    }

    logging.config.dictConfig(logging_config)
