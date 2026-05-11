using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ai.Application.Abstractions;
using Ai.Contracts;
using Ai.Domain.Entities;
using Ai.Infrastructure.Rag;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace SaasBuilder.IntegrationTests.Ai;

/// <summary>
/// Security invariant: the RAG pipeline must never query the vector store without a
/// specific tenant filter. Cross-tenant queries must be rejected at the guard clause
/// level — before any I/O occurs.
/// </summary>
public sealed class RagTenantScopeTests
{
    private readonly Mock<IEmbeddingClient> _embeddingClient = new();
    private readonly Mock<IVectorStore> _vectorStore = new();
    private readonly Mock<ILlmClient> _llmClient = new();

    private RagPipeline CreatePipeline() =>
        new RagPipeline(
            _embeddingClient.Object,
            _vectorStore.Object,
            _llmClient.Object,
            NullLogger<RagPipeline>.Instance);

    [Fact]
    public async Task QueryAsync_WhenTenantIdIsEmpty_ThrowsArgumentException_BeforeAnyIo()
    {
        // Arrange
        RagPipeline pipeline = CreatePipeline();

        // Act
        Func<Task> act = () => pipeline.QueryAsync("What is 2+2?", Guid.Empty, CancellationToken.None);

        // Assert — guard fires before embedding or search are called
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*tenantId*");

        _embeddingClient.Verify(
            c => c.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "EmbedAsync must not be called when tenantId is empty — that would be a wasted call before an invariant violation.");

        _vectorStore.Verify(
            s => s.SearchAsync(
                It.IsAny<float[]>(),
                It.IsAny<int>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "SearchAsync must not be called when tenantId is empty.");
    }

    [Fact]
    public async Task QueryAsync_WhenTenantIdIsProvided_PassesSameTenantIdToVectorStore()
    {
        // Arrange
        Guid tenantId = Guid.NewGuid();
        float[] embedding = new float[1536];
        IReadOnlyList<VectorSearchResult> chunks = Array.Empty<VectorSearchResult>();

        _embeddingClient
            .Setup(c => c.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embedding);

        _vectorStore
            .Setup(s => s.SearchAsync(embedding, It.IsAny<int>(), tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(chunks);

        _llmClient
            .Setup(l => l.ChatAsync(It.IsAny<ChatRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse
            {
                Message = new ChatMessage { Role = ChatRole.Assistant, Content = "42" },
            });

        RagPipeline pipeline = CreatePipeline();

        // Act
        ChatResponse response = await pipeline.QueryAsync("What is 2+2?", tenantId, CancellationToken.None);

        // Assert — the correct tenant was threaded through to the vector store
        _vectorStore.Verify(
            s => s.SearchAsync(embedding, It.IsAny<int>(), tenantId, It.IsAny<CancellationToken>()),
            Times.Once,
            "SearchAsync must be called exactly once with the caller-supplied tenantId.");

        response.Message.Content.Should().Be("42");
    }
}
