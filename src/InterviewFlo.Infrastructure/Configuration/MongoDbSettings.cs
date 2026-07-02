namespace InterviewFlo.Infrastructure.Configuration;

public sealed class MongoDbSettings
{
    public string ConnectionString { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "InterviewFlo";

    public string UsersCollectionName { get; set; } = "users";

    public string InterviewsCollectionName { get; set; } = "interviews";
}
