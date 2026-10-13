using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace AdminAPI.Models;

[Table("risk_list")]
[Index("ValueType", "ValueHash", Name = "ix_risk_list_lookup")]
[Index("ListType", "ValueType", "ValueHash", Name = "uq_risk_list_entry", IsUnique = true)]
public partial class RiskList
{
    [Key]
    [Column("risk_list_id")]
    public int RiskListId { get; set; }

    [Column("list_type")]
    [StringLength(10)]
    public string ListType { get; set; } = null!;

    [Column("value_type")]
    [StringLength(15)]
    public string ValueType { get; set; } = null!;

    [Column("value_hash")]
    [StringLength(128)]
    public string ValueHash { get; set; } = null!;

    [Column("reason")]
    [StringLength(500)]
    public string Reason { get; set; } = null!;

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("created_by")]
    public int? CreatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
