using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;

namespace OnCallHelperMcp.Hosting;

/// <summary>
/// Same idea as the API's Program.cs: the Lambda's environment holds only the NAME of an SSM
/// SecureString parameter (e.g. ONCALL_AUTH0_CLIENT_SECRET_PARAM); the value is read at startup and
/// exposed as the plain variable (ONCALL_AUTH0_CLIENT_SECRET). Secrets never sit in the image or in git.
/// </summary>
public static class SsmSecrets
{
    public static async Task LoadIntoEnvironmentAsync(IEnumerable<string> names)
    {
        var pending = names
            .Select(n => (Name: n, Param: Environment.GetEnvironmentVariable(n + "_PARAM")))
            .Where(x => !string.IsNullOrWhiteSpace(x.Param) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(x.Name)))
            .ToList();
        if (pending.Count == 0) return;

        using var ssm = new AmazonSimpleSystemsManagementClient();
        foreach (var (name, param) in pending)
        {
            var response = await ssm.GetParameterAsync(new GetParameterRequest { Name = param, WithDecryption = true });
            Environment.SetEnvironmentVariable(name, response.Parameter?.Value);
        }
    }
}
