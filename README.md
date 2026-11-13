# 🏛️ Nomad DDD Template

> **Enterprise-Grade API Template** • Domain-Driven Design • Built with .NET

<div align="center">

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![EF Core](https://img.shields.io/badge/EF%20Core-7.0-512BD4?style=for-the-badge&logo=dotnet)](https://learn.microsoft.com/ef/core/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-Ready-336791?style=for-the-badge&logo=postgresql)](https://www.postgresql.org/)
[![Docker](https://img.shields.io/badge/Docker-Ready-2496ED?style=for-the-badge&logo=docker)](https://www.docker.com/)
[![Status](https://img.shields.io/badge/Status-Production%20Ready-27AE60?style=for-the-badge)](/)

</div>

---

## ✨ About Core Template

A **custom, opinionated API template** for building scalable, maintainable systems using **Domain-Driven Design (DDD)** principles. Built with .NET CLI tooling and designed for enterprise applications that demand clean architecture.

Perfect for teams that want to move fast without sacrificing structure.

---

## 🚀 Quick Start

### 1️⃣ Clone the Repository

```bash
git clone https://github.com/wearenomad/ddd-core-template
cd ddd-core-template
```

### 2️⃣ Install as a .NET Template

Navigate to the parent directory, then run:

```bash
# From one level above the template folder
dotnet new install ./ddd-core-template
```

**Or specify the full path:**
```bash
dotnet new install /path/to/ddd-core-template
```

> 💡 **Tip:** All configuration is handled in `.template.config/template.json` — no manual setup needed.

### 3️⃣ Create Your First Project

```bash
dotnet new nomad-ddd -n "MyAwesomeProject"
```

Your project will be created in the current directory with the structure ready to go.

### 4️⃣ Customize Your Settings

Update the generated project's configuration files:
- `appsettings.json` — Application settings & database connections
- `launchSettings.json` — Launch profiles & environment config

---

## 📦 Removing or Updating the Template

**Uninstall if needed:**
```bash
# By name (will show the actual path if this doesn't work)
dotnet new uninstall nomad-ddd

# Or by path
dotnet new uninstall /path/to/ddd-core-template
```

---

## 🐳 Docker Support

### Pull Latest Images

```bash
docker pull mcr.microsoft.com/dotnet/sdk:10.0
docker pull mcr.microsoft.com/dotnet/aspnet:10.0
```

### Build Your Docker Image

```bash
docker build -t core-template:1.0 .
```

### Run Your Container

```bash
docker run -p 8080:80 core-template:1.0
```

Visit `http://localhost:8080` to see your app in action! 🌐

**Need another instance?** Just change the port:
```bash
docker run -p 9000:80 core-template:1.0
```

### Useful Docker Commands

| Command | Purpose |
|---------|---------|
| `docker ps` | List running containers |
| `docker logs <container_id>` | View container logs |
| `docker stop <container_id>` | Stop a running container |
| `docker rmi core-template:1.0` | Remove the image |

---

## 🗄️ Database Migrations

Manage your Entity Framework Core migrations with ease:

### Step 1️⃣ Navigate to Infrastructure
```bash
cd src/Infrastructure/Bravo.Infrastructure.PostgreSQL/
```

### Step 2️⃣ Create a Migration
```bash
dotnet ef migrations add MigrationName \
  -o Migrations \
  -s ../../Presentation/Bravo.Presentation/Bravo.Presentation.csproj
```

### Step 3️⃣ Apply Migration to Database
```bash
dotnet ef database update \
  -s ../../Presentation/Bravo.Presentation/Bravo.Presentation.csproj
```

### Remove Latest Migration
```bash
dotnet ef migrations remove \
  -s ../../Presentation/Bravo.Presentation/Bravo.Presentation.csproj
```

> 💡 **Pro Tip:** Create a migration script in your project for faster iterations.

---

## 📁 Project Structure

```
ddd-core-template/
├── src/
│   ├── Presentation/
│   │   └── Bravo.Presentation/       # API entry point
│   ├── Application/
│   │   └── Bravo.Application/        # Business logic & use cases
│   ├── Domain/
│   │   ├── Bravo.Domain/             # Domain entities & rules
│   │   └── Bravo.Shared/             # Shared domain models
│   └── Infrastructure/
│       ├── Bravo.Infrastructure.PostgreSQL/    # Database access
│       ├── Bravo.Infrastructure.Dapper/        # Query optimization
│       ├── Bravo.Infrastructure.Auth/          # Authentication
│       └── Bravo.Infrastructure.Email/         # Email service
├── Dockerfile
├── docker-compose.yml
└── .template.config/
    └── template.json                         # Template configuration
```

---

## 🛠️ Tech Stack

| Layer | Technology | Version |
|-------|-----------|---------|
| **Framework** | .NET | 10.0 |
| **ORM** | Entity Framework Core | 7.0 |
| **mORM** | Dapper | 2.1.72 |
| **Database** | PostgreSQL | Latest |
| **Query** | Dapper | Latest |
| **API** | ASP.NET Core | 10.0 |
| **Container** | Docker | Latest |

> 📈 **Framework Versioning:** Updated yearly to stay current with .NET releases.

## 📚 Learn More

- [Domain-Driven Design by Eric Evans](https://www.domainlanguage.com/ddd/)
- [.NET Official Docs](https://dotnet.microsoft.com/learn/dotnet/)
- [Entity Framework Core](https://learn.microsoft.com/ef/core/)
- [PostgreSQL Documentation](https://www.postgresql.org/docs/)






## 📄 To update packages 
Install dotnet update tool:
```bash
dotnet tool install --global dotnet-outdated-tool
```

And update all instantly:
```bash
dotnet outdated --upgrade
```


---

<div align="center">

**Made with ❤️ by the Nomad Squad**


</div>
