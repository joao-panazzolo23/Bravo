.PHONY: migration migration-list db-update run watch-run open-port \
	docker-build docker-up docker-up-build docker-down docker-down-v docker-restart docker-logs docker-ps docker-db

INFRA := src/Infrastructure/Bravo.Infrastructure.PostgreSQL/Bravo.Infrastructure.PostgreSQL.csproj
API := src/Presentation/Bravo.Presentation/Bravo.Presentation.csproj

# ---------- EF Core ----------
migration:
	@test -n "$(name)" || (echo "Usage: make migration name=MigrationName" && exit 1)
	dotnet ef migrations add $(name) --project $(INFRA) --startup-project $(API)

db-update:
	dotnet ef database update --project $(INFRA) --startup-project $(API)

migration-list:
	dotnet ef migrations list --project $(INFRA) --startup-project $(API)

# ---------- Run ----------
run:
	dotnet run --environment Development --project $(API)

watch-run:
	dotnet watch run --environment Development --project $(API)

open-port:
	dotnet watch run --no-launch-profile --environment Development --urls "http://0.0.0.0:5150" --project $(API)

# ---------- Docker ----------
docker-build:
	docker compose build

docker-up:
	docker compose up -d

docker-up-build:
	docker compose up -d --build

docker-down:
	docker compose down

docker-down-v:
	docker compose down -v

docker-restart:
	docker compose restart

docker-logs:
	docker compose logs -f

docker-ps:
	docker compose ps

docker-db:
	docker compose up -d postgres

aspire:
	npx -y @microsoft/aspire-cli dashboard run
