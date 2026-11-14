namespace QuanLyCafe.Domain.Ordering;

public class Table
{
    public Guid TableID { get; set; } = Guid.NewGuid();
    public string TableName { get; set; } = "";
    public TableStatus Status { get; set; } = TableStatus.Empty;
}
