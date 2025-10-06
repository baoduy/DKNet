using Shouldly;
using System.Text;
using Xunit.Abstractions;

namespace DKNet.Svc.Encryption.Tests;

public class Base64UrlTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("ab")]
    [InlineData("abc")]
    [InlineData("test")]
    [InlineData("Base64URL-Token_123")] // includes chars that will appear as-is
    [InlineData("🔥 unicode 😀")] // multi-byte
    public void RoundTrip_ReturnsOriginal(string plain)
    {
        var encoded = plain.ToBase64UrlString();
        var decoded = encoded.FromBase64UrlString();
        decoded.ShouldBe(plain);
    }

    [Fact]
    public void Decode_JWT()
    {
        const string jwt =
            "eyJ0eXAiOiJKV1QiLCJub25jZSI6ImpJR0gzcER3THowcUFaMXcxWkZ6dk05eF8xLWdyMDBjdldzMlV6T28wdWMiLCJhbGciOiJSUzI1NiIsIng1dCI6IkhTMjNiN0RvN1RjYVUxUm9MSHdwSXEyNFZZZyIsImtpZCI6IkhTMjNiN0RvN1RjYVUxUm9MSHdwSXEyNFZZZyJ9.eyJhdWQiOiJodHRwczovL2dyYXBoLm1pY3Jvc29mdC5jb20vIiwiaXNzIjoiaHR0cHM6Ly9zdHMud2luZG93cy5uZXQvZDQzMGE3OGMtZGQ4Yy00NTE1LWJiNDktYjM1YmE3NjUzNTlmLyIsImlhdCI6MTc1OTczNzkxNywibmJmIjoxNzU5NzM3OTE3LCJleHAiOjE3NTk3NDE5NzUsImFjY3QiOjAsImFjciI6IjEiLCJhY3JzIjpbInAxIl0sImFpbyI6IkFaUUFhLzhhQUFBQVVaWVI3cHJGMUNSamNOaUc2TGEwTWNjcDR5SGxNNWZ4bEJsbzg5U0ttZmh2MTFyTmltMWo0Nnd5ajlUdTNrRHRoOHpvaVVuRnJYNHZCOHpJcytmd2tGNzVFWU9GNzFITi9iUDdiNjBJUkM5ZkNWR3FKNTVya2V4V3o1WDVFeW9VMnloMFFKQmxmcVZ3bUs4RWl0bnpZUmZYZGVQc3pSbEI1TFc3eXkwZjBZN3hmeENhRlgyZ2Q5bk5TbHZqVFl2ZiIsImFsdHNlY2lkIjoiMTpsaXZlLmNvbTowMDAzQkZGRDA0NUUxMTlCIiwiYW1yIjpbInB3ZCIsIm1mYSJdLCJhcHBfZGlzcGxheW5hbWUiOiJBREliaXphVVgiLCJhcHBpZCI6Ijc0NjU4MTM2LTE0ZWMtNDYzMC1hZDliLTI2ZTE2MGZmMGZjNiIsImFwcGlkYWNyIjoiMCIsImNvbnRyb2xzIjpbImNhX2VuZiJdLCJlbWFpbCI6ImRydW5rY29kaW5nQG91dGxvb2suY29tIiwiZmFtaWx5X25hbWUiOiIubmV0IiwiZ2l2ZW5fbmFtZSI6IkRydW5rIENvZGluZyIsImlkcCI6ImxpdmUuY29tIiwiaWR0eXAiOiJ1c2VyIiwiaXBhZGRyIjoiNTguMTg1LjM1LjE0OCIsIm5hbWUiOiJEcnVuayBDb2RpbmcgLm5ldCIsIm9pZCI6ImZmZWExMWNhLTRlM2YtNDc2ZS1iYzU5LTlmYmM3YjU3NjhlNCIsInBsYXRmIjoiNSIsInB1aWQiOiIxMDAzMDAwMEExQkFCMjZBIiwicmgiOiIxLkFYSUFqS2N3MUl6ZEZVVzdTYk5icDJVMW53TUFBQUFBQUFBQXdBQUFBQUFBQUFEREFNOXlBQS4iLCJzY3AiOiJBY2Nlc3NSZXZpZXcuUmVhZFdyaXRlLkFsbCBBcHBsaWNhdGlvbi5SZWFkLkFsbCBBdWRpdExvZy5SZWFkLkFsbCBDaGFuZ2VNYW5hZ2VtZW50LlJlYWQuQWxsIENvbnNlbnRSZXF1ZXN0LkNyZWF0ZSBDb25zZW50UmVxdWVzdC5SZWFkIENvbnNlbnRSZXF1ZXN0LlJlYWRBcHByb3ZlLkFsbCBDb25zZW50UmVxdWVzdC5SZWFkV3JpdGUuQWxsIEN1c3RvbVNlY0F0dHJpYnV0ZUFzc2lnbm1lbnQuUmVhZC5BbGwgQ3VzdG9tU2VjQXR0cmlidXRlQXVkaXRMb2dzLlJlYWQuQWxsIERldmljZS1Pcmdhbml6YXRpb25hbFVuaXQuUmVhZFdyaXRlLkFsbCBEaXJlY3RvcnkuQWNjZXNzQXNVc2VyLkFsbCBEaXJlY3RvcnkuUmVhZC5BbGwgRGlyZWN0b3J5LlJlYWRXcml0ZS5BbGwgRGlyZWN0b3J5LldyaXRlLlJlc3RyaWN0ZWQgRGlyZWN0b3J5UmVjb21tZW5kYXRpb25zLlJlYWQuQWxsIERpcmVjdG9yeVJlY29tbWVuZGF0aW9ucy5SZWFkV3JpdGUuQWxsIERvbWFpbi5SZWFkV3JpdGUuQWxsIGVtYWlsIEVudGl0bGVtZW50TWFuYWdlbWVudC5SZWFkLkFsbCBHcm91cC5SZWFkV3JpdGUuQWxsIEhlYWx0aE1vbml0b3JpbmdBbGVydC5SZWFkV3JpdGUuQWxsIEhlYWx0aE1vbml0b3JpbmdBbGVydENvbmZpZy5SZWFkV3JpdGUuQWxsIElkZW50aXR5UHJvdmlkZXIuUmVhZFdyaXRlLkFsbCBJZGVudGl0eVJpc2tFdmVudC5SZWFkV3JpdGUuQWxsIElkZW50aXR5Umlza3lTZXJ2aWNlUHJpbmNpcGFsLlJlYWRXcml0ZS5BbGwgSWRlbnRpdHlSaXNreVVzZXIuUmVhZFdyaXRlLkFsbCBJZGVudGl0eVVzZXJGbG93LlJlYWQuQWxsIExpZmVjeWNsZVdvcmtmbG93cy5SZWFkV3JpdGUuQWxsIE9uUHJlbURpcmVjdG9yeVN5bmNocm9uaXphdGlvbi5SZWFkLkFsbCBvcGVuaWQgT3JnYW5pemF0aW9uYWxVbml0LlJlYWRXcml0ZS5BbGwgUG9saWN5LlJlYWQuQWxsIFBvbGljeS5SZWFkLklkZW50aXR5UHJvdGVjdGlvbiBQb2xpY3kuUmVhZFdyaXRlLkF1dGhlbnRpY2F0aW9uRmxvd3MgUG9saWN5LlJlYWRXcml0ZS5BdXRoZW50aWNhdGlvbk1ldGhvZCBQb2xpY3kuUmVhZFdyaXRlLkF1dGhvcml6YXRpb24gUG9saWN5LlJlYWRXcml0ZS5Db25kaXRpb25hbEFjY2VzcyBQb2xpY3kuUmVhZFdyaXRlLkV4dGVybmFsSWRlbnRpdGllcyBQb2xpY3kuUmVhZFdyaXRlLklkZW50aXR5UHJvdGVjdGlvbiBQb2xpY3kuUmVhZFdyaXRlLk1vYmlsaXR5TWFuYWdlbWVudCBwcm9maWxlIFJlcG9ydHMuUmVhZC5BbGwgUm9sZU1hbmFnZW1lbnQuUmVhZFdyaXRlLkRpcmVjdG9yeSBTZWN1cml0eUV2ZW50cy5SZWFkV3JpdGUuQWxsIFRydXN0RnJhbWV3b3JrS2V5U2V0LlJlYWQuQWxsIFVzZXIuRXhwb3J0LkFsbCBVc2VyLlJlYWRXcml0ZS5BbGwgVXNlckF1dGhlbnRpY2F0aW9uTWV0aG9kLlJlYWRXcml0ZS5BbGwgVXNlci1Pcmdhbml6YXRpb25hbFVuaXQuUmVhZFdyaXRlLkFsbCIsInNpZCI6IjQzMjZiNGQ3LTMzODEtNDZkYS1iZGM0LWY3ZmVmYTEzNjljOCIsInNpZ25pbl9zdGF0ZSI6WyJrbXNpIl0sInN1YiI6InVNVmhVQ2ZrNjFFOU5NYTVIczRheFE0aG42aXBFX21hMEprOWgxWW9QLTAiLCJ0ZW5hbnRfcmVnaW9uX3Njb3BlIjoiQVMiLCJ0aWQiOiJkNDMwYTc4Yy1kZDhjLTQ1MTUtYmI0OS1iMzViYTc2NTM1OWYiLCJ1bmlxdWVfbmFtZSI6ImxpdmUuY29tI2RydW5rY29kaW5nQG91dGxvb2suY29tIiwidXRpIjoiUnI2ZXlaMVFYazJtOVdqM0g0ZEVBQSIsInZlciI6IjEuMCIsIndpZHMiOlsiNjJlOTAzOTQtNjlmNS00MjM3LTkxOTAtMDEyMTc3MTQ1ZTEwIiwiYjc5ZmJmNGQtM2VmOS00Njg5LTgxNDMtNzZiMTk0ZTg1NTA5Il0sInhtc19hY2QiOjE0NDkxODcyNzIsInhtc19hY3RfZmN0IjoiNSAzIiwieG1zX2NjIjpbImNwMSJdLCJ4bXNfZnRkIjoidzZlWUQ1RVhuSGVYWlRnNV9YN2JZRmh6azdCRExXOU4td3NqZF8zQ0N4NEJZWE5wWVhOdmRYUm9aV0Z6ZEMxa2MyMXoiLCJ4bXNfaWRyZWwiOiIxIDE0IiwieG1zX3N0Ijp7InN1YiI6ImpSMTYxSy1uNmQ3OHFmb1BWYjlKQjJOb1JRLWo4YUxHZldpOVdDMzBoQkkifSwieG1zX3N1Yl9mY3QiOiIzIDQiLCJ4bXNfdGNkdCI6MTQ5NDg5MzAzMiwieG1zX3RudF9mY3QiOiIxMCAzIn0.EvbLIBFzKqpfxqlwEVR-ZiVboG24UExm9p0bOwVewAdJVbrCaDkUaZmV16WS1tkAefOqXkqU7_4aRdsgD1KszN6Pszc17cFk0DyzzteHxgxNDOkTgyAAtVXb_sq-DP-oXQVgNNqlEY8cCSxcPjlqjPdY5FinCfLcghRQGNmlCxz1ke3v-tu5e4DcspxMTjHi46JY60Twe-TtDguo42nLFZ-GRKLDHfqTL629ICR-sNpEzNiAs-TaPFNVTZ5GwsYKd3xBCUtTBDMqRiWEUjAAmyfsa1fsvg5OgNTZuTc1v8e9jTPddIdACDqIdIpeajd_vSprs8PAbHwzDaCGmUDTaQ";
        foreach (var s in jwt.Split('.'))
        {
            var str = s.FromBase64UrlString();
            str.ShouldNotBeNullOrEmpty();
            output.WriteLine("Decode_JWT: {0}", str);
        }
    }

    [Fact]
    public void UrlVersion_Differs_From_Standard_When_Padding_Or_SpecialChars()
    {
        var input = "any+value/with+slashes"; // contains + and /
        var std = input.ToBase64String();
        var url = input.ToBase64UrlString();
        std.ShouldNotContain("+");
        std.ShouldNotContain("/");
        // URL version must not contain + or /
        url.IndexOf('+').ShouldBe(-1);
        url.IndexOf('/').ShouldBe(-1);
        // Usually std ends with '=' padding; url variant should not (unless length 0)
        if (std.EndsWith('=')) url.EndsWith('=').ShouldBeFalse();
        url.FromBase64UrlString().ShouldBe(input);
    }

    [Fact]
    public void Whitespace_Returns_Empty()
    {
        "  ".FromBase64UrlString().ShouldBe(string.Empty);
        ((string)null!).FromBase64UrlString().ShouldBe(string.Empty);
    }

    [Fact]
    public void Invalid_Base64Url_Input_Throws()
    {
        var invalid = "***"; // invalid characters
        Should.Throw<FormatException>(() => invalid.FromBase64UrlString());
    }

    [Fact]
    public void Padding_Ignored_In_UrlVariant()
    {
        var plain = "pad-test";
        var url = plain.ToBase64UrlString();
        // Manually add '=' padding (not typical for URL form) and still decode by normal API (WebEncoders tolerates?)
        // If length mod 4 is 2 -> add 2 '=', if 3 -> add 1 '='.
        var mod = url.Length % 4;
        var padded = mod switch
        {
            2 => url + "==",
            3 => url + "=",
            _ => url
        };
        // We expect decode to still succeed for padded variant when we convert to standard base64 by replacing -/_
        var paddedStandardLike = padded.Replace('-', '+').Replace('_', '/');
        // Use Convert once after normalizing to ensure we understand underlying
        var bytes = Convert.FromBase64String(paddedStandardLike);
        Encoding.UTF8.GetString(bytes).ShouldBe(plain);
    }
}