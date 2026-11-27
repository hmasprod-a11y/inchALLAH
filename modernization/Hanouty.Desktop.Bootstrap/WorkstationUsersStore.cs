using System.Data;
using MySql.Data.MySqlClient;

namespace Hanouty.Desktop.Bootstrap;

internal sealed record WorkstationUserRow(string User, string Role, Dictionary<string, string> Permissions);

internal sealed class WorkstationUsersStore(string connectionString, Func<string?> authenticatedActor, Func<string?> readSessionPin)
{
    private static readonly string[] KnownPermissionColumns =
        ["MODIF_QTE", "ANNULATION", "PRIX_LIBRE", "REMISE", "ARTICLES", "CLOTURE", "DEPENSES", "STATISTIQUES", "PARAMETRES"];

    private MySqlConnection Open()
    {
        var cs = new MySqlConnectionStringBuilder(connectionString) { CharacterSet = "utf8mb4" };
        if (cs.Server is not ("127.0.0.1" or "localhost" or "::1")) throw new InvalidOperationException("Base locale requise.");
        var db = new MySqlConnection(cs.ConnectionString);
        db.Open();
        return db;
    }

    private static MySqlCommand Command(MySqlConnection db, MySqlTransaction tx, string sql, params (string Key, object Value)[] args)
    {
        var cmd = new MySqlCommand(sql, db, tx);
        foreach (var arg in args) cmd.Parameters.AddWithValue(arg.Key, arg.Value);
        return cmd;
    }

    private string RequireAdmin(MySqlConnection db, MySqlTransaction tx)
    {
        var actor = authenticatedActor();
        if (string.IsNullOrWhiteSpace(actor)) throw new UnauthorizedAccessException("Session ADMIN requise.");
        using var engine = Command(db, tx, "SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='TABLE_PASS'");
        if (Convert.ToString(engine.ExecuteScalar()) != "InnoDB") throw new InvalidOperationException("TABLE_PASS indisponible.");
        using var cmd = Command(db, tx, "SELECT ListUser,Catégorie,ListPass FROM TABLE_PASS WHERE BINARY ListUser=BINARY @actor FOR UPDATE", ("@actor", actor));
        var pin = readSessionPin();
        try
        {
            using var rows = cmd.ExecuteReader();
            if (!rows.Read() || rows.IsDBNull(0) || rows.IsDBNull(1) || rows.GetString(1) != "ADMIN" ||
                rows.IsDBNull(2) || string.IsNullOrEmpty(pin) || rows.GetString(2) != pin || rows.Read())
                throw new UnauthorizedAccessException("Gestion des utilisateurs réservée à ADMIN.");
            return actor;
        }
        finally { pin = null; }
    }

