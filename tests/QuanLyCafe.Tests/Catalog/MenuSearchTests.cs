namespace QuanLyCafe.Tests.Catalog;

public class MenuSearchTests
{
    [Theory]
    [InlineData("Cà phê sữa", "ca phe", true)]
    [InlineData("Cà phê sữa", "CÀ PHÊ", true)]
    [InlineData("Cà phê đen", "den", true)]
    [InlineData("Trà đào", " dao ", true)]
    [InlineData("Bạc xỉu", "bac xiu", true)]
    [InlineData("Trà đào", "cam", false)]
    public void Tim_mon_khong_phan_biet_dau_va_hoa_thuong(string name, string q, bool expected) =>
        Assert.Equal(expected, CatalogService.NameMatches(name, q));
}
