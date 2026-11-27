using System.Windows;
using Hanouty.Desktop.Wpf.Presentation;

namespace Hanouty.Desktop.Wpf;
public partial class MainWindow
{
    private Action? _openCustomerFiles;
    private bool _customerFilesOpen;
    public void ConfigureCustomerFiles(Action open) => _openCustomerFiles = open;
    public bool ShowCustomerFiles()
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _alternatePaymentOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null } ||
            _openCustomerFiles is null) return false;
        _customerFilesOpen = true;
        try { _openCustomerFiles(); return true; }
        finally { _customerFilesOpen = false; RefreshTrustedBasketTotal(); }
    }
    private void OpenCustomerFiles_Click(object sender, RoutedEventArgs args) => ShowCustomerFiles();
    public string PromptCustomerFilesApproval(string role)
    {
        if (role is not ("MANAGER" or "ADMIN") || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null }) return "";
        return ReadPin("Autorisation des fichiers clients", $"Saisissez le PIN du rôle {role}.");
    }

    private Action? _openSupplierAccounts;
    private Action? _openSupplierSettlementDialog;
    private Action? _openCustomerSettlementDialog;

    public void ConfigureSupplierAccounts(Action open) => _openSupplierAccounts = open;
    public void ConfigureSupplierSettlementDialog(Action open) => _openSupplierSettlementDialog = open;
    public void ConfigureCustomerSettlementDialog(Action open) => _openCustomerSettlementDialog = open;

    public bool ShowSupplierAccounts()
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _alternatePaymentOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null } ||
            _openSupplierAccounts is null) return false;
        try { _openSupplierAccounts(); return true; }
        finally { RefreshTrustedBasketTotal(); }
    }

    public bool ShowSupplierSettlementDialog()
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _alternatePaymentOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null }) return false;
        if (_openSupplierSettlements is not null) { _openSupplierSettlements(); return true; }
        if (_openSupplierSettlementDialog is not null) { _openSupplierSettlementDialog(); return true; }
        return false;
    }

    public bool ShowCustomerSettlementDialog()
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _alternatePaymentOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null }) return false;
        if (_openCustomerSettlementDialog is not null) { _openCustomerSettlementDialog(); return true; }
        return false;
    }

    private void OpenSupplierAccounts_Click(object sender, RoutedEventArgs e) => ShowSupplierAccounts();
    private void OpenSupplierSettlementDialog_Click(object sender, RoutedEventArgs e) => ShowSupplierSettlementDialog();
    private void OpenCustomerSettlementDialog_Click(object sender, RoutedEventArgs e) => ShowCustomerSettlementDialog();
}
