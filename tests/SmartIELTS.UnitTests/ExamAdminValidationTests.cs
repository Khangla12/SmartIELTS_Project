using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Enums;
using Xunit;

namespace SmartIELTS.UnitTests;

public class ExamAdminValidationTests
{
    [Fact]
    public void CreateExam_defaults_are_sensible()
    {
        var dto = new CreateExamRequestDto();

        Assert.Equal(ExamType.Academic, dto.ExamType);
        Assert.Equal(165, dto.TotalDurationMinutes);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public void CreateQuestion_defaults_to_one_mark()
    {
        var dto = new CreateQuestionRequestDto();
        Assert.Equal(1.0, dto.MaxScore);
    }

    [Theory]
    [InlineData("[\"True\",\"False\",\"Not Given\"]")]
    [InlineData("[\"A\"]")]
    public void Valid_options_json_is_a_string_array(string json)
    {
        var options = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json);
        Assert.NotNull(options);
        Assert.NotEmpty(options!);
    }

    [Fact]
    public void Invalid_options_json_throws()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(
            () => System.Text.Json.JsonSerializer.Deserialize<List<string>>("not-json"));
    }

    [Fact]
    public void Terminate_request_requires_a_reason()
    {
        var dto = new TerminateSessionRequestDto();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(dto);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        bool valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            dto, context, results, validateAllProperties: true);

        Assert.False(valid);
        Assert.NotEmpty(results);
    }

    [Fact]
    public void Terminate_request_accepts_a_descriptive_reason()
    {
        var dto = new TerminateSessionRequestDto { Reason = "Sử dụng tài liệu trái phép trong phòng thi" };
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(dto);
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

        bool valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            dto, context, results, validateAllProperties: true);

        Assert.True(valid);
    }
}
