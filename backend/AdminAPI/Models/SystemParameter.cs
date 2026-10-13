using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Models;

[Table("system_parameters")]
[Index("Category", Name = "ix_system_parameters_category")]
[Index("ParamKey", Name = "system_parameters_param_key_key", IsUnique = true)]
public partial class SystemParameter
{
    [Key]
    [Column("system_parameter_id")]
    public int SystemParameterId { get; set; }

    [Column("param_key")]
    [StringLength(100)]
    public string ParamKey { get; set; } = null!;

    [Column("param_value")]
    [StringLength(500)]
    public string ParamValue { get; set; } = null!;

    [Column("category")]
    [StringLength(50)]
    public string Category { get; set; } = null!;

    [Column("description")]
    [StringLength(500)]
    public string? Description { get; set; }

    [Column("updated_by")]
    public int UpdatedBy { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
