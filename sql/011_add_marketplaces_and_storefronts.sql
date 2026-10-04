BEGIN;
CREATE TABLE IF NOT EXISTS cricastudio.marketplaces (
    id UUID PRIMARY KEY,
    name VARCHAR(120) NOT NULL CHECK (length(trim(name)) > 0),
    code VARCHAR(40) NOT NULL UNIQUE CHECK (code ~ '^[a-z0-9][a-z0-9-]{0,39}$'),
    logo_url TEXT,
    mobile_only BOOLEAN NOT NULL DEFAULT FALSE,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE TABLE IF NOT EXISTS cricastudio.storefronts (
    id UUID PRIMARY KEY,
    marketplace_id UUID NOT NULL REFERENCES cricastudio.marketplaces(id) ON DELETE RESTRICT,
    name VARCHAR(120) NOT NULL CHECK (length(trim(name)) > 0),
    description VARCHAR(500) NOT NULL DEFAULT '',
    url TEXT,
    locale VARCHAR(35) NOT NULL DEFAULT 'pt-BR',
    country_code VARCHAR(2) NOT NULL DEFAULT 'BR' CHECK (country_code = '*' OR country_code ~ '^[A-Z]{2}$'),
    status VARCHAR(16) NOT NULL DEFAULT 'draft' CHECK (status IN ('draft','published','inactive')),
    sort_order INTEGER NOT NULL DEFAULT 0 CHECK (sort_order >= 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS storefronts_public_market_order ON cricastudio.storefronts(locale,country_code,sort_order) WHERE status='published';
CREATE INDEX IF NOT EXISTS storefronts_marketplace ON cricastudio.storefronts(marketplace_id);

-- Preserve the three storefronts previously embedded in the frontend.
INSERT INTO cricastudio.marketplaces(id,name,code,mobile_only) VALUES
('11100000-0000-4000-8000-000000000001','TikTok Shop','tiktok',true),
('11100000-0000-4000-8000-000000000002','Shopee','shopee',false),
('11100000-0000-4000-8000-000000000003','Mercado Livre','mercado',false)
ON CONFLICT DO NOTHING;
INSERT INTO cricastudio.storefronts(id,marketplace_id,name,description,url,locale,country_code,status,sort_order) VALUES
('11200000-0000-4000-8000-000000000001','11100000-0000-4000-8000-000000000001','TikTok Shop','Confira nossos produtos e novidades na TikTok Shop.','https://vt.tiktok.com/ZS9SpchXC1J9N-t7rS7/','pt-BR','BR','published',10),
('11200000-0000-4000-8000-000000000002','11100000-0000-4000-8000-000000000002','Shopee','Encontre nossos produtos e novidades na Shopee.','https://collshp.com/cricastudio?view=storefront','pt-BR','BR','published',20),
('11200000-0000-4000-8000-000000000003','11100000-0000-4000-8000-000000000003','Mercado Livre','Veja a seleção da Crica disponível no Mercado Livre.','https://meli.la/19kz47o','pt-BR','BR','published',30)
ON CONFLICT DO NOTHING;
COMMIT;
