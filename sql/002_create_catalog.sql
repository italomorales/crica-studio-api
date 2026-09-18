BEGIN;

CREATE SCHEMA IF NOT EXISTS cricastudio;

CREATE OR REPLACE FUNCTION cricastudio.set_updated_at()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$;

CREATE TABLE IF NOT EXISTS cricastudio.catalog_types (
    id UUID PRIMARY KEY,
    name VARCHAR(120) NOT NULL,
    scope VARCHAR(16) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT catalog_types_name_not_blank CHECK (length(trim(name)) > 0),
    CONSTRAINT catalog_types_scope_valid CHECK (scope IN ('shop', 'suppliers', 'both'))
);

CREATE UNIQUE INDEX IF NOT EXISTS catalog_types_name_normalized_unique
    ON cricastudio.catalog_types (lower(trim(name)));

CREATE TABLE IF NOT EXISTS cricastudio.shop_products (
    id UUID PRIMARY KEY,
    type_id UUID NOT NULL REFERENCES cricastudio.catalog_types(id) ON DELETE RESTRICT,
    name VARCHAR(180) NOT NULL,
    description VARCHAR(500) NOT NULL,
    full_description TEXT,
    price_mode VARCHAR(16) NOT NULL DEFAULT 'consult',
    price NUMERIC(12, 2),
    is_demo BOOLEAN NOT NULL DEFAULT FALSE,
    is_featured BOOLEAN NOT NULL DEFAULT FALSE,
    characteristics JSONB NOT NULL DEFAULT '[]'::jsonb,
    personalization JSONB NOT NULL DEFAULT '[]'::jsonb,
    status VARCHAR(16) NOT NULL DEFAULT 'draft',
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT shop_products_name_not_blank CHECK (length(trim(name)) > 0),
    CONSTRAINT shop_products_description_not_blank CHECK (length(trim(description)) > 0),
    CONSTRAINT shop_products_price_mode_valid CHECK (price_mode IN ('consult', 'fixed', 'from')),
    CONSTRAINT shop_products_price_valid CHECK (
        (price_mode = 'consult' AND price IS NULL)
        OR (price_mode IN ('fixed', 'from') AND price > 0)
    ),
    CONSTRAINT shop_products_status_valid CHECK (status IN ('draft', 'published', 'inactive')),
    CONSTRAINT shop_products_sort_order_valid CHECK (sort_order >= 0),
    CONSTRAINT shop_products_characteristics_array CHECK (jsonb_typeof(characteristics) = 'array'),
    CONSTRAINT shop_products_personalization_array CHECK (jsonb_typeof(personalization) = 'array')
);

CREATE INDEX IF NOT EXISTS shop_products_public_order_index
    ON cricastudio.shop_products (status, sort_order, created_at)
    WHERE status = 'published';

CREATE TABLE IF NOT EXISTS cricastudio.shop_product_images (
    id UUID PRIMARY KEY,
    product_id UUID NOT NULL REFERENCES cricastudio.shop_products(id) ON DELETE CASCADE,
    image_url TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT shop_product_images_url_not_blank CHECK (length(trim(image_url)) > 0),
    CONSTRAINT shop_product_images_sort_order_valid CHECK (sort_order >= 0)
);

CREATE INDEX IF NOT EXISTS shop_product_images_product_order_index
    ON cricastudio.shop_product_images (product_id, sort_order, created_at);

CREATE TABLE IF NOT EXISTS cricastudio.affiliate_products (
    id UUID PRIMARY KEY,
    type_id UUID NOT NULL REFERENCES cricastudio.catalog_types(id) ON DELETE RESTRICT,
    name VARCHAR(180) NOT NULL,
    description VARCHAR(500) NOT NULL,
    platform VARCHAR(32) NOT NULL,
    image_url TEXT,
    affiliate_url TEXT,
    seller VARCHAR(160),
    is_demo_listing BOOLEAN NOT NULL DEFAULT FALSE,
    is_featured BOOLEAN NOT NULL DEFAULT FALSE,
    status VARCHAR(16) NOT NULL DEFAULT 'draft',
    sort_order INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT affiliate_products_name_not_blank CHECK (length(trim(name)) > 0),
    CONSTRAINT affiliate_products_description_not_blank CHECK (length(trim(description)) > 0),
    CONSTRAINT affiliate_products_platform_valid CHECK (
        platform IN ('Shopee', 'Mercado Livre', 'TikTok Shop', 'AliExpress', 'Outra')
    ),
    CONSTRAINT affiliate_products_status_valid CHECK (status IN ('draft', 'published', 'inactive')),
    CONSTRAINT affiliate_products_sort_order_valid CHECK (sort_order >= 0)
);

CREATE INDEX IF NOT EXISTS affiliate_products_public_order_index
    ON cricastudio.affiliate_products (status, sort_order, created_at)
    WHERE status = 'published';

CREATE TABLE IF NOT EXISTS cricastudio.site_settings (
    key VARCHAR(80) PRIMARY KEY,
    value TEXT NOT NULL,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT site_settings_key_not_blank CHECK (length(trim(key)) > 0)
);

INSERT INTO cricastudio.site_settings (key, value)
VALUES ('whatsapp_number', '')
ON CONFLICT (key) DO NOTHING;

DROP TRIGGER IF EXISTS catalog_types_set_updated_at ON cricastudio.catalog_types;
CREATE TRIGGER catalog_types_set_updated_at
BEFORE UPDATE ON cricastudio.catalog_types
FOR EACH ROW EXECUTE FUNCTION cricastudio.set_updated_at();

DROP TRIGGER IF EXISTS shop_products_set_updated_at ON cricastudio.shop_products;
CREATE TRIGGER shop_products_set_updated_at
BEFORE UPDATE ON cricastudio.shop_products
FOR EACH ROW EXECUTE FUNCTION cricastudio.set_updated_at();

DROP TRIGGER IF EXISTS affiliate_products_set_updated_at ON cricastudio.affiliate_products;
CREATE TRIGGER affiliate_products_set_updated_at
BEFORE UPDATE ON cricastudio.affiliate_products
FOR EACH ROW EXECUTE FUNCTION cricastudio.set_updated_at();

DROP TRIGGER IF EXISTS site_settings_set_updated_at ON cricastudio.site_settings;
CREATE TRIGGER site_settings_set_updated_at
BEFORE UPDATE ON cricastudio.site_settings
FOR EACH ROW EXECUTE FUNCTION cricastudio.set_updated_at();

COMMIT;
