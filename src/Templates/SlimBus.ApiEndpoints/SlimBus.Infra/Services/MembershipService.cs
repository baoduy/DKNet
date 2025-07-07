namespace SlimBus.Infra.Services;

internal sealed class MembershipService(DbContext dbContext) : SequenceService(dbContext, Sequences.Membership), IMembershipService;