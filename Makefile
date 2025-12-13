SHELL := bash

.PHONY: install run dev lint format test compose-infra compose-full

install:
	python -m venv .venv && . .venv/bin/activate && pip install -U pip && pip install .[dev]

run:
	. .venv/bin/activate && uvicorn app.main:app --reload --host 0.0.0.0 --port 8000

dev: run

lint:
	. .venv/bin/activate && ruff check . && black --check .

format:
	. .venv/bin/activate && black . && ruff check --fix .

test:
	. .venv/bin/activate && pytest

compose-infra:
	docker compose -f docker-compose.infra.yml up -d

compose-full:
	docker compose -f docker-compose.full.yml up -d
