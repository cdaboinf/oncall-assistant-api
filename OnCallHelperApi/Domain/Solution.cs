namespace OnCallHelperApi.Domain;

public class Solution
{
    public Guid SolutionId { get; set; }
    public string RootCause { get; set; }
    public string FixDescription { get; set; }

    public bool WasSuccessful { get; set; }

    public int TimesSuggested { get; set; }
    public int TimesSuccessful { get; set; }

    public DateTime CreatedAt { get; set; }
}