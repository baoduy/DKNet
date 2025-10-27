using DKNet.EfCore.Abstractions.Entities;

namespace DKNet.EfCore.DtoEntities;

public sealed class Person : IEntity<Guid>
{
    #region Constructors

    public Person(string firstName, string middleName, string lastName, int age)
    {
        Id = Guid.NewGuid();
        FirstName = firstName;
        MiddleName = middleName;
        LastName = lastName;
        Age = age;
        CreatedUtc = DateTime.UtcNow;
    }

    private Person()
    {
        Id = Guid.Empty;
        FirstName = string.Empty;
        MiddleName = null;
        LastName = string.Empty;
    }

    #endregion

    #region Properties

    public int Age { get; private set; }
    public DateTime CreatedUtc { get; private set; }
    public string FirstName { get; private set; }

    public Guid Id { get; private set; }
    public string LastName { get; private set; }
    public string? MiddleName { get; private set; }

    #endregion
}