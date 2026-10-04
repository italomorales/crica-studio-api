# Traduções do catálogo

O português continua nos campos originais. As traduções usam o mesmo ID do cadastro, sem duplicar tipos, temas, fornecedores ou plataformas. Fotos, preços, slugs, links, país e vínculos não são traduzidos.

## Banco e execução local

Aplique `sql/014_add_catalog_translations.sql` depois das migrações anteriores. Ela cria o cadastro de idiomas e as tabelas de tradução de tipos, temas, fornecedores e plataformas. Português, inglês e espanhol são cadastrados inicialmente. A migração pode ser reaplicada sem apagar traduções.

Para testar em paralelo com outra API local, compile em Release e execute na porta 5031:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project CricaStudio.Api/CricaStudio.Api.csproj -c Release --urls http://localhost:5031
```

No front, `.env.local` pode conter `CRICA_API_PROXY=http://localhost:5031`. Esse arquivo é ignorado pelo Git. O proxy padrão continua na porta 5030.

Aplique também `sql/015_retire_shop_product_translations.sql` em ambientes que receberam a versão anterior. Ela preserva a tabela antiga como arquivo histórico, sem leitura ou gravação pela aplicação e sem bloquear a exclusão de idiomas por esses vínculos antigos. Não apaga os produtos nem os textos históricos.

## Administração

Em **Idiomas**, cadastre o código, os nomes, a ordem e a ativação. O código permanece estável. Idiomas inativos podem ser preparados no seletor de idioma dos cadastros, mas não aparecem no seletor público. Desativação preserva todas as traduções; português permanece ativo.

Os textos da interface são mantidos exclusivamente no código do front, em `src/app/services/locales/en.ts` e `es.ts`, registrados em `src/app/services/translations.ts`. Para um novo idioma, a IA cria o dicionário, ajusta as telas e executa os testes antes de ativá-lo no cadastro de idiomas. A API gerencia apenas os dados administrativos dos idiomas. Não armazena, entrega ou valida textos da interface. Veja `front/docs/interface-languages.md` para o fluxo de inclusão.

Nos tipos, temas, fornecedores e plataformas, selecione o idioma no topo e preencha os mesmos campos e selecione **Revisada** depois de conferir. Alterar um texto volta o status para rascunho. Campos vazios de uma tradução revisada usam o português individualmente. Produtos da loja usam sempre seus textos originais, incluindo características, personalização, busca, compartilhamento e SEO. Seus tipos e temas continuam traduzíveis.

## Contrato da API

- `GET /api/catalog/languages`: idiomas ativos, nomes, bandeiras e ordem.
- `GET/POST /api/admin/catalog/languages` e `PUT /api/admin/catalog/languages/{code}`: gerenciamento autenticado.
- `DELETE /api/admin/catalog/languages/{code}`: exclui idiomas sem traduções vinculadas. Português é protegido; idiomas com traduções podem ser desativados ou excluídos depois de remover os vínculos.
- As leituras públicas dos cadastros traduzíveis aceitam `lang=en`, `lang=es` ou outro código ativo. `market=br|international` mantém o filtro de mercado independente do idioma.
- O retorno público inclui `original` e apenas traduções revisadas de idiomas ativos. Isso permite trocar o idioma sem perder o português ou o ID dos filtros. Fornecedores também incluem `platformContent` para a apresentação traduzida da plataforma.
- O admin recebe todos os estados de tradução. Escritas de catálogo aceitam `translations`, por exemplo:

```json
{
  "en": { "status": "reviewed", "fields": { "name": "Mug" } },
  "es": { "status": "draft", "fields": { "name": "Taza" } }
}
```

Nos cadastros traduzíveis, omitir `translations` ou enviar `null` preserva o existente. Enviar `{}` remove as traduções do item. Um mapa fornecido substitui as traduções do item. Os campos aceitos variam por cadastro, e as escritas do conteúdo original e das traduções são atômicas.

## Validação

`dotnet test Tests/CricaStudio.Application.Tests/CricaStudio.Application.Tests.csproj -c Release` executa os testes unitários. Para testes de persistência, configure `CRICA_PLATFORMS_TEST_DATABASE` com um banco local com a migração 014 aplicada. Os novos testes usam transações revertidas. O teste legado de temas tem variável própria e exige seu banco descartável separado.

O front usa `npm test` e `npm run build`. A revisão visual deve conferir o seletor, os filtros por ID, a página de produto, o fallback para português e os estados das traduções no seletor do admin.

A migração `016_retire_interface_texts.sql` marca a coluna antiga como histórico, quando existente. Os valores antigos permanecem preservados, mas não participam da interface nem são alterados ao salvar um idioma. Instalações novas não criam essa coluna.
