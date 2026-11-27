using System.Windows;
using System.Windows.Controls;
using Hanouty.Desktop.Wpf.Presentation;

namespace Hanouty.Desktop.Wpf;
public partial class MainWindow
{
    private Func<bool>? clearBasket;
    public void ConfigureBasketClear(Func<bool> clear) => clearBasket = clear;
    public void SetBasketActionFeedback(string message) => ManualDiscountStatus.Text = message;
    private void ClearBasket_Click(object sender, RoutedEventArgs args)
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            clearBasket is null || DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null } vm || vm.BasketLines.Count == 0) return;
        if (MessageBox.Show(this, "Vider le panier en cours ?", "Annuler le panier", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        if (clearBasket()) { vm.RefreshBasketLines(); RefreshTrustedBasketTotal(); }
    }
    private void EditBasketQuantity_Click(object sender, RoutedEventArgs args)
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null } vm ||
            (sender as FrameworkElement)?.DataContext is not BasketDisplayLine line || !vm.BasketLines.Any(current => ReferenceEquals(current, line))) return;
        var context = vm.PaymentContext; decimal? requested = null;
        _businessDocumentsOpen = true;
        try { var dialog = new TouchNumericEntryDialog("Quantité — " + line.ArticleName, line.Quantity, decimalPlaces: 0) { Owner = this }; if (dialog.ShowDialog() == true) requested = dialog.Value; }
        finally { _businessDocumentsOpen = false; }
        if (requested is decimal value && value <= int.MaxValue && ReferenceEquals(context, vm.PaymentContext)) ChangeBasketQuantity(line, (int)value);
        RefreshTrustedBasketTotal();
    }

    private Func<string, decimal, bool>? _setBasketPrice;
    public void ConfigureBasketPrice(Func<string, decimal, bool> setBasketPrice)
    {
        ArgumentNullException.ThrowIfNull(setBasketPrice);
        _setBasketPrice = setBasketPrice;
    }

    public bool ChangeBasketPrice(BasketDisplayLine line, decimal price)
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            _trustedTotalReadBusy || _originalSaleLookupBusy || _setBasketPrice is null ||
            DataContext is not CatalogueViewModel vm || !vm.FinancialSessionActive ||
            vm.CheckoutBusy || vm.CheckoutUncertain || vm.PendingOperationId is not null ||
            !vm.BasketLines.Any(current => ReferenceEquals(current, line)) || price < 0m ||
            !_setBasketPrice(line.ArticleId, price)) return false;
        vm.RefreshBasketLines();
        RefreshTrustedBasketTotal();
        CashierBasket.SelectedItem = vm.BasketLines.FirstOrDefault(current => current.ArticleId == line.ArticleId);
        return true;
    }

    private void EditBasketPrice_Click(object sender, RoutedEventArgs args)
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null } vm ||
            (sender as FrameworkElement)?.DataContext is not BasketDisplayLine line || !vm.BasketLines.Any(current => ReferenceEquals(current, line))) return;
        var context = vm.PaymentContext; decimal? requested = null;
        _businessDocumentsOpen = true;
        try
        {
            var dialog = new TouchNumericEntryDialog("Prix libre (PRIX_LIBRE) — " + line.ArticleName, line.UnitPriceMad, decimalPlaces: 2) { Owner = this };
            if (dialog.ShowDialog() == true) requested = dialog.Value;
        }
        finally { _businessDocumentsOpen = false; }
        if (requested is decimal value && ReferenceEquals(context, vm.PaymentContext)) ChangeBasketPrice(line, value);
        RefreshTrustedBasketTotal();
    }

    private void NumericKeypad_Click(object sender, RoutedEventArgs args)
    {
        ShowCashierEntry(forPayment: true);
    }
}
