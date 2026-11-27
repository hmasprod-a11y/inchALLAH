using System.Windows;
using System.Windows.Controls;

namespace Hanouty.Desktop.Wpf;
public sealed class DailyLogicaSettingsDialog : Window
{
    public DailyLogicaSettingsDialog(IDailyLogicaSettingsWorkflow workflow)
    {
        Title = "Paramètres LOGICA — ticket et paiement"; Width = 670; Height = 650; MinWidth = 540; MinHeight = 500; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(20) }; Content = root;
        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) }; DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
        var save = new Button { Content = "Enregistrer les paramètres", MinHeight = 44, Padding = new Thickness(16, 4, 16, 4), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var reload = new Button { Content = "Relire les paramètres", MinHeight = 44, Padding = new Thickness(12, 4, 12, 4) }; actions.Children.Add(save); actions.Children.Add(reload);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) }; DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);
        var form = new StackPanel(); root.Children.Add(new ScrollViewer { Content = form, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        form.Children.Add(new TextBlock { Text = "Ticket et modes de paiement", FontSize = 24, FontWeight = FontWeights.SemiBold });
        form.Children.Add(new TextBlock { Text = "Paramètres partagés LOGICA. L’imprimante propre à ce poste se règle dans sa fenêtre dédiée.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) });
        var template = new ComboBox { MinHeight = 38 }; var connection = new ComboBox { MinHeight = 38 };
        form.Children.Add(new TextBlock { Text = "Modèle de ticket" }); form.Children.Add(template);
        form.Children.Add(new TextBlock { Text = "Branchement", Margin = new Thickness(0, 8, 0, 0) }); form.Children.Add(connection);
        CheckBox Flag(string label) { var box = new CheckBox { Content = label, MinHeight = 36, VerticalContentAlignment = VerticalAlignment.Center }; form.Children.Add(box); return box; }
        var payment = Flag("Afficher les modes de paiement sur le ticket"); var number = Flag("Afficher le numéro du ticket"); var qr = Flag("Afficher le QR du ticket");
        var paymentWindow = Flag("Activer le choix des modes de paiement");
        
        form.Children.Add(new TextBlock { Text = "Port afficheur (AFFICHEUR_PORT)", Margin = new Thickness(0, 8, 0, 0) });
        var afficheurPort = new TextBox { MinHeight = 36 }; form.Children.Add(afficheurPort);
        form.Children.Add(new TextBlock { Text = "Port balance (BALANCE_PORT)", Margin = new Thickness(0, 8, 0, 0) });
        var balancePort = new TextBox { MinHeight = 36 }; form.Children.Add(balancePort);
        form.Children.Add(new TextBlock { Text = "Code Wifi (CODE_WIFI)", Margin = new Thickness(0, 8, 0, 0) });
        var codeWifi = new TextBox { MinHeight = 36 }; form.Children.Add(codeWifi);

        var gestionStock = Flag("Activer la gestion du stock (GESTION_STOCK)");
        var blocageStock = Flag("Bloquer en cas de stock insuffisant (BLOCAGE_STOCK_INSUFFISANT)");
        var afficherModeV = Flag("Afficher Mode V (AFFICHER_MODE_V)");
        var htTtc = Flag("Afficher HT/TTC (HT_TTC)");
        var solderSansTicket = Flag("Solder sans ticket (SOLDER_SANS_TICKET)");

        form.Children.Add(new TextBlock { Text = "Langue (LANGUE)", Margin = new Thickness(0, 8, 0, 0) });
        var langue = new ComboBox { MinHeight = 38, ItemsSource = new[] { "FR", "AR" } }; form.Children.Add(langue);
        form.Children.Add(new TextBlock { Text = "Thème (THEME)", Margin = new Thickness(0, 8, 0, 0) });
        var theme = new ComboBox { MinHeight = 38, ItemsSource = new[] { "Default", "Dark" } }; form.Children.Add(theme);

        form.Children.Add(new TextBlock { Text = "Pied de document (PIED_DOC)", Margin = new Thickness(0, 8, 0, 0) });
        var piedDoc = new TextBox { MinHeight = 60, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap }; form.Children.Add(piedDoc);

        form.Children.Add(new TextBlock { Text = "Commentaire A (COMMENTAIRES_A)", Margin = new Thickness(0, 8, 0, 0) });
        var comA = new TextBox { MinHeight = 36 }; form.Children.Add(comA);
        form.Children.Add(new TextBlock { Text = "Commentaire B (COMMENTAIRES_B)", Margin = new Thickness(0, 8, 0, 0) });
        var comB = new TextBox { MinHeight = 36 }; form.Children.Add(comB);
        form.Children.Add(new TextBlock { Text = "Commentaire C (COMMENTAIRES_C)", Margin = new Thickness(0, 8, 0, 0) });
        var comC = new TextBox { MinHeight = 36 }; form.Children.Add(comC);

        form.Children.Add(new TextBlock { Text = "Pied du ticket", Margin = new Thickness(0, 8, 0, 4) });
        var footer = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 110, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; System.Windows.Automation.AutomationProperties.SetName(footer, "Pied du ticket LOGICA"); form.Children.Add(footer);
        DailyLogicaSettingsSnapshot? snapshot = null;
        void Lock(bool locked) { form.IsEnabled = !locked; }
        void Load()
        {
            var value = workflow.Reload(); snapshot = value;
            template.ItemsSource = new[] { value.Values.Template, "FR_3C" }.Distinct(StringComparer.Ordinal).ToArray(); template.SelectedItem = value.Values.Template;
            connection.ItemsSource = new[] { value.Values.Connection, "USB" }.Distinct(StringComparer.Ordinal).ToArray(); connection.SelectedItem = value.Values.Connection;
            payment.IsChecked = value.Values.ShowPayment; number.IsChecked = value.Values.ShowTicketNumber; qr.IsChecked = value.Values.ShowQr; paymentWindow.IsChecked = value.Values.PaymentWindow;
            
            afficheurPort.Text = value.Values.AfficheurPort;
            balancePort.Text = value.Values.BalancePort;
            codeWifi.Text = value.Values.CodeWifi;
            gestionStock.IsChecked = value.Values.GestionStock;
            blocageStock.IsChecked = value.Values.BlocageStockInsuffisant;
            afficherModeV.IsChecked = value.Values.AfficherModeV;
            htTtc.IsChecked = value.Values.HtTtc;
            solderSansTicket.IsChecked = value.Values.SolderSansTicket;
            langue.SelectedItem = value.Values.Langue;
            theme.SelectedItem = value.Values.Theme;
            piedDoc.Text = value.Values.PiedDoc;
            comA.Text = value.Values.CommentairesA;
            comB.Text = value.Values.CommentairesB;
            comC.Text = value.Values.CommentairesC;

            footer.MaxLength = value.MaximumValueLength; footer.Text = value.Values.Footer; Lock(false); save.Content = "Enregistrer les paramètres";
            status.Text = value.Values.Template == "FR_3C" && value.Values.Connection == "USB" ? "FR_3C / USB est le parcours raccordé. Le QR contient l’identité du ticket, pas une URL." :
                "La route enregistrée reste conservée. Seul FR_3C / USB peut être nouvellement choisi ici ; les autres modèles ne sont pas annoncés comme raccordés.";
        }
        reload.Click += (_, _) => { try { Load(); } catch (Exception ex) { status.Text = ex.Message; } };
        save.Click += (_, _) =>
        {
            if (snapshot is null) return; save.IsEnabled = reload.IsEnabled = false;
            try
            {
                var draft = workflow.Pending?.Desired ?? new DailyLogicaSettingsValues(template.SelectedItem as string ?? "", connection.SelectedItem as string ?? "",
                    payment.IsChecked == true, number.IsChecked == true, qr.IsChecked == true, footer.Text, paymentWindow.IsChecked == true,
                    afficheurPort.Text, balancePort.Text, codeWifi.Text, gestionStock.IsChecked == true, blocageStock.IsChecked == true,
                    langue.SelectedItem as string ?? "FR", theme.SelectedItem as string ?? "Default", afficherModeV.IsChecked == true,
                    htTtc.IsChecked == true, piedDoc.Text, solderSansTicket.IsChecked == true, comA.Text, comB.Text, comC.Text);
                snapshot = workflow.Save(workflow.Pending?.Expected ?? snapshot, draft); Lock(false); status.Text = "Paramètres enregistrés et relus. Ils s’appliquent aux prochaines opérations et réimpressions.";
                save.Content = "Enregistrer les paramètres";
            }
            catch (DailyLogicaSettingsRefusedException ex) { status.Text = "Aucune modification appliquée. " + ex.Message; Lock(false); }
            catch (Exception ex) { Lock(workflow.Pending is not null); save.Content = workflow.Pending is null ? "Enregistrer les paramètres" : "Vérifier le même enregistrement";
                status.Text = "Résultat à vérifier. Réessayez la même modification ou relisez les valeurs actuelles. " + ex.Message; }
            finally { save.IsEnabled = reload.IsEnabled = true; }
        };
        Loaded += (_, _) => { try { Load(); } catch (Exception ex) { Lock(true); save.IsEnabled = false; status.Text = ex.Message; } };
    }
}
