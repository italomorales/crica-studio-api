BEGIN;
CREATE TABLE IF NOT EXISTS cricastudio.blog_visitors (
 id uuid PRIMARY KEY,
 provider varchar(16) NOT NULL CHECK(provider IN ('google','facebook','test')),
 subject varchar(255) NOT NULL,
 name varchar(120) NOT NULL,
 created_at timestamptz NOT NULL DEFAULT current_timestamp,
 UNIQUE(provider,subject)
);
CREATE TABLE IF NOT EXISTS cricastudio.blog_activity (
 id uuid PRIMARY KEY,
 post_id uuid NOT NULL REFERENCES cricastudio.blog_posts(id) ON DELETE CASCADE,
 language_code varchar(35) NOT NULL REFERENCES cricastudio.catalog_languages(code),
 visitor_id uuid NOT NULL REFERENCES cricastudio.blog_visitors(id),
 kind varchar(16) NOT NULL CHECK(kind IN ('comment','like','share')),
 parent_id uuid REFERENCES cricastudio.blog_activity(id) ON DELETE CASCADE,
 text varchar(1500) NOT NULL DEFAULT '',
 status varchar(16) NOT NULL DEFAULT 'pending' CHECK(status IN ('pending','approved','rejected','spam')),
 created_at timestamptz NOT NULL DEFAULT current_timestamp,
 moderated_at timestamptz,
 moderated_by uuid,
 CHECK(parent_id IS NULL OR kind='comment'),
 CHECK(kind<>'comment' OR length(trim(text))>0)
);
CREATE UNIQUE INDEX IF NOT EXISTS blog_activity_unique_like ON cricastudio.blog_activity(post_id,language_code,visitor_id) WHERE kind='like';
CREATE INDEX IF NOT EXISTS blog_activity_public ON cricastudio.blog_activity(post_id,language_code,status,created_at,id);
CREATE INDEX IF NOT EXISTS blog_activity_moderation ON cricastudio.blog_activity(status,created_at DESC,id);
COMMIT;
