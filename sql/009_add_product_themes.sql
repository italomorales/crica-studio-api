BEGIN;

CREATE TABLE IF NOT EXISTS cricastudio.catalog_themes (
    id UUID PRIMARY KEY,
    name VARCHAR(120) NOT NULL CHECK (length(trim(name)) > 0),
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE UNIQUE INDEX IF NOT EXISTS catalog_themes_name_normalized_unique
    ON cricastudio.catalog_themes (lower(trim(name)));

CREATE TABLE IF NOT EXISTS cricastudio.shop_product_themes (
    product_id UUID NOT NULL REFERENCES cricastudio.shop_products(id) ON DELETE CASCADE,
    theme_id UUID NOT NULL REFERENCES cricastudio.catalog_themes(id) ON DELETE RESTRICT,
    PRIMARY KEY (product_id, theme_id)
);
CREATE INDEX IF NOT EXISTS shop_product_themes_theme_index
    ON cricastudio.shop_product_themes (theme_id, product_id);

DROP TRIGGER IF EXISTS catalog_themes_set_updated_at ON cricastudio.catalog_themes;
CREATE TRIGGER catalog_themes_set_updated_at
BEFORE UPDATE ON cricastudio.catalog_themes
FOR EACH ROW EXECUTE FUNCTION cricastudio.set_updated_at();

COMMIT;
