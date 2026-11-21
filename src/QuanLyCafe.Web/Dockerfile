# Build từ gốc repo:  docker build -f src/QuanLyCafe.Web/Dockerfile -t quanlycafe-web .
# Chạy cần biến môi trường: Api__BaseUrl (ví dụ http://api:8080/).
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/QuanLyCafe.Web/QuanLyCafe.Web.csproj src/QuanLyCafe.Web/
RUN dotnet restore src/QuanLyCafe.Web/QuanLyCafe.Web.csproj
COPY src/QuanLyCafe.Web/ src/QuanLyCafe.Web/
RUN dotnet publish src/QuanLyCafe.Web/QuanLyCafe.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuanLyCafe.Web.dll"]
