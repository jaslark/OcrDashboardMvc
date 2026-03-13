using PetaPoco;
using System;

namespace OcrDashboardMvc.Models
{
    /// <summary>
    /// Base class cho các model s? d?ng PetaPoco
/// </summary>
    /// <typeparam name="T">Model type</typeparam>
    public class DocProTrungGianDataContext<T> where T : class
    {
    private static IDatabase? _database;

        /// <summary>
        /// L?y instance c?a database connection
        /// </summary>
        public static IDatabase GetDatabase(string connectionString)
        {
      if (_database == null)
        {
                _database = new PetaPoco.Database(connectionString, "Npgsql");
            }
            return _database;
        }

        /// <summary>
        /// L?y instance c?a database connection t? IServiceProvider
      /// </summary>
        public static IDatabase GetDatabase(IServiceProvider serviceProvider)
   {
          var database = serviceProvider.GetService(typeof(IDatabase)) as IDatabase;
        if (database == null)
     {
             throw new InvalidOperationException("Database service not registered. Please register IDatabase in Program.cs");
      }
            return database;
        }
    }
}
