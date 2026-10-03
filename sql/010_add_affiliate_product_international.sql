BEGIN;

ALTER TABLE cricastudio.affiliate_products
    ADD COLUMN IF NOT EXISTS is_international boolean NOT NULL DEFAULT false;

COMMIT;
