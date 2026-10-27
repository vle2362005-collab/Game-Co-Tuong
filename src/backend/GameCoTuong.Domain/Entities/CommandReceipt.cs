namespace GameCoTuong.Domain.Entities;

public class CommandReceipt
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid RequestId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public Guid? TargetId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string ResponseBody { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
