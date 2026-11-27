using System;
using System.Windows;
using Hanouty.Desktop.Wpf.Presentation;

namespace Hanouty.Desktop.Wpf;

public partial class App : Application
{
    // Legacy diagnostic seam only; production startup must not fall back to this registration.
    public static Func<(ICatalogueReader Catalogue, IProductSelection Checkout, ICheckoutSubmission Submission)>? ResolveServices { get; set; }

    // Architecture-owned CreateFinancial host registers this existing WPF startup
    // seam with its trusted cashier/payment context. PIN stays inside Checkout
    // preparation callback; never pass or retain it in WPF.
    public static Func<(ICatalogueReader Catalogue, IProductSelection Checkout,
        ICheckoutSubmission Submission, CashierPaymentContext Payment)>? ResolveFinancialServices { get; set; }

    public static bool HasAuthorizedFinancialRegistration(ICatalogueReader? catalogue,
        IProductSelection? checkout, ICheckoutSubmission? submission, CashierPaymentContext? payment) =>
        catalogue is not null && checkout is not null &&
        submission is IContextualCheckoutSubmission &&
        submission is ICheckoutOutcomeReconciliation &&
        payment?.IsWellFormed == true &&
        string.Equals(payment.TenderCode, "Cash", StringComparison.OrdinalIgnoreCase);

    // Allows Windows STA tests to assert that the diagnostic resolver cannot
    // unlock a production process, without displaying a modal startup dialog.
    public static bool HasProductionStartupResolver => ResolveFinancialServices is not null;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        try
        {
            ICatalogueReader? catalogue = null;
            IProductSelection? checkout = null;
            ICheckoutSubmission? submission = null;
            CashierPaymentContext? payment = null;

            if (HasProductionStartupResolver)
            {
                var services = ResolveFinancialServices?.Invoke();
                if (services is { } financial &&
                    HasAuthorizedFinancialRegistration(financial.Catalogue, financial.Checkout,
                        financial.Submission, financial.Payment))
                {
                    catalogue = financial.Catalogue;
                    checkout = financial.Checkout;
                    submission = financial.Submission;
                    payment = financial.Payment;
                }
            }

            if (catalogue is null || checkout is null || submission is null)
            {
                MainWindow = new MainWindow();
            }
            else
            {
                MainWindow = new MainWindow(catalogue, checkout, submission, payment!, requireFreshPaymentContext: true);
            }
            MainWindow.WindowState = WindowState.Maximized;
            MainWindow.Loaded += (_, _) => { if (MainWindow is MainWindow mw) mw.ShowPinLogin(); };
            MainWindow.Show();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            try
            {
                MainWindow = new MainWindow();
                MainWindow.WindowState = WindowState.Maximized;
                MainWindow.Loaded += (_, _) => { if (MainWindow is MainWindow mw) mw.ShowPinLogin(); };
                MainWindow.Show();
            }
            catch
            {
                MessageBox.Show("Unable to start authorized local services: " + ex.Message,
                    "Hanouty — startup blocked", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(2);
            }
        }
    }
}
