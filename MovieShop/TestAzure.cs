using System;
using Azure.Storage.Blobs;

var connectionString = Environment.GetEnvironmentVariable("AZURE_BLOB_CONNECTION");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("ERROR: Connection string not found");
    return;
}

try
{
    var blobServiceClient = new BlobServiceClient(connectionString);
    var containerClient = blobServiceClient.GetBlobContainerClient("movie-videos");
    var exists = containerClient.Exists();
    Console.WriteLine($"Container exists: {exists.Value}");
    Console.WriteLine("Azure Blob connection: SUCCESS");
}
catch (Exception ex)
{
    Console.WriteLine($"ERROR: {ex.Message}");
}
