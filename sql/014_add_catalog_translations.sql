BEGIN;
CREATE TABLE IF NOT EXISTS cricastudio.catalog_languages (
    code varchar(35) PRIMARY KEY,
    name varchar(80) NOT NULL CHECK (length(trim(name)) > 0),
    native_name varchar(80) NOT NULL CHECK (length(trim(native_name)) > 0),
    flag_code varchar(2),
    is_active boolean NOT NULL DEFAULT true,
    sort_order integer NOT NULL DEFAULT 0 CHECK (sort_order >= 0),
    CHECK (code ~ '^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$'),
    CHECK (code <> 'pt' OR is_active)
);
INSERT INTO cricastudio.catalog_languages(code,name,native_name,flag_code,sort_order)
VALUES ('pt','Português','Português','BR',0),('en','Inglês','English','US',10),('es','Espanhol','Español','ES',20)
ON CONFLICT (code) DO NOTHING;

-- Separate tables retain real foreign keys and cascade only with their own item.
CREATE TABLE IF NOT EXISTS cricastudio.type_translations (
    entity_id uuid NOT NULL REFERENCES cricastudio.catalog_types(id) ON DELETE CASCADE,
    language_code varchar(35) NOT NULL REFERENCES cricastudio.catalog_languages(code) ON DELETE RESTRICT,
    status varchar(16) NOT NULL CHECK (status IN ('pending','draft','reviewed')),
    fields jsonb NOT NULL CHECK (jsonb_typeof(fields) = 'object'),
    updated_at timestamptz NOT NULL DEFAULT current_timestamp,
    PRIMARY KEY(entity_id,language_code)
);
CREATE TABLE IF NOT EXISTS cricastudio.theme_translations (LIKE cricastudio.type_translations INCLUDING DEFAULTS INCLUDING CONSTRAINTS INCLUDING INDEXES);
ALTER TABLE cricastudio.theme_translations DROP CONSTRAINT IF EXISTS theme_translations_entity_fk;
ALTER TABLE cricastudio.theme_translations ADD CONSTRAINT theme_translations_entity_fk FOREIGN KEY(entity_id) REFERENCES cricastudio.catalog_themes(id) ON DELETE CASCADE;
CREATE TABLE IF NOT EXISTS cricastudio.affiliate_translations (LIKE cricastudio.type_translations INCLUDING DEFAULTS INCLUDING CONSTRAINTS INCLUDING INDEXES);
ALTER TABLE cricastudio.affiliate_translations DROP CONSTRAINT IF EXISTS affiliate_translations_entity_fk;
ALTER TABLE cricastudio.affiliate_translations ADD CONSTRAINT affiliate_translations_entity_fk FOREIGN KEY(entity_id) REFERENCES cricastudio.affiliate_products(id) ON DELETE CASCADE;
CREATE TABLE IF NOT EXISTS cricastudio.platform_translations (LIKE cricastudio.type_translations INCLUDING DEFAULTS INCLUDING CONSTRAINTS INCLUDING INDEXES);
ALTER TABLE cricastudio.platform_translations DROP CONSTRAINT IF EXISTS platform_translations_entity_fk;
ALTER TABLE cricastudio.platform_translations ADD CONSTRAINT platform_translations_entity_fk FOREIGN KEY(entity_id) REFERENCES cricastudio.platforms(id) ON DELETE CASCADE;
-- LIKE deliberately does not copy foreign keys.
DO $$ DECLARE t text; BEGIN
    FOREACH t IN ARRAY ARRAY['theme_translations','affiliate_translations','platform_translations'] LOOP
        IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname=t || '_language_fk' AND conrelid=('cricastudio.' || t)::regclass) THEN
            EXECUTE format('ALTER TABLE cricastudio.%I ADD CONSTRAINT %I FOREIGN KEY(language_code) REFERENCES cricastudio.catalog_languages(code) ON DELETE RESTRICT',t,t || '_language_fk');
        END IF;
    END LOOP;
END $$;
COMMIT;
