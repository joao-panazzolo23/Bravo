# Clean Architecture Guidelines — Historical Electoral Analysis Platform

## 1. Objective

Before implementing or modifying anything, inspect the existing solution and understand its Clean Architecture structure.

The project already has Entity Framework Core and Dapper implemented. Do not reinstall, replace, or unnecessarily reconfigure either technology.

Your task is to understand and respect the existing architectural boundaries when implementing the electoral and geographic persistence layer.

Do not redesign the entire architecture. Follow the existing conventions unless a concrete architectural problem requires a change.

## 2. Architectural Layers

Respect the following responsibilities:

### Domain

The Domain layer contains the core business model and rules.

It should contain:

- Entities and aggregate roots.
- Value objects.
- Domain enums.
- Domain interfaces when they represent genuine domain abstractions.
- Business rules and invariants.
- Domain events, if applicable.
- Domain services (interfaces) such as repositories. They must never be placed at Application.

It must NOT depend on:

- Entity Framework Core.
- Dapper.
- Npgsql.
- Database connections.
- SQL queries.
- Infrastructure implementations.
- Application or Presentation layers.

The Domain must remain independent of persistence technologies.

### Application

The Application layer coordinates use cases and defines application contracts.

It may contain:

- Commands and Queries.
- Handlers.
- Application services.
- Repository interfaces.
- Read/query interfaces.
- Application-level abstractions.
- Request and response models.
- Validation and orchestration logic.

Application interfaces must describe what the application needs, not expose infrastructure implementation details.

Do not expose DbContext, IDbConnection, NpgsqlConnection, or EF Core-specific types through application contracts.

### Infrastructure

The Infrastructure layer implements technical details and external dependencies.

It should contain:

- EF Core DbContext and entity configurations.
- Repository implementations.
- Dapper query implementations.
- Database connection management.
- SQL queries.
- Npgsql-specific operations.
- Persistence mappings.
- Transaction implementations.
- Database-specific infrastructure.

Infrastructure implements interfaces defined by Application or Domain where appropriate.

It must not contain business rules that belong to Domain.

### Presentation

The Presentation layer exposes the application to external consumers.

It may contain:

- API endpoints.
- Controllers.
- HTTP configuration.
- Authentication and authorization.
- Dependency injection composition.
- Request and response serialization.

Presentation must not directly implement persistence logic or contain SQL queries.

## 3. Dependency Direction

Maintain the Clean Architecture dependency rule:

- Domain must not depend on any outer layer.
- Application may depend on Domain.
- Infrastructure may depend on Application and Domain.
- Presentation may depend on Application and reference Infrastructure only for composition and dependency injection.

The dependency direction must always point inward.

Do not introduce circular dependencies.

## 4. EF Core and Dapper Responsibilities

Both technologies are already implemented in the project.

Use them according to their intended responsibilities:

### Entity Framework Core

Use EF Core for:

- Domain-oriented persistence.
- Aggregate persistence.
- Entity relationships.
- Change tracking when required.
- Migrations.
- Relational configuration.
- Conventional transactional operations.

EF Core configurations and DbContext implementations belong in Infrastructure.

### Dapper

Use Dapper for:

- Analytical queries.
- Historical comparisons.
- Aggregations.
- Rankings.
- Complex SQL queries.
- Lightweight read projections.
- Queries requiring explicit control over SQL execution.

Dapper implementations and SQL queries belong in Infrastructure.

Application should define the contracts required by analytical use cases without depending directly on Dapper.

### Important

Do not force every operation through EF Core.

Do not force every operation through Dapper.

Do not duplicate persistence logic unnecessarily.

Do not create abstractions that hide useful capabilities without a concrete architectural reason.

## 5. Repository and Query Responsibilities

Do not automatically create one repository per entity.

Repositories should represent meaningful aggregate persistence boundaries.

For analytical read operations, use dedicated query interfaces or read abstractions when appropriate.

Do not put complex analytical SQL inside domain entities or aggregate repositories merely to reuse an existing abstraction.

Keep write and read responsibilities clearly separated when this improves maintainability.

Avoid generic repositories that expose unnecessary CRUD operations for every entity.

Do not use try catches. They add no handling in the code at all and hides errors.

Use clean code, KISS, DRY, SOLID as source of truths. They should be part of your coding guidelines on
how code should looks like.

Only one class inside each file. It keeps code traceable and findable.

Using clean architecture, orchestration must be always made at Application.

Domain Services should be interfaces set in domain and implemented at Infrastructure.

Do not use Tuples when using domain services. Create a record to represent much more cohesive code.

