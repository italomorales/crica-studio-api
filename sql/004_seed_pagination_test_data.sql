BEGIN;

-- Dados para validar a paginação administrativa.
-- Execute após 002_create_catalog.sql e 003_seed_demo_catalog.sql.
-- Insere 25 produtos e 25 fornecedores: em conjunto com a carga inicial,
-- cada tela administrativa terá mais de uma página de 20 itens.

INSERT INTO cricastudio.shop_products
    (id, type_id, name, description, full_description, price_mode, is_demo,
     characteristics, personalization, status, sort_order)
SELECT
    ('91000000-0000-0000-0000-' || lpad(n::text, 12, '0'))::uuid,
    CASE WHEN n % 2 = 0
        THEN '10000000-0000-0000-0000-000000000002'::uuid
        ELSE '10000000-0000-0000-0000-000000000001'::uuid
    END,
    format('Produto de teste %s', n),
    format('Cadastro de teste número %s para validar a paginação da administração.', n),
    format('Descrição completa do produto de teste número %s.', n),
    'consult',
    FALSE,
    jsonb_build_array(format('Característica de teste %s', n)),
    jsonb_build_array('Nome ou frase', 'Arte personalizada'),
    CASE WHEN n % 10 = 0 THEN 'inactive' WHEN n % 7 = 0 THEN 'draft' ELSE 'published' END,
    1000 + n * 10
FROM generate_series(1, 25) AS series(n)
ON CONFLICT (id) DO NOTHING;

INSERT INTO cricastudio.affiliate_products
    (id, type_id, name, description, platform, is_demo_listing, status, sort_order)
SELECT
    ('92000000-0000-0000-0000-' || lpad(n::text, 12, '0'))::uuid,
    CASE n % 3
        WHEN 0 THEN '10000000-0000-0000-0000-000000000003'::uuid
        WHEN 1 THEN '10000000-0000-0000-0000-000000000004'::uuid
        ELSE '10000000-0000-0000-0000-000000000005'::uuid
    END,
    format('Fornecedor de teste %s', n),
    format('Indicação de teste número %s para validar a paginação da administração.', n),
    (ARRAY['Shopee', 'Mercado Livre', 'TikTok Shop', 'AliExpress', 'Outra'])[(n - 1) % 5 + 1],
    FALSE,
    CASE WHEN n % 10 = 0 THEN 'inactive' WHEN n % 7 = 0 THEN 'draft' ELSE 'published' END,
    1000 + n * 10
FROM generate_series(1, 25) AS series(n)
ON CONFLICT (id) DO NOTHING;

COMMIT;
