using Dapper;
using EasyGames.Interfaces;
using EasyGames.Models;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Transactions;
using System.Xml.Linq;

namespace EasyGames.Services
{
    public enum TransactionTypes
    {
        Credit = 1,
        Debit = 2
    }
    public class TransactionService : ITransaction
    {
        private readonly IConfiguration configuration;
        private readonly IDbConnection _connection;
        public TransactionService(IConfiguration configuration, IDbConnection connection)
        {
            this.configuration = configuration;
            _connection = connection;
        }

        void ITransaction.Create(decimal amount, int transactionTypeId, int clientId, string comment)
        {
            using (var connection = new SqlConnection(configuration.GetConnectionString("UserContext")))
            {
                connection.Open();
                string query = "INSERT INTO TransactionTable (Amount, TransactionID, ClientID, Comment) Values (@Amount, @TransactionID, @ClientID, @Comment)";
                connection.Execute(query, new { Amount = amount, TransactionTypeID = transactionTypeId, ClientID = clientId, Comment = comment });
            }

        }

        public Task Delete(int id)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<TransactionWithClientInfo>> GetAllTransactionsWithClientInfo(int clientId)
        {
            using (var connection = new SqlConnection(configuration.GetConnectionString("UserContext")))
            {
                await connection.OpenAsync();

                string query = @"
                SELECT 
                    t.TransactionID,
                    t.Amount,
                    t.TransactionTypeID,
                    tt.TransactionTypeName,
                    t.ClientID,
                    t.Comment,
                    c.Name AS ClientName,
                    c.Surname AS ClientSurname,
                    c.ClientBalance
                FROM TransactionTable t
                INNER JOIN ClientTable c ON t.ClientID = c.ClientID
                INNER JOIN TransactionTypeTable tt ON t.TransactionTypeID = tt.TransactionTypeID
                WHERE t.ClientID = @ClientId";

                var transactions = await connection.QueryAsync<TransactionWithClientInfo>(query, new { ClientId = clientId });

                return transactions;
            }
        }

        public IEnumerable<TransactionWithClientInfo> GetAllTransactionsWithClientInfo()
        {
            using (var connection = new SqlConnection(configuration.GetConnectionString("UserContext")))
            {
                connection.Open();

                string query = @"
                SELECT 
                    t.TransactionID,
                    t.Amount,
                    t.TransactionTypeID,
                    tt.TransactionTypeName,
                    t.ClientID,
                    t.Comment,
                    c.Name AS ClientName,
                    c.Surname AS ClientSurname,
                    c.ClientBalance
                FROM TransactionTable t
                INNER JOIN ClientTable c ON t.ClientID = c.ClientID
                INNER JOIN TransactionTypeTable tt ON t.TransactionTypeID = tt.TransactionTypeID;";

                var transactions = connection.Query<TransactionWithClientInfo>(query);

                return transactions;
            }
        }

        public async Task<TransactionWithClientInfo> UpdateComment(long transactionId, string comment)
        {
            using (var connection = new SqlConnection(configuration.GetConnectionString("UserContext")))
            {
                await connection.OpenAsync();

                // Check if the transaction with the provided ID exists
                if (!await TransactionExistsAsync(transactionId, connection))
                {
                    throw new InvalidOperationException($"Transaction with ID {transactionId} does not exist.");
                }

                string query = "UPDATE TransactionTable SET Comment = @Comment WHERE TransactionID = @TransactionId";
                await connection.ExecuteAsync(query, new { Comment = comment, TransactionId = transactionId });

                // Retrieve the updated transaction information
                string selectQuery = @"
                SELECT 
                    t.TransactionID,
                    t.Amount,
                    t.TransactionTypeID,
                    tt.TransactionTypeName,
                    t.ClientID,
                    t.Comment,
                    c.Name AS ClientName,
                    c.Surname AS ClientSurname,
                    c.ClientBalance
                FROM TransactionTable t
                INNER JOIN ClientTable c ON t.ClientID = c.ClientID
                INNER JOIN TransactionTypeTable tt ON t.TransactionTypeID = tt.TransactionTypeID
                WHERE t.TransactionID = @TransactionId";

                var updatedTransaction = await connection.QueryFirstOrDefaultAsync<TransactionWithClientInfo>(selectQuery, new { TransactionId = transactionId });

                return updatedTransaction;
            }
        }
        private async Task<bool> TransactionExistsAsync(long transactionId, SqlConnection connection)
        {
            // Check if the transaction with the given ID exists in the database
            string query = "SELECT 1 FROM TransactionTable WHERE TransactionID = @TransactionId";
            return await connection.ExecuteScalarAsync<bool>(query, new { TransactionId = transactionId });
        }

