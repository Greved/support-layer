import logging
import uuid
from contextlib import asynccontextmanager

from fastapi import FastAPI, Request
from prometheus_client import make_asgi_app
from starlette.middleware.base import BaseHTTPMiddleware

from app.api.internal import router as internal_router
from app.api.routes import router
from app.core.config import get_settings
from app.core.log_context import request_id_ctx, tenant_id_ctx
from app.core.logging_config import configure_logging
from app.core.telemetry import configure_telemetry

logger = logging.getLogger(__name__)


class RequestIdMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next):
        request_id = str(uuid.uuid4())
        tenant_id = request.headers.get("x-tenant-id", "")

        request_token = request_id_ctx.set(request_id)
        tenant_token = tenant_id_ctx.set(tenant_id)
        try:
            response = await call_next(request)
        finally:
            request_id_ctx.reset(request_token)
            tenant_id_ctx.reset(tenant_token)

        response.headers["X-Request-ID"] = request_id
        return response


@asynccontextmanager
async def lifespan(app: FastAPI):
    settings = get_settings()
    logger.info("Starting %s", settings.app_name)
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

    app.add_middleware(RequestIdMiddleware)

    app.include_router(router, prefix=settings.api_prefix)
    app.include_router(internal_router, prefix="/internal")

    # Expose Prometheus metrics endpoint
    app.mount("/metrics", make_asgi_app())

    # OpenTelemetry (OTLP) is enabled only when OTEL_EXPORTER_OTLP_ENDPOINT is configured.
    configure_telemetry(app, settings.app_name)

    return app


app = create_app()
