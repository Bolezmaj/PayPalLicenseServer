using Microsoft.AspNetCore.Mvc;
using RestSharp;
using System.Text.Json;
using System.Net.Mail;
using System.Net;
using System.Text;

[Route("api/[controller]")]
[ApiController]
public class PayPalController : ControllerBase
{
    private const string PayPalClientId = "AUsUB0XbE33-ZcD0Z3PQjjbQ2hg7KZONmgEcsNnjcSFu9gAqh4bjPwTTASAoHMldCin60I0ucWcxE8xE";
    private const string PayPalSecret = "EMgilZcNPh5vtR5tBgN-HEHZkpD5YLPTID8UFtYttwx7PCSHdrKEHdDyrWWGli1DOee38Tm4RI2TyfV6";
    private const string PayPalApiUrl = "https://api-m.paypal.com";

    [HttpPost("verify-payment")]
    public async Task<IActionResult> VerifyPayment([FromBody] PaymentRequest request)
    {
        if (string.IsNullOrEmpty(request.TransactionId) || string.IsNullOrEmpty(request.Email))
            return BadRequest(new { status = "error", message = "Invalid request" });

        // Step 1: Get PayPal OAuth Token
        var authClient = new RestClient($"{PayPalApiUrl}/v1/oauth2/token");
        var authRequest = new RestRequest();
        authRequest.AddHeader("Accept", "application/json");
        authRequest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
        authRequest.AddHeader("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{PayPalClientId}:{PayPalSecret}")));
        authRequest.AddParameter("grant_type", "client_credentials");

        var authResponse = await authClient.ExecutePostAsync<AuthResponse>(authRequest);
        if (authResponse.Data == null || string.IsNullOrEmpty(authResponse.Data.AccessToken))
            return Unauthorized(new { status = "error", message = "PayPal authentication failed" });

        // Step 2: Verify Payment with PayPal
        var client = new RestClient($"{PayPalApiUrl}/v2/checkout/orders/{request.TransactionId}");
        var verifyRequest = new RestRequest();
        verifyRequest.AddHeader("Authorization", $"Bearer {authResponse.Data.AccessToken}");

        var verifyResponse = await client.ExecuteGetAsync<PayPalOrderResponse>(verifyRequest);
        if (verifyResponse.Data == null || verifyResponse.Data.Status != "COMPLETED")
            return BadRequest(new { status = "error", message = "Invalid or unverified PayPal payment" });

        // Step 3: Generate License Key
        string licenseKey = GenerateLicenseKey();

        // Step 4: Send License Key via Email
        bool emailSent = SendLicenseEmail(request.Email, licenseKey);
        if (!emailSent)
            return StatusCode(500, new { status = "error", message = "Failed to send email" });

        return Ok(new { status = "success", licenseKey });
    }

    private static string GenerateLicenseKey()
    {
        return Guid.NewGuid().ToString().ToUpper().Replace("-", "").Substring(0, 16);
    }

    private static bool SendLicenseEmail(string email, string licenseKey)
    {
        try
        {
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("your_email@gmail.com", "your_app_password"),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress("your_email@gmail.com"),
                Subject = "Your License Key",
                Body = $"Thank you for your purchase!\n\nYour License Key: {licenseKey}\n\nEnjoy your software!",
                IsBodyHtml = false
            };

            mailMessage.To.Add(email);
            smtpClient.Send(mailMessage);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

// DTO Models
public class PaymentRequest
{
    public string TransactionId { get; set; }
    public string Email { get; set; }
}

public class AuthResponse
{
    public string AccessToken { get; set; }
}

public class PayPalOrderResponse
{
    public string Status { get; set; }
}
