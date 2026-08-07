using AuthService.Models;

namespace AuthService.Interfaces
{
    public interface IUserAccountService
    {
        Task<List<ApplicationUser>> GetTeachersAsync(
            CancellationToken cancellationToken);

        Task<ApplicationUser?> CreateTeacherAsync(
            string userName,
            string password,
            string displayName,
            CancellationToken cancellationToken);
    }
}
