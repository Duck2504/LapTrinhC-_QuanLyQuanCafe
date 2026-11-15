# Build từ gốc repo:  docker build -f src/QuanLyCafe.Api/Dockerfile -t quanlycafe-api .
# Chạy cần biến môi trường: ConnectionStrings__Default, Jwt__Key (>= 32 ký tự), Seed__ManagerPassword.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Chép csproj trước để cache bước restore khi chỉ sửa code.
COPY src/QuanLyCafe.Domain/QuanLyCafe.Domain.csproj src/QuanLyCafe.Domain/
COPY src/QuanLyCafe.Application/QuanLyCafe.Application.csproj src/QuanLyCafe.Application/
COPY src/QuanLyCafe.Infrastructure/QuanLyCafe.Infrastructure.csproj src/QuanLyCafe.Infrastructure/
COPY src/QuanLyCafe.Api/QuanLyCafe.Api.csproj src/QuanLyCafe.Api/
RUN dotnet restore src/QuanLyCafe.Api/QuanLyCafe.Api.csproj
COPY src/ src/
RUN dotnet publish src/QuanLyCafe.Api/QuanLyCafe.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Serilog ghi logs/ — user không phải root cần quyền ghi thư mục này.
RUN mkdir logs && chown $APP_UID logs
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "QuanLyCafe.Api.dll"]
