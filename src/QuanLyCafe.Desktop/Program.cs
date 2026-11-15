namespace QuanLyCafe.Desktop;
using QuanLyCafe.Desktop.Services;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    public static SessionStore Session { get; } = new();
    public static ApiClient Api { get; private set; } = null!;
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        Api = new ApiClient(Session, "https://localhost:7001/");
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }    
}