from collections.abc import Iterator
from contextlib import contextmanager
from contextvars import ContextVar

request_id_ctx: ContextVar[str] = ContextVar("request_id", default="")
tenant_id_ctx: ContextVar[str] = ContextVar("tenant_id", default="")


@contextmanager
def bind_tenant_id(tenant_id: str | None) -> Iterator[None]:
    token = tenant_id_ctx.set((tenant_id or "").strip())
    try:
        yield
    finally:
        tenant_id_ctx.reset(token)
