using Microsoft.Data.Sqlite;
using System.IO;

class Database
{
    private const string DATABASE_NAME = "ADIX.db";

    public static void Initialize()
    {

         

        using var conn = new SqliteConnection($"Data Source={DATABASE_NAME}");
        conn.Open();

        string script = File.ReadAllText("C:\\Users\\explo\\OneDrive\\Desktop\\XBCIS\\Project\\WorkIntegrated\\ADIX\\ADIX\\Database\\ADIX.sql");
        using var cmd = new SqliteCommand(script, conn);
        cmd.ExecuteNonQuery();

       
        
    }
    
}