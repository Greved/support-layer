SHELL := bash

.PHONY: install run dev lint format test compose-infra compose-full compose-infra-secrets compose-full-secrets ingest pre-commit hooks k6-widget k6-upload k6-admin k6-all zap-baseline

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

ingest:
	. .venv/bin/activate && python -m ingestion.cli ingest "data/**/*.pdf" "data/**/*.html" "data/**/*.md"

compose-infra:
	docker compose -f docker-compose.infra.yml up -d

compose-full:
	docker compose -f docker-compose.full.yml up -d

compose-infra-secrets:
	docker compose -f docker-compose.infra.yml -f docker-compose.secrets.yml up -d

compose-full-secrets:
	docker compose -f docker-compose.full.yml -f docker-compose.secrets.yml up -d

compose-observability:
	docker compose -f docker-compose.observability.yml up -d

hooks:
	. .venv/bin/activate && pre-commit install

pre-commit:
	. .venv/bin/activate && pre-commit run --all-files

k6-widget:
	k6 run --summary-export k6/widget-chat-summary.json k6/scenarios/widget-chat.js

k6-upload:
	k6 run --summary-export k6/pdf-upload-summary.json k6/scenarios/pdf-upload.js

k6-admin:
	k6 run --summary-export k6/admin-dashboard-summary.json k6/scenarios/admin-dashboard.js

k6-all:
	powershell -ExecutionPolicy Bypass -File k6/run-all.ps1

zap-baseline:
	powershell -ExecutionPolicy Bypass -File deploy/security/run-zap-baseline.ps1 -TargetUrl http://localhost:8088
