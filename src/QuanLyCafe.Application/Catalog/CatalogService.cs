using Microsoft.Extensions.Logging;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Application.Catalog;

#pragma warning disable CA1050 // Declare types in namespaces
public class CatalogService(
    IProductRepository repo,
    IMaterialRepository materials,
    ICategoryRepository categories,
    ILogger<CatalogService> log)
#pragma warning restore CA1050 // Declare types in namespaces
{
    // Tìm không dấu: "ca phe" khớp "Cà phê". Collation vi-VN coi ê/ô/đ là chữ riêng nên phải tự bỏ dấu.
    public static bool NameMatches(string name, string q) => Plain(name).Contains(Plain(q.Trim()));

    private static string Plain(string s) => new string(s.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray())
        .ToLowerInvariant().Replace('đ', 'd');

    public async Task<List<ProductDto>> GetProductsAsync(CancellationToken ct) =>
        (await repo.GetAllAsync(onlyActive: false, ct)).Select(ToDto).ToList();

    // Thực đơn đang bán; danh mục đã ngừng thì ẩn món của nó. q tìm theo tên, không phân biệt dấu/hoa thường.
    public async Task<List<MenuItemDto>> GetMenuAsync(CancellationToken ct, string? q = null, Guid? categoryId = null) =>
        (await repo.GetAllAsync(onlyActive: true, ct))
            .Where(p => p.Category is null || p.Category.IsActive)
            .Where(p => categoryId is null || p.CategoryID == categoryId)
            .Where(p => string.IsNullOrWhiteSpace(q) || NameMatches(p.ProductName, q))
            .Select(ToMenuItem).ToList();

    public async Task<MenuItemDto> GetMenuItemAsync(Guid id, CancellationToken ct)
    {
        var p = await repo.GetByIdAsync(id, ct);
        if (p is not { IsActive: true } || p.Category is { IsActive: false })
            throw new NotFoundException("Không tìm thấy món.");
        return ToMenuItem(p);
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(bool onlyActive, CancellationToken ct) =>
        (await categories.GetAllAsync(onlyActive, ct))
            .Select(c => new CategoryDto(c.CategoryID, c.CategoryName, c.IsActive)).ToList();

    public async Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest req, CancellationToken ct)
    {
        var name = await ValidateCategoryAsync(req, null, ct);
        var c = new Category { CategoryName = name, IsActive = true };
        categories.Add(c);
        await categories.SaveChangesAsync(ct);
        return new(c.CategoryID, c.CategoryName, c.IsActive);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(Guid id, SaveCategoryRequest req, CancellationToken ct)
    {
        var c = await categories.GetByIdAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy danh mục.");
        c.CategoryName = await ValidateCategoryAsync(req, id, ct);
        c.IsActive = req.IsActive; // "Xoá" trên form = IsActive=false
        await categories.SaveChangesAsync(ct);
        return new(c.CategoryID, c.CategoryName, c.IsActive);
    }

    private async Task<string> ValidateCategoryAsync(SaveCategoryRequest req, Guid? id, CancellationToken ct)
    {
        var name = req.CategoryName?.Trim() ?? "";
        if (name.Length is 0 or > 50)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Tên danh mục phải có từ 1 đến 50 ký tự.");
        if (await categories.NameExistsAsync(name, id, ct))
            throw new ValidationException(ErrorCodes.ValidationFailed, $"Danh mục \"{name}\" đã tồn tại.");
        return name;
    }

    public async Task<ProductDto> CreateAsync(SaveProductRequest req, CancellationToken ct)
    {
        Validate(req);
        var p = new Product { ProductID = Guid.NewGuid(), ProductName = req.ProductName.Trim(),
                              Price = req.Price, IsActive = true };
        await ApplyDetailsAsync(p, req, ct);
        await repo.AddAsync(p, ct);
        await repo.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest req, CancellationToken ct)
    {
        Validate(req);
        var p = await repo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException("Không tìm thấy sản phẩm");
        var oldPrice = p.Price;
        p.ProductName = req.ProductName.Trim();
        p.Price = req.Price;
        p.IsActive = req.IsActive;          // "Xoá" trên form = IsActive=false
        await ApplyDetailsAsync(p, req, ct);
        await repo.SaveChangesAsync(ct);
        if (oldPrice != p.Price)
            log.LogInformation("Đổi giá sản phẩm {ProductId} ({ProductName}): {OldPrice} -> {NewPrice}",
                p.ProductID, p.ProductName, oldPrice, p.Price);

        return ToDto(p);
    }
    private static void Validate(SaveProductRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ProductName))
            throw new ValidationException(
                "PRODUCT_NAME_REQUIRED",
                "Tên sản phẩm không được để trống"
            );

        if (r.Price < 0)
            throw new ValidationException(
                ErrorCodes.PriceNegative,
                "Giá bán phải lớn hơn hoặc bằng 0"
            );
    }
    // Danh mục, mô tả, ảnh: phần hiển thị trên Web người dùng.
    private async Task ApplyDetailsAsync(Product p, SaveProductRequest req, CancellationToken ct)
    {
        var description = string.IsNullOrWhiteSpace(req.Description) ? null : req.Description.Trim();
        var imageUrl = string.IsNullOrWhiteSpace(req.ImageUrl) ? null : req.ImageUrl.Trim();
        if (description?.Length > 500)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Mô tả tối đa 500 ký tự.");
        // Chỉ nhận http(s) hoặc đường dẫn tương đối "/..." để trang Web không hiển thị link lạ (javascript:, data:...).
        var relative = imageUrl?.StartsWith('/') == true && !imageUrl.StartsWith("//");
        var web = Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        // Có "\" thì chặn: trình duyệt hiểu "/\evil.com" như "//evil.com" (tải ảnh từ host lạ).
        if (imageUrl is not null && (imageUrl.Length > 300 || imageUrl.Contains('\\') || !(relative || web)))
            throw new ValidationException(ErrorCodes.ValidationFailed, "Link ảnh phải bắt đầu bằng http://, https:// hoặc / và tối đa 300 ký tự.");

        Category? category = null;
        if (req.CategoryId is { } categoryId)
        {
            category = await categories.GetByIdAsync(categoryId, ct);
            // Giữ nguyên danh mục cũ thì được, kể cả khi danh mục đó đã ngừng; chỉ chặn gán mới vào danh mục ngừng.
            if (category is null || !category.IsActive && categoryId != p.CategoryID)
                throw new ValidationException(ErrorCodes.ValidationFailed, "Danh mục không tồn tại hoặc đã ngừng.");
        }

        p.Description = description;
        p.ImageUrl = imageUrl;
        p.CategoryID = category?.CategoryID;
        p.Category = category;
    }

    private static ProductDto ToDto(Product p) => new(p.ProductID, p.ProductName, p.Price, p.IsActive,
        p.CategoryID, p.Category?.CategoryName, p.Description, p.ImageUrl);

    private static MenuItemDto ToMenuItem(Product p) => new(p.ProductID, p.ProductName, p.Price,
        p.CategoryID, p.Category?.CategoryName, p.Description, p.ImageUrl);

    public async Task SetBomAsync(
    Guid productId, SetBomRequest req, CancellationToken ct)
    {
        var product = await repo.GetWithBomAsync(productId, ct)
            ?? throw new NotFoundException("Không tìm thấy sản phẩm.");

        if (req.Lines is null)
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Bạn cần gửi danh sách định lượng.");

        const decimal maxQuantity = 999_999_999_999_999.999m;

        // Kiểm tra từng dòng trước khi cộng các dòng trùng.
        foreach (var line in req.Lines)
        {
            if (line is null || line.MaterialId == Guid.Empty)
                throw new ValidationException(
                    ErrorCodes.ValidationFailed, "Mã nguyên liệu không hợp lệ.");

            var quantity = line.QuantityRequired;

            if (quantity <= 0 || quantity > maxQuantity ||
                decimal.Round(quantity, 3) != quantity)
            {
                throw new ValidationException(
                    ErrorCodes.ValidationFailed,
                    "Định lượng phải lớn hơn 0, trong giới hạn lưu trữ và có tối đa 3 chữ số thập phân.");
            }
        }

        // MaterialId → tổng định lượng trong request.
        var quantities = req.Lines
            .GroupBy(line => line.MaterialId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.QuantityRequired));

        if (quantities.Values.Any(quantity => quantity > maxQuantity))
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Tổng định lượng vượt giới hạn lưu trữ.");

        var existingMaterials = await materials.GetByIdsAsync(quantities.Keys, ct);

        foreach (var materialId in quantities.Keys)
        {
            if (!existingMaterials.ContainsKey(materialId))
                throw new NotFoundException($"Không tìm thấy nguyên liệu {materialId}.");
        }

        // Dòng cũ vắng trong request mới thì xóa.
        var currentLines = product.ProductBOMs.ToDictionary(line => line.MaterialID);
        var oldQuantities = product.ProductBOMs
            .ToDictionary(line => line.MaterialID, line => line.QuantityRequired);
        var removedLines = currentLines.Values
            .Where(line => !quantities.ContainsKey(line.MaterialID))
            .ToList();

        repo.RemoveBomLines(removedLines);

        // Cùng MaterialID thì sửa tại chỗ. MaterialID mới thì thêm dòng.
        foreach (var (materialId, quantity) in quantities)
        {
            if (currentLines.TryGetValue(materialId, out var existingLine))
            {
                existingLine.QuantityRequired = quantity;
            }
            else
            {
                product.ProductBOMs.Add(new ProductBOM
                {
                    ProductID = product.ProductID,
                    MaterialID = materialId,
                    QuantityRequired = quantity
                });
            }
        }

        await repo.SaveChangesAsync(ct);

        foreach (var materialId in oldQuantities.Keys.Union(quantities.Keys))
        {
            decimal? oldQuantity = oldQuantities.TryGetValue(materialId, out var oldValue) ? oldValue : null;
            decimal? newQuantity = quantities.TryGetValue(materialId, out var newValue) ? newValue : null;
            if (oldQuantity != newQuantity)
                log.LogInformation(
                    "Đổi BOM sản phẩm {ProductId} ({ProductName}), nguyên liệu {MaterialId}: {OldQuantity} -> {NewQuantity}",
                    product.ProductID, product.ProductName, materialId, oldQuantity, newQuantity);
        }
    }

    public async Task<List<BomLineDto>> GetBomAsync(
    Guid productId, CancellationToken ct)
    {
        var product = await repo.GetWithBomAsync(productId, ct)
            ?? throw new NotFoundException("Không tìm thấy sản phẩm.");

        if (product.ProductBOMs.Count == 0)
            return [];

        var materialsById = await materials.GetByIdsAsync(
            product.ProductBOMs.Select(line => line.MaterialID), ct);

        return product.ProductBOMs.Select(line =>
        {
            var material = materialsById[line.MaterialID];

            return new BomLineDto(
                line.MaterialID,
                material.MaterialName,
                material.Unit,
                line.QuantityRequired);
        }).ToList();
    }
}


