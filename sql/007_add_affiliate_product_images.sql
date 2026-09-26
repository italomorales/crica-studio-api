BEGIN;
ALTER TABLE cricastudio.affiliate_products ADD COLUMN IF NOT EXISTS images jsonb NOT NULL DEFAULT '[]'::jsonb;
UPDATE cricastudio.affiliate_products SET images = jsonb_build_array(image_url) WHERE image_url IS NOT NULL AND images = '[]'::jsonb;
COMMIT;
