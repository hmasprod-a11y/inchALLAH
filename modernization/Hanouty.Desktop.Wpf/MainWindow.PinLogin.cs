namespace Hanouty.Desktop.Wpf;

public enum TablePassRole
{
    Cashier = 1,
    Supervisor = 2,
    Manager = 3,
    Administrator = 4
}

public partial class MainWindow
{
    private Func<string, bool>? _pinLogin;
    private Func<string, TablePassRole>? _tablePassAuth;

    public void ConfigurePinLogin(Func<string, bool> authenticate) => _pinLogin = authenticate;
    public void ConfigureTablePassAuth(Func<string, TablePassRole> auth) => _tablePassAuth = auth;

    public bool VerifyTablePass(TablePassRole minimumRole, string actionName)
    {
        var pin = ReadPin($"Autorisation requise : {actionName}", $"Saisissez le code d’accès requis ({minimumRole}) :", "");
        if (pin.Length == 0) return false;

        if (_tablePassAuth is not null)
        {
            var role = _tablePassAuth(pin);
            return role >= minimumRole;
        }

        return _pinLogin?.Invoke(pin) == true;
    }

    public void SetLoginFeedback(string message)
    {
        var identity = ReadSessionIdentity();
        CashierSessionLabel.Text = (identity is null ? "Session fermée" : $"Caisse {identity.RegisterId} · {identity.CashierId}") + " · " + message;
        CashierSessionLabel.ToolTip = message;
    }
    private bool SignInWithPin()
    {
        _entryPinCashier = "";
        var pin = "";
        try
        {
            pin = ReadPin("Connexion à la caisse", "Saisissez votre code d’accès.", "");
            return pin.Length != 0 && _pinLogin?.Invoke(pin) == true;
        }
        finally { pin = ""; _entryPinCashier = null; }
    }
}

