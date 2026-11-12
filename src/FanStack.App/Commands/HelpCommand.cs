using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using FanStack.App.Services;
using FanStack.Core.Naming;

namespace FanStack.App;

/// <summary>Senza argomenti: piccola finestra di aiuto in italiano, con creazione rapida di uno stack.</summary>
internal static class HelpCommand
{
    public static int Run()
    {
        var app = Program.CreateApplication();
        var window = new HelpWindow();
        window.Closed += (_, _) => app.Shutdown();
        window.Show();
        return app.Run();
    }
}

/// <summary>Finestra di aiuto: passi per creare e fissare uno stack.</summary>
internal sealed class HelpWindow : Window
{
    private readonly TextBox _name;

    public HelpWindow()
    {
        Title = "FanStack: aiuto";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI Variable Text, Segoe UI");
        FontSize = 14;
        var theme = Theme.Current();
        var fg = new SolidColorBrush(theme.IsDark ? Color.FromRgb(0xF2, 0xF2, 0xF4) : Color.FromRgb(0x1C, 0x1D, 0x21));
        Background = new SolidColorBrush(theme.IsDark ? Color.FromRgb(0x20, 0x21, 0x26) : Color.FromRgb(0xF7, 0xF7, 0xF9));
        Foreground = fg;

        var panel = new StackPanel { Margin = new Thickness(28, 24, 28, 24) };
        panel.Children.Add(new TextBlock { Text = "FanStack", FontSize = 26, FontWeight = FontWeights.SemiBold, Foreground = fg });
        panel.Children.Add(new TextBlock
        {
            Text = "Cartelle di app che si aprono a ventaglio dalla taskbar.",
            Opacity = 0.75,
            Margin = new Thickness(0, 2, 0, 18),
            Foreground = fg,
        });

        panel.Children.Add(Step(fg, "1", "Scegli un nome e premi Crea. Si apre la cartella dello stack e compare un collegamento sul Desktop."));

        var row = new DockPanel { Margin = new Thickness(30, 2, 0, 14), LastChildFill = true };
        var create = new Button { Content = "Crea", Padding = new Thickness(18, 5, 18, 5), Margin = new Thickness(8, 0, 0, 0), IsDefault = true };
        DockPanel.SetDock(create, Dock.Right);
        _name = new TextBox { Text = "Ufficio", Padding = new Thickness(6, 4, 6, 4), VerticalContentAlignment = VerticalAlignment.Center };
        row.Children.Add(create);
        row.Children.Add(_name);
        panel.Children.Add(row);
        create.Click += (_, _) => Create();

        panel.Children.Add(Step(fg, "2", "Trascina nella cartella i collegamenti delle app (dal menu Start, dal Desktop o con tasto destro > Crea collegamento). Per scegliere l'ordine, metti un numero davanti al nome: \"01 - Word\"."));
        panel.Children.Add(Step(fg, "3", "Sul Desktop: tasto destro sul collegamento dello stack, poi \"Mostra altre opzioni\", poi \"Aggiungi alla barra delle applicazioni\". (Windows non permette ai programmi di farlo da soli.)"));
        panel.Children.Add(Step(fg, "4", "Clicca l'icona sulla taskbar: le app escono a ventaglio. Clic su un'app per avviarla; Esc o clic fuori per chiudere. Frecce e Invio funzionano da tastiera."));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var openRoot = new Button { Content = "Apri la cartella FanStacks", Padding = new Thickness(12, 5, 12, 5) };
        openRoot.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.StacksRoot);
            AppLauncher.OpenFolder(AppPaths.StacksRoot);
        };
        var openLog = new Button { Content = "Apri il log", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0) };
        openLog.Click += (_, _) =>
        {
            Directory.CreateDirectory(AppPaths.DataRoot);
            AppLauncher.OpenFolder(AppPaths.DataRoot);
        };
        buttons.Children.Add(openRoot);
        buttons.Children.Add(openLog);
        panel.Children.Add(buttons);

        panel.Children.Add(new TextBlock
        {
            Text = "Da riga di comando: FanStack.exe --create \"Nome\"  ·  --stack \"cartella\"  ·  --refresh-icons",
            FontSize = 12,
            Opacity = 0.6,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0),
            Foreground = fg,
        });

        Content = panel;
        Loaded += (_, _) =>
        {
            _name.Focus();
            _name.SelectAll();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private static UIElement Step(Brush fg, string number, string text)
    {
        var tb = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8), Foreground = fg };
        tb.Inlines.Add(new Run(number + ".  ") { FontWeight = FontWeights.SemiBold });
        tb.Inlines.Add(new Run(text));
        return tb;
    }

    private void Create()
    {
        var validation = StackNaming.Validate(_name.Text);
        if (!validation.IsValid)
        {
            MessageBox.Show(this, validation.Error, "FanStack", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        Cursor = Cursors.Wait;
        try
        {
            CreateCommand.Run(validation.Name, quiet: false);
        }
        finally
        {
            Cursor = null;
        }
    }
}
