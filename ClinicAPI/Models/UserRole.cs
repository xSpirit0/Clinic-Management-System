using System;
using System.Collections.Generic;

namespace ClinicAPI.Models;

public partial class UserRole
{
    public int UserRoleId { get; set; }

    public string Role { get; set; } = null!;

    public virtual ICollection<AppUser> AppUsers { get; set; } = new List<AppUser>();
}
