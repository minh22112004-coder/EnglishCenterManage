namespace ManagementSystem.Api.Models
{
    public class PaymentSettings
    {
        public string BankId { get; set; } = "vietcombank";
        public string AccountNo { get; set; } = "";
        public string AccountName { get; set; } = "";
        public string CustomQrUrl { get; set; } = "";
    }
}
