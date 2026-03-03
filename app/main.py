import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api.internal import router as internal_router
from app.api.routes import router
from app.core.config import get_settings
from app.core.logging_config import configure_logging

logger = logging.getLogger(__name__)


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    logger.info("Starting %s", settings.app_name)
    # TODO: initialize Haystack pipelines once available.
    yield
    logger.info("Shutting down %s", settings.app_name)


def create_app() -> FastAPI:
    settings = get_settings()
    configure_logging(settings.log_level, settings.log_file)
    app = FastAPI(
        title=settings.app_name,
        lifespan=lifespan,
        docs_url="/api/docs",
        redoc_url="/api/redoc",
        openapi_url="/api/openapi.json",
    )
    app.include_router(router, prefix=settings.api_prefix)
    app.include_router(internal_router, prefix="/internal")
    return app


app = create_app()
