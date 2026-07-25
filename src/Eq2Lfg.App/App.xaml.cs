using System.Windows;
using Eq2Lfg.App.ViewModels;
using Eq2Lfg.Core.Config;
using Hardcodet.Wpf.TaskbarNotification;

namespace Eq2Lfg.App;

public partial class App : Application
{
    private TaskbarIcon? trayIcon;
    private MainWindow? mainWindow;
    private MainViewModel? viewModel;
    private bool exiting;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var settings = AppSettings.Load(AppSettings.DefaultPath);
        viewModel = new MainViewModel(settings);

        mainWindow = new MainWindow { DataContext = viewModel };
        mainWindow.Closing += (_, args) =>
        {
            if (!exiting)
            {
                // Close hides to tray; monitoring continues.
                args.Cancel = true;
                mainWindow.Hide();
            }
        };

        trayIcon = BuildTrayIcon();
        mainWindow.Show();
    }

    private TaskbarIcon BuildTrayIcon()
    {
        var openItem = new System.Windows.Controls.MenuItem { Header = "Open" };
        openItem.Click += (_, _) => ShowMainWindow();
        var exitItem = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exitItem.Click += (_, _) => ExitApplication();

        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(exitItem);

        var icon = new TaskbarIcon
        {
            ToolTipText = "EQ2 LFG Monitor",
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/app.ico")),
            ContextMenu = menu,
        };
        icon.TrayLeftMouseUp += (_, _) => ShowMainWindow();
        return icon;
    }

    private void ShowMainWindow()
    {
        if (mainWindow is null)
        {
            return;
        }

        mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }

    private void ExitApplication()
    {
        exiting = true;
        viewModel?.Monitor.Dispose();
        trayIcon?.Dispose();
        Shutdown();
    }
}
