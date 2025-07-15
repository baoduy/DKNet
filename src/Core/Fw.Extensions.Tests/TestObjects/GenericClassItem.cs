namespace Fw.Extensions.Tests.TestObjects;

public interface IGenericClassItem<T> where T : class;

public abstract class GenericClassItem<T> : IGenericClassItem<T> where T : class;

public sealed class Implemented : GenericClassItem<TestItem>;