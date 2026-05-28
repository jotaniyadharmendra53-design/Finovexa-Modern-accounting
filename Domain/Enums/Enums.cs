using System;
using System.Collections.Generic;
using System.Text;


namespace InvoiceSaaS.Domain.Enums
{
    public enum InvoiceStatus : byte
    {
        Draft = 0,
        Sent = 1,
        Paid = 2,
        Overdue = 3,
        Cancelled = 4,
        PartiallyPaid = 5
    }

    public enum EmailType
    {
        InvoiceSent,
        ForgotPassword,
        Welcome,
        InvoiceOverdue,
        PasswordChanged
    }

    // ── Tax system enum ───────────────────────────────────────
    public enum TaxType : byte
    {
        GST = 0,   // India
        VAT = 1,   // EU / most of world
        SalesTax = 2    // USA
    }

    // ── India GST sub-type ────────────────────────────────────
    public enum GstType : byte
    {
        CgstSgst = 0,   // Intra-state: CGST + SGST
        Igst = 1    // Inter-state: IGST only
    }

}
