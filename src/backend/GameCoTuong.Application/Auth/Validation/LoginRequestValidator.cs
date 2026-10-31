using GameCoTuong.Application.Auth.Models;

namespace GameCoTuong.Application.Auth.Validation;

public static class LoginRequestValidator
{
    public static Dictionary<string, string[]> Validate(LoginRequest? request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors["request"] = ["Dữ liệu yêu cầu đăng nhập không được để trống."];
            return ToResult(errors);
        }

        // 1. RequestId
        if (request.RequestId == Guid.Empty)
        {
            AddError(errors, "requestId", "RequestId là bắt buộc và phải là GUID hợp lệ.");
        }

        // 2. Username
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            AddError(errors, "username", "Tên đăng nhập không được để trống.");
        }
        else
        {
            var trimmedUsername = request.Username.Trim();
            if (trimmedUsername.Length < 3 || trimmedUsername.Length > 30)
            {
                AddError(errors, "username", "Tên đăng nhập phải có độ dài từ 3 đến 30 ký tự.");
            }
        }

        // 3. Password
        if (string.IsNullOrEmpty(request.Password))
        {
            AddError(errors, "password", "Mật khẩu không được để trống.");
        }
        else
        {
            if (request.Password.Length < 8 || request.Password.Length > 128)
            {
                AddError(errors, "password", "Mật khẩu phải có độ dài từ 8 đến 128 ký tự.");
            }
        }

        return ToResult(errors);
    }

    private static void AddError(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list))
        {
            list = [];
            errors[field] = list;
        }
        list.Add(message);
    }

    private static Dictionary<string, string[]> ToResult(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(k => k.Key, v => v.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
}
