using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using MySql.Data.MySqlClient;

namespace Hanouty.Desktop.Bootstrap;

internal sealed class CataloguePriceDialog : Window
{
    private static string? SelectWorkbook(Window owner)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Excel (*.xlsx)|*.xlsx" };
        return picker.ShowDialog(owner) == true ? picker.FileName : null;
    }

    internal CataloguePriceDialog(CataloguePriceEditor service, Func<CataloguePriceEditor.Price, decimal, bool> save,
        Func<string, string, string, decimal, string?, string?> create,
        Func<CataloguePriceEditor.Card, string, string, string, CataloguePriceEditor.CardDetails, bool> saveCard,
        Func<IReadOnlyList<CataloguePriceEditor.ImportLine>, int?> import, Func<Window, string?>? selectImportFile = null, string? selectedArticleId = null, bool creating = false)
    {
        StockWorkspaceAppearance.Apply(this);
        Title = "Articles â€” fiche et prix";
        Width = 900; Height = 720; MinWidth = 640; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(20) };
        var id = new TextBox { MinHeight = 36, IsReadOnly = true, Visibility = Visibility.Collapsed };
        var price = new TextBox { MinHeight = 36, IsEnabled = false };
        System.Windows.Automation.AutomationProperties.SetName(id, "Identifiant de lâ€™article");
        System.Windows.Automation.AutomationProperties.SetName(price, "Nouveau prix de vente");
        var open = new Button { Content = "Lire le prix", MinHeight = 36, Margin = new Thickness(0, 6, 0, 8) };
        var write = new Button { Content = "Enregistrer le prix", MinHeight = 36, Margin = new Thickness(0, 6, 0, 0), IsEnabled = false };
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
        var search = new TextBox { MinHeight = 40, MaxLength = 128 };
        System.Windows.Automation.AutomationProperties.SetName(search, "Rechercher un article par nom ou code-barres");
        var exact = new CheckBox { Content = "Correspondance exacte", Margin = new Thickness(0, 6, 0, 6) };
        System.Windows.Automation.AutomationProperties.SetName(exact, "Recherche exacte du catalogue");
        var find = new Button { Content = "Rechercher les articles", MinHeight = 40 };
        var choices = new ListBox { MinHeight = 80, MaxHeight = 160, DisplayMemberPath = "Label",
            Margin = new Thickness(0, 6, 0, 6) };
        System.Windows.Automation.AutomationProperties.SetName(choices, "Articles trouvÃ©s");
        var selected = new TextBlock { Text = "SÃ©lectionnez un article dans les rÃ©sultats.", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(new TextBlock { Text = "Nom ou code-barres" });
        panel.Children.Add(search);
        panel.Children.Add(exact);
        panel.Children.Add(find);
        panel.Children.Add(choices);
        panel.Children.Add(selected);
        void RevokeSelection()
        {
            choices.ItemsSource = null;
            id.Clear();
            price.Clear();
            selected.Text = "SÃ©lectionnez un article dans les rÃ©sultats.";
        }
        void Find()
        {
            RevokeSelection();
            try
            {
                var result = service.Search(search.Text, exact.IsChecked == true);
                choices.ItemsSource = result.Articles;
                status.Text = result.HasMore ? "Plus de 50 articles : prÃ©cisez la recherche." :
                    result.Articles.Count == 0 ? "Aucun article trouvÃ©." : $"{result.Articles.Count} article(s). SÃ©lectionnez la variante souhaitÃ©e.";
            }
            catch (Exception ex) when (ex is MySqlException or InvalidOperationException or ArgumentException)
            { status.Text = ex is ArgumentException or InvalidOperationException ? ex.Message : "Recherche indisponible."; }
        }
        search.TextChanged += (_, _) => RevokeSelection();
        exact.Checked += (_, _) => RevokeSelection();
        exact.Unchecked += (_, _) => RevokeSelection();
        find.Click += (_, _) => Find();
        search.KeyDown += (_, args) =>
        {
            if (args.Key == System.Windows.Input.Key.Enter) { args.Handled = true; Find(); }
        };
        choices.SelectionChanged += (_, _) =>
        {
            if (choices.SelectedItem is not CataloguePriceEditor.ArticleChoice choice) return;
            id.Text = choice.Id;
            selected.Text = choice.Label;
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
        Hanouty.Desktop.Wpf.TouchNumericEntry.Attach(price, "Prix de vente (MAD)", allowZero: true);
        panel.Children.Add(id);
        panel.Children.Add(open);
        panel.Children.Add(new TextBlock { Text = "Prix de vente (MAD)" });
        panel.Children.Add(price);
        panel.Children.Add(write);
        panel.Children.Add(status);
        var importButton = new Button { Content = "Importer les articles Excel", MinHeight = 36,
            Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(importButton);
        importButton.Click += (_, _) =>
        {
            var file = (selectImportFile ?? SelectWorkbook)(this);
            if (file is not null) ReviewImport(file);
        };

        void ReviewImport(string file)
        {
            try
            {
                var lines = CatalogueWorkbook.Read(file);
                var review = new Window
                {
                    Title = "VÃ©rifier les articles Ã  importer", Owner = this, Width = 800, Height = 520,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                var layout = new DockPanel { Margin = new Thickness(16) };
                var confirm = new Button { Content = $"Importer {lines.Count} article(s)", MinHeight = 42,
                    Margin = new Thickness(0, 8, 0, 0) };
                DockPanel.SetDock(confirm, Dock.Bottom);
                layout.Children.Add(confirm);
                layout.Children.Add(new DataGrid
                {
                    ItemsSource = lines, IsReadOnly = true, CanUserAddRows = false
                });
                confirm.Click += (_, _) => review.DialogResult = true;
                review.Content = layout;
                if (review.ShowDialog() != true) return;
                var count = import(lines);
                if (count.HasValue) status.Text = $"{count.Value} article(s) importÃ©(s).";
            }
            catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
            {
                status.Text = ex is ArgumentException or System.IO.InvalidDataException or UnauthorizedAccessException or InvalidOperationException
                    ? ex.Message : "Import non confirmÃ©. VÃ©rifiez le fichier et le catalogue.";
            }
        }
        var card = new Expander { Header = "Nouvel article", Margin = new Thickness(0, 14, 0, 0) };
        var fields = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        TextBox Field(string label)
        {
            fields.Children.Add(new TextBlock { Text = label });
            var field = new TextBox { MinHeight = 32, Margin = new Thickness(0, 2, 0, 6) };
            System.Windows.Automation.AutomationProperties.SetName(field, label);
            fields.Children.Add(field);
            return field;
        }
        var name = Field("Nom du nouvel article");
        var code = Field("Code-barres du nouvel article");
        IReadOnlyList<string> families;
        try { families = service.ReadFamilies(); }
        catch (MySqlException)
        {
            families = Array.Empty<string>();
            status.Text = "Liste des familles indisponible.";
        }

        ComboBox FamilyField(Panel parent, string label)
        {
            parent.Children.Add(new TextBlock { Text = label });
            var field = new ComboBox { ItemsSource = families, MinHeight = 32, Margin = new Thickness(0, 2, 0, 6) };
            System.Windows.Automation.AutomationProperties.SetName(field, label);
            parent.Children.Add(field);
            return field;
        }
        var family = FamilyField(fields, "Famille du nouvel article");
        var newPrice = Field("Prix du nouvel article");
        Hanouty.Desktop.Wpf.TouchNumericEntry.Attach(newPrice, "Prix du nouvel article (MAD)", allowZero: true);
        var variant = Field("Identifiant de lâ€™article source (variante, facultatif)");
        var add = new Button { Content = "CrÃ©er lâ€™article", MinHeight = 36 };
        add.Click += (_, _) =>
        {
            if (!decimal.TryParse(newPrice.Text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture, out var amount) || amount < 0 || decimal.Round(amount, 2) != amount)
            {
                status.Text = "Saisissez un prix Ã  deux dÃ©cimales.";
                return;
            }
            add.IsEnabled = false;
            try
            {
                var articleId = create(name.Text.Trim(), code.Text.Trim(), family.SelectedItem as string ?? "", amount,
                    string.IsNullOrWhiteSpace(variant.Text) ? null : variant.Text.Trim());
                if (articleId is not null)
                {
                    RevokeSelection();
                    id.Text = articleId;
                    selected.Text = name.Text + " Â· " + amount.ToString("N2") + " MAD";
                    status.Text = $"Article {articleId} crÃ©Ã©.";
                    name.Clear();
                    code.Clear();
                    family.SelectedIndex = -1;
                    newPrice.Clear();
                    variant.Clear();
                }
            }
            catch (UnauthorizedAccessException)
            {
                status.Text = "CrÃ©ation rÃ©servÃ©e Ã  un administrateur authentifiÃ©.";
            }
            catch (Exception ex) when (ex is MySqlException or InvalidOperationException or ArgumentException)
            {
                status.Text = ex is InvalidOperationException or ArgumentException ? ex.Message : "CrÃ©ation non confirmÃ©e.";
            }
            finally { add.IsEnabled = true; }
        };
        fields.Children.Add(add);
        card.Content = fields;
        panel.Children.Add(card);
        var edit = new Expander { Header = "Modifier la fiche", Margin = new Thickness(0, 14, 0, 0) };
        var editFields = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
        var readCard = new Button { Content = "Charger la fiche", MinHeight = 36 };
        editFields.Children.Add(readCard);
        TextBox EditField(string label)
        {
            editFields.Children.Add(new TextBlock { Text = label });
            var field = new TextBox { MinHeight = 32, Margin = new Thickness(0, 2, 0, 6) };
            System.Windows.Automation.AutomationProperties.SetName(field, label);
            editFields.Children.Add(field);
            return field;
        }
        var editName = EditField("Nom de la fiche");
        var editCode = EditField("Code-barres de la fiche");
        var editFamily = FamilyField(editFields, "Famille de la fiche");
        var description = EditField("Description");
        var reference = EditField("RÃ©fÃ©rence");
        var unit = EditField("UnitÃ©");
        var location = EditField("Emplacement");
        var editGroup = EditField("Groupe / Sous-famille");
        var editPurchase = EditField("Prix d'achat (MAD)");
        var editPrice1 = EditField("Prix de vente 1 (MAD)");
        var editPrice2 = EditField("Prix de vente 2 (MAD)");
        var editPrice3 = EditField("Prix de vente 3 (MAD)");
        var editWholesale = EditField("Prix de gros / QuantitÃ© (MAD)");
        var editWholesaleMin = EditField("QuantitÃ© min. gros");
        var editMinStock = EditField("Stock minimum");
        var editVat = EditField("Taux TVA (%)");
        var editPromo = EditField("Promotion (%)");
        var apply = new Button { Content = "Enregistrer la fiche", MinHeight = 36, IsEnabled = false };
        editFields.Children.Add(apply);
        edit.Content = editFields;
        panel.Children.Add(edit);
        panel.Children.Remove(card); panel.Children.Remove(edit); panel.Children.Remove(status);
        card.IsExpanded = edit.IsExpanded = true;
        var pages = new TabControl();
        TabItem Page(string label, UIElement body) { var tab = new TabItem { Header = label, Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }; pages.Items.Add(tab); return tab; }
        Page("Rechercher / Prix", panel);
        var createPage = Page("Ajouter un article", card);
        var editPage = Page("Modifier la fiche", edit);
        var layout = new DockPanel { Margin = new Thickness(12) };
        status.FontSize = 16; status.MinHeight = 34; DockPanel.SetDock(status, Dock.Bottom); layout.Children.Add(status); layout.Children.Add(pages); Content = layout;
        Loaded += (_, _) => {
            if (creating) { pages.SelectedItem = createPage; name.Focus(); }
            else if (selectedArticleId is not null) { id.Text = selectedArticleId; open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); readCard.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); pages.SelectedItem = editPage; }
        };
        MaxHeight = SystemParameters.WorkArea.Height;

        CataloguePriceEditor.Card? reviewedCard = null;
        id.TextChanged += (_, _) => { reviewedCard = null; apply.IsEnabled = false; };
        readCard.Click += (_, _) =>
        {
            reviewedCard = null;
            apply.IsEnabled = false;
            try
            {
                reviewedCard = service.ReadCard(id.Text.Trim());
                editName.Text = reviewedCard.Name;
                editCode.Text = reviewedCard.Barcode;
                editFamily.ItemsSource = families.Contains(reviewedCard.Family, StringComparer.Ordinal)
                    ? families : families.Append(reviewedCard.Family).ToArray();
                editFamily.SelectedItem = reviewedCard.Family;
                description.Text = reviewedCard.Details.Description;
                reference.Text = reviewedCard.Details.Reference;
                unit.Text = reviewedCard.Details.Unit;
                location.Text = reviewedCard.Details.Location;
                editGroup.Text = reviewedCard.Details.Group;
                editPurchase.Text = reviewedCard.Details.PurchasePrice.ToString("0.00", CultureInfo.InvariantCulture);
                editPrice1.Text = reviewedCard.Details.Price1.ToString("0.00", CultureInfo.InvariantCulture);
                editPrice2.Text = reviewedCard.Details.Price2.ToString("0.00", CultureInfo.InvariantCulture);
                editPrice3.Text = reviewedCard.Details.Price3.ToString("0.00", CultureInfo.InvariantCulture);
                editWholesale.Text = reviewedCard.Details.WholesalePrice.ToString("0.00", CultureInfo.InvariantCulture);
                editWholesaleMin.Text = reviewedCard.Details.WholesaleMinQty.ToString("0.###", CultureInfo.InvariantCulture);
                editMinStock.Text = reviewedCard.Details.MinStock.ToString("0.###", CultureInfo.InvariantCulture);
                editVat.Text = reviewedCard.Details.VatRate.ToString("0.##", CultureInfo.InvariantCulture);
                editPromo.Text = reviewedCard.Details.Promotion.ToString("0.##", CultureInfo.InvariantCulture);
                apply.IsEnabled = true;
            }
            catch (Exception ex) when (ex is MySqlException or InvalidOperationException)
            { status.Text = "Fiche article indisponible."; }
        };
        apply.Click += (_, _) =>
        {
            if (reviewedCard is not { } current) return;
            apply.IsEnabled = false;
            try
            {
                var details = new CataloguePriceEditor.CardDetails(description.Text.Trim(), reference.Text.Trim(),
                    unit.Text.Trim(), location.Text.Trim());
                if (saveCard(current, editName.Text.Trim(), editCode.Text.Trim(),
                    editFamily.SelectedItem as string ?? current.Family, details))
                {
                    reviewedCard = null;
                    status.Text = $"Fiche {current.Id} enregistrÃ©e.";
                }
                else apply.IsEnabled = true;
            }
            catch (Exception ex) when (ex is MySqlException or InvalidOperationException or ArgumentException or UnauthorizedAccessException)
            {
                reviewedCard = null;
                status.Text = ex is ArgumentException or InvalidOperationException or UnauthorizedAccessException
                    ? ex.Message : "Modification non confirmÃ©e. Rechargez la fiche.";
            }
        };

        CataloguePriceEditor.Price? reviewed = null;
        void Reset(string message)
        {
            reviewed = null;
            price.IsEnabled = false;
            write.IsEnabled = false;
            status.Text = message;
        }
        id.TextChanged += (_, _) => Reset("");
        open.Click += (_, _) =>
        {
            Reset("");
            try
            {
                reviewed = service.Read(id.Text.Trim());
                price.Text = reviewed.Amount.ToString("0.00", CultureInfo.CurrentCulture);
                status.Text = reviewed.Name;
                selected.Text = reviewed.Name + " Â· " + reviewed.Amount.ToString("N2") + " MAD";
                price.IsEnabled = true;
                write.IsEnabled = true;
            }
            catch (Exception ex) when (ex is ArgumentException or MySqlException or InvalidOperationException)
            {
                Reset("Article ou prix indisponible.");
            }
        };
        write.Click += (_, _) =>
        {
            if (reviewed is not { } snapshot ||
                !decimal.TryParse(price.Text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var amount) ||
                amount < 0 || decimal.Round(amount, 2) != amount)
            {
                status.Text = "Saisissez un prix positif ou nul, Ã  deux dÃ©cimales.";
                return;
            }
            write.IsEnabled = false;
            try
            {
                if (save(snapshot, amount))
                    Reset($"Prix de {snapshot.Name} enregistrÃ© : {amount:N2} MAD.");
                else write.IsEnabled = true;
            }
            catch (UnauthorizedAccessException)
            {
                Reset("Modification rÃ©servÃ©e Ã  un administrateur authentifiÃ©.");
            }
            catch (Exception ex) when (ex is MySqlException or InvalidOperationException)
            {
                Reset("Modification non confirmÃ©e. Relisez le prix avant de rÃ©essayer.");
            }
        };
    }
}

