#!/bin/bash
set -e

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -v api_password="$API_PASSWORD" \
    -v admin_password="$ADMIN_PASSWORD" \
    -f /docker-entrypoint-initdb.d/scripts/schema.sql

psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
    -f /docker-entrypoint-initdb.d/scripts/seed.sql