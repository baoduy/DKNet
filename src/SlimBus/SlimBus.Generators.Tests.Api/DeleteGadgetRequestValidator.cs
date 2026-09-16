using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace SlimBus.Generators.Tests.Api;

/// <summary>
///     DRK-1326 §5 scenario 2 fixture: refuses to delete a <c>Gadget</c> that still has <c>Widget</c> rows
///     referencing it (this project's analogue of "an account group holds accounts"). Registered for DI as a
///     plain <see cref="IValidator{T}" />; only the route groups in <see cref="GadgetTestHost" /> that call
///     <c>AddFluentValidationAutoValidation()</c> ever consult it.
/// </summary>
public sealed class DeleteGadgetRequestValidator : AbstractValidator<DeleteGadgetRequest>
{
    public DeleteGadgetRequestValidator(GadgetDbContext db)
    {
        RuleFor(x => x.Id)
            .MustAsync((id, cancellationToken) => IsEmptyAsync(db, id, cancellationToken))
            .WithMessage("Gadget still has widgets and cannot be deleted.");
    }

    private static async Task<bool> IsEmptyAsync(GadgetDbContext db, Guid gadgetId, CancellationToken cancellationToken) =>
        !await db.Widgets.AnyAsync(w => w.GadgetId == gadgetId, cancellationToken);
}