Each infrastructure project must have one reason to change.
If you need to make requests to two differente API`s of different contexts,
create another Infrastructure project and make the requests.

Prefer design patterns over tradicional if/else/switch cases.
you can find them all here:
https://refactoring.guru/design-patterns

Classes should be small and concise. They must have maximum one reason to change.

## 6. Dependency Injection

Follow the existing dependency injection conventions.

Infrastructure implementations must be registered through the appropriate composition mechanism.

Application must not instantiate Infrastructure implementations directly.

Do not introduce service locators or manually instantiate database implementations inside handlers or domain entities.

When using DI, prefer primary constructor instead creating properties and large constructors

## Appendix — HttpClient Usage and Dependency Injection

### Core Principle

**Never inject `IHttpClientFactory` directly into application services, API clients, repositories, or other business classes.**

`IHttpClientFactory` must be used exclusively at the dependency composition/registration level to create and configure `HttpClient` instances.

### Mandatory Guidelines

1. **Use Typed or Named HttpClients:** Register HTTP integrations through `AddHttpClient<TClient>()` or named clients when appropriate.
2. **Constructor Injection:** Inject the configured `HttpClient` directly into the class that needs to perform HTTP requests.
3. **No Runtime Factory Dependencies:** Classes must not receive `IHttpClientFactory` just to call `CreateClient()` internally.
4. **Centralized Configuration:** Base addresses, default headers, timeouts, authentication handlers, retry policies, and other HTTP configurations must be defined during DI registration.
5. **Single Responsibility:** HTTP-consuming classes should focus on their integration responsibilities, not on creating or configuring their own dependencies.

### Expected Implementation

**Correct:**

```csharp
// Dependency registration
services.AddHttpClient<ExternalApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["ExternalApi:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(30);
});
```

```csharp
public sealed class ExternalApiClient(HttpClient httpClient)
{
    public async Task<string> GetDataAsync(CancellationToken cancellationToken)
    {
        return await httpClient.GetStringAsync("data", cancellationToken);
    }
}
```

**Incorrect:**

```csharp
public sealed class ExternalApiService(IHttpClientFactory httpClientFactory)
{
    public async Task<string> GetDataAsync()
    {
        var client = httpClientFactory.CreateClient("ExternalApi");

        return await client.GetStringAsync("data");
    }
}
```

### Architectural Rule

The factory belongs to the composition infrastructure, not to the application logic.

**Prefer injecting configured dependencies over injecting mechanisms responsible for constructing those dependencies.**

## 7. Implementation Rules

Before modifying code:

1. Inspect the existing solution structure.
2. Identify the responsibilities of each project.
3. Locate existing EF Core and Dapper implementations.
4. Understand repository and query conventions.
5. Identify the dependency injection registration points.
6. Follow existing naming and coding conventions, avoiding generic names and nomeclature.

When implementing new functionality:

- Place each class in the correct layer.
- Keep domain entities persistence-agnostic.
- Keep SQL and database-specific code in Infrastructure.
- Keep application contracts independent of implementation details.
- Reuse existing database configurations.
- Avoid unnecessary abstractions and duplicated code.
- Preserve existing functionality.
- Do not move files or restructure projects without a concrete reason.
- Do not leave comments on your code

## 8. Final Validation

After implementation:

- Verify that project dependencies respect Clean Architecture.
- Verify that Domain has no Infrastructure dependencies.
- Verify that Application does not expose database-specific types.
- Verify that EF Core and Dapper remain encapsulated in Infrastructure.
- Verify that dependency injection resolves all required implementations.
- Run the solution build and relevant tests.

Report any architectural violations found and explain any necessary corrections.

## 9. Don`t create tests for everything you need.

Testing externals libraries and APIs, such as Entity Framework and IBGE APIs is useless.
Libraries are almost always working fine and the APIs usually dont change often, and when they do, they will have versioning.
Test Domain logic instead.

## 10. Do not modify or create code FOR Entity Framework Core`s MIGRATIONS.

DO NOT, BY ANY MEANS, CREATE A MIGRATION BY HAND. ONLY IF I TOLD YOU TO IGNORE THAT RULE.
Entity Framework already tracks down entities and create Migrations for you.
The only thing you gotta do is create a new DbSet with Entity as T and run

```
make migration name=MigrationName
```

And it generates the Migration code for you.

## Final Principle

**Clean Architecture is about dependency control and separation of responsibilities, not about creating the maximum possible number of interfaces, projects, or abstractions.**

Preserve the existing architecture, respect its boundaries, and use EF Core and Dapper as implementation details of the Infrastructure layer.
