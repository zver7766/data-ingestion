using DataIngestion.Data;
using DataIngestion.Data.Entities;
using DataIngestion.Contracts.Customers;
using DataIngestion.Services.Customers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DataIngestion.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(AppDbContext db, CustomerTransactionsService transactionsService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Customer>>> List(CancellationToken ct)
    {
        var customers = await db.Customers
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .ToListAsync(ct);

        return Ok(customers);
    }

    [HttpPost]
    public async Task<ActionResult<Customer>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Name is required." });
        }

        var customer = new Customer
        {
            Name = request.Name.Trim()
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);

        return Created($"/api/customers/{customer.Id}", customer);
    }

    [HttpGet("{id:long}/transactions")]
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
            StatusCodes.Status404NotFound => NotFound(new { message = error ?? "Not found." }),
            StatusCodes.Status400BadRequest => BadRequest(new { message = error ?? "Bad request." }),
            _ => StatusCode(StatusCodes.Status500InternalServerError)
        };
    }
}