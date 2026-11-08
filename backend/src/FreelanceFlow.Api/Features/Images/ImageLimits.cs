namespace FreelanceFlow.Api.Features.Images;

/// <summary>Plan 08 request limits (07-security §5 sizes; spec §7: 5 images per client request).</summary>
public static class ImageLimits
{
    public const int MaxFilesPerAdminRequest = 10;
    public const int MaxFilesPerPortalRequest = 5;
    public const int MaxBulkDelete = 200;
    /// <summary>10 images × 20 MB + form overhead.</summary>
    public const long MaxAdminBodyBytes = 220L * 1024 * 1024;
    /// <summary>5 images × 20 MB + text fields.</summary>
    public const long MaxPortalRequestBodyBytes = 101L * 1024 * 1024;

    public static Dictionary<string, string[]> ValidateCount(int count, int max, bool required)
    {
        var errors = new Dictionary<string, string[]>();
        if (required && count == 0) errors["files"] = ["Chọn ít nhất 1 ảnh"];
        else if (count > max) errors["files"] = [$"Tối đa {max} ảnh mỗi lần gửi"];
        return errors;
    }
}
