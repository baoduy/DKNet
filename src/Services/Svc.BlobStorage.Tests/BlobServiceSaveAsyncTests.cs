// Copyright (c) https://drunkcoding.net. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// Author: DRUNK Coding Team
// File: BlobServiceSaveAsyncTests.cs
// Description: Integration tests for ValidateFile enforcement in SaveAsync across different blob storage providers.

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Svc.BlobStorage.Tests.Fixtures;
using DKNet.Svc.BlobStorage.Abstractions;
using DKNet.Svc.BlobStorage.Local;
using DKNet.Svc.BlobStorage.AwsS3;
using DKNet.Svc.BlobStorage.AzureStorage;

namespace Svc.BlobStorage.Tests;

public class BlobServiceSaveAsyncTests
{
    #region LocalBlobService Tests

    [Fact]
    public async Task Local_SaveAsync_DisallowedExtension_ShouldThrowFileLoadException()
    {
        using var fixture = new LocalBlobServiceFixture();
        var options = new LocalDirectoryOptions { RootFolder = fixture.TestRoot, IncludedExtensions = [".txt"] };
        var service = new LocalBlobService(Options.Create(options), NullLogger<LocalBlobService>.Instance);
        var blobData = new BlobDetails.BlobData("test.pdf", BinaryData.FromString("test"));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File extension is invalid.");
    }

    [Fact]
    public async Task Local_SaveAsync_OversizedFile_ShouldThrowFileLoadException()
    {
        using var fixture = new LocalBlobServiceFixture();
        var options = new LocalDirectoryOptions { RootFolder = fixture.TestRoot, MaxFileSizeInMb = 1 };
        var service = new LocalBlobService(Options.Create(options), NullLogger<LocalBlobService>.Instance);
        var largeContent = new string('x', 2 * 1024 * 1024);
        var blobData = new BlobDetails.BlobData("test.txt", BinaryData.FromString(largeContent));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File size is invalid.");
    }

    [Fact]
    public async Task Local_SaveAsync_FileNameTooLong_ShouldThrowFileLoadException()
    {
        using var fixture = new LocalBlobServiceFixture();
        var options = new LocalDirectoryOptions { RootFolder = fixture.TestRoot, MaxFileNameLength = 5 };
        var service = new LocalBlobService(Options.Create(options), NullLogger<LocalBlobService>.Instance);
        var blobData = new BlobDetails.BlobData("verylongfilename.txt", BinaryData.FromString("test"));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File name is invalid.");
    }

    [Fact]
    public async Task Local_SaveAsync_DefaultOptions_ShouldSucceed()
    {
        using var fixture = new LocalBlobServiceFixture();
        var options = new LocalDirectoryOptions { RootFolder = fixture.TestRoot };
        var service = new LocalBlobService(Options.Create(options), NullLogger<LocalBlobService>.Instance);
        var blobData = new BlobDetails.BlobData("test.txt", BinaryData.FromString("test"));

        var result = await service.SaveAsync(blobData);
        result.ShouldBe("test.txt");
    }

    #endregion

    #region S3BlobService Tests

    [Fact]
    public async Task S3_SaveAsync_DisallowedExtension_ShouldThrowFileLoadException()
    {
        using var fixture = new S3BlobServiceFixture();
        var options = new S3Options 
        { 
            ConnectionString = "https://c4bf6253a59daf70a445861c23b45778.r2.cloudflarestorage.com",
            AccessKey = "c5240e9de9fb8f2b24d67315eed90737",
            Secret = "df8dc0fe841d98c8c8429e3fbe5a6e0e784865e835860b4ffeb65913d7e7346b",
            BucketName = "dev",
            DisablePayloadSigning = true,
            IncludedExtensions = [".txt"] 
        };
        var service = new S3BlobService(Options.Create(options), NullLogger<S3BlobService>.Instance);
        var blobData = new BlobDetails.BlobData("test.pdf", BinaryData.FromString("test"));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File extension is invalid.");
    }

