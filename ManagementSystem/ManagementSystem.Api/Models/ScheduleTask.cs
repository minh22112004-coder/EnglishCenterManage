using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ManagementSystem.Api.Models
{
    public class ScheduleTask
    {
        [Key]
        public int Id { get; set; }
        public int WorkScheduleId { get; set; }
        
        [Required]
        public string TaskName { get; set; } = null!;
        public bool IsCompleted { get; set; }

        [ForeignKey("WorkScheduleId")]
        public WorkSchedule WorkSchedule { get; set; } = null!;
    }
}
