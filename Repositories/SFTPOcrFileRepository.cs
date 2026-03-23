using OcrDashboardMvc.Models;
using PetaPoco;

namespace OcrDashboardMvc.Repositories
{
    public interface ISFTPOcrFileRepository
    {
        Task<List<SFTPOcrFile>> GetAllAsync();
        Task<SFTPOcrFile?> GetByIdAsync(long id);
        Task<Page<SFTPOcrFile>> GetPagedAsync(long page, long itemsPerPage);
        Task<long> InsertAsync(SFTPOcrFile file);
        Task<int> UpdateAsync(SFTPOcrFile file);
        Task<int> DeleteAsync(long id);
        Task<List<SFTPOcrFile>> GetByStatusAsync(int status);
    }

    public class SFTPOcrFileRepository : ISFTPOcrFileRepository
    {
        private readonly IDatabase _database;

        public SFTPOcrFileRepository(IDatabase database)
        {
            _database = database;
        }

        public async Task<List<SFTPOcrFile>> GetAllAsync()
        {
            return await _database.FetchAsync<SFTPOcrFile>("SELECT * FROM ocr_clos.ocr_requests"); //ocr_clos.sftpocrfile
        }

        public async Task<SFTPOcrFile?> GetByIdAsync(long id)
        {
            return await _database.SingleOrDefaultAsync<SFTPOcrFile>("SELECT * FROM ocr_clos.ocr_requests WHERE id = @0", id);
        }

        public async Task<Page<SFTPOcrFile>> GetPagedAsync(long page, long itemsPerPage)
        {
            return await _database.PageAsync<SFTPOcrFile>(page, itemsPerPage, "SELECT * FROM ocr_clos.ocr_requests ORDER BY id DESC");
        }

        public async Task<long> InsertAsync(SFTPOcrFile file)
        {
            return (long)await _database.InsertAsync(file);
        }

        public async Task<int> UpdateAsync(SFTPOcrFile file)
        {
            return await _database.UpdateAsync(file);
        }

        public async Task<int> DeleteAsync(long id)
        {
            return await _database.DeleteAsync<SFTPOcrFile>(id);
        }

        public async Task<List<SFTPOcrFile>> GetByStatusAsync(int status)
        {
            return await _database.FetchAsync<SFTPOcrFile>("SELECT * FROM ocr_clos.ocr_requests WHERE status = @0", status);
        }
    }
}
