namespace EventAPI.DTOs
{
    public class ApiResponseDTO<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public T? Data { get; set; }
        public List<string>? Errors { get; set; }

        public static ApiResponseDTO<T> SuccessResponse(T data, string message = "Success")
        {
            return new ApiResponseDTO<T> { Success = true, Message = message, Data = data };
        }

        public static ApiResponseDTO<T> FailResponse(string message, List<string>? errors = null)
        {
            return new ApiResponseDTO<T> { Success = false, Message = message, Errors = errors };
        }
    }

    public class PagedResultDTO<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }
}
