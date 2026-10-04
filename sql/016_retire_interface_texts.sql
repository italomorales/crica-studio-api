BEGIN;
-- Preserve previous overrides as unused history; interface copy lives in the front.
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'cricastudio' AND table_name = 'catalog_languages'
          AND column_name = 'interface_texts') THEN
        COMMENT ON COLUMN cricastudio.catalog_languages.interface_texts IS
            'Legacy interface-text overrides archive; no longer read or written by the application.';
    END IF;
END $$;
COMMIT;
