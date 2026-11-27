using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Hanouty.Desktop.Wpf;
using Microsoft.VisualBasic.CompilerServices;
using MySql.Data.MySqlClient;

namespace Hanouty.Desktop.Bootstrap;
internal sealed class DailyLogicaSettingsStore(string connectionString, Action? afterCommit = null)
{
    internal sealed record Access(DailyLogicaSettingsStore Owner, string Actor);
    private static readonly string[] Keys = { "MODELE_TICKET", "BRANCHEMENT", "AFFICHER_MODE_P", "AFFICHER_N_TICKET", "AFFICHER_CODE_BARRE", "PIED_TICKET", "FENETRE_MODE_P", "AFFICHEUR_PORT", "BALANCE_PORT", "CODE_WIFI", "GESTION_STOCK", "BLOCAGE_STOCK_INSUFFISANT", "LANGUE", "THEME", "AFFICHER_MODE_V", "HT_TTC", "PIED_DOC", "SOLDER_SANS_TICKET", "COMMENTAIRES_A", "COMMENTAIRES_B", "COMMENTAIRES_C" };
    private MySqlConnection Open()
    {
        var cs = new MySqlConnectionStringBuilder(connectionString);
        if (cs.Server is not ("127.0.0.1" or "localhost" or "::1")) throw new InvalidOperationException("Base locale requise pour les paramÃ¨tres.");
        cs.CharacterSet = "utf8mb4"; var db = new MySqlConnection(cs.ConnectionString); db.Open(); return db;
    }
    private static MySqlCommand Command(MySqlConnection db, MySqlTransaction tx, string sql, params (string Name, object Value)[] values)
    {
        var command = new MySqlCommand(sql, db, tx); foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value); return command;
    }
    private static void RequireTables(MySqlConnection db, MySqlTransaction tx)
    {
        foreach (var table in new[] { "TABLE_PASS", "TABLE_PARAMETRES" })
        {
            using var command = Command(db, tx, "SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME=@table", ("@table", table));
            if (Convert.ToString(command.ExecuteScalar()) != "InnoDB") throw new InvalidOperationException("ParamÃ¨tres transactionnels indisponibles.");
        }
    }
    private static void Admin(MySqlConnection db, MySqlTransaction tx, string actor, string? pin)
    {
        using var command = Command(db, tx, "SELECT CatÃ©gorie,ListPass FROM TABLE_PASS WHERE BINARY ListUser=BINARY @actor FOR UPDATE", ("@actor", actor));
        using var row = command.ExecuteReader();
        if (!row.Read() || row.IsDBNull(0) || row.GetString(0) != "ADMIN" || pin is not null && (string.IsNullOrWhiteSpace(pin) || row.IsDBNull(1) || row.GetString(1) != pin) || row.Read())
            throw new UnauthorizedAccessException("Les paramÃ¨tres LOGICA sont rÃ©servÃ©s Ã  ADMIN avec son PIN actuel.");
    }
    internal Access Authorize(string actor, Func<string> readPin)
    {
        var pin = readPin();
        try
        {
            if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(pin)) throw new UnauthorizedAccessException("Autorisation des paramÃ¨tres annulÃ©e.");
            using var db = Open(); using var tx = db.BeginTransaction(IsolationLevel.ReadCommitted); RequireTables(db, tx); Admin(db, tx, actor, pin);
            tx.Commit(); return new Access(this, actor);
        }
        finally { pin = ""; }
    }
    private void Check(Access access, MySqlConnection db, MySqlTransaction tx, string? pin = null)
    {
        if (!ReferenceEquals(access.Owner, this)) throw new UnauthorizedAccessException("Session des paramÃ¨tres Ã©trangÃ¨re.");
        RequireTables(db, tx); Admin(db, tx, access.Actor, pin);
    }
    private static (DailyLogicaSettingsSnapshot Snapshot, Dictionary<string, string> Raw) Read(MySqlConnection db, MySqlTransaction tx)
    {
        using var command = Command(db, tx, "SELECT OBJET,VALEUR FROM TABLE_PARAMETRES WHERE UPPER(OBJET) IN " +
            "('MODELE_TICKET','BRANCHEMENT','AFFICHER_MODE_P','AFFICHER_N_TICKET','AFFICHER_CODE_BARRE','PIED_TICKET','FENETRE_MODE_P') ORDER BY BINARY OBJET FOR UPDATE");
        var raw = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var rows = command.ExecuteReader())
            while (rows.Read())
            {
                if (rows.IsDBNull(0) || rows.IsDBNull(1) || !Keys.Contains(rows.GetString(0), StringComparer.Ordinal) || !raw.TryAdd(rows.GetString(0), rows.GetString(1)))
                    throw new InvalidOperationException("ParamÃ¨tre LOGICA manquant, ambigu ou mal nommÃ©.");
            }
        if (raw.Count != Keys.Length) throw new InvalidOperationException("Les paramÃ¨tres LOGICA requis doivent exister une seule fois.");
        bool Flag(string key)
        {
            try { return Conversions.ToBoolean(raw[key]); }
            catch (Exception ex) when (ex is InvalidCastException or FormatException) { throw new InvalidOperationException("Valeur boolÃ©enne LOGICA invalide : " + key, ex); }
        }
        string Opt(string k, string def) => raw.TryGetValue(k, out var v) ? v : def;
        bool OptFlag(string k, bool def) { if (!raw.TryGetValue(k, out var v)) return def; try { return Conversions.ToBoolean(v); } catch (Exception ex) when (ex is InvalidCastException or FormatException) { throw new InvalidOperationException("Valeur boolÃ©enne LOGICA invalide : " + k, ex); } }
        var values = new DailyLogicaSettingsValues(raw["MODELE_TICKET"], raw["BRANCHEMENT"], Flag("AFFICHER_MODE_P"), Flag("AFFICHER_N_TICKET"), Flag("AFFICHER_CODE_BARRE"), raw["PIED_TICKET"], Flag("FENETRE_MODE_P"), Opt("AFFICHEUR_PORT", ""), Opt("BALANCE_PORT", ""), Opt("CODE_WIFI", ""), OptFlag("GESTION_STOCK", true), OptFlag("BLOCAGE_STOCK_INSUFFISANT", false), Opt("LANGUE", "FR"), Opt("THEME", "Default"), OptFlag("AFFICHER_MODE_V", true), OptFlag("HT_TTC", false), Opt("PIED_DOC", ""), OptFlag("SOLDER_SANS_TICKET", false), Opt("COMMENTAIRES_A", ""), Opt("COMMENTAIRES_B", ""), Opt("COMMENTAIRES_C", ""));
        using var length = Command(db, tx, "SELECT CHARACTER_MAXIMUM_LENGTH FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='TABLE_PARAMETRES' AND COLUMN_NAME='VALEUR'");
        var size = length.ExecuteScalar(); if (size is null or DBNull || Convert.ToInt64(size) <= 0) throw new InvalidOperationException("Format du paramÃ¨tre VALEUR indisponible.");
        var max = (int)Math.Min(int.MaxValue, Convert.ToInt64(size));
        var revision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(Keys.Select(key => new[] { key, raw[key] })))));
        return (new(values, revision, max), raw);
    }
    internal DailyLogicaSettingsSnapshot Read(Access access)
    {
        using var db = Open(); using var tx = db.BeginTransaction(IsolationLevel.RepeatableRead); Check(access, db, tx); var current = Read(db, tx).Snapshot; tx.Commit(); return current;
    }
    private static Dictionary<string, string> Values(DailyLogicaSettingsValues value) => new(StringComparer.Ordinal)
    {
        ["MODELE_TICKET"] = value.Template, ["BRANCHEMENT"] = value.Connection, ["AFFICHER_MODE_P"] = Conversions.ToString(value.ShowPayment),
        ["AFFICHER_N_TICKET"] = Conversions.ToString(value.ShowTicketNumber), ["AFFICHER_CODE_BARRE"] = Conversions.ToString(value.ShowQr), ["PIED_TICKET"] = value.Footer, ["FENETRE_MODE_P"] = Conversions.ToString(value.PaymentWindow)
    };
    internal DailyLogicaSettingsSnapshot Save(Access access, DailyLogicaSettingsSnapshot expected, DailyLogicaSettingsValues desired, string pin)
    {
        var attempted = false;
        try
        {
            using var db = Open(); using var tx = db.BeginTransaction(IsolationLevel.Serializable); Check(access, db, tx, pin);
            var (current, raw) = Read(db, tx);
            if (desired.Template is null || desired.Connection is null || desired.Footer is null || desired.Footer.Length > current.MaximumValueLength ||
                desired.Footer.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
                throw new ArgumentException("Pied de ticket trop long ou contenant un caractÃ¨re non imprimable.");
            if (desired.Template != current.Values.Template && desired.Template != "FR_3C" || desired.Connection != current.Values.Connection && desired.Connection != "USB")
                throw new NotSupportedException("Seul le modÃ¨le FR_3C par USB peut Ãªtre nouvellement sÃ©lectionnÃ© ici. Les autres routes restent Ã  raccorder.");
            // Deterministic setters have no financial side effect. If a previous
            // acknowledgement was lost, the already desired state is verified
            // read-only instead of issuing duplicate writes or overwriting a later edit.
            if (current.Values == desired) { tx.Commit(); return current; }
            if (current.Revision != expected.Revision || current.Values != expected.Values)
                throw new InvalidOperationException("Les paramÃ¨tres ont changÃ©. Relisez-les avant de modifier.");
            var oldValues = Values(current.Values); var nextValues = Values(desired);
            var expectedRaw = new Dictionary<string, string>(raw, StringComparer.Ordinal);
            foreach (var key in Keys.Where(key => oldValues[key] != nextValues[key]))
            {
                expectedRaw[key] = nextValues[key];
                using var update = Command(db, tx, "UPDATE TABLE_PARAMETRES SET VALEUR=@value WHERE BINARY OBJET=BINARY @key", ("@value", nextValues[key]), ("@key", key));
                if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Ã‰criture du paramÃ¨tre non confirmÃ©e : " + key);
            }
            var (verified, verifiedRaw) = Read(db, tx);
            if (verified.Values != desired || Keys.Any(key => !string.Equals(verifiedRaw[key], expectedRaw[key], StringComparison.Ordinal)))
                throw new InvalidOperationException("La relecture des paramÃ¨tres diffÃ¨re de la modification demandÃ©e.");
            attempted = true; tx.Commit(); afterCommit?.Invoke(); return verified;
        }
        catch (Exception ex) when (!attempted && ex is not OutOfMemoryException and not StackOverflowException)
        { throw new DailyLogicaSettingsRefusedException(ex.Message, ex); }
    }
}

