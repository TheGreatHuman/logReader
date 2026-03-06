using System.Windows;

namespace LogVision.Views;

public partial class EditLogPackageDialog : Window
{
    public string EditedName { get; private set; } = string.Empty;
    public string EditedDescription { get; private set; } = string.Empty;

    public EditLogPackageDialog(string name, string description)
    {
        InitializeComponent();
        TxtName.Text = name;
        TxtDescription.Text = description;
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("请输入日志名称", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            TxtName.Focus();
            return;
        }

        EditedName = TxtName.Text.Trim();
        EditedDescription = TxtDescription.Text?.Trim() ?? "";
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
