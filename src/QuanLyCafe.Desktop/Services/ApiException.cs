namespace QuanLyCafe.Desktop.Services;

public class ApiException : Exception
{
    public int Status { get; }
    public string? Code { get; }      // ERR-...
    public ApiException(int status, string? code, string message) : base(message)
    {
        Status = status;
        Code = code;
    }
}