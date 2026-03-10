using PetaPoco;

namespace OcrDashboardMvc.Services
{
    public interface IDatabaseService
    {
        IDatabase GetDatabase();
    }

    public class DatabaseService : IDatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
    _connectionString = configuration.GetConnectionString("TrungGianConnectionString") 
    ?? throw new InvalidOperationException("Connection string 'TrungGianConnectionString' not found.");
        }

        public IDatabase GetDatabase()
        {
            return new Database(_connectionString, "Npgsql");
     }
}
}
