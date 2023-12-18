using EasyGames.Interfaces;
using EasyGames.Models;
using EasyGames.Services;
using Microsoft.AspNetCore.Mvc;

namespace EasyGames.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TransactionController : ControllerBase
    {
        private readonly ITransaction _transaction;

        public TransactionController(ITransaction transaction)
        {
            _transaction = transaction;
        }

        // GET: api/Transaction
        [HttpGet]
        public IActionResult GetAllTransactions()
        {
            // Assuming you have a model that represents the combined information from both tables
            var transactions = _transaction.GetAllTransactionsWithClientInfo();
            return Ok(transactions);
        }

        // POST: api/Transaction
        [HttpPost]
        public IActionResult CreateTransaction(int clientId, string name, string surname, decimal amount, string TransactionTypeName, string comment)
        {
            if (amount < 0)
            {
                // If amount is negative, subtract from ClientBalance
                _transaction.UpdateTransactionAndClientBalance(clientId, name, surname, amount, "Debit", comment);
            }
            else
            {
                // If amount is positive, add to ClientBalance
                _transaction.UpdateTransactionAndClientBalance(clientId, name, surname, amount, "Credit", comment);
            }

            return Ok("Transaction created successfully");
        }

        [HttpGet("GetTransactionsByClient/{clientId}")]
        public async Task<IActionResult> GetTransactionsByClient(int clientId)
        {
            // Implement logic to fetch transactions by client ID
            var transactions = await _transaction.GetAllTransactionsWithClientInfo(clientId);
            return Ok(transactions);
        }

        // POST: api/Transaction/UpdateComment/5
        [HttpPost("UpdateComment/{transactionId}")]
        public async Task<IActionResult> UpdateComment(long transactionId, [FromBody] string comment)
        {
            try
            {
                // Ensure that the comment is not null or empty
                if (string.IsNullOrWhiteSpace(comment))
                {
                    return BadRequest("Comment cannot be empty");
                }

                // Call the UpdateComment method from your service
                var updatedTransaction = await _transaction.UpdateComment(transactionId, comment);

                return Ok("Comment updated successfully");
            }
            catch (ArgumentException argumentEx)
            {
                // Handle the case where the comment is empty
                return BadRequest(argumentEx.Message);
            }
            catch (InvalidOperationException notFoundEx)
            {
                // Handle the case where the transaction with the given ID is not found
                return NotFound(notFoundEx.Message);
            }
            catch (Exception ex)
            {
                // Handle other exceptions
                return StatusCode(500, $"An error occurred: {ex.Message}");
            }
        }
    }
}

