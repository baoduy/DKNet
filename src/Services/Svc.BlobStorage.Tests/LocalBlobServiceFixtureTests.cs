// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: LocalBlobServiceFixtureTests.cs
// Description: Acceptance tests proving two LocalBlobServiceFixture instances never touch each
// other's files, so the assembly can keep running its test classes in parallel.

using Svc.BlobStorage.Tests.Fixtures;

namespace Svc.BlobStorage.Tests;

public class LocalBlobServiceFixtureTests
{
    [Fact]
    public void TwoFixturesGetDisjointRoots()
    {
        using var fixtureA = new LocalBlobServiceFixture();
        using var fixtureB = new LocalBlobServiceFixture();

        fixtureA.TestRoot.ShouldNotBe(fixtureB.TestRoot);
        Directory.Exists(fixtureA.TestRoot).ShouldBeTrue();
        Directory.Exists(fixtureB.TestRoot).ShouldBeTrue();
    }

    [Fact]
    public async Task SecondFixtureConstructionKeepsFirstFixtureContent()
    {
        using var fixtureA = new LocalBlobServiceFixture();
        var blob = new BlobDetails.BlobData("a-content.txt", new BinaryData("keep-a"u8.ToArray()))
        {
            Overwrite = true,
            Type = BlobTypes.File
        };
        await fixtureA.Service.SaveAsync(blob);

        using var fixtureB = new LocalBlobServiceFixture();

        var request = new BlobRequest("a-content.txt") { Type = BlobTypes.File };
        (await fixtureA.Service.CheckExistsAsync(request)).ShouldBeTrue();
        File.Exists(Path.Combine(fixtureA.TestRoot, "a-content.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task DisposingOneFixtureKeepsOtherFixtureContent()
    {
        using var fixtureA = new LocalBlobServiceFixture();
        var fixtureB = new LocalBlobServiceFixture();

        var blobA = new BlobDetails.BlobData("a-persist.txt", new BinaryData("keep-a"u8.ToArray()))
        {
            Overwrite = true,
            Type = BlobTypes.File
        };
        await fixtureA.Service.SaveAsync(blobA);

        var blobB = new BlobDetails.BlobData("b-persist.txt", new BinaryData("keep-b"u8.ToArray()))
        {
            Overwrite = true,
            Type = BlobTypes.File
        };
        await fixtureB.Service.SaveAsync(blobB);

        fixtureB.Dispose();

        var requestA = new BlobRequest("a-persist.txt") { Type = BlobTypes.File };
        (await fixtureA.Service.CheckExistsAsync(requestA)).ShouldBeTrue();
        File.Exists(Path.Combine(fixtureA.TestRoot, "a-persist.txt")).ShouldBeTrue();
    }
}
