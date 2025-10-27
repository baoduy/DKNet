using System.Net;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shouldly;

namespace DKNet.AspCore.SlimBus.Tests;

public class ProblemDetailsExtensionsModelStateTests
{
    #region Methods

    [Fact]
    public void ToProblemDetails_ModelState_Invalid_ReturnsProblemDetails()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("field1", "error1");
        modelState.AddModelError("field2", "error2");
        var result = modelState.ToProblemDetails();
        result.ShouldNotBeNull();
        result!.Status.ShouldBe((int)HttpStatusCode.BadRequest);
        result.Title.ShouldBe("Error");
        ((IEnumerable<string>)result.Extensions["errors"]!).Count().ShouldBe(2);
    }

    [Fact]
    public void ToProblemDetails_ModelState_Valid_ReturnsNull()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("key", string.Empty); // Add then clear to make valid
        modelState.Clear();
        modelState.IsValid.ShouldBeTrue();
        var result = modelState.ToProblemDetails();
        result.ShouldBeNull();
    }

    #endregion
}