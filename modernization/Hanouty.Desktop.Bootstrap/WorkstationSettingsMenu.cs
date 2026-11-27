using System.Windows;
using System.Windows.Controls;

namespace Hanouty.Desktop.Bootstrap;
internal sealed class WorkstationSettingsMenu : Window
{
    internal WorkstationSettingsMenu(Action ticketSettings, Action printerSettings,bool extended=false, Action? userSettings=null)
    {
        Title = "ParamÃ¨tres de la caisse"; Width = 540; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(24) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "ParamÃ¨tres de la caisse", FontSize = 24, FontWeight = FontWeights.SemiBold });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) };
        void Choice(string title, string description, Action action)
        {
            var content = new StackPanel(); content.Children.Add(new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeights.SemiBold });
            content.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
            var button = new Button { Content = content, MinHeight = 80, Padding = new Thickness(14), Margin = new Thickness(0, 14, 0, 0), HorizontalContentAlignment = HorizontalAlignment.Left };
            System.Windows.Automation.AutomationProperties.SetName(button, title);
            button.Click += (_, _) =>
            {
                try { status.Text = ""; action(); }
                catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
                { status.Text = ex is UnauthorizedAccessException or InvalidOperationException ? ex.Message : "Ces paramÃ¨tres sont indisponibles."; }
            };
            panel.Children.Add(button);
        }
        Choice(extended?"Vente, stock, caisse et ticket":"Ticket et modes de paiement", extended?"Options LOGICA de vente, correction du stock, clÃ´tures, fonds de caisse et ticket.":"ModÃ¨le, branchement, numÃ©ro, QR, modes de paiement et pied du ticket LOGICA.", ticketSettings);
        Choice("Imprimante de ce poste", "Choisir lâ€™imprimante Windows et conserver ce choix lors des mises Ã  jour.", printerSettings);
        if (userSettings is not null) Choice("Utilisateurs et codes PIN", "Créer, modifier ou supprimer les comptes caissiers, managers et administrateurs (TABLE_PASS).", userSettings);
        panel.Children.Add(status);
        var close = new Button { Content = "Fermer", IsCancel = true, MinHeight = 40, Margin = new Thickness(0, 14, 0, 0) }; panel.Children.Add(close);
    }
}
