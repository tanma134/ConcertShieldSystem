using System.ComponentModel.DataAnnotations;

namespace AuthenticationAPI.DTOs
{
    /// <summary>
    /// Body for POST api/auth/users/{id}/roles — grants a role additively.
    /// </summary>
    public class AddRoleDTO
    {
        [Required]
        [MaxLength(50)]
        public string RoleName { get; set; } = null!;
    }
}