    [Fact]
    public async Task S3_SaveAsync_OversizedFile_ShouldThrowFileLoadException()
    {
        using var fixture = new S3BlobServiceFixture();
        var options = new S3Options 
        { 
            ConnectionString = "https://c4bf6253a59daf70a445861c23b45778.r2.cloudflarestorage.com",
            AccessKey = "c5240e9de9fb8f2b24d67315eed90737",
            Secret = "df8dc0fe841d98c8c8429e3fbe5a6e0e784865e835860b4ffeb65913d7e7346b",
            BucketName = "dev",
            DisablePayloadSigning = true,
            MaxFileSizeInMb = 1 
        };
        var service = new S3BlobService(Options.Create(options), NullLogger<S3BlobService>.Instance);
        var largeContent = new string('x', 2 * 1024 * 1024);
        var blobData = new BlobDetails.BlobData("test.txt", BinaryData.FromString(largeContent));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File size is invalid.");
    }

    [Fact]
    public async Task S3_SaveAsync_FileNameTooLong_ShouldThrowFileLoadException()
    {
        using var fixture = new S3BlobServiceFixture();
        var options = new S3Options 
        { 
            ConnectionString = "https://c4bf6253a59daf70a445861c23b45778.r2.cloudflarestorage.com",
            AccessKey = "c5240e9de9fb8f2b24d67315eed90737",
            Secret = "df8dc0fe841d98c8c8429e3fbe5a6e0e784865e835860b4ffeb65913d7e7346b",
            BucketName = "dev",
            DisablePayloadSigning = true,
            MaxFileNameLength = 5 
        };
        var service = new S3BlobService(Options.Create(options), NullLogger<S3BlobService>.Instance);
        var blobData = new BlobDetails.BlobData("verylongfilename.txt", BinaryData.FromString("test"));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File name is invalid.");
    }

    [Fact]
    public async Task S3_SaveAsync_DefaultOptions_ShouldSucceed()
    {
        using var fixture = new S3BlobServiceFixture();
        var options = new S3Options 
        { 
            ConnectionString = "https://c4bf6253a59daf70a445861c23b45778.r2.cloudflarestorage.com",
            AccessKey = "c5240e9de9fb8f2b24d67315eed90737",
            Secret = "df8dc0fe841d98c8c8429e3fbe5a6e0e784865e835860b4ffeb65913d7e7346b",
            BucketName = "dev",
            DisablePayloadSigning = true
        };
        var service = new S3BlobService(Options.Create(options), NullLogger<S3BlobService>.Instance);
        var blobData = new BlobDetails.BlobData("test.txt", BinaryData.FromString("test")) { Overwrite = true };

        var result = await service.SaveAsync(blobData);
        result.ShouldBe("test.txt");
    }

    [Fact]
    public async Task S3_FullCycle_ShouldWork()
    {
        using var fixture = new S3BlobServiceFixture();
        var service = fixture.Service;
        var blobName = $"fullcycle-{Guid.NewGuid()}.txt";
        var blobData = new BlobDetails.BlobData(blobName, BinaryData.FromString("S3 Cycle Test")) { Overwrite = true };

        // Save
        await service.SaveAsync(blobData);

        // Exists
        (await service.CheckExistsAsync(new BlobRequest(blobName))).ShouldBeTrue();

        // Get
        var result = await service.GetAsync(new BlobRequest(blobName));
        result.ShouldNotBeNull();
        result!.Data.ToString().ShouldBe("S3 Cycle Test");

        // Public URL
        var url = await service.GetPublicAccessUrl(new BlobRequest(blobName));
        url.ShouldNotBeNull();

        // List
        var items = new List<BlobDetails.BlobResult>();
        await foreach (var item in service.ListItemsAsync(new BlobRequest(""))) items.Add(item);
        items.ShouldNotBeEmpty();

        // Delete
        (await service.DeleteAsync(new BlobRequest(blobName))).ShouldBeTrue();
        (await service.CheckExistsAsync(new BlobRequest(blobName))).ShouldBeFalse();
    }

