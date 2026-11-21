-- Dữ liệu demo: 10 bàn, 12 nguyên liệu, 4 danh mục, 15 món (có mô tả), BOM cho mọi món.
-- Chạy SAU khi API đã khởi động một lần (migration đã tạo bảng). Chạy lại nhiều lần không bị trùng.
--   sqlcmd -S localhost -d QuanLyCafe -E -i scripts/seed-demo.sql
--   Docker: sqlcmd -S localhost,1433 -U sa -P "<SA_PASSWORD>" -C -d QuanLyCafe -i scripts/seed-demo.sql
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

-- Bàn
INSERT INTO Tables (TableID, TableName, Status)
SELECT NEWID(), v.TableName, 0
FROM (VALUES (N'Bàn 01'),(N'Bàn 02'),(N'Bàn 03'),(N'Bàn 04'),(N'Bàn 05'),
             (N'Bàn 06'),(N'Bàn 07'),(N'Bàn 08'),(N'Bàn 09'),(N'Bàn 10')) v(TableName)
WHERE NOT EXISTS (SELECT 1 FROM Tables t WHERE t.TableName = v.TableName);

-- Nguyên liệu (tồn kho, mức tối thiểu)
INSERT INTO Materials (MaterialID, MaterialName, Unit, StockQuantity, MinQuantity)
SELECT NEWID(), v.Name, v.Unit, v.Stock, v.MinQ
FROM (VALUES
    (N'Cà phê hạt',      N'g',    5000, 1000),
    (N'Sữa đặc',         N'ml',   3000,  500),
    (N'Sữa tươi',        N'ml',   6000, 1000),
    (N'Đường',           N'g',    4000,  500),
    (N'Đá viên',         N'g',   20000, 3000),
    (N'Trà đen',         N'g',    1000,  200),
    (N'Trà xanh matcha', N'g',     800,  150),
    (N'Bột cacao',       N'g',    1000,  200),
    (N'Cam tươi',        N'quả',    60,   10),
    (N'Chanh',           N'quả',    50,   10),
    (N'Đào ngâm',        N'g',    2000,  300),
    (N'Trân châu',       N'g',    3000,  500)
) v(Name, Unit, Stock, MinQ)
WHERE NOT EXISTS (SELECT 1 FROM Materials m WHERE m.MaterialName = v.Name);

-- Món
INSERT INTO Products (ProductID, ProductName, Price, IsActive)
SELECT NEWID(), v.Name, v.Price, 1
FROM (VALUES
    (N'Cà phê đen',        25000), (N'Cà phê sữa',        29000), (N'Bạc xỉu',           32000),
    (N'Cà phê sữa tươi',   35000), (N'Latte',             45000), (N'Cappuccino',        45000),
    (N'Americano',         35000), (N'Trà đào',           39000), (N'Trà chanh',         25000),
    (N'Trà sữa trân châu', 39000), (N'Matcha latte',      49000), (N'Cacao nóng',        35000),
    (N'Cacao đá',          37000), (N'Nước cam',          35000), (N'Chanh tuyết',       35000)
) v(Name, Price)
WHERE NOT EXISTS (SELECT 1 FROM Products p WHERE p.ProductName = v.Name);

-- Danh mục + gán danh mục, mô tả cho món (chỉ điền chỗ còn trống, không ghi đè dữ liệu Manager đã sửa)
INSERT INTO Categories (CategoryID, CategoryName, IsActive)
SELECT NEWID(), v.Name, 1
FROM (VALUES (N'Cà phê'), (N'Trà'), (N'Đá xay & Cacao'), (N'Nước ép')) v(Name)
WHERE NOT EXISTS (SELECT 1 FROM Categories c WHERE c.CategoryName = v.Name);

UPDATE p SET
    CategoryID  = COALESCE(p.CategoryID, c.CategoryID),
    Description = COALESCE(p.Description, v.Description)
