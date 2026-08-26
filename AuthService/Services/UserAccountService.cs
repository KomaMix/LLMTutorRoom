using AuthService.Data;
using AuthService.Interfaces;
using AuthService.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AuthService.Services
{
    public sealed class UserAccountService : IUserAccountService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuthDbContext _dbContext;

        public UserAccountService(
            UserManager<ApplicationUser> userManager,
            AuthDbContext dbContext)
        {
            _userManager = userManager;
            _dbContext = dbContext;
        }

        public async Task<List<ApplicationUser>> GetTeachersAsync(
            CancellationToken cancellationToken)
        {
            var teachers = await _userManager.GetUsersInRoleAsync(UserRole.Teacher.ToString());

            return teachers
                .OrderBy(user => user.UserName)
                .ToList();
        }

        public async Task<ApplicationUser?> CreateTeacherAsync(
            string userName,
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            return await CreateUserAsync(
                userName,
                email,
                password,
                UserRole.Teacher,
                cancellationToken);
        }

        public async Task<ApplicationUser?> CreateStudentAsync(
            string userName,
            string email,
            string password,
            CancellationToken cancellationToken)
        {
            return await CreateUserAsync(
                userName,
                email,
                password,
                UserRole.Student,
                cancellationToken);
        }

        private async Task<ApplicationUser?> CreateUserAsync(
            string userName,
            string email,
            string password,
            UserRole role,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var trimmedUserName = userName.Trim();
            var trimmedEmail = email.Trim();
            var userExists = await _userManager.FindByNameAsync(trimmedUserName)
                ?? await _userManager.FindByEmailAsync(trimmedEmail);

            if (userExists is not null)
                return null;

            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(cancellationToken)
                : null;

            var user = new ApplicationUser
            {
                UserName = trimmedUserName,
                Email = trimmedEmail
            };

            IdentityResult createResult;
            try
            {
                createResult = await _userManager.CreateAsync(user, password);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
            {
                return null;
            }

            if (IsDuplicateUser(createResult))
                return null;

            ThrowIfFailed(createResult, $"Create {role.ToString().ToLowerInvariant()}");

            var addRoleResult = await _userManager.AddToRoleAsync(user, role.ToString());
            if (!addRoleResult.Succeeded)
            {
                if (transaction is null)
                    await _userManager.DeleteAsync(user);

                ThrowIfFailed(addRoleResult, $"Add {role.ToString().ToLowerInvariant()} role");
            }

            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);

            return user;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception)
        {
            return exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            };
        }

        private static bool IsDuplicateUser(IdentityResult result)
        {
            return result.Errors.Any(error =>
                error.Code is "DuplicateUserName" or "DuplicateEmail");
        }

        private static void ThrowIfFailed(IdentityResult result, string operation)
        {
            if (result.Succeeded)
                return;

            var errors = string.Join(
                "; ",
                result.Errors.Select(error => $"{error.Code}: {error.Description}"));

            throw new InvalidOperationException(
                $"{operation} failed. Identity errors: {errors}.");
        }
    }
}
