
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.SqlClient;
using System.IO;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace ADIX.Database
{
    class AzureDatabasse
    {
        private const string CONNECTION_STRING = "server=tcp:adixserver.database.windows.net,1433;Initial Catalog = ADIXDB; Persist Security Info=False;User ID = adixAdmin; Password=Adix$@12354;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout = 30;";
        public static void Initialize()
        {
            using var conn = new SqlConnection(CONNECTION_STRING);
            conn.Open();

            string script = File.ReadAllText("ADIX.sql");
            using var cmd = new SqlCommand(script, conn);
            cmd.ExecuteNonQuery();
        }
}
}
