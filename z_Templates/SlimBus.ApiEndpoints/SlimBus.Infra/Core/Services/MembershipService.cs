namespace SlimBus.Infra.Core.Services;

internal sealed class MembershipService(DbContext dbContext) : SequenceService(dbContext, Sequences.Membership), IMembershipService;