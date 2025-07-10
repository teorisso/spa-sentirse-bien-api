using System.ComponentModel.DataAnnotations;

namespace SentirseWellApi.Models
{
    public class GoogleAuthDto
    {
        [Required]
        public string IdToken { get; set; } = string.Empty;
    }

    public class GoogleUserInfo
    {
        public string Sub { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GivenName { get; set; } = string.Empty;
        public string FamilyName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
        public bool EmailVerified { get; set; }
    }
} 