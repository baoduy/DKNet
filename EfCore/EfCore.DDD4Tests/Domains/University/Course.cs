using EfCore.DDD4Tests.Abstracts;
using EfCore.DDD4Tests.Events;

namespace EfCore.DDD4Tests.Domains.University;

public class Course(string name, string ownedBy) : AggregateRoot(Guid.Empty, ownedBy, "Unit test")
{
    public string Name { get; private set; } = name;

    public string Status { get; private set; }

    public void Start()
    {
        Status = "Started";
        AddEvent(new CourseStartedEvent());
        AddEvent(new CourseStatusChangedEvent());
    }
}