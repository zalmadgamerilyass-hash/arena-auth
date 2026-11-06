# Stage 1: Build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project definition and restore dependencies
COPY ["BlackoutAuth.Server/BlackoutAuth.Server.csproj", "BlackoutAuth.Server/"]
RUN dotnet restore "BlackoutAuth.Server/BlackoutAuth.Server.csproj"

# Copy source code and publish
COPY BlackoutAuth.Server/ BlackoutAuth.Server/
WORKDIR "/src/BlackoutAuth.Server"
RUN dotnet publish "BlackoutAuth.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV PORT=5000
EXPOSE 5000

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "BlackoutAuth.Server.dll"]