FROM Products p
JOIN (VALUES
    (N'Cà phê đen',        N'Cà phê',          N'Robusta rang đậm pha phin, đắng thơm, hậu ngọt nhẹ.'),
    (N'Cà phê sữa',        N'Cà phê',          N'Cà phê phin hòa sữa đặc, vị truyền thống Sài Gòn.'),
    (N'Bạc xỉu',           N'Cà phê',          N'Nhiều sữa, ít cà phê, béo nhẹ dễ uống.'),
    (N'Cà phê sữa tươi',   N'Cà phê',          N'Cà phê phin với sữa tươi thanh mát.'),
    (N'Latte',             N'Cà phê',          N'Espresso và sữa tươi đánh nóng, lớp bọt mịn.'),
    (N'Cappuccino',        N'Cà phê',          N'Espresso, sữa nóng và lớp bọt sữa dày.'),
    (N'Americano',         N'Cà phê',          N'Espresso pha loãng với nước, uống nóng hoặc đá.'),
    (N'Trà đào',           N'Trà',             N'Trà đen ủ lạnh với đào ngâm giòn ngọt.'),
    (N'Trà chanh',         N'Trà',             N'Trà đen, chanh tươi, chua ngọt giải nhiệt.'),
    (N'Trà sữa trân châu', N'Trà',             N'Trà đen sữa tươi kèm trân châu đường đen.'),
    (N'Matcha latte',      N'Trà',             N'Matcha Nhật pha sữa tươi, thơm vị trà xanh.'),
    (N'Cacao nóng',        N'Đá xay & Cacao',  N'Cacao nguyên chất khuấy sữa nóng.'),
    (N'Cacao đá',          N'Đá xay & Cacao',  N'Cacao sữa lạnh, đậm vị sô-cô-la.'),
    (N'Nước cam',          N'Nước ép',         N'Cam vắt tươi mỗi ly, không thêm hương liệu.'),
    (N'Chanh tuyết',       N'Nước ép',         N'Chanh xay đá mịn, chua ngọt mát lạnh.')
) v(Product, Category, Description) ON p.ProductName = v.Product
JOIN Categories c ON c.CategoryName = v.Category;

-- BOM: định mức nguyên liệu cho 1 ly
INSERT INTO Product_BOMs (ProductID, MaterialID, QuantityRequired)
SELECT p.ProductID, m.MaterialID, v.Qty
FROM (VALUES
    (N'Cà phê đen',        N'Cà phê hạt', 20), (N'Cà phê đen',        N'Đường', 10),       (N'Cà phê đen',        N'Đá viên', 150),
    (N'Cà phê sữa',        N'Cà phê hạt', 20), (N'Cà phê sữa',        N'Sữa đặc', 30),     (N'Cà phê sữa',        N'Đá viên', 150),
    (N'Bạc xỉu',           N'Cà phê hạt', 10), (N'Bạc xỉu',           N'Sữa đặc', 30),     (N'Bạc xỉu',           N'Sữa tươi', 80),  (N'Bạc xỉu', N'Đá viên', 150),
    (N'Cà phê sữa tươi',   N'Cà phê hạt', 20), (N'Cà phê sữa tươi',   N'Sữa tươi', 100),   (N'Cà phê sữa tươi',   N'Đá viên', 150),
    (N'Latte',             N'Cà phê hạt', 18), (N'Latte',             N'Sữa tươi', 200),
    (N'Cappuccino',        N'Cà phê hạt', 18), (N'Cappuccino',        N'Sữa tươi', 150),
    (N'Americano',         N'Cà phê hạt', 18), (N'Americano',         N'Đá viên', 150),
    (N'Trà đào',           N'Trà đen', 5),     (N'Trà đào',           N'Đào ngâm', 60),    (N'Trà đào',           N'Đường', 20),     (N'Trà đào', N'Đá viên', 150),
    (N'Trà chanh',         N'Trà đen', 5),     (N'Trà chanh',         N'Chanh', 1),        (N'Trà chanh',         N'Đường', 20),     (N'Trà chanh', N'Đá viên', 150),
    (N'Trà sữa trân châu', N'Trà đen', 5),     (N'Trà sữa trân châu', N'Sữa tươi', 100),   (N'Trà sữa trân châu', N'Trân châu', 50), (N'Trà sữa trân châu', N'Đường', 20),
    (N'Matcha latte',      N'Trà xanh matcha', 10), (N'Matcha latte', N'Sữa tươi', 200),   (N'Matcha latte',      N'Đường', 10),
    (N'Cacao nóng',        N'Bột cacao', 25),  (N'Cacao nóng',        N'Sữa tươi', 200),   (N'Cacao nóng',        N'Đường', 10),
    (N'Cacao đá',          N'Bột cacao', 25),  (N'Cacao đá',          N'Sữa tươi', 150),   (N'Cacao đá',          N'Đá viên', 150),
    (N'Nước cam',          N'Cam tươi', 3),    (N'Nước cam',          N'Đường', 10),       (N'Nước cam',          N'Đá viên', 100),
    (N'Chanh tuyết',       N'Chanh', 1),       (N'Chanh tuyết',       N'Đường', 25),       (N'Chanh tuyết',       N'Đá viên', 200)
) v(Product, Material, Qty)
JOIN Products  p ON p.ProductName  = v.Product
JOIN Materials m ON m.MaterialName = v.Material
WHERE NOT EXISTS (SELECT 1 FROM Product_BOMs b WHERE b.ProductID = p.ProductID AND b.MaterialID = m.MaterialID);

COMMIT;

SELECT (SELECT COUNT(*) FROM Tables) AS Tables, (SELECT COUNT(*) FROM Materials) AS Materials,
       (SELECT COUNT(*) FROM Products) AS Products, (SELECT COUNT(*) FROM Product_BOMs) AS BomLines;
