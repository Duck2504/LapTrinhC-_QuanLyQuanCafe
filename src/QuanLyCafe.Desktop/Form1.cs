namespace QuanLyCafe.Desktop;

public partial class Form1 : Form
{
    private Button btnTest =new Button() { Text = "Test API", Location = new Point(10, 10), Size = new Size(100, 30) };

    private async void btnTest_Click(object sender, EventArgs e)
    {
        try
        {
            // Gọi endpoint có sẵn của template API
            var result = await Program.Api.GetAsync<object>("weatherforecast");
            MessageBox.Show("Gọi API thành công!");
        }
        catch (Exception ex)
        {
            MessageBox.Show("Lỗi: " + ex.Message);
        }
    }
    public Form1()
    {
        InitializeComponent();
        Controls.Add(btnTest);
        btnTest.Click += btnTest_Click;
    }
    
}
