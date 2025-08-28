using System.Data.SQLite;
using System.IO;

class Database
{
    private const string DATABASE_NAME = "ADIX.db";

    public static void Initialize()
    {
        using var conn = new SQLiteConnection($"Data Source={DATABASE_NAME};Version=3");
        conn.Open();

        string script = File.ReadAllText("ADIX.SQL");
        using var cmd = new SQLiteCommand(script, conn);
        cmd.ExecuteNonQuery();
    }
    
}