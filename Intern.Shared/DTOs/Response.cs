using System.Text.Json.Serialization;

namespace Intern.Shared.DTOs
{
    public class Response<T> where T : class
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public T Data { get; private set; }

        public int StatusCode { get; private set; }

        [JsonIgnore]
        public bool IsSuccess { get; private set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ErrorDto Error { get; private set; }

        public static Response<T> Success(T data, int statusCode)
        {
            return new Response<T> { Data = data, StatusCode = statusCode, Error = default, IsSuccess = true };
        }

        public static Response<T> Success(int statusCode)
        {
            return new Response<T> { Data = default, StatusCode = statusCode, Error = default, IsSuccess = true };
        }

        public static Response<T> Fail(ErrorDto error, int statusCode)
        {
            return new Response<T> { StatusCode = statusCode, Error = error, IsSuccess = false };
        }

        public static Response<T> Fail(string errorMessage, int statusCode, bool isShow)
        {
            var error = new ErrorDto(errorMessage, isShow);
            return new Response<T> { StatusCode = statusCode, Error = error, IsSuccess = false };
        }

        /// <summary>
        /// Kararlı hata kodu ile Fail. Frontend `Code`'u i18n key'ine mapleyebilir.
        /// </summary>
        public static Response<T> Fail(string code, string errorMessage, int statusCode, bool isShow)
        {
            var error = new ErrorDto(code, errorMessage, isShow);
            return new Response<T> { StatusCode = statusCode, Error = error, IsSuccess = false };
        }

        /// <summary>
        /// Kararlı hata kodu + i18n interpolasyon parametreleri ile Fail. Frontend
        /// `errors.codes.{code}` çevirisini <paramref name="parameters"/> ile doldurur.
        /// </summary>
        public static Response<T> Fail(
            string code,
            string errorMessage,
            Dictionary<string, string> parameters,
            int statusCode,
            bool isShow)
        {
            var error = new ErrorDto(code, errorMessage, parameters, isShow);
            return new Response<T> { StatusCode = statusCode, Error = error, IsSuccess = false };
        }
    }
}
