using DKNet.EfCore.Abstractions.Attributes;

namespace EfCore.TestDataLayer;

[StaticData(nameof(EnumStatus))]
public enum EnumStatus
{
    UnKnow = 0,
    Active = 1,
    InActive = 2,
}

[StaticData("EnumStatusOther")]
public enum EnumStatus1
{
    [Display(Name = "AA", Description = "BB")]
    UnKnow = 0,

    Active = 1,
    InActive = 2,
}

[SqlSequence]
public enum SequencesTest
{
    Order,

    [Sequence(typeof(short), FormatString = "T{DateTime:yyMMdd}{1:00000}", IncrementsBy = 1, Max = short.MaxValue)]
    Invoice,

    [Sequence(typeof(long), IncrementsBy = 1, Max = long.MaxValue)]
    Payment,
}