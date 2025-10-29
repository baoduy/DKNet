using System.Globalization;

namespace DKNet.Svc.BlobStorage.Abstractions;

/// <summary>
///     Provides BlobExtensions functionality.
/// </summary>
public static class BlobExtensions
{
    #region Methods

    /// <summary>
    ///     GetContentTypeByExtension operation.
    /// </summary>
    public static string GetContentTypeByExtension(this string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLower(CultureInfo.CurrentCulture);
        return ext switch
        {
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".html" => "text/html",
            ".htm" => "text/html",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream"
        };
    }

    #endregion
}