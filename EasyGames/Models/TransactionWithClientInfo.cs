namespace EasyGames.Models
{
    public class TransactionWithClientInfo
    {
        public long TransactionID { get; set; }
        public decimal Amount { get; set; }
        public int TransactionTypeID { get; set; }
        public int ClientID { get; set; }
        public string Comment { get; set; }
        public string ClientName { get; set; }
        public string ClientSurname { get; set; }
        public decimal ClientBalance { get; set; }
        public string TransactionTypeName { get; set; }
    }

}
