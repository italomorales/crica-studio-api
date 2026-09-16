BEGIN;

-- Carga inicial do catálogo demonstrativo. As chaves são fixas para que o script
-- possa ser executado novamente sem alterar cadastros já existentes.
INSERT INTO cricastudio.catalog_types (id, name, scope, is_active)
VALUES
    ('10000000-0000-0000-0000-000000000001', 'Caneca', 'both', TRUE),
    ('10000000-0000-0000-0000-000000000002', 'Botton', 'both', TRUE),
    ('10000000-0000-0000-0000-000000000003', 'Máquina de bottons', 'suppliers', TRUE),
    ('10000000-0000-0000-0000-000000000004', 'Embalagem', 'suppliers', TRUE),
    ('10000000-0000-0000-0000-000000000005', 'Acessório', 'suppliers', TRUE),
    ('10000000-0000-0000-0000-000000000006', 'Quadro', 'shop', FALSE)
ON CONFLICT DO NOTHING;

INSERT INTO cricastudio.shop_products
    (id, type_id, name, description, full_description, price_mode, is_demo, characteristics, personalization, status, sort_order)
VALUES
    ('20000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'Caneca branca personalizada', 'Uma tela em branco para sua frase, ilustração ou memória favorita.', 'Uma tela em branco para sua frase, ilustração ou memória favorita.', 'consult', TRUE, '["Modelo de demonstração: caneca branca"]', '["Nome ou frase", "Ilustração ou fotografia", "Arte enviada por você"]', 'published', 10),
    ('20000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000001', 'Caneca com alça e interior coloridos', 'Um toque de cor para dar ainda mais personalidade à sua ideia.', 'Um toque de cor para dar ainda mais personalidade à sua ideia.', 'consult', TRUE, '["Modelo de demonstração: alça e interior coloridos", "Cores disponíveis a confirmar no atendimento"]', '["Nome ou mensagem", "Estampa temática", "Combinação de cores a consultar"]', 'published', 20),
    ('20000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000001', 'Caneca com colher', 'Sua arte favorita em um modelo com espaço para a colher.', 'Sua arte favorita em um modelo com espaço para a colher.', 'consult', TRUE, '["Modelo de demonstração: alça com suporte para colher"]', '["Frase especial", "Nome", "Ilustração temática"]', 'published', 30),
    ('20000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000001', 'Caneca para empresas e eventos', 'A identidade da sua marca ou a lembrança de um encontro especial.', 'A identidade da sua marca ou a lembrança de um encontro especial.', 'consult', TRUE, '["Modelo de demonstração para projetos corporativos e eventos"]', '["Marca da empresa", "Identidade do evento", "Nomes individuais"]', 'published', 40),
    ('20000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000002', 'Botton com alfinete 32 mm', 'Uma pequena forma de levar sua ideia com você.', 'Uma pequena forma de levar sua ideia com você.', 'consult', TRUE, '["Modelo de demonstração: alfinete, 32 mm"]', '["Ilustração", "Nome ou frase curta", "Marca"]', 'published', 50),
    ('20000000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000002', 'Botton com alfinete 44 mm', 'Mais espaço para sua mensagem, sua causa ou sua criação.', 'Mais espaço para sua mensagem, sua causa ou sua criação.', 'consult', TRUE, '["Modelo de demonstração: alfinete, 44 mm"]', '["Arte temática", "Frase", "Identidade de evento"]', 'published', 60),
    ('20000000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000002', 'Botton chaveiro', 'Personalidade para acompanhar as chaves no dia a dia.', 'Personalidade para acompanhar as chaves no dia a dia.', 'consult', TRUE, '["Modelo de demonstração: botton com argola", "Tamanho a combinar no atendimento"]', '["Nome", "Arte autoral", "Marca ou evento"]', 'published', 70),
    ('20000000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000002', 'Botton abridor 58 mm', 'Uma lembrança com a sua arte e uma função a mais.', 'Uma lembrança com a sua arte e uma função a mais.', 'consult', TRUE, '["Modelo de demonstração: abridor, 58 mm"]', '["Estampa temática", "Identidade de festa", "Marca"]', 'published', 80),
    ('20000000-0000-0000-0000-000000000009', '10000000-0000-0000-0000-000000000001', 'Caneca para aniversário', 'Um exemplo de cadastro em preparação para uma festa.', 'Rascunho demonstrativo para experimentar a edição antes de publicar.', 'consult', TRUE, '["Modelo de demonstração: caneca branca"]', '["Nome ou frase", "Ilustração ou fotografia", "Arte enviada por você"]', 'draft', 90),
    ('20000000-0000-0000-0000-000000000010', '10000000-0000-0000-0000-000000000002', 'Botton para encontro criativo', 'Exemplo de um item que saiu do catálogo e pode ser reativado.', 'Uma pequena forma de levar sua ideia com você.', 'consult', TRUE, '["Modelo de demonstração: alfinete, 32 mm"]', '["Ilustração", "Nome ou frase curta", "Marca"]', 'inactive', 100)
ON CONFLICT DO NOTHING;

INSERT INTO cricastudio.shop_product_images (id, product_id, image_url, sort_order)
VALUES
    ('30000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000001', '/assets/product-1.webp', 10),
    ('30000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000002', '/assets/product-2.webp', 10),
    ('30000000-0000-0000-0000-000000000003', '20000000-0000-0000-0000-000000000003', '/assets/product-3.webp', 10),
    ('30000000-0000-0000-0000-000000000004', '20000000-0000-0000-0000-000000000004', '/assets/product-4.webp', 10),
    ('30000000-0000-0000-0000-000000000005', '20000000-0000-0000-0000-000000000005', '/assets/product-5.webp', 10),
    ('30000000-0000-0000-0000-000000000006', '20000000-0000-0000-0000-000000000006', '/assets/product-6.webp', 10),
    ('30000000-0000-0000-0000-000000000007', '20000000-0000-0000-0000-000000000007', '/assets/product-7.webp', 10),
    ('30000000-0000-0000-0000-000000000008', '20000000-0000-0000-0000-000000000008', '/assets/product-8.webp', 10),
    ('30000000-0000-0000-0000-000000000009', '20000000-0000-0000-0000-000000000009', '/assets/product-1.webp', 10),
    ('30000000-0000-0000-0000-000000000010', '20000000-0000-0000-0000-000000000010', '/assets/product-5.webp', 10)
ON CONFLICT DO NOTHING;

INSERT INTO cricastudio.affiliate_products
    (id, type_id, name, description, platform, is_demo_listing, status, sort_order)
VALUES
    ('40000000-0000-0000-0000-000000000001', '10000000-0000-0000-0000-000000000001', 'Canecas para personalização', 'Modelos em branco para explorar projetos de estamparia.', 'Shopee', TRUE, 'published', 10),
    ('40000000-0000-0000-0000-000000000002', '10000000-0000-0000-0000-000000000002', 'Componentes para bottons', 'Peças para compor bottons e experimentar diferentes modelos.', 'Mercado Livre', TRUE, 'published', 20),
    ('40000000-0000-0000-0000-000000000003', '10000000-0000-0000-0000-000000000004', 'Caixas para canecas', 'Embalagens para organizar a apresentação dos seus projetos.', 'Shopee', TRUE, 'published', 30),
    ('40000000-0000-0000-0000-000000000004', '10000000-0000-0000-0000-000000000005', 'Fita para sublimação', 'Acessório para auxiliar no posicionamento da arte durante o trabalho.', 'TikTok Shop', TRUE, 'published', 40),
    ('40000000-0000-0000-0000-000000000005', '10000000-0000-0000-0000-000000000002', 'Argolas para chaveiros', 'Componentes para montar peças e lembranças personalizadas.', 'Mercado Livre', TRUE, 'published', 50),
    ('40000000-0000-0000-0000-000000000006', '10000000-0000-0000-0000-000000000004', 'Embalagens para bottons', 'Opções para separar e apresentar pequenas peças.', 'TikTok Shop', TRUE, 'published', 60),
    ('40000000-0000-0000-0000-000000000007', '10000000-0000-0000-0000-000000000003', 'Máquina de bottons com matrizes', 'Exemplo de indicação para começar a explorar a produção de bottons.', 'Shopee', TRUE, 'draft', 70),
    ('40000000-0000-0000-0000-000000000008', '10000000-0000-0000-0000-000000000003', 'Máquina de bottons com matrizes', 'O mesmo produto, cadastrado separadamente para outra plataforma.', 'AliExpress', TRUE, 'draft', 80)
ON CONFLICT DO NOTHING;

COMMIT;
