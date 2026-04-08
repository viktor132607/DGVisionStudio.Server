using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DGVisionStudio.Application.Interfaces;

namespace DGVisionStudio.Infrastructure.Services;

public class EmailService : IEmailService
{
	private readonly IConfiguration _configuration;
	private readonly HttpClient _httpClient;

	public EmailService(IConfiguration configuration)
	{
		_configuration = configuration;
		_httpClient = new HttpClient();
	}

	public async Task SendAsync(string toEmail, string subject, string body)
	{
		var apiKey = _configuration["Resend:ApiKey"];
		var fromEmail = _configuration["Resend:FromEmail"];
		var fromName = _configuration["Resend:FromName"] ?? "DG Vision Studio";

		if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(fromEmail))
			throw new InvalidOperationException("Resend settings are not configured.");

		using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
		request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

		var payload = new
		{
			from = $"{fromName} <{fromEmail}>",
			to = new[] { toEmail },
			subject,
			html = body
		};

		request.Content = new StringContent(
			JsonSerializer.Serialize(payload),
			Encoding.UTF8,
			"application/json");

		using var response = await _httpClient.SendAsync(request);

		if (!response.IsSuccessStatusCode)
		{
			var error = await response.Content.ReadAsStringAsync();
			throw new InvalidOperationException($"Resend email send failed: {(int)response.StatusCode} {error}");
		}
	}
}