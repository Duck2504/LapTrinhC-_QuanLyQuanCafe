namespace QuanLyCafe.Desktop;

using QuanLyCafe.Desktop.Forms;
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
        Api = new ApiClient(Session, "https://localhost:7027/");
        ApplicationConfiguration.Initialize();
        IAuthApi auth = new FakeAuthApi();

        while (true)
        {
            using var login = new frmLogin(auth);
            if (login.ShowDialog() != DialogResult.OK)
                break;

            using var main = new frmMain(auth);
            Application.Run(main);

            if (!main.LoggedOut)
                break;
        }
    }    
}
