using System.Windows;
using Hanouty.Desktop.Wpf.Presentation;

namespace Hanouty.Desktop.Wpf;

public partial class MainWindow
{
    private Action? _openBusinessDocuments;
    private bool _businessDocumentsOpen;
    public void ConfigureBusinessDocuments(Action open) => _openBusinessDocuments = open;
    public bool ShowBusinessDocuments()
    {
        if (_businessDocumentsOpen || _customerFilesOpen || _manualDiscountOpen || _alternatePaymentOpen || _cashierEntryOpen || _reviewOpen || _logoutOpen || _pinOpen ||
            DataContext is not CatalogueViewModel { FinancialSessionActive: true, CheckoutBusy: false, CheckoutUncertain: false, PendingOperationId: null } ||
            _openBusinessDocuments is null) return false;
        _businessDocumentsOpen = true;
        try { _openBusinessDocuments(); return true; }
        finally { _businessDocumentsOpen = false; RefreshTrustedBasketTotal(); }
    }
    private void OpenBusinessDocuments_Click(object sender, RoutedEventArgs args) => ShowBusinessDocuments();

    private Action? _openDevis;
    private Action? _openBL;
    private Action? _openFacture;
    private Action? _openAvoir;

    public void ConfigureDevis(Action open) => _openDevis = open;
    public void ConfigureBL(Action open) => _openBL = open;
    public void ConfigureFacture(Action open) => _openFacture = open;
    public void ConfigureAvoir(Action open) => _openAvoir = open;

    public bool OpenDevisWorkflow()
    {
        if (!VerifyTablePass(TablePassRole.Supervisor, "Devis") || _openDevis is null) return false;
        _openDevis();
        return true;
    }

    public bool OpenBLWorkflow()
    {
        if (!VerifyTablePass(TablePassRole.Supervisor, "BL") || _openBL is null) return false;
        _openBL();
        return true;
    }

    public bool OpenFactureWorkflow()
    {
        if (!VerifyTablePass(TablePassRole.Supervisor, "Facture") || _openFacture is null) return false;
        _openFacture();
        return true;
    }

    public bool OpenAvoirWorkflow()
    {
        if (!VerifyTablePass(TablePassRole.Supervisor, "Avoir") || _openAvoir is null) return false;
        _openAvoir();
        return true;
    }

    private void OpenDevis_Click(object sender, RoutedEventArgs e) => OpenDevisWorkflow();
    private void OpenBL_Click(object sender, RoutedEventArgs e) => OpenBLWorkflow();
    private void OpenFacture_Click(object sender, RoutedEventArgs e) => OpenFactureWorkflow();
    private void OpenAvoir_Click(object sender, RoutedEventArgs e) => OpenAvoirWorkflow();
}
