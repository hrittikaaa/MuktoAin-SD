namespace MuktoAin.Infrastructure.Email;

public class EmailSettings
{
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPass { get; set; }
    public string SenderEmail { get; set; } = "no-reply@muktoain.gov.bd";
    public string SenderName { get; set; } = "MuktoAin (মুক্ত আইন)";
    public bool EnableSsl { get; set; } = true;
    public string AppBaseUrl { get; set; } = "https://localhost:7001";
}
