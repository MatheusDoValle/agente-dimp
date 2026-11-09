using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Api.Agente.Infrastructure.Memory;

public sealed record ContextMessage(
    string Role,
    string Content,
    DateTimeOffset Timestamp);

public sealed class ConversationContextDocument
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    [BsonElement("executionId")]
    public Guid ExecutionId { get; set; }

    [BsonElement("conversationId")]
    public Guid ConversationId { get; set; }

    [BsonElement("userId")]
    public string? UserId { get; set; }

    [BsonElement("organizationId")]
    public string? OrganizationId { get; set; }

    [BsonElement("messages")]
    public List<ContextMessage> Messages { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [BsonElement("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    [BsonElement("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    [BsonElement("contextVersion")]
    public int ContextVersion { get; set; } = 1;
}

public interface IMongoConversationContextStore
{
    Task InitializeIndexesAsync(CancellationToken cancellationToken = default);
    Task SaveContextAsync(ConversationContextDocument document, CancellationToken cancellationToken = default);
    Task<ConversationContextDocument?> GetActiveContextAsync(Guid executionId, CancellationToken cancellationToken = default);
    Task DeleteByExecutionIdAsync(Guid executionId, CancellationToken cancellationToken = default);
}

public sealed class MongoConversationContextStore(
    IMongoDatabase database,
    TimeProvider clock) : IMongoConversationContextStore
{
    private readonly IMongoCollection<ConversationContextDocument> _collection =
        database.GetCollection<ConversationContextDocument>("conversation_contexts");

    public async Task InitializeIndexesAsync(CancellationToken cancellationToken = default)
    {
        var keysBuilder = Builders<ConversationContextDocument>.IndexKeys;

        // 1. Indice unico executionId + conversationId
        var uniqueIndexModel = new CreateIndexModel<ConversationContextDocument>(
            keysBuilder.Combine(
                keysBuilder.Ascending(x => x.ExecutionId),
                keysBuilder.Ascending(x => x.ConversationId)),
            new CreateIndexOptions { Unique = true, Name = "ux_execution_conversation" });

        // 2. Indice TTL em expiresAt (expireAfterSeconds = 0)
        var ttlIndexModel = new CreateIndexModel<ConversationContextDocument>(
            keysBuilder.Ascending(x => x.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_expires_at" });

        await _collection.Indexes.CreateManyAsync([uniqueIndexModel, ttlIndexModel], cancellationToken);
    }

    public async Task SaveContextAsync(ConversationContextDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.UpdatedAt = clock.GetUtcNow();

        var filter = Builders<ConversationContextDocument>.Filter.And(
            Builders<ConversationContextDocument>.Filter.Eq(x => x.ExecutionId, document.ExecutionId),
            Builders<ConversationContextDocument>.Filter.Eq(x => x.ConversationId, document.ConversationId));

        await _collection.ReplaceOneAsync(filter, document, new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    public async Task<ConversationContextDocument?> GetActiveContextAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var filter = Builders<ConversationContextDocument>.Filter.And(
            Builders<ConversationContextDocument>.Filter.Eq(x => x.ExecutionId, executionId),
            Builders<ConversationContextDocument>.Filter.Gt(x => x.ExpiresAt, now));

        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task DeleteByExecutionIdAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ConversationContextDocument>.Filter.Eq(x => x.ExecutionId, executionId);
        await _collection.DeleteManyAsync(filter, cancellationToken);
    }
}
