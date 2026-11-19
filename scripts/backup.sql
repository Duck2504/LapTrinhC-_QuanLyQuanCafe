-- NFR-05: sao lưu đầy đủ CSDL QuanLyCafe ra file .bak có ngày giờ trong tên,
-- đặt trong thư mục backup mặc định của SQL Server (dòng "Đã sao lưu" in ra đường dẫn).
--   sqlcmd -S localhost -E -C -i scripts/backup.sql
--   Docker: sqlcmd -S localhost,1433 -U sa -P "<SA_PASSWORD>" -C -i scripts/backup.sql
--           rồi lấy file ra: docker compose cp db:<đường dẫn in ra> .
-- Khôi phục: RESTORE DATABASE QuanLyCafe FROM DISK = N'<file .bak>' WITH REPLACE;
SET NOCOUNT ON;
DECLARE @file NVARCHAR(400) = CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS NVARCHAR(300))
    + N'/QuanLyCafe_' + FORMAT(SYSDATETIME(), 'yyyyMMdd_HHmmss') + N'.bak';

BACKUP DATABASE QuanLyCafe TO DISK = @file WITH INIT, CHECKSUM, STATS = 10;
RESTORE VERIFYONLY FROM DISK = @file WITH CHECKSUM; -- báo lỗi nếu file hỏng

PRINT N'Đã sao lưu: ' + @file;
