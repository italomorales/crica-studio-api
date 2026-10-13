BEGIN;
CREATE TABLE IF NOT EXISTS cricastudio.blog_authors (
    id uuid PRIMARY KEY,
    name varchar(120) NOT NULL CHECK(length(trim(name)) > 0),
    photo_url text NOT NULL DEFAULT '',
    bio varchar(600) NOT NULL DEFAULT '',
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT current_timestamp,
    updated_at timestamptz NOT NULL DEFAULT current_timestamp
);
CREATE UNIQUE INDEX IF NOT EXISTS blog_authors_name_unique ON cricastudio.blog_authors(lower(trim(name)));
ALTER TABLE cricastudio.blog_posts ADD COLUMN IF NOT EXISTS author_id uuid REFERENCES cricastudio.blog_authors(id) ON DELETE RESTRICT;
CREATE INDEX IF NOT EXISTS blog_posts_author ON cricastudio.blog_posts(author_id);
-- Existing author text is preserved. No demo author profiles are created.
COMMIT;
