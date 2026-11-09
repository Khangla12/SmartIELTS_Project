using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using Xunit;

namespace SmartIELTS.UnitTests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_produces_pbkdf2_format()
    {
        string hash = PasswordHasher.Hash("Student@123");

        Assert.Equal(3, hash.Split('.').Length);
        Assert.True(PasswordHasher.IsHashed(hash));
        Assert.NotEqual("Student@123", hash);
    }

    [Fact]
    public void Verify_accepts_the_original_password()
        => Assert.True(PasswordHasher.Verify("Student@123", PasswordHasher.Hash("Student@123")));

    [Fact]
    public void Verify_rejects_a_wrong_password()
        => Assert.False(PasswordHasher.Verify("student@123", PasswordHasher.Hash("Student@123")));

    [Fact]
    public void Verify_supports_legacy_plaintext_rows()
        => Assert.True(PasswordHasher.Verify("Student@123", "Student@123"));

    [Fact]
    public void Verify_rejects_legacy_plaintext_mismatch()
        => Assert.False(PasswordHasher.Verify("wrong", "Student@123"));

    [Fact]
    public void Hash_uses_a_unique_salt_per_call()
        => Assert.NotEqual(PasswordHasher.Hash("same-password"), PasswordHasher.Hash("same-password"));

    [Fact]
    public void Jwt_generator_creates_a_signed_token()
    {
        var user = new User { Username = "student", Role = UserRole.Candidate };
        var options = new JwtOptions
        {
            Key = "SmartIELTS_UnitTest_Signing_Key_At_Least_32_Chars",
            Issuer = "SmartIELTS",
            Audience = "SmartIELTS.Client"
        };

        string token = JwtTokenGenerator.Generate(user, options);

        Assert.Equal(3, token.Split('.').Length);
    }
}
