namespace InvoiceSaaS.Web.Models
{
    public class PaginationModel
    {
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; } = 0;
        public int PageSize { get; set; } = 20;
    }
}
