using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Contracts.Customers;
using DataIngestion.Services.Customers;
using DataIngestion.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace DataIngestion.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db, CustomerTransactionsService transactionsService) : ControllerBase
{
    /// <summary>
    /// Create a customer.
    /// </summary>
    /// <remarks>
    /// Customer name is required and is trimmed.
    /// </remarks>
    /// <response code="201">Customer created.</response>
    /// <response code="400">Validation failed.</response>
    [HttpPost]
    [ProducesResponseType(typeof(Customer), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Customer>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return this.Problem(StatusCodes.Status400BadRequest, "validation_error", "Name is required.");
        }

        var customer = new Customer
        {
            Name = request.Name.Trim()
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return Created($"/api/customers/{customer.Id}", customer);
    }

    /// <summary>
    /// Get paginated transactions for a customer.
    /// </summary>
    /// <param name="id">Customer id.</param>
    /// <param name="query">Pagination and filter parameters.</param>
    /// <param name="ct">Request cancellation token.</param>
    /// <remarks>
    /// Transactions are ordered by most recent first. Filters are optional and applied server-side for performance.
    /// Returns ProblemDetails on errors.
    /// </remarks>
    /// <response code="200">A page of transactions.</response>
    /// <response code="400">Invalid paging or filter parameters.</response>
    /// <response code="404">Customer not found.</response>
    /// <response code="500">Unexpected error.</response>
    [HttpGet("{id:long}/transactions")]
    [ProducesResponseType(typeof(CustomerTransactionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<CustomerTransactionsResponse>> GetTransactions(
        [FromRoute] long id,
        [FromQuery] CustomerTransactionsQuery query,
        CancellationToken ct = default)
    {
        var (response, error, status) = await transactionsService.GetAsync(id, query, ct);
        if (response is not null)
        {
            return Ok(response);
        }

        return status switch
        {
            StatusCodes.Status404NotFound => this.Problem(StatusCodes.Status404NotFound, "customer_not_found", error ?? "Not found."),
            StatusCodes.Status400BadRequest => this.Problem(StatusCodes.Status400BadRequest, "validation_error", error ?? "Bad request."),
            _ => this.Problem(StatusCodes.Status500InternalServerError, "internal_error", "Unexpected error.")
        };
    }
}