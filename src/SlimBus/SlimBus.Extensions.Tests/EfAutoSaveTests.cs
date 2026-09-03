using System.Reflection;
using DKNet.SlimBus.Extensions.Interceptors;
using FluentResults;
using IMessageBus = SlimMessageBus.IMessageBus;

namespace SlimBus.Extensions.Tests;

public class EfAutoSaveTests(Fixture fixture) : IClassFixture<Fixture>
{
    #region Methods

    [Fact]
    public async Task OnHandle_WriteRequestWithResponse_ShouldAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestRequest { Name = "HBD" });

        rs.IsSuccess.ShouldBeTrue();
        rs.Value.ShouldNotBe(Guid.Empty);
        TestDbContext.Called.ShouldBeTrue();
    }

    [Fact]
    public async Task OnHandle_WriteRequestWithoutResponse_ShouldAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestNoResponseRequest { Name = "HBD" });

        rs.IsSuccess.ShouldBeTrue();
        TestDbContext.Called.ShouldBeTrue();
    }

    [Fact]
    public async Task OnHandle_Query_ShouldNotAutoSave()
    {
        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var id = (await m.Send(new TestRequest { Name = "HBD" })).Value;

        TestDbContext.Called = false;
        var rs = await m.Send(new TestQuery { Id = id });

        rs.ShouldNotBeNull();
        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_QueryReturningNull_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestQuery { Id = Guid.NewGuid() });

        rs.ShouldBeNull();
        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_PagedQuery_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestPageQuery { Id = Guid.NewGuid() });

        rs.ShouldNotBeNull();
        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_RawRequest_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        await m.Send(new TestRawRequest { Name = "HBD" });

        TestDbContext.Called.ShouldBeFalse();
    }

    [Fact]
    public async Task OnHandle_FailedResult_ShouldNotAutoSave()
    {
        TestDbContext.Called = false;

        var m = fixture.ServiceProvider.GetRequiredService<IMessageBus>();
        var rs = await m.Send(new TestFailRequest { Name = "HBD" });

        rs.IsFailed.ShouldBeTrue();
        TestDbContext.Called.ShouldBeFalse();
    }

    [Theory]
    [InlineData(typeof(TestRequest), typeof(Guid), true)]
    [InlineData(typeof(TestNoResponseRequest), typeof(IResultBase), true)]
    [InlineData(typeof(TestQuery), typeof(TestQueryResult), false)]
    [InlineData(typeof(TestRawRequest), typeof(Guid), false)]
    public void IsWrite_ReadRepeatedlyForClosedGenericType_StaysCachedAndCorrect(
        Type requestType, Type responseType, bool expected)
    {
        // IsWrite is a `static readonly` field: the CLR's type initializer computes it exactly once
        // per closed EfAutoSavePostInterceptor<TRequest, TResponse>, no matter how many requests of
        // that type flow through OnHandle. Reading it repeatedly must keep returning that one cached
        // value instead of re-running the GetInterfaces()/Any() reflection walk.
        var interceptorType = typeof(EfAutoSavePostInterceptor<,>).MakeGenericType(requestType, responseType);
        var field = interceptorType.GetField("IsWrite", BindingFlags.NonPublic | BindingFlags.Static);

        field.ShouldNotBeNull();
        field.IsInitOnly.ShouldBeTrue("IsWrite must stay `readonly` so the CLR computes it once per type");

        var first = (bool)field.GetValue(null)!;
        var second = (bool)field.GetValue(null)!;

        first.ShouldBe(expected);
        second.ShouldBe(first);
    }

    #endregion
}
