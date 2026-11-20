# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so this layer is cached when only code changes
COPY src/CardShop.Core/CardShop.Core.csproj src/CardShop.Core/
COPY src/CardShop.Infrastructure/CardShop.Infrastructure.csproj src/CardShop.Infrastructure/
COPY src/CardShop.Web/CardShop.Web.csproj src/CardShop.Web/
RUN dotnet restore src/CardShop.Web/CardShop.Web.csproj

COPY src/ src/
RUN dotnet publish src/CardShop.Web/CardShop.Web.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# ---- run ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

# Folder for the sign-in cookie keys (mounted as a volume so it survives redeploys)
RUN mkdir -p /keys && chown "$APP_UID" /keys

# Run as the image's built-in non-root user
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CardShop.Web.dll"]
