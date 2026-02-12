namespace InstaImporter.Services;

using OpenAI;
using OpenAI.Audio;
using InstaImporter.Config;

public class WhisperTranscriptionService : ITranscriptionService
{
    private readonly AudioClient _audioClient;

    public WhisperTranscriptionService(AppSettings settings)
    {
        var client = new OpenAIClient(settings.OpenAI.ApiKey);
        _audioClient = client.GetAudioClient("whisper-1");
    }

    public async Task<string?> TranscribeAsync(string videoPath)
    {
        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
        {
            return null;
        }

        try
        {
            await using var audioStream = File.OpenRead(videoPath);

            var transcription = await _audioClient.TranscribeAudioAsync(
                audioStream,
                Path.GetFileName(videoPath),
                new AudioTranscriptionOptions
                {
                    Language = "en",
                    ResponseFormat = AudioTranscriptionFormat.Text
                });

            return transcription.Value.Text;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Transcription failed for {videoPath}: {ex.Message}");
            return null;
        }
    }
}
