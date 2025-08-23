using Microsoft.AspNetCore.Mvc;
using EmailTrackerBackend.Services;

namespace EmailTrackerBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly EmailService _emailService;
        private readonly DatabaseService _dbService;

        public EmailController(EmailService emailService, DatabaseService dbService)
        {
            _emailService = emailService;
            _dbService = dbService;
        }

        // ✅ Fetch emails from Gmail and save in DB
        // Example: GET /api/email/fetch?days=7
        [HttpGet("fetch")]
        public IActionResult FetchEmails([FromQuery] int days = 1)
        {
            _emailService.FetchEmails();
            return Ok(new { message = $"Emails from last {days} days fetched and stored in database." });
        }

        // ✅ List all saved emails from DB
        [HttpGet("list")]
        public IActionResult GetEmails()
        {
            var emails = _dbService.GetAllEmails();
            return Ok(emails);
        }
    }
}
