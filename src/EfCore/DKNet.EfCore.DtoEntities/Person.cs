using System.Diagnostics.CodeAnalysis;
using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities;

[ExcludeFromCodeCoverage]
public sealed class Person : IEntity<Guid>
{
    #region Constructors

    public Person(string firstName, string middleName, string lastName, int age)
    {
        this.Id = Guid.NewGuid();
        this.FirstName = firstName;
        this.MiddleName = middleName;
        this.LastName = lastName;
        this.Age = age;
        this.CreatedUtc = DateTime.UtcNow;
    }

    private Person()
    {
        this.Id = Guid.Empty;
        this.FirstName = string.Empty;
        this.MiddleName = null;
        this.LastName = string.Empty;
    }

    #endregion

    #region Properties

    public DateTime CreatedUtc { get; private set; }

    public Guid Id { get; private set; }

    public int Age { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string? MiddleName { get; private set; }

    #endregion
}