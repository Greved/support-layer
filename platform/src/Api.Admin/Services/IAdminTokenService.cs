using Core.Entities;

namespace Api.Admin.Services;

public interface IAdminTokenService
{
    string IssueAdminToken(AdminUser admin);
    string IssueImpersonationToken(Tenant tenant, Guid tenantAdminUserId, string email, string roleSlug, Guid impersonatingAdminId);
}
