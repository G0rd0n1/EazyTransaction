namespace EasyGames.Models
{
    public class Transaction
    {
        public string Name { get; set; }
        public string Surname { get; set; }
        public long TransactionID { get; set; }
        public decimal Amount { get; set; }
        public int TransactionTypeID { get; set; } 
        public int ClientID { get; set; }   
        public string? Comment { get; set; }
    }
}
