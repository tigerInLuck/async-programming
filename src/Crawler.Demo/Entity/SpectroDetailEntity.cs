using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Async.Programming;

/// <summary>
/// Represents an entity of the spectro daily details data in laboratory device entry.
/// </summary>
[Table("SpectroDetail")]
[Description("Stores the spectro daily details data in laboratory device.")]
public class SpectroDetailEntity
{
    /// <summary>
    /// The Id.
    /// </summary>
    [Key]
    [Description("The primary Id.")]
    public Guid Sd_Id { get; set; }

    /// <summary>
    /// The spectro daily id.
    /// </summary>
    [Required]
    [Index("IX_DailyId", IsUnique = false, Order = 0)]
    [Description("The spectro daily id.")]
    public Guid Sd_DailyId { get; set; }

    /// <summary>
    /// The sequence number.
    /// </summary>
    [Required]
    [Index("IX_DailyId", IsUnique = false, Order = 1)]
    [Description("The sequence number.")]
    public int Sd_SeqNo { get; set; }

    /// <summary>
    /// The kind.
    /// </summary>
    [MaxLength(32)]
    [Description("The kind.")]
    public string Sd_Kind { get; set; }

    /// <summary>
    /// The 'ID' string.
    /// </summary>
    [Index("IX_IDString", IsUnique = false, Order = 0)]
    [Description("The 'ID' string.")]
    public string Sd_IDString { get; set; }

    /// <summary>
    /// The percent.
    /// </summary>
    [MaxLength(32)]
    [Description("The percent.")]
    public string Sd_Percent { get; set; }

    /// <summary>
    /// The create date.
    /// </summary>
    [Required]
    [Description("The create date.")]
    public DateTime Sd_CreateDate { get; set; }
}