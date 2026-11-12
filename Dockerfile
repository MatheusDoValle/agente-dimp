FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copiar manifestos de dependencias e configuracao central do NuGet
COPY Directory.Build.props Directory.Packages.props NuGet.Config global.json ./
COPY src/Api.Agente/Api.Agente.csproj src/Api.Agente/

# Restaurar pacotes
RUN dotnet restore "src/Api.Agente/Api.Agente.csproj"

# Copiar codigo fonte do servico
COPY src/Api.Agente/ src/Api.Agente/
COPY docs/rules/dimp-v10/catalog.json docs/rules/dimp-v10/manifest.json docs/rules/dimp-v10/

# Compilar e publicar binario otimizado
RUN dotnet publish "src/Api.Agente/Api.Agente.csproj" -c Release -o /app/publish --no-restore

# Imagem de runtime enxuta para producao
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.Agente.dll"]
