CREATE SCHEMA IF NOT EXISTS cricastudio;

CREATE TABLE cricastudio.admin_users (
    id UUID PRIMARY KEY,
    email VARCHAR(320) NOT NULL,
    name VARCHAR(120),
    password_hash VARCHAR(256) NOT NULL,
    password_salt VARCHAR(256) NOT NULL,
    password_iterations INTEGER NOT NULL DEFAULT 210000 CHECK (password_iterations > 0),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT admin_users_email_unique UNIQUE (email),
    CONSTRAINT admin_users_email_normalized CHECK (email = lower(trim(email)))
);

-- Gere hash e salt com scripts/new-admin-user.ps1. Depois execute o INSERT exibido.
