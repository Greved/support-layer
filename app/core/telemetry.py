import logging
import os

from fastapi import FastAPI

logger = logging.getLogger(__name__)

_telemetry_initialized = False


def configure_telemetry(app: FastAPI, service_name: str) -> None:
    global _telemetry_initialized
    if _telemetry_initialized:
        return

    otlp_endpoint = os.getenv("OTEL_EXPORTER_OTLP_ENDPOINT")
    if not otlp_endpoint:
        return

    try:
        from opentelemetry import trace
        from opentelemetry.exporter.otlp.proto.http.trace_exporter import OTLPSpanExporter
        from opentelemetry.instrumentation.fastapi import FastAPIInstrumentor
        from opentelemetry.instrumentation.requests import RequestsInstrumentor
        from opentelemetry.sdk.resources import Resource
        from opentelemetry.sdk.trace import TracerProvider
        from opentelemetry.sdk.trace.export import BatchSpanProcessor
    except ImportError:
        logger.warning("OpenTelemetry dependencies are missing; tracing is disabled")
        return

    resource = Resource.create({"service.name": service_name})
    provider = TracerProvider(resource=resource)
    span_exporter = OTLPSpanExporter(endpoint=otlp_endpoint)
    provider.add_span_processor(BatchSpanProcessor(span_exporter))
    trace.set_tracer_provider(provider)

    FastAPIInstrumentor.instrument_app(app, tracer_provider=provider)
    requests_instrumentor = RequestsInstrumentor()
    if not requests_instrumentor.is_instrumented_by_opentelemetry:
        requests_instrumentor.instrument()

    _telemetry_initialized = True
    logger.info("OpenTelemetry initialized endpoint=%s service=%s", otlp_endpoint, service_name)
