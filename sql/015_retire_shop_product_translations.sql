BEGIN;
-- Previous local versions created this table. Preserve its contents as an
-- unused archive, without blocking management of the active language catalog.
DO $$ BEGIN
    IF to_regclass('cricastudio.product_translations') IS NOT NULL THEN
        ALTER TABLE cricastudio.product_translations DROP CONSTRAINT IF EXISTS product_translations_language_fk;
        COMMENT ON TABLE cricastudio.product_translations IS 'Legacy shop-product translations archive; no longer read or written by the application.';
    END IF;
END $$;
COMMIT;
