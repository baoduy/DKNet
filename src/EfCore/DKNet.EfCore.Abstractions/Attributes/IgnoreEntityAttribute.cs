﻿namespace DKNet.EfCore.Abstractions.Attributes;

/// <summary>
///     Specifies that an Entity class should be ignored by the automatic entity mapper.
///     This attribute is primarily intended for use with delivered types where automatic mapping is not desired.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class IgnoreEntityAttribute : Attribute;