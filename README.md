# Crica Studio API

API em ASP.NET Core (.NET 10), organizada em Api, Application, Domain e Infrastructure.

## Executar localmente

Pré-requisito: SDK .NET 10.

Na pasta `backend/CricaStudio.Api`, execute:

```powershell
dotnet run
```

A API estará disponível em `http://localhost:5030`.

## Health checks

```powershell
curl.exe -i http://localhost:5030/health
```

Resposta esperada: HTTP `200 OK`, com o texto `Healthy`.

O endpoint verifica se a aplicação está respondendo.

Para executar localmente com `dotnet run`, copie o arquivo de exemplo e preencha a string do RDS PostgreSQL:

```bash
cp appsettings.Local.example.json appsettings.Local.json
```

O `appsettings.Local.json` é carregado apenas em Development e já está no `.gitignore`.

O arquivo local também precisa de `Jwt:Key` com ao menos 32 caracteres. Produção recebe a
chave por `JWT_KEY`; ela não deve ser adicionada ao repositório.

Em seguida, consulte:

```powershell
curl.exe -i http://localhost:5030/health/database
```

O endpoint abre uma conexão autenticada e executa `SELECT CURRENT_DATE`. Ele retorna HTTP `200` com a data retornada pelo PostgreSQL, ou HTTP `503` se a connection string não estiver configurada ou a consulta falhar.

## Login administrativo

Execute manualmente o script [sql/001_create_admin_users.sql](sql/001_create_admin_users.sql) em cada banco. Ele cria o schema PostgreSQL `cricastudio` e a tabela `cricastudio.admin_users`.
Para criar um usuário, o script abaixo imprime o `INSERT` com PBKDF2-SHA512, salt aleatório e
210 mil iterações; execute o SQL resultante no banco desejado:

```powershell
.\scripts\new-admin-user.ps1 -Email "admin@exemplo.com" -Name "Admin" -Password "uma senha forte"
```

O endpoint `POST /api/admin/auth/login` recebe `email` e `password`, e retorna um JWT de oito
horas. `GET /api/admin/auth/me` exige o header `Authorization: Bearer <token>`.

## Executar com Docker

Com o Docker em execução, execute na pasta `backend`:

```powershell
docker build --pull -t crica-studio-api:local .
docker run --detach --name crica-studio-api-local -p 127.0.0.1:8080:8080 crica-studio-api:local
curl.exe -i http://localhost:8080/health
```

Resposta esperada: HTTP `200 OK`, com o texto `Healthy`.
O mapeamento disponibiliza a API apenas na máquina local, na porta 8080.

Para visualizar os logs:

```powershell
docker logs crica-studio-api-local
```

Para encerrar e remover o container local:

```powershell
docker stop crica-studio-api-local
docker rm crica-studio-api-local
```

A imagem permanece disponível para executar novamente. O Dockerfile usa duas etapas: o SDK compila a API e a imagem final contém o runtime ASP.NET Core e a aplicação publicada, executada como usuário sem privilégios de administrador.

## Executar no servidor

O arquivo `docker-compose.yml` foi preparado para o servidor Linux. Ele publica a API na porta HTTP padrão, expondo o endpoint em `http://IP_DO_SERVIDOR/health`.

```bash
docker compose up --build --detach
```

O container se chama `crica-studio-api` e usa a política `unless-stopped`, iniciando automaticamente após uma reinicialização do servidor, exceto se tiver sido parado manualmente.

## Deploy automático

O workflow `backend/.github/workflows/deploy.yml` executa a cada push para a branch `main` e
encaminha as credenciais ao Lightsail apenas durante a execução do `docker compose`; nenhum
arquivo de segredo é criado no servidor.

Configure estes secrets e variables no repositório GitHub da API:

| Tipo | Nome | Conteúdo |
| --- | --- |
| Secret | `DATABASE_CONNECTION_STRING` | String de conexão completa do PostgreSQL |
| Secret | `JWT_KEY` | Chave aleatória com pelo menos 32 caracteres para assinatura dos tokens |
| Secret | `AWS_ACCESS_KEY_ID` | Access key ID do usuário IAM `crica-studio-api-media` |
| Secret | `AWS_SECRET_ACCESS_KEY` | Secret access key correspondente; nunca deve ir ao repositório |
| Variable | `CATALOG_MEDIA_BUCKET` | `cricastudio.com` |
| Variable | `CATALOG_MEDIA_REGION` | `sa-east-1` |
| Variable | `CATALOG_MEDIA_PUBLIC_BASE_URL` | `https://cricastudio.com` |
