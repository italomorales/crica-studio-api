BEGIN;
CREATE TABLE IF NOT EXISTS cricastudio.platforms (
    id UUID PRIMARY KEY,
    name VARCHAR(120) NOT NULL CHECK(length(trim(name))>0),
    code VARCHAR(40) NOT NULL CHECK(code ~ '^[a-z0-9][a-z0-9-]{0,39}$'),
    description VARCHAR(500) NOT NULL DEFAULT '', url TEXT, logo_url TEXT,
    locale VARCHAR(35) NOT NULL DEFAULT 'pt-BR',
    country_code VARCHAR(2) NOT NULL DEFAULT 'BR' CHECK(country_code='*' OR country_code ~ '^[A-Z]{2}$'),
    status VARCHAR(16) NOT NULL DEFAULT 'draft' CHECK(status IN('draft','published','inactive')),
    sort_order INTEGER NOT NULL DEFAULT 0 CHECK(sort_order>=0),
    mobile_only BOOLEAN NOT NULL DEFAULT FALSE, is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(code,locale,country_code)
);
-- Flatten the previous two catalogs without losing localized storefronts or their links.
WITH existing AS (
 SELECT s.*, m.code,m.logo_url,m.mobile_only,m.is_active,
 row_number() OVER(PARTITION BY m.id ORDER BY (s.locale='pt-BR' AND s.country_code='BR') DESC,s.sort_order,s.id) AS position
 FROM cricastudio.storefronts s JOIN cricastudio.marketplaces m ON m.id=s.marketplace_id
)
INSERT INTO cricastudio.platforms(id,name,code,description,url,logo_url,locale,country_code,status,sort_order,mobile_only,is_active)
SELECT CASE WHEN position=1 THEN marketplace_id ELSE id END,name,code,description,url,logo_url,locale,country_code,status,sort_order,mobile_only,is_active FROM existing
ON CONFLICT DO NOTHING;
INSERT INTO cricastudio.platforms(id,name,code,logo_url,mobile_only,is_active)
SELECT m.id,m.name,m.code,m.logo_url,m.mobile_only,m.is_active FROM cricastudio.marketplaces m
WHERE NOT EXISTS(SELECT 1 FROM cricastudio.platforms p WHERE p.id=m.id) ON CONFLICT DO NOTHING;
INSERT INTO cricastudio.platforms(id,name,code,description,status,sort_order) VALUES
('11300000-0000-4000-8000-000000000004','AliExpress','aliexpress','','draft',40),
('11300000-0000-4000-8000-000000000005','Outra','outra','','draft',50)
ON CONFLICT DO NOTHING;
ALTER TABLE cricastudio.affiliate_products ADD COLUMN IF NOT EXISTS platform_id UUID REFERENCES cricastudio.platforms(id) ON DELETE RESTRICT;
ALTER TABLE cricastudio.affiliate_products DROP CONSTRAINT IF EXISTS affiliate_products_platform_valid;
ALTER TABLE cricastudio.affiliate_products ALTER COLUMN platform TYPE VARCHAR(120);
UPDATE cricastudio.affiliate_products a SET platform_id=(
 SELECT p.id FROM cricastudio.platforms p LEFT JOIN cricastudio.marketplaces m ON m.id=p.id
 WHERE p.name=a.platform OR m.name=a.platform
 ORDER BY (p.locale='pt-BR' AND p.country_code='BR') DESC,p.sort_order,p.id LIMIT 1
) WHERE a.platform_id IS NULL;
ALTER TABLE cricastudio.affiliate_products ALTER COLUMN platform_id SET NOT NULL;
UPDATE cricastudio.affiliate_products a SET platform=p.name FROM cricastudio.platforms p WHERE p.id=a.platform_id AND a.platform<>p.name;
CREATE INDEX IF NOT EXISTS affiliate_products_platform_id ON cricastudio.affiliate_products(platform_id);
CREATE INDEX IF NOT EXISTS platforms_market_order ON cricastudio.platforms(locale,country_code,sort_order);
COMMIT;
