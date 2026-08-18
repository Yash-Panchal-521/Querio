using Mediator;

namespace Querio.Application.Tenants.Members.ListMembers;

public sealed record ListMembersQuery(Guid TenantId) : IQuery<IReadOnlyList<MemberSummary>>;
