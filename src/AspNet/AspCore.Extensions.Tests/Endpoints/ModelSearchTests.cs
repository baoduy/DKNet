using DKNet.AspCore.Extensions.Endpoints;

namespace AspCore.Extensions.Tests.Endpoints;

/// <summary>
///     Covers <see cref="ModelSearch" />'s discovery walk — which fields a free-text search reaches and which it
///     must not. Exercised directly rather than only through HTTP because the walk is reflection over a type
///     graph: the cases that matter (a dictionary, a blob, a field the model hides, a field one hop too deep)
///     need purpose-built shapes to express, and getting any of them wrong either leaks a column the projection
///     deliberately omits or emits SQL no provider can translate.
/// </summary>
public class ModelSearchTests
{
    #region Methods

    [Fact]
    public void Clauses_TextFieldOnBothModelAndEntity_IsSearched()
    {
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldContain("Title != null && Title.Contains(@0)");
    }

    [Fact]
    public void Clauses_NestedTextField_IsSearchedThroughItsPath()
    {
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldContain("Maker.Name != null && Maker.Name.Contains(@0)");
    }

    [Fact]
    public void Clauses_TextFieldBehindACollection_IsWrappedInAny()
    {
        // Inside Any(...) the lambda parameter is implicit, so the inner path must restart from the element —
        // "Tags.Any(Tags.Label...)" would not parse.
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldContain("Tags.Any(Label != null && Label.Contains(@0))");
    }

    [Fact]
    public void Clauses_FieldTheModelDoesNotExpose_IsNotSearched()
    {
        // Secret exists on the entity only. Searching it would let a caller probe a column the projection
        // deliberately withholds — the whole reason discovery walks the model rather than the entity.
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldNotContain(clause => clause.Contains("Secret", StringComparison.Ordinal));

        // Same for a nested field the nested model omits.
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldNotContain(clause => clause.Contains("Country", StringComparison.Ordinal));
    }

    [Fact]
    public void Clauses_FieldTheEntityCannotProvide_IsNotSearched()
    {
        // ModelOnly is a computed/derived field with no entity counterpart: nothing to translate to SQL.
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldNotContain(clause => clause.Contains("ModelOnly", StringComparison.Ordinal));
    }

    [Fact]
    public void Clauses_NonTextFields_AreNotSearched()
    {
        // "Contains" is meaningless for an id or a count, and a caller wanting an exact match has 'filter'.
        var clauses = ModelSearch.Clauses<ThingModel, ThingEntity>();

        clauses.ShouldNotContain(clause => clause.Contains("Id", StringComparison.Ordinal));
        clauses.ShouldNotContain(clause => clause.Contains("Count", StringComparison.Ordinal));
    }

    [Fact]
    public void Clauses_DictionaryAndBlobFields_AreNotSearched()
    {
        // A dictionary is usually a serialized column no provider can translate an Any() over, and generating
        // one anyway turns every search into a runtime translation failure rather than a bad result.
        var clauses = ModelSearch.Clauses<ThingModel, ThingEntity>();

        clauses.ShouldNotContain(clause => clause.Contains("Meta", StringComparison.Ordinal));
        clauses.ShouldNotContain(clause => clause.Contains("Blob", StringComparison.Ordinal));
    }

    [Fact]
    public void Clauses_TextFieldMoreThanTwoHopsDeep_IsNotSearched()
    {
        // Maker.Address.City is one hop past the cap. The cap is what stops a model whose graph loops back on
        // itself from expanding forever, and keeps the generated OR to a sane width.
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldNotContain(clause => clause.Contains("City", StringComparison.Ordinal));
    }

    [Fact]
    public void Clauses_ModelWithNoTextField_IsEmpty()
    {
        // The endpoint turns this into a predicate that matches nothing, so a search over an untextual model
        // answers with an empty page rather than every row.
        ModelSearch.Clauses<NumbersOnlyModel, ThingEntity>().ShouldBeEmpty();
    }

    [Fact]
    public void Clauses_SameModelAndEntityTwice_ReturnsTheCachedResult()
    {
        ModelSearch.Clauses<ThingModel, ThingEntity>()
            .ShouldBeSameAs(ModelSearch.Clauses<ThingModel, ThingEntity>());
    }

    #endregion

    // --- Shapes under test: an entity/model pair covering every branch of the walk ------------------------

    private sealed class AddressEntity
    {
        public string City { get; set; } = string.Empty;
    }

    private sealed class AddressModel
    {
        public string City { get; init; } = string.Empty;
    }

    private sealed class MakerEntity
    {
        public string Name { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public AddressEntity Address { get; set; } = new();
    }

    private sealed class MakerModel
    {
        public string Name { get; init; } = string.Empty;

        public AddressModel Address { get; init; } = new();
    }

    private sealed class TagEntity
    {
        public string Label { get; set; } = string.Empty;
    }

    private sealed class TagModel
    {
        public string Label { get; init; } = string.Empty;
    }

    private sealed class ThingEntity
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public int Count { get; set; }

        public byte[] Blob { get; set; } = [];

        public IDictionary<string, string> Meta { get; set; } = new Dictionary<string, string>();

        public MakerEntity Maker { get; set; } = new();

        public ICollection<TagEntity> Tags { get; set; } = [];

        public string Secret { get; set; } = string.Empty;
    }

    private sealed class ThingModel
    {
        public Guid Id { get; init; }

        public string Title { get; init; } = string.Empty;

        public int Count { get; init; }

        public byte[] Blob { get; init; } = [];

        public IDictionary<string, string> Meta { get; init; } = new Dictionary<string, string>();

        public MakerModel Maker { get; init; } = new();

        public ICollection<TagModel> Tags { get; init; } = [];

        public string ModelOnly { get; init; } = string.Empty;
    }

    private sealed class NumbersOnlyModel
    {
        public Guid Id { get; init; }

        public int Count { get; init; }
    }
}