    [Fact]
    public async Task S3_DeleteFolder_ShouldWork()
    {
        using var fixture = new S3BlobServiceFixture();
        var service = fixture.Service;
        
        // Create a folder with a file
        var folderName = $"testfolder-{Guid.NewGuid()}";
        var fileName = $"{folderName}/test.txt";
        var blobData = new BlobDetails.BlobData(fileName, BinaryData.FromString("Folder Test")) { Overwrite = true };
        
        // Save file in folder
        await service.SaveAsync(blobData);
        
        // Delete folder
        var folderRequest = new BlobRequest(folderName) { Type = BlobTypes.Directory };
        (await service.DeleteAsync(folderRequest)).ShouldBeTrue();
        
        // Verify folder is gone
        (await service.CheckExistsAsync(folderRequest)).ShouldBeFalse();
    }

    [Fact]
    public async Task S3_GetItemAsync_ShouldWork()
    {
        using var fixture = new S3BlobServiceFixture();
        var service = fixture.Service;
        var blobName = $"getitem-{Guid.NewGuid()}.txt";
        var blobData = new BlobDetails.BlobData(blobName, BinaryData.FromString("GetItem Test")) { Overwrite = true };

        // Save
        await service.SaveAsync(blobData);

        // GetItem
        var item = await service.GetItemAsync(new BlobRequest(blobName));
        item.ShouldNotBeNull();
        item!.Name.ShouldBe($"/{blobName}");

        // Cleanup
        await service.DeleteAsync(new BlobRequest(blobName));
    }

    #endregion

    #region AzureStorageBlobService Tests

    [Fact]
    public async Task Azure_SaveAsync_DisallowedExtension_ShouldThrowFileLoadException()
    {
        using var fixture = new AzureStorageBlobServiceFixture();
        var options = new AzureStorageOptions 
        { 
            ConnectionString = "UseDevelopmentStorage=true", 
            ContainerName = "test", 
            IncludedExtensions = [".txt"] 
        };
        var service = new AzureStorageBlobService(Options.Create(options));
        var blobData = new BlobDetails.BlobData("test.pdf", BinaryData.FromString("test"));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File extension is invalid.");
    }

    [Fact]
    public async Task Azure_SaveAsync_OversizedFile_ShouldThrowFileLoadException()
    {
        using var fixture = new AzureStorageBlobServiceFixture();
        var options = new AzureStorageOptions 
        { 
            ConnectionString = "UseDevelopmentStorage=true", 
            ContainerName = "test", 
            MaxFileSizeInMb = 1 
        };
        var service = new AzureStorageBlobService(Options.Create(options));
        var largeContent = new string('x', 2 * 1024 * 1024);
        var blobData = new BlobDetails.BlobData("test.txt", BinaryData.FromString(largeContent));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File size is invalid.");
    }

    [Fact]
    public async Task Azure_SaveAsync_FileNameTooLong_ShouldThrowFileLoadException()
    {
        using var fixture = new AzureStorageBlobServiceFixture();
        var options = new AzureStorageOptions 
        { 
            ConnectionString = "UseDevelopmentStorage=true", 
            ContainerName = "test", 
            MaxFileNameLength = 5 
        };
        var service = new AzureStorageBlobService(Options.Create(options));
        var blobData = new BlobDetails.BlobData("verylongfilename.txt", BinaryData.FromString("test"));

        var exception = await Should.ThrowAsync<FileLoadException>(() => service.SaveAsync(blobData));
        exception.Message.ShouldBe("File name is invalid.");
    }

    [Fact]
    public async Task Azure_SaveAsync_DefaultOptions_ShouldSucceed()
    {
        using var fixture = new AzureStorageBlobServiceFixture();
        var options = new AzureStorageOptions 
        { 
            ConnectionString = "UseDevelopmentStorage=true", 
            ContainerName = "test" 
        };
        var service = new AzureStorageBlobService(Options.Create(options));
        var blobData = new BlobDetails.BlobData("test.txt", BinaryData.FromString("test"));

        var result = await service.SaveAsync(blobData);
        result.ShouldBe("test.txt");
    }

    #endregion
}
