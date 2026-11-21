using QuanLyCafe.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
// Web không truy cập DB trực tiếp: mọi dữ liệu đi qua Web API, dùng chung nghiệp vụ với Desktop.
builder.Services.AddHttpClient<CafeApi>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Thiếu cấu hình Api:BaseUrl."));
    c.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();
