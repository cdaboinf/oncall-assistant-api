using OnCallHelperApi.Application.Services;

namespace OnCallHelperApiTests.IntegrationTests;

using NUnit.Framework;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Threading.Tasks;

[TestFixture]
public class EmbeddingServiceTests
{
    private IEmbeddingService _embeddingService;

    [SetUp]
    public void Setup()
    {
        var inMemorySettings = new Dictionary<string, string>
        {
            { "OpenAI:ApiKey", "" }
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        _embeddingService = new EmbeddingService(configuration);
    }

    [Test]
    public async Task GetEmbeddingAsync_ShouldReturn_1536_Length_Vector()
    {
        var vector = await _embeddingService.GetEmbeddingAsync("test");

        Assert.That(vector, Is.Not.Null);
        Assert.That(vector.Length, Is.EqualTo(1536));
        Assert.That(vector, Is.All.Not.NaN);
    }
}