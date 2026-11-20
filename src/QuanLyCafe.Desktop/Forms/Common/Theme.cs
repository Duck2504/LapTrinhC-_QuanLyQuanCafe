namespace QuanLyCafe.Desktop.Forms;

public static class Theme
{
    public static readonly Color Primary = Color.FromArgb(0x6F, 0x4E, 0x37);
    public static readonly Color PrimaryLight = Color.FromArgb(0xA6, 0x7B, 0x5B);
    public static readonly Color Background = Color.FromArgb(0xFA, 0xF6, 0xF0);
    public static readonly Color RowAlt = Color.FromArgb(0xF3, 0xEB, 0xE1);
    private static readonly Color Disabled = Color.FromArgb(0xE4, 0xDA, 0xCF);

    public static void Apply(Form form)
    {
        form.Font = new Font("Segoe UI", 10);
        form.BackColor = Background;
        form.Load += (_, _) => Style(form.Controls);
    }

    public static void Shortcuts(Form form, Button? refresh = null)
    {
        form.KeyPreview = true;
        form.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape && !Editing(form.ActiveControl))
            {
                e.Handled = true;
                form.Close();
            }
            else if (e.KeyCode == Keys.F5 && refresh is { Enabled: true })
            {
                e.Handled = true;
                refresh.PerformClick();
            }
        };
    }

    private static bool Editing(Control? c)
    {
        for (; c is not null; c = c.Parent)
            if (c is DataGridView { IsCurrentCellInEditMode: true } or ComboBox { DroppedDown: true })
                return true;
        return false;
    }

    private static void Style(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            if (c is Button b) StyleButton(b);
            else if (c is DataGridView g) StyleGrid(g);
            Style(c.Controls);
        }
    }

    private static void StyleButton(Button b)
    {
        b.FlatStyle = FlatStyle.Flat;
        b.FlatAppearance.BorderSize = 0;
        b.FlatAppearance.MouseOverBackColor = PrimaryLight;
        b.ForeColor = Color.White;
        b.Cursor = Cursors.Hand;
        b.BackColor = b.Enabled ? Primary : Disabled;
        b.EnabledChanged += (_, _) => b.BackColor = b.Enabled ? Primary : Disabled;
    }

    private static void StyleGrid(DataGridView g)
    {
        g.EnableHeadersVisualStyles = false;
        g.RowHeadersVisible = false;
        g.BackgroundColor = Color.White;
        g.BorderStyle = BorderStyle.None;
        g.GridColor = RowAlt;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
        g.ColumnHeadersDefaultCellStyle.BackColor = Primary;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Primary;
        g.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        g.AlternatingRowsDefaultCellStyle.BackColor = RowAlt;
        g.DefaultCellStyle.SelectionBackColor = PrimaryLight;
        g.DefaultCellStyle.SelectionForeColor = Color.White;
    }
}
