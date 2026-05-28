using System;
using System.Collections.Generic;
using System.Text;

namespace InvoiceSaaS.Application.Common
{
    // ═══════════════════════════════════════════════════════════
    //  PagedResult — for DataTable / paginated lists
    // ═══════════════════════════════════════════════════════════
    public class PagedResult<T>
    {
        public IEnumerable<T> Items { get; set; } = Enumerable.Empty<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
        public bool HasPrevious => Page > 1;
        public bool HasNext => Page < TotalPages;
    }
}
