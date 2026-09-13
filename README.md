# Crica Studio API

API mínima em ASP.NET Core (.NET 10).

## Executar localmente

Pré-requisito: SDK .NET 10.

Na pasta `backend`, execute:

```powershell
dotnet run
```

A API estará disponível em `http://localhost:5030`.

## Health check

```powershell
curl.exe -i http://localhost:5030/health
```

Resposta esperada: HTTP `200 OK`, com o texto `Healthy`.

O endpoint verifica se a aplicação está respondendo. Ainda não há verificações de banco de dados ou serviços externos.

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
