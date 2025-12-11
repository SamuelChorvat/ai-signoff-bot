namespace AISignoffBot.Services;

public interface IJiraClient
{
    Task AddComment(string issueKey, string comment);
}