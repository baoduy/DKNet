namespace Fw.Extensions.Tests.TestObjects;

public enum HbdTypes
{
    None,
    [Display(Name = "HBD")] DescriptionEnum = 1,

    [Display(Name = "NamedEnum")] NamedEnum = 2,

    Enum = 3
}