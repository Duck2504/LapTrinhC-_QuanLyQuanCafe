namespace QuanLyCafe.Domain.Catalog;

// Danh mục món (Cà phê, Trà, Đá xay...). Xóa mềm bằng IsActive như Product.
public class Category
{
    public Guid CategoryID { get; set; } = Guid.NewGuid();
    public string CategoryName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
