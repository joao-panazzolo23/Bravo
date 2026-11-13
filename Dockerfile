# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .

# Skip restore, publish already handles it
RUN dotnet publish "src/Presentation/Bravo.Presentation/Bravo.Presentation.csproj" -c Release -o /app/publish

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
EXPOSE 80
EXPOSE 443

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Bravo.Presentation.dll"]

# **What each part does:**
# - `FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build` → Starts with SDK image, names it "build"
# - `COPY ["Bravo.csproj", "."]` → Copies your project file
# - `RUN dotnet restore` → Downloads NuGet packages
# - `COPY . .` → Copies all your source code
# - `RUN dotnet publish` → Compiles everything to `/app/publish`
# - `EXPOSE 80/443` → Documents which ports (HTTP/HTTPS) your app uses
# - `COPY --from=build` → Takes compiled files from "build" stage into final image
# - `ENTRYPOINT` → Command to run when container starts




##### USEFUL COMMANDS ####

# Build image
#docker build -t my-app:1.0 .

# Run container
#docker run -p 8080:80 my-app:1.0

# See running containers
#docker ps

# Stop a container
#docker stop <container_id>

# Remove an image
#docker rmi my-app:1.0

# View logs
#docker logs <container_id>

# Execute command in running container
#docker exec -it <container_id> /bin/bash

