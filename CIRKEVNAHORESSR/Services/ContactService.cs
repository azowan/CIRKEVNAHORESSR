namespace CIRKEVNAHORESSR.Services
{
    public sealed class ContactService
    {
        public Task SendAsync(ContactMessage msg, CancellationToken ct = default)
        {
            // TODO: napoj na SES/SendGrid/Azure Function apod.
            return Task.CompletedTask;
        }
    }
    
    public sealed record ContactMessage(string Name, string Email, string Subject, string Message);
}
