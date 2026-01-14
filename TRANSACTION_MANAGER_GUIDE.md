# Transaction Manager Usage Guide

## Purpose
The TransactionManager ensures data consistency by wrapping database operations in transactions with automatic retry logic for concurrency issues and database locks.

## How to Use in ViewModels

### Step 1: Inject Transaction Manager

Add to your ViewModel constructor:

```csharp
private readonly ITransactionManager _transactionManager;

public YourViewModel(AppDbContext dbContext)
{
    _dbContext = dbContext;
    _transactionManager = new TransactionManager(_dbContext);
}
```

### Step 2: Replace SaveChanges Calls

**OLD CODE (Unsafe):**
```csharp
_dbContext.Invoices.Add(invoice);
_dbContext.SaveChanges();

foreach (var product in products)
{
    _dbContext.SaleProducts.Add(product);
    _dbContext.SaveChanges();
}
```

**NEW CODE (Safe with Transaction):**
```csharp
await _transactionManager.ExecuteInTransactionAsync(async () =>
{
    _dbContext.Invoices.Add(invoice);
    await _dbContext.SaveChangesAsync();

    foreach (var product in products)
    {
        _dbContext.SaleProducts.Add(product);
    }
    await _dbContext.SaveChangesAsync();
}, "CreateInvoice");
```

### Step 3: Add Logging

```csharp
using POS.Logging;

// Before operation
AppLogger.LogTransaction("SALE_START", $"Creating new sale for customer {customerId}");

// After success
AppLogger.LogTransaction("SALE_SUCCESS", $"Sale created", new { InvoiceId = invoice.Id, Total = invoice.Total });

// On error (automatic in TransactionManager, but you can add context)
catch (TransactionException ex)
{
    AppLogger.LogError("SALE_FAILED", ex, new { CustomerId = customerId, ProductCount = products.Count });
    MessageBox.Show($"Sale failed: {ex.Message}");
}
```

## Example: Updated POSViewModel

### Original Code (ViewModels\POSViewModel.cs)

```csharp
_dbContext.Invoices.Add(invoice);
_dbContext.SaveChanges(); // Save changes to generate the Invoice Id

foreach (var product in SelectedProducts)
{
    // ... create sale product ...
    _dbContext.SaleProducts.Add(saleProduct);
    _dbContext.SaveChanges();
}
```

### Updated Code

```csharp
using POS.Persistence.Transaction;
using POS.Logging;

private readonly ITransactionManager _transactionManager;

public POSViewModel(AppDbContext dbContext)
{
    _dbContext = dbContext;
    _transactionManager = new TransactionManager(_dbContext);
}

private async Task ProcessSaleAsync()
{
    try
    {
        AppLogger.LogTransaction("SALE_START", "Processing new sale", new
        {
            CustomerName = SelectedCustomer?.Name,
            ProductCount = SelectedProducts.Count,
            Total = TotalPrice
        });

        await _transactionManager.ExecuteInTransactionAsync(async () =>
        {
            // Create invoice
            var invoice = new Invoice
            {
                // ... properties ...
            };
            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync(); // Generate InvoiceId

            // Add all sale products
            foreach (var product in SelectedProducts)
            {
                var saleProduct = new SaleProduct
                {
                    InvoiceId = invoice.Id,
                    // ... other properties ...
                };
                _dbContext.SaleProducts.Add(saleProduct);
            }
            await _dbContext.SaveChangesAsync();

            // Update inventory
            foreach (var product in SelectedProducts)
            {
                var inventoryItem = await _dbContext.Products.FindAsync(product.ProductId);
                if (inventoryItem != null)
                {
                    inventoryItem.Quantity -= product.Quantity;
                }
            }
            await _dbContext.SaveChangesAsync();

            return invoice.Id;
        }, "ProcessSale");

        AppLogger.LogTransaction("SALE_SUCCESS", $"Sale processed successfully", new
        {
            InvoiceId = invoice.Id,
            Total = TotalPrice
        });

        MessageBox.Show("Sale completed successfully!");
    }
    catch (TransactionException ex)
    {
        AppLogger.LogError("SALE_FAILED", ex, new
        {
            CustomerName = SelectedCustomer?.Name,
            ProductCount = SelectedProducts.Count
        });

        MessageBox.Show($"Sale failed: {ex.Message}\n\nPlease try again.");
    }
}
```

## Files to Update

Based on grep search, these files need updating:

1. **POS\ViewModels\PurchaseProductsViewModel.cs** (Line 281, 295)
2. **POS\ViewModels\PriceQuotationViewModel.cs** (Line 287, 302)
3. **POS\ViewModels\POSViewModel.cs** (Line 314, 329)

## Priority

Update these ViewModels **before production deployment** to ensure data consistency and prevent "database is locked" errors with 10 concurrent users.

## Testing

After updating:
1. Test each operation individually
2. Test with multiple users simultaneously
3. Verify transaction logs show proper operations
4. Test backup/restore with in-progress transactions
5. Simulate crashes during operations (recovery test)

## Benefits

✅ Automatic retry on database locks  
✅ Prevents data corruption  
✅ Complete audit trail  
✅ Easy debugging with logs  
✅ Handles concurrency conflicts  
✅ Rollback on errors  
