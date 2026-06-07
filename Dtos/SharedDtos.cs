namespace WebApplication1.DTOs;

public record WardDto { public int Id { get; set; } public string Name { get; set; } = null!; public string Description { get; set; } = null!; }
public record BedTypeDto { public int Id { get; set; } public string Name { get; set; } = null!; public string Description { get; set; } = null!; }
public record RoomDto { public string Id { get; set; } = null!; public bool HasTv { get; set; } public WardDto Ward { get; set; } = null!; }
public record AssignBedRequestDto { public int WardId { get; set; } public int BedTypeId { get; set; } public DateTime From { get; set; } public DateTime? To { get; set; } }