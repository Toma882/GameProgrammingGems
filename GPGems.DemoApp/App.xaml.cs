using System.Configuration;
using System.Data;
using System.Windows;

namespace GPGems.DemoApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (sender, args) =>
        {
            MessageBox.Show($"未处理异常: {args.Exception.Message}\n\n{args.Exception.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };
    }
}

