using System.Windows;
using System.Windows.Controls;

namespace Hanouty.Desktop.Bootstrap;

internal sealed class WorkstationUsersDialog : Window
{
    private readonly WorkstationUsersStore store;
    private readonly ListBox usersList = new() { MinHeight = 180, FontSize = 16 };
    private readonly TextBox nameBox = new() { FontSize = 17, MinHeight = 38 };
    private readonly PasswordBox pinBox = new() { FontSize = 17, MinHeight = 38 };
    private readonly ComboBox roleBox = new() { FontSize = 17, MinHeight = 38, ItemsSource = new string[] { "UTILISATEUR", "MANAGER", "ADMIN" } };
    private readonly StackPanel permsPanel = new();
    private readonly Dictionary<string, ComboBox> permBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8, 8, 8, 4) };
    private List<WorkstationUserRow> users = new();
    private List<string> permCols = new();
    private string? selectedOriginalUser;

    internal WorkstationUsersDialog(WorkstationUsersStore store)
    {
        this.store = store;
        Title = "Utilisateurs et codes PIN - session ADMIN";
        Width = 860; Height = 660; MinWidth = 680; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var root = new DockPanel { Margin = new Thickness(14) }; Content = root;
        var footer = new StackPanel(); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        footer.Children.Add(status);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        footer.Children.Add(buttons);

        Button Btn(string text, RoutedEventHandler click)
        {
            var b = new Button { Content = text, MinWidth = 130, Height = 42, Margin = new Thickness(5) };
            b.Click += click; buttons.Children.Add(b); return b;
        }
        Btn("Nouveau", (_, _) => StartNew());
        Btn("Enregistrer", (_, _) => Save());
        Btn("Supprimer", (_, _) => DeleteSelected());
        Btn("Fermer", (_, _) => Close());

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.Children.Add(grid);

        var left = new DockPanel { Margin = new Thickness(0, 0, 14, 0) };
        Grid.SetColumn(left, 0); grid.Children.Add(left);
        var leftTitle = new TextBlock { Text = "Comptes utilisateurs", FontSize = 18, Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(leftTitle, Dock.Top); left.Children.Add(leftTitle);
        usersList.SelectionChanged += (_, _) => OnSelected();
        left.Children.Add(usersList);

        var right = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
        var scroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(scroll, 1); grid.Children.Add(scroll);

        void Field(string label, FrameworkElement editor)
        {
            right.Children.Add(new TextBlock { Text = label, FontSize = 16, Margin = new Thickness(0, 10, 0, 4) });
            right.Children.Add(editor);
        }
        Field("Nom d'utilisateur", nameBox);
        Field("Code PIN (laisser vide pour conserver lçciel)", pinBox);
        Field("Catégorie / Rôle", roleBox);
        right.Children.Add(permsPanel);

        Reload();
    }

    private void Reload(string? selectUser = null)
    {
        try
        {
            (users, permCols) = store.Load();
            BuildPermControls();
            usersList.ItemsSource = users.Select(u => $"${u.User} (${u.Role})").ToList();
            if (selectUser is not null)
            {
                var idx = users.FindIndex(u => string.Equals(u.User, selectUser, StringComparison.Ordinal));
                if (idx >= 0) usersList.SelectedIndex = idx;
            }
            else if (users.Count > 0) usersList.SelectedIndex = 0;
            else StartNew();
        }
        catch (Exception e) { status.Text = e.Message; }
    }

    private void BuildPermControls()
    {
        permsPanel.Children.Clear(); permBoxes.Clear();
        if (permCols.Count == 0) return;
        permsPanel.Children.Add(new TextBlock { Text = "Droits et permissions LOGICA", FontSize = 17, Margin = new Thickness(0, 16, 0, 6) });
        foreach (var col in permCols)
        {
            permsPanel.Children.Add(new TextBlock { Text = col, FontSize = 15, Margin = new Thickness(0, 8, 0, 2) });
            var cb = new ComboBox { FontSize = 15, MinHeight = 34, ItemsSource = new string[] { "VISIBLE", "ACCES MANAGER", "ACCES ADMIN", "INVISIBLE" }, SelectedIndex = 0 };
            permBoxes[col] = cb; permsPanel.Children.Add(cb);
        }
    }

    private void OnSelected()
    {
        if (usersList.SelectedIndex < 0 || usersList.SelectedIndex >= users.Count) return;
        var u = users[usersList.SelectedIndex];
        selectedOriginalUser = u.User;
        nameBox.Text = u.User; pinBox.Password = ""; roleBox.SelectedItem = u.Role;
        foreach (var kv in permBoxes) kv.Value.SelectedItem = u.Permissions.TryGetValue(kv.Key, out var v) ? v : "VISIBLE";
        status.Text = $"Compte {selectedOriginalUser} sélectionné.";
    }

    private void StartNew()
    {
        selectedOriginalUser = null; usersList.SelectedIndex = -1;
        nameBox.Text = ""; pinBox.Password = ""; roleBox.SelectedItem = "UTILISATEUR";
        foreach (var kv in permBoxes) kv.Value.SelectedItem = 0;
        status.Text = "Création d'un nouveau compte.";
    }

    private void Save()
    {
        try
        {
            var perms = permBoxes.ToDictionary(kv => kv.Key, kv => kv.Value.SelectedItem.ToString() ?? "VISIBLE", StringComparer.OrdinalIgnoreCase);
            var isNew = selectedOriginalUser is null;
            var targetName = nameBox.Text.Trim();
            store.SaveUser(selectedOriginalUser ?? "", targetName, pinBox.Password, roleBox.SelectedItem?.ToString() ?? "UTILISATEUR", perms, isNew);
            Reload(targetName);
            status.Text = $"$Compte {targetName} enregistré.";
        }
        catch (Exception e) { status.Text = e.Message; }
    }

    private void DeleteSelected()
    {
        if (selectedOriginalUser is null) { status.Text = "Sélectionnez un compte à supprimer."; return; }
        try
        {
            var target = selectedOriginalUser;
            store.DeleteUser(target);
            Reload();
            status.Text = $"Compte {target} supprimé.";
        }
        catch (Exception e) { status.Text = e.Message; }
    }
}
