import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api.routes import router
from app.core.config import get_settings

logger = logging.getLogger(__name__)


@asynccontextmanager
def lifespan(app: FastAPI):
    settings = get_settings()
    logger.info("Starting %s", settings.app_name)
    # TODO: initialize Haystack pipelines once available.
    yield
    logger.info("Shutting down %s", settings.app_name)


def create_app() -> FastAPI:
    app = FastAPI(title="Tech Support RAG API", lifespan=lifespan)
    app.include_router(router, prefix="/api")
    return app


app = create_app()
