using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Domain.Entities
{
    public enum ProductType 
    {  
        Service = 0, 
        Physical = 1 
    }

    public enum ExpenseStatus 
    {  
        Unpaid = 0, 
        Paid = 1 
    }

    public enum EstimateStatus 
    {  
        Draft = 0, 
        Sent = 1, 
        Accepted = 2, 
        Declined = 3, 
        Invoiced = 4, 
        Expired = 5 
    }

    public enum SaleStatus 
    {  
        Draft = 0, 
        Completed = 1, 
        Refunded = 2 
    }

    public enum PaymentDirection 
    { 
        Inbound = 0, 
        Outbound = 1 
    }

    public enum FiscalYearStatus
    {
        Open = 0,   // Active — transactions can be created/edited
        Locked = 1,   // Closed — read-only, no changes allowed
        Future = 2    // Pre-created, not yet active
    }
}
