#!/usr/bin/env sh
set -eu

cd "$(dirname "$0")/.."
docker compose --profile certbot run --rm certbot renew
docker compose restart nginx