BEGIN;

ALTER TABLE cricastudio.shop_products
    ADD COLUMN IF NOT EXISTS is_featured BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IF NOT EXISTS shop_products_featured_order_index
    ON cricastudio.shop_products (sort_order, created_at)
    WHERE status = 'published' AND is_featured;

COMMIT;
