namespace InstaImporter.Services;

public interface ITranscriptionService
{
    Task<string?> TranscribeAsync(string videoPath);
}