        // Helper method to check if a transaction with the given ID exists
        private bool TransactionExists(int id, SqlConnection connection)
        {
            string checkQuery = "SELECT COUNT(1) FROM TransactionTable WHERE TransactionID = @TransactionId";
            int count = connection.ExecuteScalar<int>(checkQuery, new { TransactionId = id });

            return count > 0;
        }

        public void UpdateTransactionAndClientBalance(int clientId, string name, string surname, decimal amount, string transactionTypeName, string comment)
        {
            using (var newConnection = new SqlConnection(configuration.GetConnectionString("UserContext")))
            {
                newConnection.Open();
                using (var transaction = newConnection.BeginTransaction())
                {
                    try
                    {
                        // Ensure that the transactionTypeName parameter is not null or empty
                        if (string.IsNullOrWhiteSpace(transactionTypeName))
                        {
                            // Handle the case where transactionTypeName is not provided
                            throw new ArgumentException("TransactionTypeName cannot be null or empty.", nameof(transactionTypeName));
                        }

                        // Check if the TransactionTypeName exists in TransactionTypeTable
                        int transactionTypeId = GetTransactionTypeId(transactionTypeName, newConnection, transaction);

                        // If it doesn't exist, insert it
                        if (transactionTypeId == 0)
                        {
                            transactionTypeId = InsertTransactionType(transactionTypeName, newConnection, transaction);
                        }

                        // Check if the ClientID exists
                        if (!ClientExists(clientId, newConnection, transaction))
                        {
                            // If it doesn't exist, create a new client
                            InsertNewClient(clientId, name, surname, newConnection, transaction);
                        }

                        long transactionId = GenerateUniqueTransactionId();

                        // Insert transaction with the generated unique TransactionID
                        string insertQuery = "INSERT INTO TransactionTable (TransactionID, Amount, TransactionTypeID, ClientID, Comment) VALUES (@TransactionID, @Amount, @TransactionTypeID, @ClientID, @Comment)";
                        newConnection.Execute(insertQuery, new { TransactionID = transactionId, Amount = amount, TransactionTypeID = transactionTypeId, ClientID = clientId, Comment = comment }, transaction);

                        // Update client balance
                        string updateBalanceQuery = "UPDATE ClientTable SET ClientBalance = ClientBalance + @Amount WHERE ClientID = @ClientID";
                        newConnection.Execute(updateBalanceQuery, new { Amount = amount, ClientID = clientId }, transaction);

                        transaction.Commit();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error: {ex.Message}");

                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        // Modify these methods to work with the new model
        private int GetTransactionTypeId(string transactionTypeName, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT TransactionTypeID FROM TransactionTypeTable WHERE TransactionTypeName = @TransactionTypeName";
            int transactionTypeId = connection.ExecuteScalar<int>(query, new { TransactionTypeName = transactionTypeName }, transaction);
            return transactionTypeId;
        }

        private int InsertTransactionType(string transactionTypeName, SqlConnection connection, SqlTransaction transaction)
        {
            string insertQuery = "INSERT INTO TransactionTypeTable (TransactionTypeName) OUTPUT INSERTED.TransactionTypeID VALUES (@TransactionTypeName)";
            int transactionTypeId = connection.ExecuteScalar<int>(insertQuery, new { TransactionTypeName = transactionTypeName }, transaction);
            return transactionTypeId;
        }

        private bool ClientExists(int clientId, SqlConnection connection, SqlTransaction transaction)
        {
            string query = "SELECT COUNT(1) FROM ClientTable WHERE ClientID = @ClientID";
            int count = connection.ExecuteScalar<int>(query, new { ClientID = clientId }, transaction);
            return count > 0;
        }

        private void InsertNewClient(int clientId, string name, string surname, SqlConnection connection, SqlTransaction transaction)
        {
            string insertQuery = "INSERT INTO ClientTable (ClientID, Name, Surname, ClientBalance) VALUES (@ClientID, @Name, @Surname, 0)";
            connection.Execute(insertQuery, new { ClientID = clientId, Name = name, Surname = surname }, transaction);
        }

        private long GenerateUniqueTransactionId()
        {
            // Implement your own logic to generate a unique bigint value for TransactionID
            // You might use a sequence, timestamp, or any other mechanism that ensures uniqueness
            // For simplicity, you could use a timestamp-based approach:
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }


    }
}