    private static List<string> GetPermissionColumns(MySqlConnection db, MySqlTransaction tx)
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var cmd = Command(db, tx, "SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='TABLE_PASS'"))
        using (var r = cmd.ExecuteReader())
            while (r.Read()) if (!r.IsDBNull(0)) existing.Add(r.GetString(0));
        return KnownPermissionColumns.Where(c => existing.Contains(c)).ToList();
    }

    internal (List<WorkstationUserRow> Users, List<string> PermissionColumns) Load()
    {
        using var db = Open();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        RequireAdmin(db, tx);
        var permCols = GetPermissionColumns(db, tx);
        var selectCols = permCols.Count == 0 ? "ListUser,Catégorie" : "ListUser,Catégorie," + string.Join(",", permCols);
        var list = new List<WorkstationUserRow>();
        using (var cmd = Command(db, tx, $"$SELECT {selectCols} FROM TABLE_PASS ORDER BY BINARY ListUser FOR UPDATE"))
        using (var r = cmd.ExecuteReader())
        {
            while (r.Read())
            {
                if (r.IsDBNull(0)) continue;
                var user = r.GetString(0);
                var role = r.IsDBNull(1) ? "UTILISATEUR" : r.GetString(1);
                var perms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < permCols.Count; i++)
                    perms[permCols[i]] = r.IsDBNull(i + 2) || string.IsNullOrWhiteSpace(r.GetString(i + 2)) ? "VISIBLE" : r.GetString(i + 2);
                list.Add(new WorkstationUserRow(user, role, perms));
            }
        }
        tx.Commit();
        return (list, permCols);
    }

    internal void SaveUser(string originalUser, string userName, string? newPin, string role, IReadOnlyDictionary<string, string> permissions, bool isNew)
    {
        userName = (userName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(userName) || userName.Length > 64 || userName.Any(char.IsControl))
            throw new ArgumentException("Nom d'utilisateur invalide.");
        if (role is not ("ADMIN" or "MANAGER" or "UTILISATEUR"))
            throw new ArgumentException("Catégorie invalide.");
        if (isNew && (string.IsNullOrWhiteSpace(newPin) || newPin.Length > 64 || newPin.Any(char.IsControl)))
            throw new ArgumentException("Code PIN requis.");
        if (!string.IsNullOrEmpty(newPin) && (newPin.Length > 64 || newPin.Any(char.IsControl)))
            throw new ArgumentException("Code PIN invalide.");

        using var db = Open();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        RequireAdmin(db, tx);
        var permCols = GetPermissionColumns(db, tx);

        if (!string.IsNullOrEmpty(newPin))
        {
            using var dupPin = Command(db, tx, isNew
                ? "SELECT COUNT(*) FROM TABLE_PASS WHERE BINARY ListPass=BINARY @pin"
                : "SELECT COUNT(*) FROM TABLE_PASS WHERE BINARY ListPass=BINARY @pin AND BINARY ListUser<>BINARY @orig",
                ("@pin", newPin), ("@orig", originalUser));
            if (Convert.ToInt32(dupPin.ExecuteScalar()) > 0)
                throw new InvalidOperationException("Ce code PIN est déjà utilisé.");
        }

        if (isNew)
        {
            using var exists = Command(db, tx, "SELECT COUNT(*) FROM TABLE_PASS WHERE BINARY ListUser=BINARY @u", ("@u", userName));
            if (Convert.ToInt32(exists.ExecuteScalar()) > 0)
                throw new InvalidOperationException("Cet utilisateur existe déjà.");
            var cols = new List<string> { "ListUser", "ListPass", "Catégorie" };
            var vals = new List<string> { "@u", "@p", "@r" };
            var args = new List<(string, object)> { ("@u", userName), ("@p", newPin!), ("@r", role) };
            foreach (var col in permCols)
            {
                var v = permissions.TryGetValue(col, out var pv) ? pv : "VISIBLE";
                cols.Add(col);
                vals.Add("@perm_" + col);
                args.Add(("@perm_" + col, v));
            }
            using var ins = Command(db, tx, $"INSERT INTO TABLE_PASS ({string.Join(",", cols)}) VALUES ({string.Join(",", vals)})", args.ToArray());
            if (ins.ExecuteNonQuery() != 1) throw new InvalidOperationException("Création non confirmée.");
        }
        else
        {
            if (role != "ADMIN")
            {
                using var adminCount = Command(db, tx, "SELECT COUNT(*) FROM TABLE_PASS WHERE BINARY Catégorie='ADMIN' AND BINARY ListUser<>BINARY @orig", ("@orig", originalUser));
                if (Convert.ToInt32(adminCount.ExecuteScalar()) < 1)
                    throw new InvalidOperationException("Impossible de retirer le rôle ADMIN au dernier administrateur.");
            }
            var sets = new List<string> { "ListUser=@u", "Catégorie=@r" };
            var args = new List<(string, object)> { ("@u", userName), ("@r", role), ("@orig", originalUser) };
            if (!string.IsNullOrEmpty(newPin)) { sets.Add("ListPass=@p"); args.Add(("@p", newPin)); }
            foreach (var col in permCols)
            {
                var v = permissions.TryGetValue(col, out var pv) ? pv : "VISIBLE";
                sets.Add(col + "=@perm_" + col);
                args.Add(("@perm_" + col, v ));
            }
            using var upd = Command(db, tx, "UPDATE TABLE_PASS SET " + string.Join(",", sets) + " WHERE BINARY ListUser=BINARY @orig", args.ToArray());
            if (upd.ExecuteNonQuery() != 1) throw new InvalidOperationException("Modification non confirmée.");
        }
        tx.Commit();
    }

    internal void DeleteUser(string userName)
    {
        using var db = Open();
        using var tx = db.BeginTransaction(IsolationLevel.Serializable);
        var actor = RequireAdmin(db, tx);
        if (string.Equals(actor, userName, StringComparison.Ordinal))
            throw new InvalidOperationException("Impossible de supprimer la session ADMIN en cours.");
        using var adminCount = Command(db, tx, "SELECT COUNT(*) FROM TABLE_PASS WHERE BINARY Catégorie='ADMIN' AND BINARY ListUser<>BINARY @u", ("@u", userName));
        if (Convert.ToInt32(adminCount.ExecuteScalar()) < 1)
            throw new InvalidOperationException("Impossible de supprimer le dernier administrateur.");
        using var del = Command(db, tx, "DELETE FROM TABLE_PASS WHERE BINARY ListUser=BINARY @u", ("@u", userName));
        if (del.ExecuteNonQuery() != 1) throw new InvalidOperationException("Suppression non confirmée.");
        tx.Commit();
    }
}
