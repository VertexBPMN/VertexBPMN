using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Forms;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class TaskFormValidationTests
{
    [Fact]
    public void PresentationChildren_CannotAuthorizeFabricatedInput()
    {
        var task = new UserTask { FormSchema = """{"components":[{"type":"text","components":[{"type":"textfield","key":"admin"}]}]}""" };
        var result = TaskFormValidator.Validate(task, new Dictionary<string, object>(StringComparer.Ordinal) { ["admin"] = "true" });
        Assert.Contains("Field 'admin' is not an allowed result variable.", result.Errors, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\"not-a-number\"")]
    public void RequiredLockedField_RejectsInvalidFrozenPrefill(string prefillJson)
    {
        var task = new UserTask
        {
            FormSchema = """{"components":[{"type":"number","key":"amount","readOnly":true,"validate":{"required":true}}]}""",
            LocalVariables = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["amount"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(prefillJson)
            }
        };
        Assert.False(TaskFormValidator.Validate(task, null).IsValid);
    }

    [Fact]
    public void LockedField_RejectsEvenUnchangedSubmittedPrefill()
    {
        var task = new UserTask
        {
            FormSchema = """{"components":[{"type":"number","key":"amount","disabled":true}]}""",
            LocalVariables = new Dictionary<string, object>(StringComparer.Ordinal) { ["amount"] = 42 }
        };
        var result = TaskFormValidator.Validate(task, task.LocalVariables);
        Assert.Contains("Field 'amount' is read-only.", result.Errors, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("textfield", "\"ok\"")]
    [InlineData("textarea", "\"ok\"")]
    [InlineData("number", "42")]
    [InlineData("checkbox", "false")]
    [InlineData("select", "\"yes\"")]
    [InlineData("radio", "\"yes\"")]
    public void RecognizedEditableInput_AcceptsCorrectType(string type, string valueJson)
    {
        var task = new UserTask { FormSchema = "{\"components\":[{\"type\":\"" + type + "\",\"key\":\"value\"}]}" };
        var values = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["value"] = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(valueJson)
        };
        Assert.True(TaskFormValidator.Validate(task, values).IsValid);
    }

    [Fact]
    public void DuplicateKey_CannotOverrideLockedField()
    {
        var task = new UserTask { FormSchema = """{"components":[{"type":"textfield","key":"admin","readOnly":true},{"type":"textfield","key":"admin"}]}""" };
        var result = TaskFormValidator.Validate(task, new Dictionary<string, object>(StringComparer.Ordinal) { ["admin"] = "changed" });
        Assert.Contains("Field 'admin' is declared more than once.", result.Errors, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"components\":[null]}")]
    [InlineData("{\"components\":[42]}")]
    [InlineData("{\"components\":[{\"type\":\"textfield\",\"key\":\"x\",\"validate\":true}]}")]
    public void MalformedSchema_ReturnsExplicitError(string schema)
    {
        var task = new UserTask { FormSchema = schema };
        var result = TaskFormValidator.Validate(task, null);
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Theory]
    [InlineData("checklist")]
    [InlineData("taglist")]
    [InlineData("datetime")]
    [InlineData("filepicker")]
    [InlineData("expression")]
    [InlineData("dynamiclist")]
    [InlineData("fabricated")]
    public void UnsupportedInput_RejectsCompletionEvenWithoutSubmission(string type)
    {
        var task = new UserTask { FormSchema = "{\"components\":[{\"type\":\"" + type + "\",\"key\":\"admin\"}]}" };
        var result = TaskFormValidator.Validate(task, null);
        Assert.Contains($"Form component type '{type}' is not supported for task completion.", result.Errors, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("text")]
    [InlineData("html")]
    [InlineData("image")]
    [InlineData("button")]
    [InlineData("group")]
    [InlineData("default")]
    [InlineData("separator")]
    [InlineData("spacer")]
    [InlineData("table")]
    [InlineData("iframe")]
    [InlineData("documentPreview")]
    public void PresentationOrLayoutKey_DoesNotAuthorizeOutput(string type)
    {
        var task = new UserTask { FormSchema = "{\"components\":[{\"type\":\"" + type + "\",\"key\":\"admin\"}]}" };
        var result = TaskFormValidator.Validate(task, new Dictionary<string, object>(StringComparer.Ordinal) { ["admin"] = true });
        Assert.Contains("Field 'admin' is not an allowed result variable.", result.Errors, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("readonly", false)]
    [InlineData("readOnly", false)]
    [InlineData("disabled", false)]
    [InlineData("readonly", true)]
    [InlineData("disabled", true)]
    public void RequiredLockedField_UsesFrozenPrefill(string lockProperty, bool inherited)
    {
        var field = "{\"type\":\"number\",\"key\":\"amount\",\"validate\":{\"required\":true}" + (inherited ? "" : ",\"" + lockProperty + "\":true") + "}";
        var component = inherited ? "{\"type\":\"group\",\"" + lockProperty + "\":true,\"components\":[" + field + "]}" : field;
        var task = new UserTask
        {
            FormSchema = "{\"components\":[" + component + "]}",
            RequiredFields = ["amount"],
            LocalVariables = new Dictionary<string, object>(StringComparer.Ordinal) { ["amount"] = 42 }
        };
        var result = TaskFormValidator.Validate(task, null);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Theory]
    [InlineData("readonly")]
    [InlineData("readOnly")]
    [InlineData("disabled")]
    public void InheritedLock_RejectsSubmittedValue(string lockProperty)
    {
        var task = new UserTask { FormSchema = "{\"components\":[{\"type\":\"group\",\"" + lockProperty + "\":true,\"components\":[{\"type\":\"group\",\"components\":[{\"type\":\"textfield\",\"key\":\"locked\",\"readonly\":false}]}]}]}" };
        var result = TaskFormValidator.Validate(task, new Dictionary<string, object>(StringComparer.Ordinal) { ["locked"] = "changed" });
        Assert.Contains("Field 'locked' is read-only.", result.Errors, StringComparer.Ordinal);
    }

    [Fact]
    public void CamelCaseReadOnly_RejectsSubmittedValue()
    {
        var task = new UserTask { FormSchema = """{"components":[{"type":"textfield","key":"locked","readOnly":true}]}""" };
        var result = TaskFormValidator.Validate(task, new Dictionary<string, object>(StringComparer.Ordinal) { ["locked"] = "changed" });
        Assert.Contains("Field 'locked' is read-only.", result.Errors, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("{\"type\":\"number\",\"key\":\"amount\"}", "amount", "not-a-number")]
    [InlineData("{\"type\":\"select\",\"key\":\"choice\",\"values\":[{\"value\":\"yes\"}]}", "choice", "no")]
    [InlineData("{\"type\":\"textfield\",\"key\":\"locked\",\"readonly\":true}", "locked", "changed")]
    [InlineData("{\"type\":\"textfield\",\"key\":\"reason\"}", "admin", "true")]
    public void InvalidField_IsRejected(string component, string key, string value)
    {
        var task = new UserTask { FormSchema = "{\"components\":[" + component + "]}" };
        var result = TaskFormValidator.Validate(task, new Dictionary<string, object>(StringComparer.Ordinal) { [key] = value });
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
}
