BEGIN;
CREATE TABLE IF NOT EXISTS cricastudio.blog_categories (
    id uuid PRIMARY KEY,
    name varchar(120) NOT NULL CHECK (length(trim(name)) > 0),
    slug varchar(140) NOT NULL UNIQUE CHECK (slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
    translations jsonb NOT NULL DEFAULT '{}' CHECK (jsonb_typeof(translations) = 'object'),
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamptz NOT NULL DEFAULT current_timestamp
);
CREATE UNIQUE INDEX IF NOT EXISTS blog_categories_name_unique ON cricastudio.blog_categories(lower(trim(name)));
CREATE TABLE IF NOT EXISTS cricastudio.blog_posts (
    id uuid PRIMARY KEY,
    category_id uuid NOT NULL REFERENCES cricastudio.blog_categories(id) ON DELETE RESTRICT,
    cover_url text NOT NULL DEFAULT '',
    author varchar(120) NOT NULL DEFAULT '',
    display_date date NOT NULL DEFAULT current_date,
    is_featured boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT current_timestamp,
    updated_at timestamptz NOT NULL DEFAULT current_timestamp
);
CREATE TABLE IF NOT EXISTS cricastudio.blog_versions (
    post_id uuid NOT NULL REFERENCES cricastudio.blog_posts(id) ON DELETE CASCADE,
    language_code varchar(35) NOT NULL REFERENCES cricastudio.catalog_languages(code) ON DELETE RESTRICT,
    title varchar(180) NOT NULL DEFAULT '',
    summary varchar(400) NOT NULL DEFAULT '',
    slug varchar(200) NOT NULL DEFAULT '' CHECK (slug = '' OR slug ~ '^[a-z0-9]+(-[a-z0-9]+)*$'),
    document jsonb NOT NULL DEFAULT '{"type":"doc","content":[]}' CHECK (jsonb_typeof(document) = 'object'),
    seo_title varchar(100) NOT NULL DEFAULT '',
    seo_description varchar(180) NOT NULL DEFAULT '',
    status varchar(16) NOT NULL DEFAULT 'draft' CHECK (status IN ('draft','published','scheduled','archived')),
    publish_at timestamptz,
    published_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT current_timestamp,
    updated_at timestamptz NOT NULL DEFAULT current_timestamp,
    search_document tsvector GENERATED ALWAYS AS (to_tsvector('simple',title || ' ' || summary)) STORED,
    PRIMARY KEY(post_id,language_code),
    CHECK (status <> 'scheduled' OR publish_at IS NOT NULL)
);
CREATE UNIQUE INDEX IF NOT EXISTS blog_versions_slug_unique ON cricastudio.blog_versions(language_code,lower(slug)) WHERE slug <> '';
CREATE INDEX IF NOT EXISTS blog_versions_publication ON cricastudio.blog_versions(language_code,status,publish_at,published_at DESC,post_id);
CREATE INDEX IF NOT EXISTS blog_versions_search ON cricastudio.blog_versions USING gin(search_document);
CREATE INDEX IF NOT EXISTS blog_posts_category ON cricastudio.blog_posts(category_id,id);
CREATE TABLE IF NOT EXISTS cricastudio.blog_images (
    id uuid PRIMARY KEY,
    url text NOT NULL UNIQUE,
    uploaded_by uuid,
    created_at timestamptz NOT NULL DEFAULT current_timestamp
);
-- No demo articles, categories or products are inserted by this migration.
COMMIT;
