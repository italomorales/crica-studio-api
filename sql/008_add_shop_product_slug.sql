BEGIN;

ALTER TABLE cricastudio.shop_products ADD COLUMN IF NOT EXISTS slug VARCHAR(220);

UPDATE cricastudio.shop_products
SET slug = trim(both '-' FROM regexp_replace(
    translate(lower(name), 'áàãâäéèêëíìîïóòõôöúùûüç', 'aaaaaeeeeiiiiooooouuuuc'),
    '[^a-z0-9]+', '-', 'g'))
WHERE slug IS NULL OR slug = '';

UPDATE cricastudio.shop_products AS product
SET slug = product.slug || '-' || left(replace(product.id::text, '-', ''), 8)
WHERE EXISTS (
    SELECT 1 FROM cricastudio.shop_products AS duplicate
    WHERE duplicate.slug = product.slug AND duplicate.id < product.id
);

ALTER TABLE cricastudio.shop_products ALTER COLUMN slug SET NOT NULL;
ALTER TABLE cricastudio.shop_products ADD CONSTRAINT shop_products_slug_not_blank CHECK (length(trim(slug)) > 0);
CREATE UNIQUE INDEX IF NOT EXISTS shop_products_slug_unique ON cricastudio.shop_products (lower(slug));
CREATE INDEX IF NOT EXISTS shop_products_published_slug_index ON cricastudio.shop_products (slug) WHERE status = 'published';

COMMIT;
