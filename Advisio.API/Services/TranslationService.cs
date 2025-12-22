using System.Text.Json;
using System.Net.Http;

namespace Advisio.API.Services;

public interface ITranslationService
{
    Task<string> TranslateToRussianAsync(string text);
}

public class TranslationService : ITranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TranslationService>? _logger;

    public TranslationService(HttpClient httpClient, ILogger<TranslationService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> TranslateToRussianAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        try
        {
            // Используем MyMemory Translation API (бесплатный, без ключа)
            // Альтернативно можно использовать другие сервисы перевода
            var apiUrl = "https://api.mymemory.translated.net/get";
            var encodedText = Uri.EscapeDataString(text);
            var url = $"{apiUrl}?q={encodedText}&langpair=en|ru";

            var response = await _httpClient.GetStringAsync(url);
            
            var translationData = JsonSerializer.Deserialize<TranslationResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (translationData?.ResponseData?.TranslatedText != null)
            {
                var translated = translationData.ResponseData.TranslatedText;
                _logger?.LogDebug("Переведено: '{Original}' -> '{Translated}'", text.Substring(0, Math.Min(50, text.Length)), translated.Substring(0, Math.Min(50, translated.Length)));
                return translated;
            }

            // Если перевод не удался, возвращаем оригинал
            _logger?.LogWarning("Не удалось перевести текст: {Text}", text.Substring(0, Math.Min(100, text.Length)));
            return text;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Ошибка при переводе текста: {Text}", text.Substring(0, Math.Min(100, text.Length)));
            // В случае ошибки возвращаем оригинальный текст
            return text;
        }
    }
}

// Модель для ответа MyMemory Translation API
public class TranslationResponse
{
    [System.Text.Json.Serialization.JsonPropertyName("responseData")]
    public TranslationData? ResponseData { get; set; }
}

public class TranslationData
{
    [System.Text.Json.Serialization.JsonPropertyName("translatedText")]
    public string? TranslatedText { get; set; }
}


