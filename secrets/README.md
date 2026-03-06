# Docker Secrets Runbook

This directory is for local secret files only.

The repository keeps this folder but ignores concrete secret values.

## 1. Create local files from templates

Copy files from `secrets/templates/` and fill real values:

```powershell
copy secrets\templates\postgres_password.txt.example secrets\postgres_password.txt
copy secrets\templates\database_url.txt.example secrets\database_url.txt
copy secrets\templates\jwt_key.txt.example secrets\jwt_key.txt
copy secrets\templates\admin_jwt_key.txt.example secrets\admin_jwt_key.txt
copy secrets\templates\internal_secret.txt.example secrets\internal_secret.txt
copy secrets\templates\redis_connection.txt.example secrets\redis_connection.txt
copy secrets\templates\redis_url.txt.example secrets\redis_url.txt
copy secrets\templates\gemini_api_key.txt.example secrets\gemini_api_key.txt
copy secrets\templates\qdrant_api_key.txt.example secrets\qdrant_api_key.txt
```

## 2. Run compose with secret override

```powershell
docker compose -f docker-compose.infra.yml -f docker-compose.secrets.yml up -d
```

For stacks that include the Python app:

```powershell
docker compose -f docker-compose.full.yml -f docker-compose.secrets.yml up -d
```

## 3. Environment conventions

- .NET services read `ConnectionStrings__Default_FILE`, `Jwt__Key_FILE`, `AdminJwt__Key_FILE`,
  `RagCore__InternalSecret_FILE`, `Redis__ConnectionString_FILE`.
- Python service reads `DATABASE_URL_FILE`, `INTERNAL_SECRET_FILE`, `GEMINI_API_KEY_FILE`,
  `QDRANT_API_KEY_FILE`, `REDIS_URL_FILE`.
