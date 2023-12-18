using EasyGames.Models;
using EasyGames.Services;

namespace EasyGames.Interfaces
{
    public interface ITransaction
    {
        void Create(decimal amount, int transactionTypeId, int clientId, string comment);
        Task Delete(int id);
        Task<IEnumerable<TransactionWithClientInfo>> GetAllTransactionsWithClientInfo(int clientId);
        //IEnumerable<Transaction> GetAllTransactions();
        IEnumerable<TransactionWithClientInfo> GetAllTransactionsWithClientInfo();

        Task<TransactionWithClientInfo> UpdateComment(long transactionId, string comment);
        void UpdateTransactionAndClientBalance(int clientId,string name, string surname, decimal amount, string TransactionTypeName, string comment);
    }
}
