using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

public class WebHook
{
    private static readonly HttpClient _client = new HttpClient();
    private readonly string _url;

    // The Constructor: This "saves" the URL into the object
    public WebHook(string url)
    {
        _url = url;
    }

    public async Task Send(string message)
    {
        var payload = new { content = message };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        await _client.PostAsync(_url, content);
    }
}
