using DKNet.EfCore.DtoGenerator;
using SlimBus.Generators.Tests.Domain.Catalog;

namespace SlimBus.Generators.Tests.Api;

/// <summary>DTO fixture driving the CRUD generator's DTO resolution for <see cref="Gadget" />.</summary>
[GenerateDto(typeof(Gadget))]
public partial record GadgetDto;
