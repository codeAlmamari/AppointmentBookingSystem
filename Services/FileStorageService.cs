namespace AppointmentBookingSystem.Services;

public class FileStorageService(IConfiguration config, ILogger<FileStorageService> logger)
{
    private readonly string _basePath = config["FileStorage:BasePath"]
        ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");

    // Allowed types 
    private static readonly string[] ImageMimes =
        ["image/jpeg", "image/png", "image/gif", "image/webp"];

    private static readonly string[] AttachmentMimes =
        ["image/jpeg", "image/png", "image/gif", "image/webp", "application/pdf"];

    // Size limits 
    private const long MaxImageSize = 5 * 1024 * 1024;  // 5 MB
    private const long MaxAttachmentSize = 10 * 1024 * 1024;  // 10 MB

    // Public methods 
    public async Task<string> SaveCustomerIdImageAsync(IFormFile file)
    {
        Validate(file, ImageMimes, MaxImageSize, "ID image");
        return await SaveAsync(file, "customer-ids");
    }

    public async Task<string> SaveAppointmentAttachmentAsync(IFormFile file)
    {
        Validate(file, AttachmentMimes, MaxAttachmentSize, "attachment");
        return await SaveAsync(file, "appointment-attachments");
    }

    public (Stream stream, string contentType) GetFile(string relativePath)
    {
        var fullPath = Path.Combine(_basePath, relativePath);

        if (!File.Exists(fullPath))
            throw new FileNotFoundException("File not found", fullPath);

        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg"
        };

        return (File.OpenRead(fullPath), contentType);
    }

    public void DeleteFile(string relativePath)
    {
        var fullPath = Path.Combine(_basePath, relativePath);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private async Task<string> SaveAsync(IFormFile file, string folder)
    {
        var dir = Path.Combine(_basePath, folder);
        Directory.CreateDirectory(dir);

        var ext = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(dir, fileName);

        await using var stream = File.Create(fullPath);
        await file.CopyToAsync(stream);

        logger.LogInformation("File saved: {Path}", fullPath);

        // Return relative path — stored in DB
        return Path.Combine(folder, fileName);
    }

    private static void Validate(IFormFile file, string[] allowedMimes, long maxSize, string context)
    {
        if (file.Length > maxSize)
            throw new ArgumentException(
                $"{context} exceeds maximum size of {maxSize / 1024 / 1024} MB");

        if (!allowedMimes.Contains(file.ContentType.ToLowerInvariant()))
            throw new ArgumentException(
                $"{context} type '{file.ContentType}' is not allowed");
    }
}