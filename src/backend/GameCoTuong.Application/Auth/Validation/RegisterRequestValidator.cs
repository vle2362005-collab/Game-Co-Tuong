using System.Net.Mail;
using GameCoTuong.Application.Auth.Models;

namespace GameCoTuong.Application.Auth.Validation;

public static class RegisterRequestValidator
{
    public static Dictionary<string, string[]> Validate(RegisterRequest? request)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (request is null)
        {
            errors["request"] = ["Dữ liệu yêu cầu đăng ký không được để trống."];
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

            if (!IsValidUsernamePattern(trimmedUsername))
            {
                AddError(errors, "username", "Tên đăng nhập chỉ được chứa chữ cái, chữ số và dấu gạch dưới (_).");
            }
        }

        // 3. DisplayName
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            AddError(errors, "displayName", "Tên hiển thị không được để trống.");
        }
        else
        {
            var trimmedDisplayName = request.DisplayName.Trim();
            if (trimmedDisplayName.Length < 1 || trimmedDisplayName.Length > 50)
            {
                AddError(errors, "displayName", "Tên hiển thị phải có độ dài từ 1 đến 50 ký tự.");
            }
        }

        // 4. Password
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

        // 5. Email
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            AddError(errors, "email", "Email không được để trống.");
        }
        else
        {
            var trimmedEmail = request.Email.Trim();
            if (trimmedEmail.Length > 254)
            {
                AddError(errors, "email", "Email không được vượt quá 254 ký tự.");
            }

            if (!IsValidEmail(trimmedEmail))
            {
                AddError(errors, "email", "Định dạng email không hợp lệ.");
            }
        }

        return ToResult(errors);
    }

    private static bool IsValidUsernamePattern(string username)
    {
        foreach (var c in username)
        {
            var isLetterOrDigit = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
            if (!isLetterOrDigit && c != '_')
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Contains(' ') || !email.Contains('@'))
        {
            return false;
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0 || atIndex >= email.Length - 1 || email.LastIndexOf('@') != atIndex)
        {
            return false;
        }

        var localPart = email.Substring(0, atIndex);
        var domainPart = email.Substring(atIndex + 1);

        if (string.IsNullOrEmpty(localPart) || string.IsNullOrEmpty(domainPart) || !domainPart.Contains('.'))
        {
            return false;
        }

        var lastDotIndex = domainPart.LastIndexOf('.');
        if (lastDotIndex <= 0 || lastDotIndex >= domainPart.Length - 1)
        {
            return false;
        }

        return MailAddress.TryCreate(email, out var mailAddress) && mailAddress.Address == email;
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
