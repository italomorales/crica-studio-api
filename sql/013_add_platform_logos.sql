BEGIN;
UPDATE cricastudio.platforms SET logo_url = CASE code
    WHEN 'shopee' THEN '/assets/construction/shopee.svg'
    WHEN 'mercado' THEN '/assets/construction/mercado-livre.svg'
    WHEN 'tiktok' THEN '/assets/construction/tiktok-shop.png'
    WHEN 'aliexpress' THEN '/assets/platforms/aliexpress.svg'
    WHEN 'amazon' THEN '/assets/platforms/amazon.svg'
    WHEN 'temu' THEN '/assets/platforms/temu.svg'
END
WHERE code IN ('shopee','mercado','tiktok','aliexpress','amazon','temu') AND (logo_url IS NULL OR trim(logo_url)='');
INSERT INTO cricastudio.platforms(id,name,code,logo_url,status,sort_order) VALUES
('11400000-0000-4000-8000-000000000006','Amazon','amazon','/assets/platforms/amazon.svg','draft',60),
('11400000-0000-4000-8000-000000000007','Temu','temu','/assets/platforms/temu.svg','draft',70)
ON CONFLICT DO NOTHING;
COMMIT;
