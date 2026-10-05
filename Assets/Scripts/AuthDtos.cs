using System;

// JSON shapes for the auth endpoints (http://44.196.88.41:8080/scalar). Field names must match the
// server's camelCase exactly - Unity's JsonUtility maps by name.

[Serializable]
public class UserDto
{
    public string id;
    public string firstName;
    public string lastName;
    public string email;
    public string role;
    public string userName;
    public int playerImageIndex;
    public int playerXp;
    public double funds;
}

[Serializable]
public class AuthResponse
{
    public string accessToken;
    public UserDto user;
}

/// Every failure body from the server looks like {"message":"..."}; title/detail cover the
/// standard ASP.NET ProblemDetails shape in case some endpoints return that instead.
[Serializable]
public class ApiErrorBody
{
    public string message;
    public string title;
    public string detail;
}

[Serializable]
public class RegisterRequest
{
    public string firstName;
    public string email;
    public string password;
}

[Serializable]
public class LoginRequest
{
    public string email;
    public string password;
}

[Serializable]
public class VerifyOtpRequest
{
    public string email;
    public string otp;
}

[Serializable]
public class ForgotPasswordRequest
{
    public string email;
}

[Serializable]
public class ResetPasswordRequest
{
    public string email;
    public string token;
    public string newPassword;
}

[Serializable]
public class SetPasswordRequest
{
    public string currentPassword;
    public string newPassword;
}

/// verify-otp's success body isn't documented - this reads a reset token from it if present.
[Serializable]
public class VerifyOtpResponse
{
    public string token;
    public string resetToken;
}
