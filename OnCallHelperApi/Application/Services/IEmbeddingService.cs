namespace OnCallHelperApi.Application.Services;

public interface IEmbeddingService
{
    /// <summary>
    /// Generate a vector embedding for a given text
    /// </summary>
    /// <param name="text">The text to embed</param>
    /// <returns>Embedding vector as float array</returns>
    Task<float[]> GetEmbeddingAsync(string text);
}