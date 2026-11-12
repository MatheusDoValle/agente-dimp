FROM mcr.microsoft.com/dotnet/sdk:10.0.401-noble-amd64 AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props NuGet.Config global.json ./
COPY src/Api.RAG/ src/Api.RAG/
RUN dotnet restore "src/Api.RAG/Api.RAG.csproj" --locked-mode
RUN dotnet publish "src/Api.RAG/Api.RAG.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY src/Api.RAG/neon-root-yr.crt /app/neon-root-yr.crt
RUN cat /etc/ssl/certs/ca-certificates.crt /app/neon-root-yr.crt > /app/neon-ca-bundle.crt
ENV PGSSLROOTCERT=/app/neon-ca-bundle.crt
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=80
ENV AllowedHosts=*
EXPOSE 80
ENTRYPOINT ["dotnet", "Api.RAG.dll"]
