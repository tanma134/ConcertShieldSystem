#!/usr/bin/env bash
set -Eeuo pipefail

shopt -s nullglob
dumps=(/docker-import/*.dump)

if ((${#dumps[@]} == 0)); then
    echo "No custom-format database dumps found in /docker-import." >&2
    exit 1
fi

for dump in "${dumps[@]}"; do
    database="${dump##*/}"
    database="${database%.dump}"

    if [[ ! "$database" =~ ^[A-Za-z0-9_]+$ ]]; then
        echo "Invalid database name in dump filename: $database" >&2
        exit 1
    fi

    exists="$(psql -v ON_ERROR_STOP=1 -d postgres -tAc \
        "SELECT 1 FROM pg_database WHERE datname = '$database'")"
    if [[ "$exists" != "1" ]]; then
        createdb --template=template0 "$database"
    fi

    echo "Restoring $database from $(basename "$dump")"
    pg_restore \
        --exit-on-error \
        --clean \
        --if-exists \
        --no-owner \
        --no-acl \
        --dbname="$database" \
        "$dump"
done
